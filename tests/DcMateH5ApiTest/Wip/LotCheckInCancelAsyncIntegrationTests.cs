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
public class LotCheckInCancelAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotCheckInCancelAsync_ShouldRevertLotToWaitAndCleanupCurrentAssignments()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-CIC-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = 900000000401m,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = 2,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotCheckInCancelAsync integration test"
                });
            Assert.True(createLotResult.IsSuccess);

            var checkInResult = await service.LotCheckInAsync(
                new WipLotCheckInInputDto
                {
                    LOT = lotCode,
                    DATA_LINK_SID = 900000000402m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    EQP_NO = arrangement.EquipmentNo,
                    SHIFT_SID = arrangement.ShiftSid,
                    LOT_SUB_STATUS_CODE = "NORMAL",
                    COMMENT = "LotCheckInCancelAsync integration test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });
            Assert.True(checkInResult.IsSuccess);

            var result = await service.LotCheckInCancelAsync(
                new WipLotCheckInCancelInputDto
                {
                    LOT = lotCode,
                    DATA_LINK_SID = 900000000403m,
                    ACCOUNT_NO = arrangement.AccountNo,
                    COMMENT = "LotCheckInCancelAsync integration test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var lot = await conn.QuerySingleOrDefaultAsync<LotRow>(
                """
                SELECT LOT_STATUS_CODE, LOT_SUB_STATUS_CODE
                FROM WIP_LOT
                WHERE LOT = @Lot
                """,
                new { Lot = lotCode });

            Assert.NotNull(lot);
            Assert.Equal("Wait", lot!.LOT_STATUS_CODE);
            Assert.Equal(string.Empty, lot.LOT_SUB_STATUS_CODE ?? string.Empty);

            var cancelHist = await conn.QuerySingleOrDefaultAsync<HistRow>(
                """
                SELECT TOP (1) ACTION_CODE, LOT_STATUS_CODE
                FROM WIP_LOT_HIST
                WHERE LOT = @Lot AND ACTION_CODE = 'CHECK_IN_CANCEL'
                ORDER BY SEQ DESC
                """,
                new { Lot = lotCode });

            Assert.NotNull(cancelHist);
            Assert.Equal("CHECK_IN_CANCEL", cancelHist!.ACTION_CODE);
            Assert.Equal("Wait", cancelHist.LOT_STATUS_CODE);

            var currentUserCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_LOT_CUR_USER WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.Equal(0, currentUserCount);

            var currentEqpCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_LOT_CUR_EQP WHERE LOT = @Lot",
                new { Lot = lotCode });
            Assert.Equal(0, currentEqpCount);

            var closedUserHistCount = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(1)
                FROM WIP_LOT_USER_HIST h
                INNER JOIN WIP_LOT_HIST inHist ON inHist.WIP_LOT_HIST_SID = h.IN_WIP_LOT_HIST_SID
                INNER JOIN WIP_LOT_HIST outHist ON outHist.WIP_LOT_HIST_SID = h.OUT_WIP_LOT_HIST_SID
                WHERE inHist.LOT = @Lot
                  AND inHist.ACTION_CODE = 'CHECK_IN'
                  AND h.OUT_FLAG = 'Y'
                  AND outHist.ACTION_CODE = 'CHECK_IN_CANCEL'
                """,
                new { Lot = lotCode });
            Assert.Equal(1, closedUserHistCount);
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
                u.SHIFT_SID AS ShiftSid,
                w.WO AS WorkOrder,
                r.WIP_ROUTE_SID AS RouteSid,
                w.RELEASE_QTY AS PreviousReleaseQty,
                e.EQM_MASTER_NO AS EquipmentNo
            FROM WIP_WO w
            INNER JOIN WIP_ROUTE r ON r.WIP_ROUTE_NO = w.ROUTE_NO OR r.WIP_ROUTE_NAME = w.ROUTE_NO
            CROSS JOIN (
                SELECT TOP (1) ACCOUNT_NO, SHIFT_SID
                FROM ADM_OPI_USER
                WHERE ACCOUNT_NO IS NOT NULL
                ORDER BY USER_SID DESC
            ) u
            CROSS JOIN (
                SELECT TOP (1) EQM_MASTER_NO
                FROM EQM_MASTER
                WHERE EQM_MASTER_NO IS NOT NULL
                ORDER BY EQM_MASTER_SID
            ) e
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
            DELETE FROM WIP_LOT_EQP_HIST
            WHERE WIP_LOT_HIST_SID IN (SELECT WIP_LOT_HIST_SID FROM WIP_LOT_HIST WHERE LOT = @Lot)
            """,
            new { Lot = lotCode },
            tx);
        await conn.ExecuteAsync("DELETE FROM WIP_LOT_CUR_EQP WHERE LOT = @Lot", new { Lot = lotCode }, tx);
        await conn.ExecuteAsync(
            """
            DELETE FROM WIP_LOT_USER_HIST
            WHERE IN_WIP_LOT_HIST_SID IN (SELECT WIP_LOT_HIST_SID FROM WIP_LOT_HIST WHERE LOT = @Lot)
            """,
            new { Lot = lotCode },
            tx);
        await conn.ExecuteAsync("DELETE FROM WIP_LOT_CUR_USER WHERE LOT = @Lot", new { Lot = lotCode }, tx);
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
        public decimal? ShiftSid { get; init; }
        public string WorkOrder { get; init; } = null!;
        public decimal RouteSid { get; init; }
        public decimal? PreviousReleaseQty { get; init; }
        public string EquipmentNo { get; init; } = null!;
    }

    private sealed class LotRow
    {
        public string LOT_STATUS_CODE { get; init; } = null!;
        public string? LOT_SUB_STATUS_CODE { get; init; }
    }

    private sealed class HistRow
    {
        public string ACTION_CODE { get; init; } = null!;
        public string LOT_STATUS_CODE { get; init; } = null!;
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
