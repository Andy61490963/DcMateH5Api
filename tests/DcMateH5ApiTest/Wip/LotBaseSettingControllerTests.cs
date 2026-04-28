using ClassLibrary;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Controllers;
using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Xunit;

namespace DcMateH5ApiTest.Wip;

public class LotBaseSettingControllerTests
{
    [Fact]
    public async Task CreateLot_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.CreateLot(new WipCreateLotInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotCheckIn_ShouldReturnBadRequestResult_WhenServiceThrows()
    {
        var controller = new WipLotSettingController(new ThrowingLotBaseSettingService());

        var actionResult = await controller.LotCheckIn(new WipLotCheckInInputDto(), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.BadRequest, badRequest.StatusCode);
        var result = Assert.IsType<Result<bool>>(badRequest.Value);
        Assert.False(result.IsSuccess);
        Assert.Equal(WipLotErrorCode.BadRequest.ToString(), result.Code);
    }

    [Fact]
    public async Task LotCheckInCancel_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotCheckInCancel(new WipLotCheckInCancelInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotCheckOut_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotCheckOut(new WipLotCheckOutInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotReassignOperation_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotReassignOperation(new WipLotReassignOperationInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotRecordDC_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotRecordDC(new WipLotRecordDcInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotHold_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotHold(new WipLotHoldInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotHoldRelease_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotHoldRelease(new WipLotHoldReleaseInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotBonus_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotBonus(new WipLotBonusInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LotScrap_ShouldReturnOkResult()
    {
        var controller = new WipLotSettingController(new FakeLotBaseSettingService());

        var actionResult = await controller.LotScrap(new WipLotScrapInputDto(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<Result<bool>>(okResult.Value);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
    }

    private sealed class FakeLotBaseSettingService : ILotBaseSettingService
    {
        public Task<Result<bool>> CreateLotAsync(WipCreateLotInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> CreateLotsAsync(IEnumerable<WipCreateLotInputDto> inputs, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotCheckInAsync(WipLotCheckInInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotCheckInCancelAsync(WipLotCheckInCancelInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotCheckOutAsync(WipLotCheckOutInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotReassignOperationAsync(WipLotReassignOperationInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotRecordDcAsync(WipLotRecordDcInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotHoldAsync(WipLotHoldInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotHoldReleaseAsync(WipLotHoldReleaseInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotBonusAsync(WipLotBonusInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<bool>> LotScrapAsync(WipLotScrapInputDto input, CancellationToken ct = default)
            => Task.FromResult(Result<bool>.Ok(true));
    }

    private sealed class ThrowingLotBaseSettingService : ILotBaseSettingService
    {
        public Task<Result<bool>> CreateLotAsync(WipCreateLotInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> CreateLotsAsync(IEnumerable<WipCreateLotInputDto> inputs, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotCheckInAsync(WipLotCheckInInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotCheckInCancelAsync(WipLotCheckInCancelInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotCheckOutAsync(WipLotCheckOutInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotReassignOperationAsync(WipLotReassignOperationInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotRecordDcAsync(WipLotRecordDcInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotHoldAsync(WipLotHoldInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotHoldReleaseAsync(WipLotHoldReleaseInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotBonusAsync(WipLotBonusInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");

        public Task<Result<bool>> LotScrapAsync(WipLotScrapInputDto input, CancellationToken ct = default)
            => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "bad request");
    }
}
