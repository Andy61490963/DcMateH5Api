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
public class CreateLotsAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateLotsAsync_ShouldInsertTwoLotsAndCleanup()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var firstLot = $"ITEST-B1-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var secondLot = $"ITEST-B2-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var result = await service.CreateLotsAsync(
            [
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000301m,
                    LOT = firstLot,
                    ALIAS_LOT1 = $"{firstLot}-A1",
                    ALIAS_LOT2 = $"{firstLot}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 1,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "CreateLotsAsync integration test 1"
                },
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000302m,
                    LOT = secondLot,
                    ALIAS_LOT1 = $"{secondLot}-A1",
                    ALIAS_LOT2 = $"{secondLot}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 1,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "CreateLotsAsync integration test 2"
                }
            ]);

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_LOT WHERE LOT IN (@FirstLot, @SecondLot)",
                new { FirstLot = firstLot, SecondLot = secondLot });

            Assert.Equal(2, count);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, firstLot, secondLot);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateLotsAsync_ShouldRollbackWhenOneLotAlreadyExists()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var existingLot = $"ITEST-RB-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var rolledBackLot = $"ITEST-RB2-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var singleResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000303m,
                    LOT = existingLot,
                    ALIAS_LOT1 = $"{existingLot}-A1",
                    ALIAS_LOT2 = $"{existingLot}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 1,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "CreateLotsAsync rollback seed"
                });

            Assert.True(singleResult.IsSuccess);

            await Assert.ThrowsAsync<DcMateClassLibrary.Helper.HttpHelper.HttpStatusCodeException>(async () =>
                await service.CreateLotsAsync(
                [
                    new WipCreateLotInputDto
                    {
                        DATA_LINK_SID = 900000000304m,
                        LOT = existingLot,
                        ALIAS_LOT1 = $"{existingLot}-B1",
                        ALIAS_LOT2 = $"{existingLot}-B2",
                        WO = arrangement.WorkOrder,
                        ROUTE_SID = arrangement.RouteSid,
                        LOT_QTY = 1,
                        REPORT_TIME = DateTime.Now,
                        ACCOUNT_NO = arrangement.AccountNo,
                        INPUT_FORM_NAME = "DcMateH5ApiTest",
                        COMMENT = "CreateLotsAsync duplicate"
                    },
                    new WipCreateLotInputDto
                    {
                        DATA_LINK_SID = 900000000305m,
                        LOT = rolledBackLot,
                        ALIAS_LOT1 = $"{rolledBackLot}-A1",
                        ALIAS_LOT2 = $"{rolledBackLot}-A2",
                        WO = arrangement.WorkOrder,
                        ROUTE_SID = arrangement.RouteSid,
                        LOT_QTY = 1,
                        REPORT_TIME = DateTime.Now,
                        ACCOUNT_NO = arrangement.AccountNo,
                        INPUT_FORM_NAME = "DcMateH5ApiTest",
                        COMMENT = "CreateLotsAsync rollback target"
                    }
                ]));

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var rollbackTargetExists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_LOT WHERE LOT = @Lot",
                new { Lot = rolledBackLot });

            Assert.Equal(0, rollbackTargetExists);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, existingLot, rolledBackLot);
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
                SELECT 1 FROM WIP_ROUTE_OPERATION ro WHERE ro.WIP_ROUTE_SID = r.WIP_ROUTE_SID
            )
            ORDER BY w.WO_SID DESC
            """);

        return row ?? throw new InvalidOperationException("No test arrangement could be resolved from the database.");
    }

    private static async Task CleanupAsync(string connectionString, string workOrder, decimal? previousReleaseQty, params string[] lots)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync("DELETE FROM WIP_LOT_HIST WHERE LOT IN @Lots", new { Lots = lots }, tx);
        await conn.ExecuteAsync("DELETE FROM WIP_LOT WHERE LOT IN @Lots", new { Lots = lots }, tx);
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
    }

    private sealed class FakeCurrentUserAccessor(string accountNo) : ICurrentUserAccessor
    {
        public CurrentUserSnapshot Get()
        {
            var claims =
                new[]
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

            return doc.RootElement.GetProperty("ConnectionStrings").GetProperty("Connection").GetString()
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
