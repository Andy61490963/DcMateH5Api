using DcMateH5Api.Areas.Wip.Interfaces;
using DcMateH5Api.SqlHelper;
using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5Api.Areas.Wip.Services;


public class SelectDtoService : ISelectDtoService
{
    private readonly SQLGenerateHelper _sqlHelper;
    public SelectDtoService(
        SQLGenerateHelper sqlHelper)
    {
        _sqlHelper = sqlHelper;
    }

    public Task<UmmUserDto?> SelectUserAsync(string accountNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<UmmUserDto>()
            .AndEq(x => x.ACCOUNT_NO, accountNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<EqmMasterDto?> SelectEquipmentAsync(string eqmMasterNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<EqmMasterDto>()
            .AndEq(x => x.EQM_MASTER_NO, eqmMasterNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<WipWoDto?> SelectWorkOrderAsync(string wo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipWoDto>()
            .AndEq(x => x.WO, wo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<WipOperationDto?> SelectOperationAsync(string operationNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipOperationDto>()
            .AndEq(x => x.WIP_OPERATION_NO, operationNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<WipDepartmentDto?> SelectDepartmentAsync(string deptNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipDepartmentDto>()
            .AndEq(x => x.DEPT_NO, deptNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }
    
    public Task<List<WipOpiWdoeacicoHistDetailDto>> SelectWipOpiHistOkAsync(decimal wipOpiSid, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipOpiWdoeacicoHistDetailDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_SID, wipOpiSid);
        
        return _sqlHelper.SelectWhereAsync(where, ct);
    }
}