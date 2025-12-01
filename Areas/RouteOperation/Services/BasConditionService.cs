using DcMateH5Api.Areas.RouteOperation.Interfaces;
using DcMateH5Api.Areas.RouteOperation.Mappers;
using DcMateH5Api.Areas.RouteOperation.Models;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.SqlHelper;

namespace DcMateH5Api.Areas.RouteOperation.Services
{
    /// <summary>
    /// BAS_CONDITION 的 CRUD 服務。
    /// </summary>
    public class BasConditionService : IBasConditionService
    {
        private readonly SQLGenerateHelper _sqlHelper;

        public BasConditionService(SQLGenerateHelper sqlHelper)
        {
            _sqlHelper = sqlHelper;
        }

        public async Task<decimal> CreateAsync(CreateConditionRequest request, CancellationToken ct)
        {
            var entity = ConditionMapper.MapperCreate(request);
            await _sqlHelper.InsertAsync(entity, ct);
            return entity.SID;
        }

        public async Task<ConditionViewModel?> GetAsync(decimal sid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_CONDITION>()
                .AndEq(x => x.SID, sid).AndNotDeleted();;

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return entity is null ? null : ConditionMapper.ToViewModel(entity);
        }

        public async Task<IEnumerable<ConditionViewModel>> GetAllAsync(CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_CONDITION>().AndNotDeleted();;
            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Select(ConditionMapper.ToViewModel).ToList();
        }

        public async Task UpdateAsync(decimal sid, UpdateConditionRequest request, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_CONDITION>()
                .AndEq(x => x.SID, sid);

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct)
                         ?? throw new InvalidOperationException($"Condition not found: {sid}");

            ConditionMapper.MapperUpdate(entity, request);
            await _sqlHelper.UpdateAllByIdAsync(entity, UpdateNullBehavior.IgnoreNulls, true, ct);
        }

        public async Task DeleteAsync(decimal sid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_CONDITION>()
                .AndEq(x => x.SID, sid);

            await _sqlHelper.DeleteWhereAsync(where, ct);
        }

        public async Task<bool> ConditionCodeExistsAsync(string conditionCode, CancellationToken ct, decimal? excludeSid = null)
        {
            var where = new WhereBuilder<BAS_CONDITION>()
                .AndEq(x => x.CONDITION_CODE, conditionCode).AndNotDeleted();;

            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(c => !excludeSid.HasValue || c.SID != excludeSid.Value);
        }
    }
}
