using System.Security.Claims;
using System.Text.Json;
using Dapper;
using DbExtensions;
using DbExtensions.DbExecutor.Service;
using DcMateClassLibrary.Models;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Log;
using DcMateH5.Abstractions.Log.Models;
using DcMateH5.Abstractions.Mms.Models;
using DcMateH5.Infrastructure.Mms;
using DcMateH5.Infrastructure.Wip;
using DcMateH5Api.Areas.Wip.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace DcMateH5ApiTest.Mms;

[Collection("DatabaseIntegration")]
public class MmsLotIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MmsLotFlow_ShouldCreateConsumeUnconsumeAndChangeState()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var lotCode = $"ITEST-MMS-L-{timestamp}";
        var mlotCode = $"ITEST-MMS-M-{timestamp}";
        var mmsService = CreateMmsService(connectionString, arrangement.AccountNo);
        var lotService = CreateLotService(connectionString, arrangement.AccountNo);

        try
        {
            await EnsureMlotStatusesAsync(connectionString);

            var lotResult = await lotService.CreateLotAsync(new WipCreateLotInputDto
            {
                DATA_LINK_SID = 900000003001m,
                LOT = lotCode,
                WO = arrangement.WorkOrder,
                ROUTE_SID = arrangement.RouteSid,
                LOT_QTY = 10,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                COMMENT = "MMS integration test lot"
            });
            Assert.True(lotResult.IsSuccess);

            var createResult = await mmsService.CreateMLotAsync(new MmsCreateMLotInputDto
            {
                DATA_LINK_SID = 900000003101m,
                MLOT = mlotCode,
                PART_NO = arrangement.PartNo,
                MLOT_QTY = 10,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                COMMENT = "MMS integration test mlot"
            });
            Assert.True(createResult.IsSuccess);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var created = await LoadMlotAsync(conn, mlotCode);
            Assert.NotNull(created);
            Assert.Equal(10, created!.MLOT_QTY);
            Assert.Equal("Wait", created.MLOT_STATUS_CODE);

            var createHistCount = await CountMlotHistAsync(conn, mlotCode, "MLOT_CREATE");
            Assert.Equal(1, createHistCount);

            var consumeResult = await mmsService.MLotConsumeAsync(new MmsMLotConsumeInputDto
            {
                DATA_LINK_SID = 900000003102m,
                LOT = lotCode,
                MLOT = mlotCode,
                CONSUME_QTY = 4,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                COMMENT = "consume mlot"
            });
            Assert.True(consumeResult.IsSuccess);

            var consumed = await LoadMlotAsync(conn, mlotCode);
            Assert.Equal(6, consumed!.MLOT_QTY);
            Assert.Equal("Wait", consumed.MLOT_STATUS_CODE);
            Assert.Equal(1, await CountCurUsedAsync(conn, lotCode, mlotCode));
            Assert.Equal(-4, await LoadLatestTransationQtyAsync(conn, mlotCode, "MLOT_CONSUME"));

            var consumeToZeroResult = await mmsService.MLotConsumeAsync(new MmsMLotConsumeInputDto
            {
                DATA_LINK_SID = 900000003103m,
                LOT = lotCode,
                MLOT = mlotCode,
                CONSUME_QTY = 6,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                COMMENT = "consume remaining mlot"
            });
            Assert.True(consumeToZeroResult.IsSuccess);

            var finished = await LoadMlotAsync(conn, mlotCode);
            Assert.Equal(0, finished!.MLOT_QTY);
            Assert.Equal("Finished", finished.MLOT_STATUS_CODE);

            var unconsumeResult = await mmsService.MLotUNConsumeAsync(new MmsMLotUNConsumeInputDto
            {
                DATA_LINK_SID = 900000003104m,
                LOT = lotCode,
                MLOT = mlotCode,
                UNCONSUME_QTY = 3,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                COMMENT = "unconsume mlot"
            });
            Assert.True(unconsumeResult.IsSuccess);

            var unconsumed = await LoadMlotAsync(conn, mlotCode);
            Assert.Equal(3, unconsumed!.MLOT_QTY);
            Assert.Equal("Finished", unconsumed.MLOT_STATUS_CODE);
            Assert.Equal(0, await CountCurUsedAsync(conn, lotCode, mlotCode));
            Assert.Equal(3, await LoadLatestTransationQtyAsync(conn, mlotCode, "MLOT_UNCONSUME"));

            var stateChangeResult = await mmsService.MLotStateChangeAsync(new MmsMLotStateChangeInputDto
            {
                DATA_LINK_SID = 900000003105m,
                MLOT = mlotCode,
                NEW_MLOT_STATE_CODE = "Wait",
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                COMMENT = "change mlot state"
            });
            Assert.True(stateChangeResult.IsSuccess);

            var changed = await LoadMlotAsync(conn, mlotCode);
            Assert.Equal(3, changed!.MLOT_QTY);
            Assert.Equal("Wait", changed.MLOT_STATUS_CODE);
            Assert.Equal(1, await CountMlotHistAsync(conn, mlotCode, "MLOT_STATE_CHANGE"));
        }
        finally
        {
            if (!ShouldKeepTestData())
            {
                await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode, mlotCode);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MLotConsumeAsync_ShouldFail_WhenQuantityIsInsufficient()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var lotCode = $"ITEST-MMS-L-F-{timestamp}";
        var mlotCode = $"ITEST-MMS-M-F-{timestamp}";
        var mmsService = CreateMmsService(connectionString, arrangement.AccountNo);
        var lotService = CreateLotService(connectionString, arrangement.AccountNo);

        try
        {
            await EnsureMlotStatusesAsync(connectionString);

            await lotService.CreateLotAsync(new WipCreateLotInputDto
            {
                DATA_LINK_SID = 900000003201m,
                LOT = lotCode,
                WO = arrangement.WorkOrder,
                ROUTE_SID = arrangement.RouteSid,
                LOT_QTY = 1,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest"
            });

            await mmsService.CreateMLotAsync(new MmsCreateMLotInputDto
            {
                DATA_LINK_SID = 900000003202m,
                MLOT = mlotCode,
                PART_NO = arrangement.PartNo,
                MLOT_QTY = 1,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest"
            });

            var ex = await Assert.ThrowsAsync<DcMateClassLibrary.Helper.HttpHelper.HttpStatusCodeException>(() =>
                mmsService.MLotConsumeAsync(new MmsMLotConsumeInputDto
                {
                    DATA_LINK_SID = 900000003203m,
                    LOT = lotCode,
                    MLOT = mlotCode,
                    CONSUME_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                }));

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, ex.StatusCode);
        }
        finally
        {
            if (!ShouldKeepTestData())
            {
                await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode, mlotCode);
            }
        }
    }

    private static bool ShouldKeepTestData()
        => string.Equals(
            Environment.GetEnvironmentVariable("KEEP_MMS_TEST_DATA"),
            "1",
            StringComparison.Ordinal);

    private static MmsLotService CreateMmsService(string connectionString, string accountNo)
        => new(CreateSqlHelper(connectionString, accountNo));

    private static LotBaseSettingService CreateLotService(string connectionString, string accountNo)
    {
        var sqlHelper = CreateSqlHelper(connectionString, accountNo);
        return new LotBaseSettingService(sqlHelper, new SelectDtoService(sqlHelper));
    }

    private static SQLGenerateHelper CreateSqlHelper(string connectionString, string accountNo)
    {
        var options = Options.Create(new DbOptions { Connection = connectionString });
        var connectionFactory = new SqlConnectionFactory(options);
        var connection = connectionFactory.Create();
        var dbExecutor = new DbExecutor(connection, new DbTransactionContext(), new FakeLogService(), new HttpContextAccessor());
        return new SQLGenerateHelper(dbExecutor, new FakeCurrentUserAccessor(accountNo));
    }

    private static async Task<TestArrangement> LoadArrangementAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var row = await conn.QuerySingleOrDefaultAsync<TestArrangement>(
            """
            SELECT TOP (1)
                u.ACCOUNT_NO AS AccountNo,
                w.WO AS WorkOrder,
                w.PART_NO AS PartNo,
                r.WIP_ROUTE_SID AS RouteSid,
                w.RELEASE_QTY AS PreviousReleaseQty
            FROM WIP_WO w
            INNER JOIN WIP_PARTNO p ON p.WIP_PARTNO_NO = w.PART_NO
            INNER JOIN WIP_ROUTE r ON r.WIP_ROUTE_NO = w.ROUTE_NO OR r.WIP_ROUTE_NAME = w.ROUTE_NO
            CROSS JOIN (
                SELECT TOP (1) ACCOUNT_NO
                FROM ADM_OPI_USER
                WHERE ACCOUNT_NO IS NOT NULL
                ORDER BY USER_SID DESC
            ) u
            WHERE EXISTS (
                SELECT 1
                FROM WIP_ROUTE_OPERATION ro
                WHERE ro.WIP_ROUTE_SID = r.WIP_ROUTE_SID
            )
            ORDER BY w.WO_SID DESC
            """);

        return row ?? throw new InvalidOperationException("No test arrangement could be resolved from the database.");
    }

    private static async Task EnsureMlotStatusesAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM MMS_MLOT_STATUS WHERE MLOT_STATUS_CODE = N'Wait')
            BEGIN
                INSERT INTO MMS_MLOT_STATUS (
                    MLOT_STATUS_SID, MLOT_STATUS_CODE, MLOT_STATUS_NAME,
                    CREATE_USER, CREATE_TIME, EDIT_USER, EDIT_TIME, CUR_FLAG
                )
                VALUES (
                    (SELECT ISNULL(MAX(MLOT_STATUS_SID), 0) + 1 FROM MMS_MLOT_STATUS),
                    N'Wait', N'待使用', N'SYSTEM', GETDATE(), N'SYSTEM', GETDATE(), 'Y'
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM MMS_MLOT_STATUS WHERE MLOT_STATUS_CODE = N'Finished')
            BEGIN
                INSERT INTO MMS_MLOT_STATUS (
                    MLOT_STATUS_SID, MLOT_STATUS_CODE, MLOT_STATUS_NAME,
                    CREATE_USER, CREATE_TIME, EDIT_USER, EDIT_TIME, CUR_FLAG
                )
                VALUES (
                    (SELECT ISNULL(MAX(MLOT_STATUS_SID), 0) + 1 FROM MMS_MLOT_STATUS),
                    N'Finished', N'已用完', N'SYSTEM', GETDATE(), N'SYSTEM', GETDATE(), 'Y'
                );
            END;
            """);
    }

    private static Task<MlotRow?> LoadMlotAsync(SqlConnection conn, string mlot)
        => conn.QuerySingleOrDefaultAsync<MlotRow>(
            "SELECT MLOT, MLOT_QTY, MLOT_STATUS_CODE FROM MMS_MLOT WHERE MLOT = @Mlot",
            new { Mlot = mlot });

    private static Task<int> CountMlotHistAsync(SqlConnection conn, string mlot, string actionCode)
        => conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM MMS_MLOT_HIST WHERE MLOT = @Mlot AND ACTION_CODE = @ActionCode",
            new { Mlot = mlot, ActionCode = actionCode });

    private static Task<int> CountCurUsedAsync(SqlConnection conn, string lot, string mlot)
        => conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM WIP_LOT_KP_CUR_USED WHERE WIP_LOT = @Lot AND MLOT = @Mlot",
            new { Lot = lot, Mlot = mlot });

    private static Task<decimal> LoadLatestTransationQtyAsync(SqlConnection conn, string mlot, string actionCode)
        => conn.ExecuteScalarAsync<decimal>(
            """
            SELECT TOP (1) TRANSATION_QTY
            FROM MMS_MLOT_HIST
            WHERE MLOT = @Mlot AND ACTION_CODE = @ActionCode
            ORDER BY CREATE_TIME DESC
            """,
            new { Mlot = mlot, ActionCode = actionCode });

    private static async Task CleanupAsync(string connectionString, string workOrder, decimal? previousReleaseQty, string lotCode, string mlotCode)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync("DELETE FROM WIP_LOT_KP_CUR_USED WHERE WIP_LOT = @Lot OR MLOT = @Mlot", new { Lot = lotCode, Mlot = mlotCode }, tx);
        await conn.ExecuteAsync("DELETE FROM MMS_MLOT_HIST WHERE MLOT = @Mlot", new { Mlot = mlotCode }, tx);
        await conn.ExecuteAsync("DELETE FROM MMS_MLOT WHERE MLOT = @Mlot", new { Mlot = mlotCode }, tx);
        await conn.ExecuteAsync("DELETE FROM WIP_LOT_HIST WHERE LOT = @Lot", new { Lot = lotCode }, tx);
        await conn.ExecuteAsync("DELETE FROM WIP_LOT WHERE LOT = @Lot", new { Lot = lotCode }, tx);
        await conn.ExecuteAsync("UPDATE WIP_WO SET RELEASE_QTY = @ReleaseQty WHERE WO = @Wo", new { ReleaseQty = previousReleaseQty, Wo = workOrder }, tx);

        await tx.CommitAsync();
    }

    private sealed class TestArrangement
    {
        public string AccountNo { get; init; } = null!;
        public string WorkOrder { get; init; } = null!;
        public string PartNo { get; init; } = null!;
        public decimal RouteSid { get; init; }
        public decimal? PreviousReleaseQty { get; init; }
    }

    private sealed class MlotRow
    {
        public string MLOT { get; init; } = null!;
        public decimal MLOT_QTY { get; init; }
        public string MLOT_STATUS_CODE { get; init; } = null!;
    }

    private sealed class FakeCurrentUserAccessor(string accountNo) : ICurrentUserAccessor
    {
        public CurrentUserSnapshot Get()
        {
            var claims = new[]
            {
                new Claim(AppClaimTypes.Account, accountNo),
                new Claim(AppClaimTypes.UserId, Guid.NewGuid().ToString()),
                new Claim(AppClaimTypes.UserLv, "1")
            };

            return CurrentUserSnapshot.From(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
        }
    }

    private sealed class FakeLogService : ILogService
    {
        public Task LogAsync(SqlLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SqlLogEntry>> GetLogsAsync(SqlLogQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SqlLogEntry>>(Array.Empty<SqlLogEntry>());
    }

    private static class TestConfiguration
    {
        public static string LoadConnectionString()
        {
            var root = FindSolutionRoot();
            var json = File.ReadAllText(Path.Combine(root, "src", "DcMateH5Api", "appsettings.json"));
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                       .GetProperty("ConnectionStrings")
                       .GetProperty("Connection")
                       .GetString()
                   ?? throw new InvalidOperationException("Connection string not found.");
        }

        private static string FindSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "DcMateH5Api.sln")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate solution root.");
        }
    }
}
