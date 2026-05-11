using DcMateH5.Abstractions.Mms.Models;
using DcMateH5Api.Models;

namespace DcMateH5.Abstractions.Mms;

public interface IMmsLotService
{
    Task<Result<bool>> CreateMLotAsync(MmsCreateMLotInputDto input, CancellationToken ct = default);
    Task<Result<bool>> MLotConsumeAsync(MmsMLotConsumeInputDto input, CancellationToken ct = default);
    Task<Result<bool>> MLotUNConsumeAsync(MmsMLotUNConsumeInputDto input, CancellationToken ct = default);
    Task<Result<bool>> MLotStateChangeAsync(MmsMLotStateChangeInputDto input, CancellationToken ct = default);
}
