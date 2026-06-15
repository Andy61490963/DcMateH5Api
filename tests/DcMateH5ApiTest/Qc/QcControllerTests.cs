using DcMateH5.Abstractions.Qc;
using DcMateH5.Abstractions.Qc.Models;
using DcMateH5Api.Areas.Qc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DcMateH5ApiTest.Qc;

public class QcControllerTests
{
    [Fact]
    public async Task CreateBatch_ShouldReturnCreatedInspectionNos()
    {
        var controller = new QcController(new FakeQcService(), NullLogger<QcController>.Instance);

        var result = await controller.CreateBatch(new QcBatchCreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<QcBatchCreateResponse>(ok.Value);
        Assert.Equal(["QC-1"], response.INSPECTION_NOS);
    }

    [Fact]
    public async Task CreateBatch_ShouldReturnServerError_WhenServiceThrows()
    {
        var controller = new QcController(new ThrowingQcService(), NullLogger<QcController>.Instance);

        var result = await controller.CreateBatch(new QcBatchCreateRequest(), CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.Equal("failed", Assert.IsType<QcErrorResponse>(error.Value).Message);
    }

    private sealed class FakeQcService : IQcService
    {
        public Task<QcBatchCreateResponse> CreateBatchAsync(QcBatchCreateRequest request, CancellationToken ct = default)
            => Task.FromResult(new QcBatchCreateResponse { INSPECTION_NOS = ["QC-1"] });
    }

    private sealed class ThrowingQcService : IQcService
    {
        public Task<QcBatchCreateResponse> CreateBatchAsync(QcBatchCreateRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("failed");
    }
}
