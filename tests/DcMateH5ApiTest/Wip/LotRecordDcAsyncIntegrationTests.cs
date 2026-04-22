using System.Security.Claims;
using System.Text.Json;
using Dapper;
using DbExtensions;
using DbExtensions.DbExecutor.Service;
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
public class LotRecordDcAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotRecordDcAsync_ShouldWriteDcHistoryAndCurrent()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-DC-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000921m,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotRecordDcAsync integration test"
                });
            Assert.True(createLotResult.IsSuccess);

            var result = await service.LotRecordDcAsync(
                new WipLotRecordDcInputDto
                {
                    ACTION_CODE = "LOT_RECORD_DC",
                    DC_TYPE = "IPQC",
                    LOT = lotCode,
                    DATA_LINK_SID = 900000000922m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    REPORT_TIME = DateTime.Now,
                    COMMENT = "LotRecordDcAsync integration test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    ITEMS =
                    [
                        new WipLotRecordDcItemInputDto
                        {
                            DC_ITEM_CODE = arrangement.DcItemCode,
                            DC_ITEM_VALUE = "12.34",
                            DC_ITEM_COMMENT = "measured by integration test"
                        }
                    ]
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var current = await conn.QuerySingleOrDefaultAsync<DcCurrentRow>(
                """
                SELECT TOP (1) DC_ITEM_CODE, DC_ITEM_VALUE, DC_ITEM_COMMENT, DC_TYPE
                FROM WIP_LOT_DC_ITEM_CURRENT
                WHERE LOT = @Lot AND DC_ITEM_CODE = @DcItemCode
                """,
                new { Lot = lotCode, arrangement.DcItemCode });
            Assert.NotNull(current);
            Assert.Equal(arrangement.DcItemCode, current!.DC_ITEM_CODE);
            Assert.Equal("12.34", current.DC_ITEM_VALUE);
            Assert.Equal("IPQC", current.DC_TYPE);

            var hist = await conn.QuerySingleOrDefaultAsync<DcHistRow>(
                """
                SELECT TOP (1) h.DC_ITEM_CODE, h.DC_ITEM_VALUE, h.DC_TYPE
                FROM WIP_LOT_DC_ITEM_HIST h
                INNER JOIN WIP_LOT_HIST lh ON lh.WIP_LOT_HIST_SID = h.WIP_LOT_HIST_SID
                WHERE lh.LOT = @Lot AND h.DC_ITEM_CODE = @DcItemCode
                ORDER BY h.WIP_LOT_DC_HIST_SID DESC
                """,
                new { Lot = lotCode, arrangement.DcItemCode });
            Assert.NotNull(hist);
            Assert.Equal("12.34", hist!.DC_ITEM_VALUE);
            Assert.Equal("IPQC", hist.DC_TYPE);

            var action = await conn.ExecuteScalarAsync<string>(
                "SELECT TOP (1) ACTION_CODE FROM WIP_LOT_HIST WHERE LOT = @Lot ORDER BY SEQ DESC",
                new { Lot = lotCode });
            Assert.Equal("LOT_RECORD_DC", action);
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
            SELECT TOP (1)
                u.ACCOUNT_NO AS AccountNo,
                w.WO AS WorkOrder,
                CAST(r.WIP_ROUTE_SID AS decimal(18, 0)) AS RouteSid,
                CAST(w.RELEASE_QTY AS decimal(18, 0)) AS PreviousReleaseQty,
                q.QMM_ITEM_NO AS DcItemCode
            FROM WIP_WO w
            INNER JOIN WIP_ROUTE r ON r.WIP_ROUTE_NO = w.ROUTE_NO OR r.WIP_ROUTE_NAME = w.ROUTE_NO
            CROSS JOIN (
                SELECT TOP (1) ACCOUNT_NO
                FROM UMM_USER
                WHERE ACCOUNT_NO IS NOT NULL
                ORDER BY USER_SID DESC
            ) u
            CROSS JOIN (
                SELECT TOP (1) QMM_ITEM_NO
                FROM QMM_DC_ITEM
                WHERE ENABLE_FLAG = 'Y'
                ORDER BY QMM_ITEM_SID
            ) q
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

        await conn.ExecuteAsync("DELETE FROM WIP_LOT_DC_ITEM_CURRENT WHERE LOT = @Lot", new { Lot = lotCode }, tx);
        await conn.ExecuteAsync(
            """
            DELETE h
            FROM WIP_LOT_DC_ITEM_HIST h
            INNER JOIN WIP_LOT_HIST lh ON lh.WIP_LOT_HIST_SID = h.WIP_LOT_HIST_SID
            WHERE lh.LOT = @Lot
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
        public string DcItemCode { get; init; } = null!;
    }

    private sealed class DcCurrentRow
    {
        public string DC_ITEM_CODE { get; init; } = null!;
        public string? DC_ITEM_VALUE { get; init; }
        public string? DC_ITEM_COMMENT { get; init; }
        public string? DC_TYPE { get; init; }
    }

    private sealed class DcHistRow
    {
        public string DC_ITEM_CODE { get; init; } = null!;
        public string? DC_ITEM_VALUE { get; init; }
        public string? DC_TYPE { get; init; }
    }

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
