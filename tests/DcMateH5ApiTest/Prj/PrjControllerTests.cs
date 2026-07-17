using System.Reflection;
using System.Xml.Linq;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.Prj;
using DcMateH5.Abstractions.Prj.Models;
using DcMateH5Api.Areas.PRJ.Controllers;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DcMateH5ApiTest.Prj;

public sealed class PrjControllerTests
{
    [Fact]
    public void Controller_ShouldExposeExpectedSixteenRoutes()
    {
        var methods = typeof(PrjController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var routes = methods
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>()
                .Select(attribute => $"{attribute.HttpMethods.Single()} {attribute.Template}"))
            .OrderBy(x => x)
            .ToArray();

        var expected = new[]
        {
            "GET Details/{detailSid:decimal}",
            "GET Options",
            "GET Options/Customers",
            "GET Options/Users",
            "GET Projects",
            "GET Projects/{projectCode}",
            "GET Projects/{projectCode}/Details",
            "PATCH Details/{detailSid:decimal}/enabled",
            "PATCH Details/{detailSid:decimal}/status",
            "PATCH Projects/{projectCode}/enabled",
            "POST Projects",
            "POST Projects/{projectCode}/Details",
            "PUT Details/{detailSid:decimal}",
            "PUT Projects/reorder",
            "PUT Projects/{projectCode}",
            "PUT Projects/{projectCode}/Details/reorder"
        }.OrderBy(x => x).ToArray();

        Assert.Equal(expected, routes);
        Assert.NotNull(typeof(PrjController).GetCustomAttribute<AuthorizeAttribute>());
        var explorer = typeof(PrjController).GetCustomAttribute<ApiExplorerSettingsAttribute>();
        Assert.Equal(SwaggerGroups.Prj, explorer?.GroupName);
    }

    [Fact]
    public void EveryApi_ShouldHaveSwaggerSummaryAndRemarks()
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "DcMateH5Api.xml");
        Assert.True(File.Exists(xmlPath), $"找不到 Swagger XML 文件：{xmlPath}");
        var document = XDocument.Load(xmlPath);
        var members = document.Descendants("member")
            .Where(x => ((string?)x.Attribute("name"))?.StartsWith("M:DcMateH5Api.Areas.PRJ.Controllers.PrjController.", StringComparison.Ordinal) == true)
            .ToList();

        var apiMethods = typeof(PrjController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(x => x.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        foreach (var method in apiMethods)
        {
            var member = members.SingleOrDefault(x => ((string?)x.Attribute("name"))?.Contains($".{method.Name}(", StringComparison.Ordinal) == true
                                                     || ((string?)x.Attribute("name"))?.EndsWith($".{method.Name}", StringComparison.Ordinal) == true);
            Assert.NotNull(member);
            Assert.False(string.IsNullOrWhiteSpace(member!.Element("summary")?.Value));
            Assert.False(string.IsNullOrWhiteSpace(member.Element("remarks")?.Value));
        }
    }

    [Fact]
    public async Task ReadApis_ShouldReturnResultEnvelope()
    {
        var controller = new PrjController(new FakePrjService());

        await AssertOk<PagedResult<PrjProjectListItemDto>>(controller.GetProjects(new PrjProjectQuery(), CancellationToken.None));
        await AssertOk<PrjProjectDto>(controller.GetProject("P001", CancellationToken.None));
        await AssertOk<PagedResult<PrjDetailDto>>(controller.GetDetails("P001", new PrjDetailQuery(), CancellationToken.None));
        await AssertOk<PrjDetailDto>(controller.GetDetail(1, CancellationToken.None));
        await AssertOk<PrjLookupOptionsDto>(controller.GetOptions(CancellationToken.None));
        await AssertOk<IReadOnlyList<PrjTextOptionDto>>(controller.GetCustomers(null, 20, CancellationToken.None));
        await AssertOk<IReadOnlyList<PrjTextOptionDto>>(controller.GetUsers(null, 20, CancellationToken.None));
    }

    [Fact]
    public async Task WriteApis_ShouldReturnExpectedEnvelope()
    {
        var controller = new PrjController(new FakePrjService());
        var now = DateTime.UtcNow;

        var createdProject = Assert.IsType<CreatedAtActionResult>(await controller.CreateProject(new CreatePrjProjectRequest { ProjectCode = "P001" }, CancellationToken.None));
        Assert.IsType<Result<PrjProjectDto>>(createdProject.Value);
        await AssertOk<PrjProjectDto>(controller.UpdateProject("P001", new UpdatePrjProjectRequest { EditTime = now }, CancellationToken.None));
        await AssertOk<PrjProjectDto>(controller.ChangeProjectEnabled("P001", new ChangeEnabledRequest { Enabled = true, EditTime = now }, CancellationToken.None));
        await AssertOk<bool>(controller.ReorderProjects(new ReorderPrjProjectsRequest { Items = [new PrjProjectOrderItem { ProjectCode = "P001", Seq = 1, EditTime = now }] }, CancellationToken.None));

        var createdDetail = Assert.IsType<CreatedAtActionResult>(await controller.CreateDetail("P001", new CreatePrjDetailRequest { StatusNo = 1 }, CancellationToken.None));
        Assert.IsType<Result<PrjDetailDto>>(createdDetail.Value);
        await AssertOk<PrjDetailDto>(controller.UpdateDetail(1, new UpdatePrjDetailRequest { StatusNo = 1, EditTime = now }, CancellationToken.None));
        await AssertOk<PrjDetailDto>(controller.ChangeDetailStatus(1, new ChangePrjDetailStatusRequest { StatusNo = 3, EditTime = now }, CancellationToken.None));
        await AssertOk<PrjDetailDto>(controller.ChangeDetailEnabled(1, new ChangeEnabledRequest { Enabled = false, EditTime = now }, CancellationToken.None));
        await AssertOk<bool>(controller.ReorderDetails("P001", new ReorderPrjDetailsRequest { Items = [new PrjDetailOrderItem { DetailSid = 1, Seq = 1, EditTime = now }] }, CancellationToken.None));
    }

    private static async Task AssertOk<T>(Task<IActionResult> action)
    {
        var result = await action;
        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<Result<T>>(ok.Value);
        Assert.True(envelope.IsSuccess);
    }

    private sealed class FakePrjService : IPrjService
    {
        private static readonly PrjProjectDto Project = new() { ProjectCode = "P001", EditTime = DateTime.UtcNow };
        private static readonly PrjDetailDto Detail = new() { DetailSid = 1, ProjectCode = "P001", StatusNo = 1, EditTime = DateTime.UtcNow };

        public Task<PagedResult<PrjProjectListItemDto>> GetProjectsAsync(PrjProjectQuery query, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PrjProjectListItemDto> { Items = [Project], Page = 1, PageSize = 20, TotalCount = 1 });
        public Task<PrjProjectDto> GetProjectAsync(string projectCode, CancellationToken ct = default) => Task.FromResult(Project);
        public Task<PrjProjectDto> CreateProjectAsync(CreatePrjProjectRequest request, CancellationToken ct = default) => Task.FromResult(Project);
        public Task<PrjProjectDto> UpdateProjectAsync(string projectCode, UpdatePrjProjectRequest request, CancellationToken ct = default) => Task.FromResult(Project);
        public Task<PrjProjectDto> ChangeProjectEnabledAsync(string projectCode, ChangeEnabledRequest request, CancellationToken ct = default) => Task.FromResult(Project);
        public Task ReorderProjectsAsync(ReorderPrjProjectsRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PagedResult<PrjDetailDto>> GetDetailsAsync(string projectCode, PrjDetailQuery query, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PrjDetailDto> { Items = [Detail], Page = 1, PageSize = 20, TotalCount = 1 });
        public Task<PrjDetailDto> GetDetailAsync(decimal detailSid, CancellationToken ct = default) => Task.FromResult(Detail);
        public Task<PrjDetailDto> CreateDetailAsync(string projectCode, CreatePrjDetailRequest request, CancellationToken ct = default) => Task.FromResult(Detail);
        public Task<PrjDetailDto> UpdateDetailAsync(decimal detailSid, UpdatePrjDetailRequest request, CancellationToken ct = default) => Task.FromResult(Detail);
        public Task<PrjDetailDto> ChangeDetailStatusAsync(decimal detailSid, ChangePrjDetailStatusRequest request, CancellationToken ct = default) => Task.FromResult(Detail);
        public Task<PrjDetailDto> ChangeDetailEnabledAsync(decimal detailSid, ChangeEnabledRequest request, CancellationToken ct = default) => Task.FromResult(Detail);
        public Task ReorderDetailsAsync(string projectCode, ReorderPrjDetailsRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PrjLookupOptionsDto> GetOptionsAsync(CancellationToken ct = default) => Task.FromResult(new PrjLookupOptionsDto());
        public Task<IReadOnlyList<PrjTextOptionDto>> GetCustomersAsync(string? keyword, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PrjTextOptionDto>>([]);
        public Task<IReadOnlyList<PrjTextOptionDto>> GetUsersAsync(string? keyword, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PrjTextOptionDto>>([]);
    }
}
