using System.Security.Claims;
using System.Text.Json;
using Dapper;
using DbExtensions;
using DbExtensions.DbExecutor.Service;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateClassLibrary.Models;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Eqm.Models;
using DcMateH5.Abstractions.Log;
using DcMateH5.Abstractions.Log.Models;
using DcMateH5.Infrastructure.Eqm;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace DcMateH5ApiTest.Eqm;

[Collection("DatabaseIntegration")]
public class EqmStatusChangeIntegrationTests
{
    private const string OperatorAccount = "EqmApiTest";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StatusChangeAsync_ShouldUpdateMasterAndInsertHistory()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var dataLinkSid = 900000002101m;
        var service = CreateService(connectionString);

        try
        {
            var result = await service.StatusChangeAsync(
                new EqmStatusChangeInputDto
                {
                    DATA_LINK_SID = dataLinkSid,
                    EQM_NO = arrangement.EqmNo,
                    EQM_STATUS_NO = arrangement.TargetStatusNo,
                    REASON_NO = arrangement.ReasonNo,
                    REPORT_TIME = new DateTime(2026, 5, 5, 10, 0, 0),
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    UPDATE_EQM_MASTER = true
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var master = await conn.QuerySingleAsync<MasterStatusRow>(
                """
                SELECT STATUS, STATUS_SID, STATUS_CHANGE_TIME, EDIT_USER
                FROM EQM_MASTER
                WHERE EQM_MASTER_NO = @EqmNo
                """,
                new { arrangement.EqmNo });
            Assert.Equal(arrangement.TargetStatusNo, master.STATUS);
            Assert.Equal(arrangement.TargetStatusSid, master.STATUS_SID);
            Assert.Equal("EqmApiTest", master.EDIT_USER);
            Assert.Equal(new DateTime(2026, 5, 5, 10, 0, 0), master.STATUS_CHANGE_TIME);

            var hist = await LoadHistoryAsync(conn, dataLinkSid);
            Assert.NotNull(hist);
            Assert.Equal(arrangement.EqmNo, hist!.EQM_NO);
            Assert.Equal(arrangement.TargetStatusNo, hist.TO_EQM_STATUS_CODE);
            Assert.Equal(arrangement.ReasonNo, hist.TRIG_REASON_CODE);
            Assert.Equal(OperatorAccount, hist.TRIG_USER);
            Assert.Equal("Day_Shift", hist.SHIFT_NO);
            Assert.Equal("2026-05-05", hist.SHIFT_DAY);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement, dataLinkSid);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StatusChangeAsync_ShouldInsertHistoryOnly_WhenUpdateEqmMasterIsFalse()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var dataLinkSid = 900000002102m;
        var service = CreateService(connectionString);

        try
        {
            var result = await service.StatusChangeAsync(
                new EqmStatusChangeInputDto
                {
                    DATA_LINK_SID = dataLinkSid,
                    EQM_NO = arrangement.EqmNo,
                    EQM_STATUS_NO = arrangement.TargetStatusNo,
                    REASON_NO = arrangement.ReasonNo,
                    REPORT_TIME = new DateTime(2026, 5, 5, 10, 5, 0),
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    UPDATE_EQM_MASTER = false
                });

            Assert.True(result.IsSuccess);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var master = await conn.QuerySingleAsync<MasterStatusRow>(
                """
                SELECT STATUS, STATUS_SID, STATUS_CHANGE_TIME, EDIT_USER
                FROM EQM_MASTER
                WHERE EQM_MASTER_NO = @EqmNo
                """,
                new { arrangement.EqmNo });
            Assert.Equal(arrangement.OriginalStatus, master.STATUS);
            Assert.Equal(arrangement.OriginalStatusSid, master.STATUS_SID);

            var hist = await LoadHistoryAsync(conn, dataLinkSid);
            Assert.NotNull(hist);
            Assert.Equal(arrangement.TargetStatusNo, hist!.TO_EQM_STATUS_CODE);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement, dataLinkSid);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StatusChangeAsync_ShouldResolveNightShiftToPreviousShiftDay()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var dataLinkSid = 900000002103m;
        var service = CreateService(connectionString);

        try
        {
            var result = await service.StatusChangeAsync(
                new EqmStatusChangeInputDto
                {
                    DATA_LINK_SID = dataLinkSid,
                    EQM_NO = arrangement.EqmNo,
                    EQM_STATUS_NO = arrangement.TargetStatusNo,
                    REASON_NO = arrangement.ReasonNo,
                    REPORT_TIME = new DateTime(2026, 5, 5, 1, 30, 0),
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    UPDATE_EQM_MASTER = false
                });

            Assert.True(result.IsSuccess);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            var hist = await LoadHistoryAsync(conn, dataLinkSid);

            Assert.NotNull(hist);
            Assert.Equal("Night_Shift", hist!.SHIFT_NO);
            Assert.Equal("2026-05-04", hist.SHIFT_DAY);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement, dataLinkSid);
        }
    }

    [Theory]
    [InlineData("UNKNOWN_EQM", "Idle", "1", "Eqm not found")]
    [InlineData("MC1", "UNKNOWN_STATUS", "1", "Eqm status not found or disabled")]
    [InlineData("MC1", "Idle", "UNKNOWN_REASON", "Reason not found or disabled")]
    [Trait("Category", "Integration")]
    public async Task StatusChangeAsync_ShouldRejectInvalidReferenceData(
        string eqmNo,
        string statusNo,
        string reasonNo,
        string expectedMessage)
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var service = CreateService(connectionString);

        var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.StatusChangeAsync(
            new EqmStatusChangeInputDto
            {
                DATA_LINK_SID = 900000002104m,
                EQM_NO = eqmNo,
                EQM_STATUS_NO = statusNo,
                REASON_NO = reasonNo,
                REPORT_TIME = new DateTime(2026, 5, 5, 10, 0, 0),
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                UPDATE_EQM_MASTER = false
            }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatusChangeAsync_ShouldRejectMissingRequiredInput()
    {
        var service = CreateService(TestConfiguration.LoadConnectionString());

        var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.StatusChangeAsync(
            new EqmStatusChangeInputDto
            {
                DATA_LINK_SID = 0,
                EQM_NO = "MC1",
                EQM_STATUS_NO = "Idle",
                REASON_NO = "1",
                REPORT_TIME = new DateTime(2026, 5, 5, 10, 0, 0),
                INPUT_FORM_NAME = "DcMateH5ApiTest",
                UPDATE_EQM_MASTER = false
            }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("DATA_LINK_SID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EqmStatusService CreateService(string connectionString)
    {
        var options = Options.Create(new DbOptions { Connection = connectionString });
        var connectionFactory = new SqlConnectionFactory(options);
        var connection = connectionFactory.Create();
        var dbExecutor = new DbExecutor(connection, new DbTransactionContext(), new FakeLogService(), new HttpContextAccessor());
        var sqlHelper = new SQLGenerateHelper(dbExecutor, new FakeCurrentUserAccessor(OperatorAccount));
        return new EqmStatusService(sqlHelper, new FakeCurrentUserAccessor(OperatorAccount));
    }

    private static async Task<TestArrangement> LoadArrangementAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var arrangement = await conn.QuerySingleOrDefaultAsync<TestArrangement>(
            """
            SELECT TOP (1)
                m.EQM_MASTER_NO AS EqmNo,
                m.STATUS AS OriginalStatus,
                m.STATUS_SID AS OriginalStatusSid,
                m.EDIT_STATUS_TIME AS OriginalEditStatusTime,
                m.STATUS_CHANGE_TIME AS OriginalStatusChangeTime,
                m.EDIT_USER AS OriginalEditUser,
                m.EDIT_TIME AS OriginalEditTime,
                s.EQM_STATUS_NO AS TargetStatusNo,
                s.EQM_STATUS_SID AS TargetStatusSid,
                r.REASON_NO AS ReasonNo
            FROM EQM_MASTER m
            CROSS APPLY (
                SELECT TOP (1) EQM_STATUS_NO, EQM_STATUS_SID
                FROM EQM_STATUS
                WHERE ENABLE_FLAG = 'Y'
                  AND (m.STATUS IS NULL OR EQM_STATUS_NO <> m.STATUS)
                ORDER BY EQM_STATUS_SID
            ) s
            CROSS APPLY (
                SELECT TOP (1) REASON_NO
                FROM ADM_REASON
                WHERE ENABLE_FLAG = 'Y'
                  AND REASON_NO IS NOT NULL
                ORDER BY ADM_REASON_SID
            ) r
            WHERE m.EQM_MASTER_NO IS NOT NULL
              AND m.ENABLE_FLAG = 'Y'
            ORDER BY m.EQM_MASTER_SID
            """);

        return arrangement ?? throw new InvalidOperationException("No Eqm test arrangement could be resolved from the database.");
    }

    private static async Task<HistoryRow?> LoadHistoryAsync(SqlConnection conn, decimal dataLinkSid)
    {
        return await conn.QuerySingleOrDefaultAsync<HistoryRow>(
            """
            SELECT TOP (1)
                EQM_NO,
                TO_EQM_STATUS_CODE,
                TRIG_REASON_CODE,
                TRIG_USER,
                SHIFT_NO,
                SHIFT_DAY
            FROM EQM_STATUS_CHANGE_HIST
            WHERE DATA_LINK_SID = @DataLinkSid
            ORDER BY CREATE_TIME DESC
            """,
            new { DataLinkSid = dataLinkSid });
    }

    private static async Task CleanupAsync(string connectionString, TestArrangement arrangement, decimal dataLinkSid)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync(
            "DELETE FROM EQM_STATUS_CHANGE_HIST WHERE DATA_LINK_SID = @DataLinkSid",
            new { DataLinkSid = dataLinkSid },
            tx);
        await conn.ExecuteAsync(
            """
            UPDATE EQM_MASTER
            SET STATUS = @OriginalStatus,
                STATUS_SID = @OriginalStatusSid,
                EDIT_STATUS_TIME = @OriginalEditStatusTime,
                STATUS_CHANGE_TIME = @OriginalStatusChangeTime,
                EDIT_USER = @OriginalEditUser,
                EDIT_TIME = @OriginalEditTime
            WHERE EQM_MASTER_NO = @EqmNo
            """,
            arrangement,
            tx);

        await tx.CommitAsync();
    }

    private sealed class TestArrangement
    {
        public string EqmNo { get; init; } = null!;
        public string? OriginalStatus { get; init; }
        public decimal? OriginalStatusSid { get; init; }
        public DateTime? OriginalEditStatusTime { get; init; }
        public DateTime? OriginalStatusChangeTime { get; init; }
        public string? OriginalEditUser { get; init; }
        public DateTime? OriginalEditTime { get; init; }
        public string TargetStatusNo { get; init; } = null!;
        public decimal TargetStatusSid { get; init; }
        public string ReasonNo { get; init; } = null!;
    }

    private sealed class MasterStatusRow
    {
        public string? STATUS { get; init; }
        public decimal? STATUS_SID { get; init; }
        public DateTime? STATUS_CHANGE_TIME { get; init; }
        public string? EDIT_USER { get; init; }
    }

    private sealed class HistoryRow
    {
        public string EQM_NO { get; init; } = null!;
        public string TO_EQM_STATUS_CODE { get; init; } = null!;
        public string TRIG_REASON_CODE { get; init; } = null!;
        public string TRIG_USER { get; init; } = null!;
        public string? SHIFT_NO { get; init; }
        public string? SHIFT_DAY { get; init; }
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
