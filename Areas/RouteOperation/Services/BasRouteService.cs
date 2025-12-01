using DcMateH5Api.Areas.RouteOperation.Interfaces;
using DcMateH5Api.Areas.RouteOperation.Mappers;
using DcMateH5Api.Areas.RouteOperation.Models;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.SqlHelper;

namespace DcMateH5Api.Areas.RouteOperation.Services
{
    /// <summary>
    /// BAS_ROUTE 的 CRUD 服務。
    /// </summary>
    public class BasRouteService : IBasRouteService
    {
        private readonly SQLGenerateHelper _sqlHelper;

        public BasRouteService(SQLGenerateHelper sqlHelper)
        {
            _sqlHelper = sqlHelper;
        }

        public async Task<decimal> CreateAsync(CreateRouteRequest request, CancellationToken ct)
        {
            var entity = RouteMapper.MapperCreate(request);
            await _sqlHelper.InsertAsync(entity, ct);
            return entity.SID;
        }

        public async Task<RouteViewModel?> GetAsync(decimal sid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE>()
                .AndEq(x => x.SID, sid);

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return entity is null ? null : RouteMapper.ToViewModel(entity);
        }

        public async Task<IEnumerable<RouteViewModel>> GetAllAsync(CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE>().AndNotDeleted();
            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Select(RouteMapper.ToViewModel).ToList();
        }

        public async Task UpdateAsync(decimal sid, UpdateRouteRequest request, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE>()
                .AndEq(x => x.SID, sid);

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct)
                         ?? throw new InvalidOperationException($"Route not found: {sid}");

            RouteMapper.MapperUpdate(entity, request);
            await _sqlHelper.UpdateAllByIdAsync(entity, UpdateNullBehavior.IgnoreNulls, true, ct);
        }

        public async Task DeleteAsync(decimal sid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE>()
                .AndEq(x => x.SID, sid);

            // 沒有 IsDelete 欄位的狀況下，這裡會是實體刪除。
            await _sqlHelper.DeleteWhereAsync(where, ct);
        }

        public async Task<bool> RouteCodeExistsAsync(string routeCode, CancellationToken ct, decimal? excludeSid = null)
        {
            var where = new WhereBuilder<BAS_ROUTE>()
                .AndEq(x => x.ROUTE_CODE, routeCode).AndNotDeleted();;

            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(r => !excludeSid.HasValue || r.SID != excludeSid.Value);
        }
    }
}
