using DbExtensions;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5.Infrastructure.Wip;

public class BaseInfoCheckExistService : IBaseInfoCheckExistService
{
    private readonly SQLGenerateHelper _sqlHelper;

    public BaseInfoCheckExistService(
        SQLGenerateHelper sqlHelper)
    {
        _sqlHelper = sqlHelper;
    }

    public Task<AdmUserDto?> CheckUserExistAsync(string accountNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<AdmUserDto>()
            .AndEq(x => x.ACCOUNT_NO, accountNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<EqmMasterDto?> CheckEquipmentExistAsync(string eqmMasterNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<EqmMasterDto>()
            .AndEq(x => x.EQM_MASTER_NO, eqmMasterNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<WipWoDto?> CheckWorkOrderExistAsync(string wo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipWoDto>()
            .AndEq(x => x.WO, wo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<WipOperationDto?> CheckOperationExistAsync(string operationNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipOperationDto>()
            .AndEq(x => x.WIP_OPERATION_NO, operationNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<WipDepartmentDto?> CheckDepartmentExistAsync(string deptNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipDepartmentDto>()
            .AndEq(x => x.DEPT_NO, deptNo);
        
        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<WipPartNoDto?> CheckPartNoExistAsync(string partNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<WipPartNoDto>()
            .AndEq(x => x.WIP_PARTNO_NO, partNo);

        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<TolMasterDto?> CheckToolExistAsync(string tolNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<TolMasterDto>()
            .AndEq(x => x.TOL_MASTER_NO, tolNo);

        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }

    public Task<TolMasterDetailsDto?> CheckToolDetailExistAsync(
        string tolNo,
        string tolDetalsNo,
        CancellationToken ct = default)
    {
        var where = new WhereBuilder<TolMasterDetailsDto>()
            .AndEq(x => x.TOL_MASTER_NO, tolNo)
            .AndEq(x => x.TOL_MASTER_DETALS_NO, tolDetalsNo);

        return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
    }
}
