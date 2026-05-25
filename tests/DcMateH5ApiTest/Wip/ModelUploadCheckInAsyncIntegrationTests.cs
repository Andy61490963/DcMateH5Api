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
public class ModelUploadCheckInAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ModelUploadCheckInAsync_ShouldInsertTolHistAndCavRows()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        await EnsureTestPartNosExistAsync(connectionString, arrangement);
        var service = CreateService(connectionString, arrangement.AccountNo);
        var now = DateTime.Now;
        var checkInTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Local);
        WipModelUploadCheckInResponseDto? result = null;

        try
        {
            result = await service.ModelUploadCheckInAsync(
                new WipModelUploadCheckInInputDto
                {
                    Account = new List<string> { arrangement.AccountNo },
                    Equipment = arrangement.EquipmentNos,
                    CheckInTime = checkInTime,
                    Operation = arrangement.OperationNo,
                    Department = arrangement.DepartmentNo,
                    Comment = "ModelUploadCheckInAsync integration test",
                    Details = new List<WipModelUploadCheckInDetailInputDto>
                    {
                        new()
                        {
                            WorkOrder = arrangement.FirstWorkOrder,
                            TolNo = arrangement.TolNo,
                            TolDetalsNo = arrangement.FirstTolDetalsNo,
                            PartNo = arrangement.FirstTolPartNo,
                            Cav = 2
                        },
                        new()
                        {
                            WorkOrder = arrangement.SecondWorkOrder,
                            TolNo = arrangement.TolNo,
                            TolDetalsNo = arrangement.SecondTolDetalsNo,
                            PartNo = arrangement.SecondTolPartNo,
                            Cav = 4
                        }
                    }
                });

            Assert.True(result.TolSid > 0);
            Assert.Equal(2, result.HistSids.Count);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var tol = await conn.QuerySingleOrDefaultAsync<TolRow>(
                """
                SELECT WIP_OPI_WDOEACICO_HIST_TOL_SID, TOL_NO, MODLE_UPLOAD_START, IS_ACTIVE
                FROM WIP_OPI_WDOEACICO_HIST_TOL
                WHERE WIP_OPI_WDOEACICO_HIST_TOL_SID = @TolSid
                """,
                new { result.TolSid });

            Assert.NotNull(tol);
            Assert.Equal(result.TolSid, tol!.WIP_OPI_WDOEACICO_HIST_TOL_SID);
            Assert.Equal(arrangement.TolNo, tol.TOL_NO);
            Assert.Equal(checkInTime, tol.MODLE_UPLOAD_START);
            Assert.Equal("Y", tol.IS_ACTIVE);

            var hists = (await conn.QueryAsync<HistRow>(
                """
                SELECT WIP_OPI_WDOEACICO_HIST_SID, WO, EQP_NO, PART_NO, TOL_NO, TOL_DETALS_NO,
                       CHECK_IN_TIME, WIP_OPI_WDOEACICO_HIST_TOL_SID, COMPLETED, ENABLE_FLAG
                FROM WIP_OPI_WDOEACICO_HIST
                WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids
                ORDER BY TOL_DETALS_NO
                """,
                new { HistSids = result.HistSids })).ToList();

            Assert.Equal(2, hists.Count);
            Assert.All(hists, hist =>
            {
                Assert.Equal(result.TolSid, hist.WIP_OPI_WDOEACICO_HIST_TOL_SID);
                Assert.Null(hist.EQP_NO);
                Assert.Equal(arrangement.TolNo, hist.TOL_NO);
                Assert.Equal(checkInTime, hist.CHECK_IN_TIME);
                Assert.Equal("N", hist.COMPLETED);
                Assert.Equal("Y", hist.ENABLE_FLAG);
            });
            var firstHist = Assert.Single(hists, x => x.WO == arrangement.FirstWorkOrder);
            Assert.Equal(arrangement.FirstTolDetalsNo, firstHist.TOL_DETALS_NO);
            Assert.Equal(arrangement.FirstTolPartNo, firstHist.PART_NO);

            var secondHist = Assert.Single(hists, x => x.WO == arrangement.SecondWorkOrder);
            Assert.Equal(arrangement.SecondTolDetalsNo, secondHist.TOL_DETALS_NO);
            Assert.Equal(arrangement.SecondTolPartNo, secondHist.PART_NO);

            var histEquipments = (await conn.QueryAsync<HistEquipmentRow>(
                """
                SELECT WIP_OPI_WDOEACICO_HIST_SID, EQP_NO
                FROM WIP_OPI_WDOEACICO_HIST_EQP
                WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids
                ORDER BY WIP_OPI_WDOEACICO_HIST_SID, EQP_NO
                """,
                new { HistSids = result.HistSids })).ToList();

            Assert.Equal(result.HistSids.Count * arrangement.EquipmentNos.Count, histEquipments.Count);
            foreach (var histSid in result.HistSids)
            {
                var equipmentNos = histEquipments
                    .Where(x => x.WIP_OPI_WDOEACICO_HIST_SID == histSid)
                    .Select(x => x.EQP_NO)
                    .OrderBy(x => x)
                    .ToList();
                Assert.Equal(arrangement.EquipmentNos.OrderBy(x => x).ToList(), equipmentNos);
            }

            var cavs = (await conn.QueryAsync<CavRow>(
                """
                SELECT WIP_OPI_WDOEACICO_HIST_CAV_SID, WIP_OPI_WDOEACICO_HIST_SID, OPI_CAV, START_TIME, END_TIME
                FROM WIP_OPI_WDOEACICO_HIST_CAV
                WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids
                ORDER BY OPI_CAV
                """,
                new { HistSids = result.HistSids })).ToList();

            Assert.Equal(2, cavs.Count);
            Assert.Equal("2", cavs[0].OPI_CAV);
            Assert.Equal("4", cavs[1].OPI_CAV);
            Assert.All(cavs, cav =>
            {
                Assert.Equal(checkInTime, cav.START_TIME);
                Assert.Null(cav.END_TIME);
            });

            var updatedUploadEnd = checkInTime.AddHours(2);
            var updatedRemoveStart = checkInTime.AddHours(4);
            await service.EditModelUploadCavAsync(
                new WipEditModelUploadCavInputDto
                {
                    WIP_OPI_WDOEACICO_HIST_CAV_SID = cavs[0].WIP_OPI_WDOEACICO_HIST_CAV_SID,
                    OPI_CAV = 8
                });
            await service.EditModelUploadEndAsync(
                new WipEditModelUploadEndInputDto
                {
                    WIP_OPI_WDOEACICO_HIST_TOL_SID = result.TolSid,
                    MODLE_UPLOAD_END = updatedUploadEnd
                });
            await service.EditModelRemoveStartAsync(
                new WipEditModelRemoveStartInputDto
                {
                    WIP_OPI_WDOEACICO_HIST_TOL_SID = result.TolSid,
                    MODLE_REMOVE_START = updatedRemoveStart
                });

            var updatedCav = await conn.QuerySingleAsync<string>(
                """
                SELECT OPI_CAV
                FROM WIP_OPI_WDOEACICO_HIST_CAV
                WHERE WIP_OPI_WDOEACICO_HIST_CAV_SID = @CavSid
                """,
                new { CavSid = cavs[0].WIP_OPI_WDOEACICO_HIST_CAV_SID });
            var updatedTol = await conn.QuerySingleAsync<TolDateRow>(
                """
                SELECT MODLE_UPLOAD_END, MODLE_REMOVE_START
                FROM WIP_OPI_WDOEACICO_HIST_TOL
                WHERE WIP_OPI_WDOEACICO_HIST_TOL_SID = @TolSid
                """,
                new { result.TolSid });

            Assert.Equal("8", updatedCav);
            Assert.Equal(updatedUploadEnd, updatedTol.MODLE_UPLOAD_END);
            Assert.Equal(updatedRemoveStart, updatedTol.MODLE_REMOVE_START);

            var modelUploadCheckOutTime = checkInTime.AddHours(6);
            await service.ModelUploadCheckOutAsync(
                new WipModelUploadCheckOutInputDto
                {
                    WIP_OPI_WDOEACICO_HIST_TOL_SID = result.TolSid,
                    CHECK_OUT_TIME = modelUploadCheckOutTime
                });

            var checkedOutTol = await conn.QuerySingleAsync<TolDateRow>(
                """
                SELECT MODLE_UPLOAD_END, MODLE_REMOVE_START, MODLE_REMOVE_END
                FROM WIP_OPI_WDOEACICO_HIST_TOL
                WHERE WIP_OPI_WDOEACICO_HIST_TOL_SID = @TolSid
                """,
                new { result.TolSid });
            var checkedOutHists = (await conn.QueryAsync<HistCheckOutRow>(
                """
                SELECT CHECK_OUT_TIME, COMPLETED
                FROM WIP_OPI_WDOEACICO_HIST
                WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids
                """,
                new { HistSids = result.HistSids })).ToList();
            var checkedOutCavEndTimes = (await conn.QueryAsync<DateTime?>(
                """
                SELECT END_TIME
                FROM WIP_OPI_WDOEACICO_HIST_CAV
                WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids
                """,
                new { HistSids = result.HistSids })).ToList();

            Assert.Equal(updatedUploadEnd, checkedOutTol.MODLE_UPLOAD_END);
            Assert.Equal(modelUploadCheckOutTime, checkedOutTol.MODLE_REMOVE_END);
            Assert.All(checkedOutHists, hist =>
            {
                Assert.Equal(modelUploadCheckOutTime, hist.CHECK_OUT_TIME);
                Assert.Equal("Y", hist.COMPLETED);
            });
            Assert.All(checkedOutCavEndTimes, endTime => Assert.Equal(modelUploadCheckOutTime, endTime));
        }
        finally
        {
            if (result != null)
            {
                await CleanupAsync(connectionString, result.TolSid, result.HistSids);
            }

            await CleanupTestPartNosAsync(connectionString, arrangement.InsertedPartNos);
        }
    }

    private static WipBaseSettingService CreateService(string connectionString, string accountNo)
    {
        var options = Options.Create(new DbOptions { Connection = connectionString });
        var connectionFactory = new SqlConnectionFactory(options);
        var connection = connectionFactory.Create();
        var dbExecutor = new DbExecutor(
            connection,
            new DbTransactionContext(),
            new FakeLogService(),
            new HttpContextAccessor());

        var sqlHelper = new SQLGenerateHelper(dbExecutor, new FakeCurrentUserAccessor(accountNo));

        return new WipBaseSettingService(
            sqlHelper,
            new BaseInfoCheckExistService(sqlHelper),
            new SelectDtoService(sqlHelper));
    }

    private static async Task<TestArrangement> LoadArrangementAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var rows = (await conn.QueryAsync<WorkOrderRow>(
            """
            SELECT TOP (2) WO AS WorkOrder, PART_NO AS PartNo
            FROM WIP_WO
            WHERE WO IS NOT NULL
              AND PART_NO IS NOT NULL
            ORDER BY WO_SID DESC
            """)).ToList();

        if (rows.Count < 2)
        {
            throw new InvalidOperationException("At least two work orders are required for the integration test.");
        }

        var toolRows = (await conn.QueryAsync<ToolDetailRow>(
            """
            WITH CandidateTool AS (
                SELECT TOP (1) d.TOL_MASTER_NO
                FROM TOL_MASTER_DETAILS d
                INNER JOIN TOL_MASTER m ON m.TOL_MASTER_NO = d.TOL_MASTER_NO
                WHERE d.TOL_MASTER_NO IS NOT NULL
                  AND d.TOL_MASTER_DETALS_NO IS NOT NULL
                  AND d.PART_NO IS NOT NULL
                GROUP BY d.TOL_MASTER_NO
                HAVING COUNT(1) >= 2
                ORDER BY d.TOL_MASTER_NO
            )
            SELECT TOP (2)
                d.TOL_MASTER_NO AS TolNo,
                d.TOL_MASTER_DETALS_NO AS TolDetalsNo,
                d.PART_NO AS PartNo
            FROM TOL_MASTER_DETAILS d
            INNER JOIN CandidateTool c ON c.TOL_MASTER_NO = d.TOL_MASTER_NO
            WHERE d.TOL_MASTER_DETALS_NO IS NOT NULL
              AND d.PART_NO IS NOT NULL
            ORDER BY d.TOL_MASTER_DETALS_SID
            """)).ToList();

        if (toolRows.Count < 2)
        {
            throw new InvalidOperationException("At least one tool with two details is required for the integration test.");
        }

        var row = await conn.QuerySingleOrDefaultAsync<TestArrangement>(
            """
            SELECT TOP (1)
                u.ACCOUNT_NO AS AccountNo,
                o.WIP_OPERATION_NO AS OperationNo,
                d.DEPT_NO AS DepartmentNo
            FROM (
                SELECT TOP (1) ACCOUNT_NO
                FROM ADM_OPI_USER
                WHERE ACCOUNT_NO IS NOT NULL
                ORDER BY USER_SID DESC
            ) u
            CROSS JOIN (
                SELECT TOP (1) WIP_OPERATION_NO
                FROM WIP_OPERATION
                WHERE WIP_OPERATION_NO IS NOT NULL
                ORDER BY WIP_OPERATION_SID
            ) o
            CROSS JOIN (
                SELECT TOP (1) DEPT_NO
                FROM WIP_DEPARTMENT
                WHERE DEPT_NO IS NOT NULL
                ORDER BY DEPT_SID
            ) d
            """);

        if (row == null)
        {
            throw new InvalidOperationException("No test arrangement could be resolved from the database.");
        }

        row.EquipmentNos = (await conn.QueryAsync<string>(
            """
            SELECT TOP (2) EQM_MASTER_NO
            FROM EQM_MASTER
            WHERE EQM_MASTER_NO IS NOT NULL
            ORDER BY EQM_MASTER_SID
            """)).ToList();

        if (row.EquipmentNos.Count < 2)
        {
            throw new InvalidOperationException("At least two equipments are required for the integration test.");
        }

        row.FirstWorkOrder = rows[0].WorkOrder;
        row.FirstPartNo = rows[0].PartNo;
        row.SecondWorkOrder = rows[1].WorkOrder;
        row.SecondPartNo = rows[1].PartNo;
        row.TolNo = toolRows[0].TolNo;
        row.FirstTolDetalsNo = toolRows[0].TolDetalsNo;
        row.FirstTolPartNo = toolRows[0].PartNo;
        row.SecondTolDetalsNo = toolRows[1].TolDetalsNo;
        row.SecondTolPartNo = toolRows[1].PartNo;
        return row;
    }

    private static async Task EnsureTestPartNosExistAsync(string connectionString, TestArrangement arrangement)
    {
        var partNos = new[] { arrangement.FirstTolPartNo, arrangement.SecondTolPartNo }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        foreach (var partNo in partNos)
        {
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_PARTNO WHERE WIP_PARTNO_NO = @PartNo",
                new { PartNo = partNo });
            if (exists > 0)
            {
                continue;
            }

            var sid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + arrangement.InsertedPartNos.Count + 900000000000m;
            await conn.ExecuteAsync(
                """
                INSERT INTO WIP_PARTNO
                    (WIP_PARTNO_SID, WIP_PARTNO_NO, WIP_PARTNO_NAME, PARTNO_SPEC, ENABLE_FLAG, IS_DELETE)
                VALUES
                    (@Sid, @PartNo, @PartNo, @PartNo, 'Y', 0)
                """,
                new { Sid = sid, PartNo = partNo });

            arrangement.InsertedPartNos.Add(partNo);
        }
    }

    private static async Task CleanupAsync(string connectionString, decimal tolSid, IReadOnlyCollection<decimal> histSids)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync(
            "DELETE FROM WIP_OPI_WDOEACICO_HIST_CAV WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids",
            new { HistSids = histSids },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_OPI_WDOEACICO_HIST_USER WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids",
            new { HistSids = histSids },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_OPI_WDOEACICO_HIST_EQP WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids",
            new { HistSids = histSids },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_OPI_WDOEACICO_HIST WHERE WIP_OPI_WDOEACICO_HIST_SID IN @HistSids",
            new { HistSids = histSids },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_OPI_WDOEACICO_HIST_TOL WHERE WIP_OPI_WDOEACICO_HIST_TOL_SID = @TolSid",
            new { TolSid = tolSid },
            tx);

        await tx.CommitAsync();
    }

    private static async Task CleanupTestPartNosAsync(string connectionString, IReadOnlyCollection<string> partNos)
    {
        if (partNos.Count == 0)
        {
            return;
        }

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            "DELETE FROM WIP_PARTNO WHERE WIP_PARTNO_NO IN @PartNos",
            new { PartNos = partNos });
    }

    private sealed class TestArrangement
    {
        public string AccountNo { get; init; } = null!;
        public List<string> EquipmentNos { get; set; } = new();
        public string OperationNo { get; init; } = null!;
        public string DepartmentNo { get; init; } = null!;
        public string FirstWorkOrder { get; set; } = null!;
        public string FirstPartNo { get; set; } = null!;
        public string SecondWorkOrder { get; set; } = null!;
        public string SecondPartNo { get; set; } = null!;
        public string TolNo { get; set; } = null!;
        public string FirstTolDetalsNo { get; set; } = null!;
        public string FirstTolPartNo { get; set; } = null!;
        public string SecondTolDetalsNo { get; set; } = null!;
        public string SecondTolPartNo { get; set; } = null!;
        public List<string> InsertedPartNos { get; } = new();
    }

    private sealed class WorkOrderRow
    {
        public string WorkOrder { get; init; } = null!;
        public string PartNo { get; init; } = null!;
    }

    private sealed class ToolDetailRow
    {
        public string TolNo { get; init; } = null!;
        public string TolDetalsNo { get; init; } = null!;
        public string PartNo { get; init; } = null!;
    }

    private sealed class TolRow
    {
        public decimal WIP_OPI_WDOEACICO_HIST_TOL_SID { get; init; }
        public string TOL_NO { get; init; } = null!;
        public DateTime MODLE_UPLOAD_START { get; init; }
        public string IS_ACTIVE { get; init; } = null!;
    }

    private sealed class HistRow
    {
        public decimal WIP_OPI_WDOEACICO_HIST_SID { get; init; }
        public string WO { get; init; } = null!;
        public string? EQP_NO { get; init; }
        public string PART_NO { get; init; } = null!;
        public string TOL_NO { get; init; } = null!;
        public string TOL_DETALS_NO { get; init; } = null!;
        public DateTime CHECK_IN_TIME { get; init; }
        public decimal WIP_OPI_WDOEACICO_HIST_TOL_SID { get; init; }
        public string COMPLETED { get; init; } = null!;
        public string ENABLE_FLAG { get; init; } = null!;
    }

    private sealed class CavRow
    {
        public decimal WIP_OPI_WDOEACICO_HIST_CAV_SID { get; init; }
        public decimal WIP_OPI_WDOEACICO_HIST_SID { get; init; }
        public string OPI_CAV { get; init; } = null!;
        public DateTime START_TIME { get; init; }
        public DateTime? END_TIME { get; init; }
    }

    private sealed class TolDateRow
    {
        public DateTime? MODLE_UPLOAD_END { get; init; }
        public DateTime? MODLE_REMOVE_START { get; init; }
        public DateTime? MODLE_REMOVE_END { get; init; }
    }

    private sealed class HistCheckOutRow
    {
        public DateTime? CHECK_OUT_TIME { get; init; }
        public string COMPLETED { get; init; } = null!;
    }

    private sealed class HistEquipmentRow
    {
        public decimal WIP_OPI_WDOEACICO_HIST_SID { get; init; }
        public string EQP_NO { get; init; } = null!;
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
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate solution root.");
        }
    }
}
