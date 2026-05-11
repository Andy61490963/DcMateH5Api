using ClassLibrary;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Mms;
using DcMateH5.Abstractions.Mms.Models;
using DcMateH5Api.Areas.MMS.Controllers;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Xunit;

namespace DcMateH5ApiTest.Mms;

public class MmsLotControllerTests
{
    [Fact]
    public async Task CreateMLot_ShouldReturnOkResult()
    {
        var controller = new MmsLotController(new FakeMmsLotService());

        var actionResult = await controller.CreateMLot(new MmsCreateMLotInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task MLotConsume_ShouldReturnBadRequestResult_WhenServiceThrows()
    {
        var controller = new MmsLotController(new ThrowingMmsLotService());

        var actionResult = await controller.MLotConsume(new MmsMLotConsumeInputDto(), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.BadRequest, badRequest.StatusCode);
        var result = Assert.IsType<Result<bool>>(badRequest.Value);
        Assert.False(result.IsSuccess);
        Assert.Equal(MmsLotErrorCode.BadRequest.ToString(), result.Code);
    }

    [Fact]
    public async Task MLotUNConsume_ShouldReturnOkResult()
    {
        var controller = new MmsLotController(new FakeMmsLotService());

        var actionResult = await controller.MLotUNConsume(new MmsMLotUNConsumeInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task MLotStateChange_ShouldReturnOkResult()
    {
        var controller = new MmsLotController(new FakeMmsLotService());

        var actionResult = await controller.MLotStateChange(new MmsMLotStateChangeInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    private sealed class FakeMmsLotService : IMmsLotService
    {
        public Task<Result<bool>> CreateMLotAsync(MmsCreateMLotInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> MLotConsumeAsync(MmsMLotConsumeInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> MLotUNConsumeAsync(MmsMLotUNConsumeInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> MLotStateChangeAsync(MmsMLotStateChangeInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));
    }

    private sealed class ThrowingMmsLotService : IMmsLotService
    {
        public Task<Result<bool>> CreateMLotAsync(MmsCreateMLotInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> MLotConsumeAsync(MmsMLotConsumeInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> MLotUNConsumeAsync(MmsMLotUNConsumeInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> MLotStateChangeAsync(MmsMLotStateChangeInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");
    }
}
