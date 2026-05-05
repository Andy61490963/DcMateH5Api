using System.Security.Claims;
using System.Text.Json;
using Dapper;
using DbExtensions;
using DbExtensions.DbExecutor.Service;
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
public class LotQuantityAdjustAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotBonusAsync_ShouldIncreaseLotQtyAndWriteReasonHistory()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-BONUS-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            await CreateTestLotAsync(service, arrangement, lotCode, 5, 900000001001m);

            var result = await service.LotBonusAsync(
                new WipLotBonusInputDto
                {
                    LOT = lotCode,
                    BONUS_QTY = 3,
                    REASON_SID = arrangement.ReasonSid,
                    DATA_LINK_SID = 900000001002m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "LotBonusAsync integration test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var lotQty = await conn.QuerySingleAsync<LotQuantityRow>(
                "SELECT LOT_QTY, NG_QTY, LAST_TRANS_TIME FROM WIP_LOT WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.Equal(8, lotQty.LOT_QTY);
            Assert.Equal(0, lotQty.NG_QTY);
            Assert.NotNull(lotQty.LAST_TRANS_TIME);

            var hist = await conn.QuerySingleOrDefaultAsync<ReasonHistoryRow>(
                """
                SELECT TOP (1)
                    h.ACTION_CODE,
                    h.TOTAL_OK_QTY,
                    h.TOTAL_NG_QTY,
                    r.REASON_TYPE,
                    r.REASON_SID,
                    r.REASON_CODE,
                    r.REASON_QTY
                FROM WIP_LOT_HIST h
                INNER JOIN WIP_LOT_REASON_HIST r ON r.WIP_LOT_HIST_SID = h.WIP_LOT_HIST_SID
                WHERE h.LOT = @Lot
                ORDER BY h.SEQ DESC
                """,
                new { Lot = lotCode });

            Assert.NotNull(hist);
            Assert.Equal("LOT_BONUS", hist!.ACTION_CODE);
            Assert.Equal(3, hist.TOTAL_OK_QTY);
            Assert.Equal(0, hist.TOTAL_NG_QTY);
            Assert.Equal("BONUS", hist.REASON_TYPE.Trim());
            Assert.Equal(arrangement.ReasonSid, hist.REASON_SID);
            Assert.Equal(arrangement.ReasonNo, hist.REASON_CODE);
            Assert.Equal(3, hist.REASON_QTY);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotScrapAsync_ShouldDecreaseLotQtyIncreaseNgQtyAndWriteReasonHistory()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-SCRAP-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            await CreateTestLotAsync(service, arrangement, lotCode, 5, 900000001011m);

            var result = await service.LotScrapAsync(
                new WipLotScrapInputDto
                {
                    LOT = lotCode,
                    SCRAP_QTY = 2,
                    REASON_SID = arrangement.ReasonSid,
                    DATA_LINK_SID = 900000001012m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "LotScrapAsync integration test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var lotQty = await conn.QuerySingleAsync<LotQuantityRow>(
                "SELECT LOT_QTY, NG_QTY, LAST_TRANS_TIME FROM WIP_LOT WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.Equal(3, lotQty.LOT_QTY);
            Assert.Equal(2, lotQty.NG_QTY);
            Assert.NotNull(lotQty.LAST_TRANS_TIME);

            var hist = await conn.QuerySingleOrDefaultAsync<ReasonHistoryRow>(
                """
                SELECT TOP (1)
                    h.ACTION_CODE,
                    h.TOTAL_OK_QTY,
                    h.TOTAL_NG_QTY,
                    r.REASON_TYPE,
                    r.REASON_SID,
                    r.REASON_CODE,
                    r.REASON_QTY
                FROM WIP_LOT_HIST h
                INNER JOIN WIP_LOT_REASON_HIST r ON r.WIP_LOT_HIST_SID = h.WIP_LOT_HIST_SID
                WHERE h.LOT = @Lot
                ORDER BY h.SEQ DESC
                """,
                new { Lot = lotCode });

            Assert.NotNull(hist);
            Assert.Equal("LOT_NG", hist!.ACTION_CODE);
            Assert.Equal(0, hist.TOTAL_OK_QTY);
            Assert.Equal(2, hist.TOTAL_NG_QTY);
            Assert.Equal("NG", hist.REASON_TYPE.Trim());
            Assert.Equal(arrangement.ReasonSid, hist.REASON_SID);
            Assert.Equal(arrangement.ReasonNo, hist.REASON_CODE);
            Assert.Equal(2, hist.REASON_QTY);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotScrapAsync_ShouldRejectScrapQtyGreaterThanCurrentLotQty()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-SCRAP2-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            await CreateTestLotAsync(service, arrangement, lotCode, 1, 900000001021m);

            var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.LotScrapAsync(
                new WipLotScrapInputDto
                {
                    LOT = lotCode,
                    SCRAP_QTY = 2,
                    REASON_SID = arrangement.ReasonSid,
                    DATA_LINK_SID = 900000001022m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "LotScrapAsync insufficient quantity test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                }));

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.StatusCode);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var lotQty = await conn.QuerySingleAsync<LotQuantityRow>(
                "SELECT LOT_QTY, NG_QTY, LAST_TRANS_TIME FROM WIP_LOT WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.Equal(1, lotQty.LOT_QTY);
            Assert.Equal(0, lotQty.NG_QTY);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    private static async Task CreateTestLotAsync(
        LotBaseSettingService service,
        TestArrangement arrangement,
        string lotCode,
        decimal lotQty,
        decimal dataLinkSid)
    {
        var result = await service.CreateLotAsync(
            new WipCreateLotInputDto
            {
                DATA_LINK_SID = dataLinkSid,
                LOT = lotCode,
                ALIAS_LOT1 = $"{lotCode}-A1",
                ALIAS_LOT2 = $"{lotCode}-A2",
                WO = arrangement.WorkOrder,
                ROUTE_SID = arrangement.RouteSid,
                LOT_QTY = lotQty,
                REPORT_TIME = DateTime.Now,
                ACCOUNT_NO = arrangement.AccountNo,
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                COMMENT = "Lot quantity adjustment integration test"
            });

        Assert.True(result.IsSuccess);
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
            SELECT TOP (1)
                u.ACCOUNT_NO AS AccountNo,
                w.WO AS WorkOrder,
                CAST(r.WIP_ROUTE_SID AS decimal(18, 0)) AS RouteSid,
                w.RELEASE_QTY AS PreviousReleaseQty,
                CAST(ar.ADM_REASON_SID AS decimal(18, 0)) AS ReasonSid,
                ar.REASON_NO AS ReasonNo
            FROM WIP_WO w
            INNER JOIN WIP_ROUTE r ON r.WIP_ROUTE_NO = w.ROUTE_NO OR r.WIP_ROUTE_NAME = w.ROUTE_NO
            CROSS JOIN (
                SELECT TOP (1) ACCOUNT_NO
                FROM ADM_OPI_USER
                WHERE ACCOUNT_NO IS NOT NULL
                ORDER BY USER_SID DESC
            ) u
            CROSS JOIN (
                SELECT TOP (1) ADM_REASON_SID, REASON_NO
                FROM ADM_REASON
                WHERE ENABLE_FLAG = 'Y'
                ORDER BY ADM_REASON_SID
            ) ar
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

        await conn.ExecuteAsync(
            """
            DELETE r
            FROM WIP_LOT_REASON_HIST r
            INNER JOIN WIP_LOT_HIST h ON h.WIP_LOT_HIST_SID = r.WIP_LOT_HIST_SID
            WHERE h.LOT = @Lot
            """,
            new { Lot = lotCode },
            tx);
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
        public decimal ReasonSid { get; init; }
        public string ReasonNo { get; init; } = null!;
    }

    private sealed class LotQuantityRow
    {
        public decimal LOT_QTY { get; init; }
        public decimal NG_QTY { get; init; }
        public DateTime? LAST_TRANS_TIME { get; init; }
    }

    private sealed class ReasonHistoryRow
    {
        public string ACTION_CODE { get; init; } = null!;
        public decimal TOTAL_OK_QTY { get; init; }
        public decimal TOTAL_NG_QTY { get; init; }
        public string REASON_TYPE { get; init; } = null!;
        public decimal REASON_SID { get; init; }
        public string REASON_CODE { get; init; } = null!;
        public decimal REASON_QTY { get; init; }
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
