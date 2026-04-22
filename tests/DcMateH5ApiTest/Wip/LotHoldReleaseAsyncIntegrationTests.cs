using System.Security.Claims;
using System.Text.Json;
using Dapper;
using DbExtensions;
using DbExtensions.DbExecutor.Service;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateClassLibrary.Models;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Log;
using DcMateH5.Abstractions.Log.Models;
using DcMateH5.Infrastructure.Wip;
using DcMateH5Api.Areas.Wip.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace DcMateH5ApiTest.Wip;

[Collection(DatabaseIntegrationCollection.Name)]
public class LotHoldReleaseAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotHoldReleaseAsync_ShouldReleaseHoldAndRestorePreviousStatus()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-HREL-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000811m,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotHoldReleaseAsync integration test"
                });
            Assert.True(createLotResult.IsSuccess);

            var holdResult = await service.LotHoldAsync(
                new WipLotHoldInputDto
                {
                    LOT = lotCode,
                    REASON_SID = arrangement.HoldReasonSid,
                    DATA_LINK_SID = 900000000812m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "LotHoldReleaseAsync hold step",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });
            Assert.True(holdResult.IsSuccess);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var holdSid = await conn.ExecuteScalarAsync<decimal>(
                "SELECT TOP (1) WIP_LOT_HOLD_HIST_SID FROM WIP_LOT_HOLD_HIST WHERE LOT = @Lot AND RELEASE_FLAG = 'N' ORDER BY WIP_LOT_HOLD_HIST_SID DESC",
                new { Lot = lotCode });

            var result = await service.LotHoldReleaseAsync(
                new WipLotHoldReleaseInputDto
                {
                    LOT = lotCode,
                    LOT_HOLD_SID = holdSid,
                    REASON_SID = arrangement.ReleaseReasonSid,
                    DATA_LINK_SID = 900000000813m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "LotHoldReleaseAsync release step",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            var lotStatus = await conn.ExecuteScalarAsync<string>(
                "SELECT LOT_STATUS_CODE FROM WIP_LOT WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.Equal("Wait", lotStatus);

            var lotTimes = await conn.QuerySingleAsync<LotTimeRow>(
                "SELECT LAST_TRANS_TIME, LAST_STATUS_CHANGE_TIME FROM WIP_LOT WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.NotNull(lotTimes.LAST_TRANS_TIME);
            Assert.NotNull(lotTimes.LAST_STATUS_CHANGE_TIME);

            var holdHist = await conn.QuerySingleOrDefaultAsync<LotHoldReleaseRow>(
                """
                SELECT TOP (1) RELEASE_FLAG, RELEASE_REASON_SID, RELEASE_REASON_CODE, RELEASE_REASON_NAME
                FROM WIP_LOT_HOLD_HIST
                WHERE LOT = @Lot
                ORDER BY WIP_LOT_HOLD_HIST_SID DESC
                """,
                new { Lot = lotCode });

            Assert.NotNull(holdHist);
            Assert.Equal("Y", holdHist!.RELEASE_FLAG);
            Assert.Equal(arrangement.ReleaseReasonSid, holdHist.RELEASE_REASON_SID);
            Assert.Equal(arrangement.ReleaseReasonNo, holdHist.RELEASE_REASON_CODE);
            Assert.Equal(arrangement.ReleaseReasonName, holdHist.RELEASE_REASON_NAME);

            var action = await conn.ExecuteScalarAsync<string>(
                "SELECT TOP (1) ACTION_CODE FROM WIP_LOT_HIST WHERE LOT = @Lot ORDER BY SEQ DESC",
                new { Lot = lotCode });
            Assert.Equal("LOT_HOLD_RELEASE", action);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotHoldReleaseAsync_ShouldRejectUnknownLotHoldSid()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-HREL2-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000841m,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotHoldReleaseAsync unknown hold sid test"
                });
            Assert.True(createLotResult.IsSuccess);

            var holdResult = await service.LotHoldAsync(
                new WipLotHoldInputDto
                {
                    LOT = lotCode,
                    REASON_SID = arrangement.HoldReasonSid,
                    DATA_LINK_SID = 900000000842m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "Hold before invalid release",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });
            Assert.True(holdResult.IsSuccess);

            var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.LotHoldReleaseAsync(
                new WipLotHoldReleaseInputDto
                {
                    LOT = lotCode,
                    LOT_HOLD_SID = arrangement.InvalidHoldSid,
                    REASON_SID = arrangement.ReleaseReasonSid,
                    DATA_LINK_SID = 900000000843m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "Unknown hold sid",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                }));

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Contains("Open lot hold history not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotHoldReleaseAsync_ShouldRejectWhenLotIsNotHold()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-HREL3-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000851m,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotHoldReleaseAsync non-hold status test"
                });
            Assert.True(createLotResult.IsSuccess);

            var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.LotHoldReleaseAsync(
                new WipLotHoldReleaseInputDto
                {
                    LOT = lotCode,
                    LOT_HOLD_SID = arrangement.InvalidHoldSid,
                    REASON_SID = arrangement.ReleaseReasonSid,
                    DATA_LINK_SID = 900000000852m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "Release without hold",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                }));

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Contains("LOT status must be Hold", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotHoldReleaseAsync_ShouldRejectWhenMultipleOpenHoldHistoriesExist()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-HREL4-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000861m,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotHoldReleaseAsync multi open hold test"
                });
            Assert.True(createLotResult.IsSuccess);

            var holdResult = await service.LotHoldAsync(
                new WipLotHoldInputDto
                {
                    LOT = lotCode,
                    REASON_SID = arrangement.HoldReasonSid,
                    DATA_LINK_SID = 900000000862m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "Create initial hold",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });
            Assert.True(holdResult.IsSuccess);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var holdSid = await conn.ExecuteScalarAsync<decimal>(
                "SELECT TOP (1) WIP_LOT_HOLD_HIST_SID FROM WIP_LOT_HOLD_HIST WHERE LOT = @Lot AND RELEASE_FLAG = 'N' ORDER BY WIP_LOT_HOLD_HIST_SID DESC",
                new { Lot = lotCode });

            await conn.ExecuteAsync(
                """
                INSERT INTO WIP_LOT_HOLD_HIST
                (
                    WIP_LOT_HOLD_HIST_SID,
                    HOLD_WIP_LOT_HIST_SID,
                    LOT,
                    HOLD_REASON_SID,
                    HOLD_REASON_CODE,
                    HOLD_REASON_NAME,
                    HOLD_REASON_COMMENT,
                    PRE_LOT_STATUS_SID,
                    RELEASE_FLAG
                )
                SELECT
                    @NewLotHoldSid,
                    @NewHoldHistSid,
                    LOT,
                    HOLD_REASON_SID,
                    HOLD_REASON_CODE,
                    HOLD_REASON_NAME,
                    HOLD_REASON_COMMENT,
                    PRE_LOT_STATUS_SID,
                    'N'
                FROM WIP_LOT_HOLD_HIST
                WHERE WIP_LOT_HOLD_HIST_SID = @SourceLotHoldSid
                """,
                new
                {
                    NewLotHoldSid = NewSid(),
                    NewHoldHistSid = NewSid(),
                    SourceLotHoldSid = holdSid
                });

            var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.LotHoldReleaseAsync(
                new WipLotHoldReleaseInputDto
                {
                    LOT = lotCode,
                    LOT_HOLD_SID = holdSid,
                    REASON_SID = arrangement.ReleaseReasonSid,
                    DATA_LINK_SID = 900000000863m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "Release with corrupted open hold data",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Conflict, exception.StatusCode);
            Assert.Contains("Multiple unreleased hold records", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    private static LotBaseSettingService CreateService(string connectionString, string accountNo)
    {
        var options = Options.Create(new DbOptions { Connection = connectionString });
        var connectionFactory = new SqlConnectionFactory(options);
        var connection = connectionFactory.Create();
        var dbExecutor = new DbExecutor(connection, new DbTransactionContext(), new FakeLogService(), new HttpContextAccessor());
        var sqlHelper = new SQLGenerateHelper(dbExecutor, new FakeCurrentUserAccessor(accountNo));
        return new LotBaseSettingService(sqlHelper, new SelectDtoService(sqlHelper));
    }

    private static async Task<TestArrangement> LoadArrangementAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var row = await conn.QuerySingleOrDefaultAsync<TestArrangement>(
            """
            WITH Reasons AS (
                SELECT TOP (2)
                    ADM_REASON_SID,
                    REASON_NO,
                    REASON_NAME,
                    ROW_NUMBER() OVER (ORDER BY ADM_REASON_SID) AS RN
                FROM ADM_REASON
                WHERE ENABLE_FLAG = 'Y'
            )
            SELECT TOP (1)
                u.ACCOUNT_NO AS AccountNo,
                w.WO AS WorkOrder,
                CAST(r.WIP_ROUTE_SID AS decimal(18, 0)) AS RouteSid,
                w.RELEASE_QTY AS PreviousReleaseQty,
                CAST(hr.ADM_REASON_SID AS decimal(18, 0)) AS HoldReasonSid,
                hr.REASON_NO AS HoldReasonNo,
                hr.REASON_NAME AS HoldReasonName,
                CAST(rr.ADM_REASON_SID AS decimal(18, 0)) AS ReleaseReasonSid,
                rr.REASON_NO AS ReleaseReasonNo,
                rr.REASON_NAME AS ReleaseReasonName,
                CAST((SELECT ISNULL(MAX(WIP_LOT_HOLD_HIST_SID), 0) + 999999 FROM WIP_LOT_HOLD_HIST) AS decimal(18, 0)) AS InvalidHoldSid
            FROM WIP_WO w
            INNER JOIN WIP_ROUTE r ON r.WIP_ROUTE_NO = w.ROUTE_NO OR r.WIP_ROUTE_NAME = w.ROUTE_NO
            CROSS JOIN (
                SELECT TOP (1) ACCOUNT_NO
                FROM UMM_USER
                WHERE ACCOUNT_NO IS NOT NULL
                ORDER BY USER_SID DESC
            ) u
            CROSS JOIN (SELECT * FROM Reasons WHERE RN = 1) hr
            CROSS JOIN (SELECT * FROM Reasons WHERE RN = 2) rr
            WHERE EXISTS (SELECT 1 FROM WIP_ROUTE_OPERATION ro WHERE ro.WIP_ROUTE_SID = r.WIP_ROUTE_SID)
            ORDER BY w.WO_SID DESC
            """);

        return row ?? throw new InvalidOperationException("No test arrangement could be resolved from the database.");
    }

    private static async Task CleanupAsync(string connectionString, string workOrder, decimal? previousReleaseQty, string lotCode)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync("DELETE FROM WIP_LOT_HOLD_HIST WHERE LOT = @Lot", new { Lot = lotCode }, tx);
        await conn.ExecuteAsync("DELETE FROM WIP_LOT_HIST WHERE LOT = @Lot", new { Lot = lotCode }, tx);
        await conn.ExecuteAsync("DELETE FROM WIP_LOT WHERE LOT = @Lot", new { Lot = lotCode }, tx);
        await conn.ExecuteAsync(
            "UPDATE WIP_WO SET RELEASE_QTY = @ReleaseQty WHERE WO = @Wo",
            new { ReleaseQty = previousReleaseQty, Wo = workOrder },
            tx);

        await tx.CommitAsync();
    }

    private sealed class TestArrangement
    {
        public string AccountNo { get; init; } = null!;
        public string WorkOrder { get; init; } = null!;
        public decimal RouteSid { get; init; }
        public decimal? PreviousReleaseQty { get; init; }
        public decimal HoldReasonSid { get; init; }
        public string HoldReasonNo { get; init; } = null!;
        public string HoldReasonName { get; init; } = null!;
        public decimal ReleaseReasonSid { get; init; }
        public string ReleaseReasonNo { get; init; } = null!;
        public string ReleaseReasonName { get; init; } = null!;
        public decimal InvalidHoldSid { get; init; }
    }

    private sealed class LotHoldReleaseRow
    {
        public string RELEASE_FLAG { get; init; } = null!;
        public decimal RELEASE_REASON_SID { get; init; }
        public string RELEASE_REASON_CODE { get; init; } = null!;
        public string RELEASE_REASON_NAME { get; init; } = null!;
    }

    private sealed class LotTimeRow
    {
        public DateTime? LAST_TRANS_TIME { get; init; }
        public DateTime? LAST_STATUS_CHANGE_TIME { get; init; }
    }

    private static decimal NewSid() => RandomHelper.GenerateRandomDecimal();

    private sealed class FakeCurrentUserAccessor(string accountNo) : ICurrentUserAccessor
    {
        public CurrentUserSnapshot Get()
        {
            var claims = new[]
            {
                new Claim(DcMateClassLibrary.Models.AppClaimTypes.Account, accountNo),
                new Claim(DcMateClassLibrary.Models.AppClaimTypes.UserId, Guid.NewGuid().ToString()),
                new Claim(DcMateClassLibrary.Models.AppClaimTypes.UserLv, "1")
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
