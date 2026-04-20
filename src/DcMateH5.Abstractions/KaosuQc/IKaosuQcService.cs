using DcMateH5.Abstractions.KaosuQc.Models;

namespace DcMateH5.Abstractions.KaosuQc;

public interface IKaosuQcService
{
    /// <summary>
    /// 批次新增 Kaosu 品檢單頭與單身。
    /// </summary>
    Task<KaosuQcBatchCreateResponse> CreateBatchAsync(KaosuQcBatchCreateRequest request, CancellationToken ct = default);
}