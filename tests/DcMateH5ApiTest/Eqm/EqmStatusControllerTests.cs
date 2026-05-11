using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Eqm;
using DcMateH5.Abstractions.Eqm.Models;
using DcMateH5Api.Areas.Eqm.Controllers;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Xunit;

namespace DcMateH5ApiTest.Eqm;

public class EqmStatusControllerTests
{
    [Fact]
    public async Task StatusChange_ShouldReturnOkResult()
    {
        var controller = new EqmStatusController(new FakeEqmStatusService());

        var actionResult = await controller.StatusChange(new EqmStatusChangeInputDto(), CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
    }

    [Fact]
    public async Task StatusChange_ShouldReturnBadRequestResult_WhenServiceThrows()
    {
        var controller = new EqmStatusController(new ThrowingEqmStatusService());

        var actionResult = await controller.StatusChange(new EqmStatusChangeInputDto(), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal("bad request", badRequest.Value);
    }

    [Fact]
    public async Task StatusChangeGet_ShouldReturnOkResult()
    {
        var controller = new EqmStatusController(new FakeEqmStatusService());

        var actionResult = await controller.StatusChangeGet(new EqmStatusChangeInputDto(), CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
    }

    [Fact]
    public async Task StatusChangeGet_ShouldReturnBadRequestResult_WhenServiceThrows()
    {
        var controller = new EqmStatusController(new ThrowingEqmStatusService());

        var actionResult = await controller.StatusChangeGet(new EqmStatusChangeInputDto(), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal("bad request", badRequest.Value);
    }

    private sealed class FakeEqmStatusService : IEqmStatusService
    {
        public Task<Result<bool>> StatusChangeAsync(EqmStatusChangeInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));
    }

    private sealed class ThrowingEqmStatusService : IEqmStatusService
    {
        public Task<Result<bool>> StatusChangeAsync(EqmStatusChangeInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");
    }
}
