using DcMateH5Api.Areas.RouteOperation.Interfaces;
using DcMateH5Api.Areas.RouteOperation.Mappers;
using DcMateH5Api.Areas.RouteOperation.Models;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.SqlHelper;

namespace DcMateH5Api.Areas.RouteOperation.Services
{
    /// <summary>
    /// BAS_OPERATION 的 CRUD 服務。
    /// </summary>
    public class BasOperationService : IBasOperationService
    {
        private readonly SQLGenerateHelper _sqlHelper;

        public BasOperationService(SQLGenerateHelper sqlHelper)
        {
            _sqlHelper = sqlHelper;
        }

        public async Task<decimal> CreateAsync(CreateOperationRequest request, CancellationToken ct)
        {
            var entity = OperationMapper.MapperCreate(request);
            await _sqlHelper.InsertAsync(entity, ct);
            return entity.SID;
        }

        public async Task<OperationViewModel?> GetAsync(decimal sid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_OPERATION>()
                .AndEq(x => x.SID, sid).AndNotDeleted();;

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return entity is null ? null : OperationMapper.ToViewModel(entity);
        }

        public async Task<IEnumerable<OperationViewModel>> GetAllAsync(CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_OPERATION>().AndNotDeleted();;
            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Select(OperationMapper.ToViewModel).ToList();
        }

        public async Task UpdateAsync(decimal sid, UpdateOperationRequest request, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_OPERATION>()
                .AndEq(x => x.SID, sid);

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct)
                         ?? throw new InvalidOperationException($"Operation not found: {sid}");

            OperationMapper.MapperUpdate(entity, request);
            await _sqlHelper.UpdateAllByIdAsync(entity, UpdateNullBehavior.IgnoreNulls, true, ct);
        }

        public async Task DeleteAsync(decimal sid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_OPERATION>()
                .AndEq(x => x.SID, sid);

            await _sqlHelper.DeleteWhereAsync(where, ct);
        }

        public async Task<bool> OperationCodeExistsAsync(string operationCode, CancellationToken ct, decimal? excludeSid = null)
        {
            var where = new WhereBuilder<BAS_OPERATION>()
                .AndEq(x => x.OPERATION_CODE, operationCode).AndNotDeleted();

            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(o => !excludeSid.HasValue || o.SID != excludeSid.Value);
        }
    }
}
