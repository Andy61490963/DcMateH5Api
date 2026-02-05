using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.SqlHelper;
using DCMATEH5API.Areas.Menu.Services;
using DcMateH5Api.Areas.Wip.Interfaces;
using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5Api.Areas.Wip.Services;


public class BaseInfoCheckExistService : IBaseInfoCheckExistService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPasswordHasher _passwordHasher; 
    private readonly IConfiguration _config;
    private readonly IMenuService _menuService;
    public BaseInfoCheckExistService(
        SQLGenerateHelper sqlHelper,
        IHttpContextAccessor httpContextAccessor,
        IPasswordHasher passwordHasher,
        IConfiguration config,
        IMenuService menuService)
    {
        _sqlHelper = sqlHelper;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = passwordHasher;
        _config = config; 
        _menuService = menuService;
    }

    public Task<UmmUserDto?> CheckUserExistAsync(string accountNo, CancellationToken ct = default)
    {
        var where = new WhereBuilder<UmmUserDto>()
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
}