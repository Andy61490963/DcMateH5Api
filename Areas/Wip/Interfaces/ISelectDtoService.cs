using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5Api.Areas.Wip.Interfaces
{
    /// <summary>
    /// 提供使用者驗證功能的服務介面。
    /// </summary>
    public interface ISelectDtoService
    {
        Task<UmmUserDto?> SelectUserAsync(string accountNo, CancellationToken ct = default);
        Task<EqmMasterDto?> SelectEquipmentAsync(string eqmMasterNo, CancellationToken ct = default);
        Task<WipWoDto?> SelectWorkOrderAsync(string wo, CancellationToken ct = default);
        Task<WipOperationDto?> SelectOperationAsync(string operationNo, CancellationToken ct = default);
        Task<WipDepartmentDto?> SelectDepartmentAsync(string deptNo, CancellationToken ct = default);
        
        Task<List<WipOpiWdoeacicoHistDetailDto>> SelectWipOpiHistOkAsync(decimal wipOpiSid, CancellationToken ct = default);
    }
}
