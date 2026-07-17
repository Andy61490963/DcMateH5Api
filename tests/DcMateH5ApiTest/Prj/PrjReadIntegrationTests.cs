using System.Security.Claims;
using System.Text.Json;
using System.Net;
using Dapper;
using DbExtensions;
using DbExtensions.DbExecutor.Service;
using DcMateClassLibrary.Models;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Log;
using DcMateH5.Abstractions.Log.Models;
using DcMateH5.Abstractions.Prj.Models;
using DcMateH5.Infrastructure.Prj;
using DcMateClassLibrary.Helper.HttpHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DcMateH5ApiTest.Prj;

[Collection("DatabaseIntegration")]
public sealed class PrjReadIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadApis_ShouldQueryExistingPrjTablesWithoutMutation()
    {
        await using var connection = new SqlConnection(LoadConnectionString());
        var executor = new DbExecutor(connection, new DbTransactionContext(), new FakeLogService(), new HttpContextAccessor());
        var service = new PrjService(executor, new FakeCurrentUserAccessor());

        var projects = await service.GetProjectsAsync(new PrjProjectQuery { Page = 1, PageSize = 10 });
        Assert.True(projects.TotalCount > 0);
        Assert.NotEmpty(projects.Items);
        Assert.DoesNotContain(projects.Items, x => x.StartTime?.Date == new DateTime(1900, 1, 1));
        Assert.DoesNotContain(projects.Items, x => x.ExpectedTime?.Date == new DateTime(1900, 1, 1));
        Assert.DoesNotContain(projects.Items, x => x.EndTime?.Date == new DateTime(1900, 1, 1));

        var project = await service.GetProjectAsync(projects.Items[0].ProjectCode);
        Assert.Equal(projects.Items[0].ProjectCode, project.ProjectCode);

        var details = await service.GetDetailsAsync(project.ProjectCode, new PrjDetailQuery { Page = 1, PageSize = 10 });
        Assert.DoesNotContain(details.Items, x => x.StartTime?.Date == new DateTime(1900, 1, 1));
        Assert.DoesNotContain(details.Items, x => x.ExpectedTime?.Date == new DateTime(1900, 1, 1));
        Assert.DoesNotContain(details.Items, x => x.EndTime?.Date == new DateTime(1900, 1, 1));

        var options = await service.GetOptionsAsync();
        Assert.NotEmpty(options.ProjectStatuses);
        Assert.NotEmpty(options.ProjectTypes);
        Assert.NotEmpty(options.DetailStatuses);
        Assert.NotEmpty(options.ProcessTypes);

        Assert.NotEmpty(await service.GetCustomersAsync(null, 5));
        Assert.NotEmpty(await service.GetUsersAsync(null, 5));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WriteFlow_ShouldCreateUpdateDetectConflictAndSoftDelete()
    {
        var connectionString = LoadConnectionString();
        var setup = await LoadSetupAsync(connectionString);
        var projectCode = $"ITEST-PRJ-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        await using var serviceConnection = new SqlConnection(connectionString);
        var executor = new DbExecutor(serviceConnection, new DbTransactionContext(), new FakeLogService(), new HttpContextAccessor());
        var service = new PrjService(executor, new FakeCurrentUserAccessor());

        try
        {
            var created = await service.CreateProjectAsync(new CreatePrjProjectRequest
            {
                ProjectCode = projectCode,
                ProjectName = "PRJ 整合測試",
                StatusNo = setup.ProjectStatusNo,
                TypeNo = setup.ProjectTypeNo,
                CustomerNo = setup.CustomerNo,
                StartTime = DateTime.Today,
                ExpectedTime = DateTime.Today.AddDays(30)
            });

            Assert.Equal(projectCode, created.ProjectCode);
            Assert.NotNull(created.EditTime);

            var updated = await service.UpdateProjectAsync(projectCode, new UpdatePrjProjectRequest
            {
                ProjectName = "PRJ 整合測試更新",
                StatusNo = setup.ProjectStatusNo,
                TypeNo = setup.ProjectTypeNo,
                CustomerNo = setup.CustomerNo,
                StartTime = DateTime.Today,
                ExpectedTime = DateTime.Today.AddDays(45),
                EditTime = created.EditTime
            });
            Assert.Equal("PRJ 整合測試更新", updated.ProjectName);

            var conflict = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                service.UpdateProjectAsync(projectCode, new UpdatePrjProjectRequest
                {
                    ProjectName = "使用過期版本更新",
                    StatusNo = setup.ProjectStatusNo,
                    TypeNo = setup.ProjectTypeNo,
                    CustomerNo = setup.CustomerNo,
                    EditTime = created.EditTime
                }));
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

            await service.ReorderProjectsAsync(new ReorderPrjProjectsRequest
            {
                Items = [new PrjProjectOrderItem { ProjectCode = projectCode, Seq = 10, EditTime = updated.EditTime }]
            });
            var reorderedProject = await service.GetProjectAsync(projectCode);
            Assert.Equal(10, reorderedProject.Seq);

            var detail = await service.CreateDetailAsync(projectCode, new CreatePrjDetailRequest
            {
                ProcessTypeNo = setup.ProcessTypeNo,
                StatusNo = setup.DetailStatusNo,
                Summary = "PRJ 整合測試工作",
                PrincipalUser = setup.AccountNo,
                ExpectedTime = DateTime.Today.AddDays(10)
            });
            Assert.Equal(projectCode, detail.ProjectCode);

            var detailUpdated = await service.UpdateDetailAsync(detail.DetailSid, new UpdatePrjDetailRequest
            {
                ProcessTypeNo = setup.ProcessTypeNo,
                StatusNo = setup.DetailStatusNo,
                Summary = "PRJ 整合測試工作更新",
                PrincipalUser = setup.AccountNo,
                ExpectedTime = DateTime.Today.AddDays(12),
                EditTime = detail.EditTime
            });
            Assert.Equal("PRJ 整合測試工作更新", detailUpdated.Summary);

            await service.ReorderDetailsAsync(projectCode, new ReorderPrjDetailsRequest
            {
                Items = [new PrjDetailOrderItem { DetailSid = detail.DetailSid, Seq = 20, EditTime = detailUpdated.EditTime }]
            });
            var reorderedDetail = await service.GetDetailAsync(detail.DetailSid);
            Assert.Equal(20, reorderedDetail.Seq);

            var activeDetailConflict = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                service.ChangeProjectEnabledAsync(projectCode, new ChangeEnabledRequest
                {
                    Enabled = false,
                    EditTime = reorderedProject.EditTime
                }));
            Assert.Equal(HttpStatusCode.Conflict, activeDetailConflict.StatusCode);

            var statusChanged = await service.ChangeDetailStatusAsync(detail.DetailSid, new ChangePrjDetailStatusRequest
            {
                StatusNo = setup.DetailStatusNo,
                EditTime = reorderedDetail.EditTime
            });
            var detailDisabled = await service.ChangeDetailEnabledAsync(detail.DetailSid, new ChangeEnabledRequest
            {
                Enabled = false,
                EditTime = statusChanged.EditTime
            });
            Assert.False(detailDisabled.Enabled);

            var projectDisabled = await service.ChangeProjectEnabledAsync(projectCode, new ChangeEnabledRequest
            {
                Enabled = false,
                EditTime = reorderedProject.EditTime
            });
            Assert.False(projectDisabled.Enabled);
        }
        finally
        {
            await CleanupAsync(connectionString, projectCode);
        }
    }

    private static async Task<IntegrationSetup> LoadSetupAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<IntegrationSetup>("""
            SELECT
                (SELECT TOP (1) PROJECT_MAINTAIN_STATUS_NO FROM PRJ_MASTER_STATUS ORDER BY PROJECT_MAINTAIN_STATUS_NO) AS ProjectStatusNo,
                (SELECT TOP (1) TYPE_NO FROM PRJ_MASTER_TYPE ORDER BY TYPE_NO) AS ProjectTypeNo,
                (SELECT TOP (1) PROJECT_STATUS_NO FROM PRJ_DETAIL_STATUS ORDER BY PROJECT_STATUS_NO) AS DetailStatusNo,
                (SELECT TOP (1) PROCESS_TYPE FROM PRJ_DETAIL_PROCESS_TYPE ORDER BY PROCESS_TYPE) AS ProcessTypeNo,
                (SELECT TOP (1) CUSTOMER_NO FROM ADM_CUSTOMER WHERE ENABLE_FLAG = 'Y' ORDER BY CUSTOMER_NO) AS CustomerNo,
                (SELECT TOP (1) ACCOUNT_NO FROM ADM_USER WHERE ENABLE_FLAG = 'Y' ORDER BY ACCOUNT_NO) AS AccountNo;
            """);
    }

    private static async Task CleanupAsync(string connectionString, string projectCode)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("DELETE FROM PRJ_DETAIL WHERE PROJECT_CODE = @ProjectCode;", new { ProjectCode = projectCode }, transaction);
        await connection.ExecuteAsync("DELETE FROM PRJ_MASTER WHERE PROJECT_CODE = @ProjectCode;", new { ProjectCode = projectCode }, transaction);
        await transaction.CommitAsync();
    }

    private static string LoadConnectionString()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DcMateH5Api.sln")))
            directory = directory.Parent;
        if (directory == null) throw new DirectoryNotFoundException("找不到方案根目錄。");

        var json = File.ReadAllText(Path.Combine(directory.FullName, "src", "DcMateH5Api", "appsettings.json"));
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("ConnectionStrings").GetProperty("Connection").GetString()
               ?? throw new InvalidOperationException("找不到資料庫連線字串。");
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserSnapshot Get()
        {
            var claims = new[]
            {
                new Claim(AppClaimTypes.Account, "PRJ_INTEGRATION_TEST"),
                new Claim(AppClaimTypes.UserId, Guid.NewGuid().ToString()),
                new Claim(AppClaimTypes.UserLv, "1")
            };
            return CurrentUserSnapshot.From(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
        }
    }

    private sealed class IntegrationSetup
    {
        public decimal ProjectStatusNo { get; init; }
        public decimal ProjectTypeNo { get; init; }
        public decimal DetailStatusNo { get; init; }
        public decimal ProcessTypeNo { get; init; }
        public string CustomerNo { get; init; } = string.Empty;
        public string AccountNo { get; init; } = string.Empty;
    }

    private sealed class FakeLogService : ILogService
    {
        public Task LogAsync(SqlLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SqlLogEntry>> GetLogsAsync(SqlLogQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SqlLogEntry>>([]);
    }
}
