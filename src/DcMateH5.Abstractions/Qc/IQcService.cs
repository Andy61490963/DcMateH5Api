using DcMateH5.Abstractions.Qc.Models;

namespace DcMateH5.Abstractions.Qc;

public interface IQcService
{
    Task<QcBatchCreateResponse> CreateBatchAsync(QcBatchCreateRequest request, CancellationToken ct = default);
}
