using DcMateH5.Abstractions.EQM.Models;
using DcMateH5Api.Models; // 引入 Result<T> 所在的命名空間
using System.Threading;
using System.Threading.Tasks;

namespace DcMateH5.Abstractions.EQM;

public interface IEqmAutoDcService
{
    /// <summary>
    /// 自動資料收集數據處理與計算
    /// </summary>
    Task<Result<bool>> ProcessAutoDcUploadAsync(EqmAutoDcInputDto input, CancellationToken ct = default);
}