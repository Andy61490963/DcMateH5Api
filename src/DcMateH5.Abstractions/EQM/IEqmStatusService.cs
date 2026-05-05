using DcMateH5.Abstractions.Eqm.Models;
using DcMateH5Api.Models;

namespace DcMateH5.Abstractions.Eqm;

public interface IEqmStatusService
{
    Task<Result<bool>> StatusChangeAsync(EqmStatusChangeInputDto input, CancellationToken ct = default);
}
