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
public class LotReassignOperationAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotReassignOperationAsync_ShouldMoveLotToNewOperation()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-REASSIGN-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000911m,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotReassignOperationAsync integration test"
                });
            Assert.True(createLotResult.IsSuccess);

            var result = await service.LotReassignOperationAsync(
                new WipLotReassignOperationInputDto
                {
                    LOT = lotCode,
                    DATA_LINK_SID = 900000000912m,
                    NEW_OPER_SEQ = arrangement.NextOperSeq,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "LotReassignOperationAsync integration test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var lotRow = await conn.QuerySingleAsync<LotRow>(
                "SELECT OPERATION_SEQ, ROUTE_OPER_SID FROM WIP_LOT WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.Equal(arrangement.NextOperSeq, (int)lotRow.OPERATION_SEQ);
            Assert.Equal(arrangement.NextRouteOperSid, lotRow.ROUTE_OPER_SID);

            var actions = (await conn.QueryAsync<string>(
                """
                SELECT TOP (3) ACTION_CODE
                FROM WIP_LOT_HIST
                WHERE LOT = @Lot
                ORDER BY SEQ DESC
                """,
                new { Lot = lotCode })).ToList();

            Assert.Contains("LOT_RESSIGN_OPER", actions);
            Assert.Contains("OPER_END", actions);
            Assert.Contains("OPER_START", actions);
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
            WITH RouteOps AS (
                SELECT
                    r.WIP_ROUTE_SID,
                    ro.WIP_ROUTE_OPERATION_SID,
                    ro.SEQ,
                    ROW_NUMBER() OVER (PARTITION BY r.WIP_ROUTE_SID ORDER BY ro.SEQ) AS RN
                FROM WIP_ROUTE r
                INNER JOIN WIP_ROUTE_OPERATION ro ON ro.WIP_ROUTE_SID = r.WIP_ROUTE_SID
            ),
            RouteChoice AS (
                SELECT TOP (1)
                    w.WO,
                    CAST(w.RELEASE_QTY AS decimal(18, 0)) AS PreviousReleaseQty,
                    CAST(r.WIP_ROUTE_SID AS decimal(18, 0)) AS RouteSid,
                    CAST(ro1.SEQ AS int) AS FirstOperSeq,
                    CAST(ro2.SEQ AS int) AS NextOperSeq,
                    CAST(ro2.WIP_ROUTE_OPERATION_SID AS decimal(18, 0)) AS NextRouteOperSid
                FROM WIP_WO w
                INNER JOIN WIP_ROUTE r ON r.WIP_ROUTE_NO = w.ROUTE_NO OR r.WIP_ROUTE_NAME = w.ROUTE_NO
                INNER JOIN RouteOps ro1 ON ro1.WIP_ROUTE_SID = r.WIP_ROUTE_SID AND ro1.RN = 1
                INNER JOIN RouteOps ro2 ON ro2.WIP_ROUTE_SID = r.WIP_ROUTE_SID AND ro2.RN = 2
                ORDER BY w.WO_SID DESC
            )
            SELECT TOP (1)
                u.ACCOUNT_NO AS AccountNo,
                rc.WO AS WorkOrder,
                rc.RouteSid,
                rc.PreviousReleaseQty,
                rc.FirstOperSeq,
                rc.NextOperSeq,
                rc.NextRouteOperSid
            FROM RouteChoice rc
            CROSS JOIN (
                SELECT TOP (1) ACCOUNT_NO
                FROM UMM_USER
                WHERE ACCOUNT_NO IS NOT NULL
                ORDER BY USER_SID DESC
            ) u
            """);

        return row ?? throw new InvalidOperationException("No test arrangement could be resolved from the database.");
    }

    private static async Task CleanupAsync(string connectionString, string workOrder, decimal? previousReleaseQty, string lotCode)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

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
        public int FirstOperSeq { get; init; }
        public int NextOperSeq { get; init; }
        public decimal NextRouteOperSid { get; init; }
    }

    private sealed class LotRow
    {
        public decimal OPERATION_SEQ { get; init; }
        public decimal? ROUTE_OPER_SID { get; init; }
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
