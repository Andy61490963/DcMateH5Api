namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormOrphanCleanupService
{
    /// <summary>
    /// 清除未被最終 Schema（SCHEMA_TYPE = 5）引用的表單孤兒資料
    /// 依父子關係連鎖清理：
    /// Master → Config → Dropdown / ValidationRule / Options
    /// </summary>
    Task<bool> SoftDeleteOrphansAsync(Guid editUser, CancellationToken ct);
}