using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services;

/// <summary>
/// 清除未被最終 Schema（SCHEMA_TYPE=5）使用的孤兒設定（Soft Delete）。
/// 清理策略：先刪父（Master），再用「找不到父節點」一路往下刪。
/// </summary>
public sealed class FormOrphanCleanupService : IFormOrphanCleanupService
{
    private readonly SqlConnection _con;
    private readonly ITransactionService _tx;

    public FormOrphanCleanupService(SqlConnection con, ITransactionService tx)
    {
        _con = con;
        _tx = tx;
    }

    /// <summary>
    /// 執行孤兒清理（Soft Delete）
    /// </summary>
    /// <returns>
    /// true  = 有實際清到資料  
    /// false = 本次沒有任何資料需要清理
    /// </returns>
    public async Task<bool> SoftDeleteOrphansAsync(Guid editUser, CancellationToken ct)
    {
        const string sql = @"
DECLARE @EditUser UNIQUEIDENTIFIER = @p_EditUser;
DECLARE @Now DATETIME = GETDATE();
DECLARE @SafeBefore DATETIME = DATEADD(HOUR, -1, GETDATE());
DECLARE @Affected INT = 0;

/* 1) Master */
;WITH Schema5UsedIds AS
(
    SELECT DISTINCT v.UsedId
    FROM dbo.FORM_FIELD_MASTER m
    CROSS APPLY (VALUES
        (m.BASE_TABLE_ID),
        (m.DETAIL_TABLE_ID),
        (m.VIEW_TABLE_ID),
        (m.MAPPING_TABLE_ID)
    ) v(UsedId)
    WHERE m.IS_DELETE = 0
      AND m.SCHEMA_TYPE = 5
      AND v.UsedId IS NOT NULL
),
OrphanMasters AS
(
    SELECT m.ID
    FROM dbo.FORM_FIELD_MASTER m
    LEFT JOIN Schema5UsedIds u ON u.UsedId = m.ID
    WHERE m.IS_DELETE = 0
      AND m.SCHEMA_TYPE <> 5
      AND u.UsedId IS NULL
      AND ISNULL(m.EDIT_TIME, m.CREATE_TIME) < @SafeBefore
)
UPDATE m
SET m.IS_DELETE = 1,
    m.EDIT_USER = @EditUser,
    m.EDIT_TIME = @Now
FROM dbo.FORM_FIELD_MASTER m
JOIN OrphanMasters om ON om.ID = m.ID;

SET @Affected += @@ROWCOUNT;

/* 2) Config */
UPDATE c
SET c.IS_DELETE = 1,
    c.EDIT_USER = @EditUser,
    c.EDIT_TIME = @Now
FROM dbo.FORM_FIELD_CONFIG c
WHERE c.IS_DELETE = 0
  AND ISNULL(c.EDIT_TIME, c.CREATE_TIME) < @SafeBefore
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.FORM_FIELD_MASTER m
      WHERE m.ID = c.FORM_FIELD_MASTER_ID
        AND m.IS_DELETE = 0
  );

SET @Affected += @@ROWCOUNT;

/* 3) DropdownOptions */
UPDATE opt
SET opt.IS_DELETE = 1,
    opt.EDIT_USER = @EditUser,
    opt.EDIT_TIME = @Now
FROM dbo.FORM_FIELD_DROPDOWN_OPTIONS opt
WHERE opt.IS_DELETE = 0
  AND ISNULL(opt.EDIT_TIME, opt.CREATE_TIME) < @SafeBefore
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.FORM_FIELD_DROPDOWN d
      WHERE d.ID = opt.FORM_FIELD_DROPDOWN_ID
        AND d.IS_DELETE = 0
  );

SET @Affected += @@ROWCOUNT;

/* 4) Dropdown */
UPDATE d
SET d.IS_DELETE = 1,
    d.EDIT_USER = @EditUser,
    d.EDIT_TIME = @Now
FROM dbo.FORM_FIELD_DROPDOWN d
WHERE d.IS_DELETE = 0
  AND ISNULL(d.EDIT_TIME, d.CREATE_TIME) < @SafeBefore
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.FORM_FIELD_CONFIG c
      WHERE c.ID = d.FORM_FIELD_CONFIG_ID
        AND c.IS_DELETE = 0
  );

SET @Affected += @@ROWCOUNT;

/* 5) ValidationRule */
UPDATE vr
SET vr.IS_DELETE = 1,
    vr.EDIT_USER = @EditUser,
    vr.EDIT_TIME = @Now
FROM dbo.FORM_FIELD_VALIDATION_RULE vr
WHERE vr.IS_DELETE = 0
  AND ISNULL(vr.EDIT_TIME, vr.CREATE_TIME) < @SafeBefore
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.FORM_FIELD_CONFIG c
      WHERE c.ID = vr.FORM_FIELD_CONFIG_ID
        AND c.IS_DELETE = 0
  );

SET @Affected += @@ROWCOUNT;

SELECT @Affected;
";

        var affected = await _tx.WithTransactionAsync<int>(async (tx, token) =>
        {
            var cmd = new CommandDefinition(
                sql,
                new { p_EditUser = editUser },
                transaction: tx,
                cancellationToken: token);

            return await _con.ExecuteScalarAsync<int>(cmd);
        }, ct);
        
        return affected > 0;
    }
}
