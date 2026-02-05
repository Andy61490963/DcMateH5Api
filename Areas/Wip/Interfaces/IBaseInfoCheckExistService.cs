using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5Api.Areas.Wip.Interfaces
{
    /// <summary>
    /// 提供使用者驗證功能的服務介面。
    /// </summary>
    public interface IBaseInfoCheckExistService
    {
        Task<UmmUserDto?> CheckUserExistAsync(string accountNo, CancellationToken ct = default);
        Task<EqmMasterDto?> CheckEquipmentExistAsync(string eqmMasterNo, CancellationToken ct = default);
        Task<WipWoDto?> CheckWorkOrderExistAsync(string wo, CancellationToken ct = default);
        Task<WipOperationDto?> CheckOperationExistAsync(string operationNo, CancellationToken ct = default);
        Task<WipDepartmentDto?> CheckDepartmentExistAsync(string deptNo, CancellationToken ct = default);
    }
}
