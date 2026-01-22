using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormDesignerService
{
    Task<List<FormFieldMasterDto>> GetFormMasters( FormFunctionType functionType, string? q, CancellationToken ct );
    
    Task UpdateFormName( UpdateFormNameViewModel model, CancellationToken ct );

    Task DeleteFormMaster(Guid id, CancellationToken ct = default);
    
    Task<FormDesignerIndexViewModel> GetFormDesignerIndexViewModel( FormFunctionType functionType, Guid? id, CancellationToken ct );
    
    List<string> SearchTables( string? tableName, TableQueryType queryType );
    
    Guid GetOrCreateFormMasterId( FormFieldMasterDto model );
    
    Task<FormFieldListViewModel?> EnsureFieldsSaved( string tableName, Guid? formMasterId, TableSchemaQueryType type );
    
    Task<FormFieldListViewModel> GetFieldsByTableName( string tableName, Guid? formMasterId, TableSchemaQueryType schemaType );

    /// <summary>
    /// 依欄位設定 ID 取得單一欄位設定。
    /// </summary>
    /// <param name="fieldId">欄位設定唯一識別碼</param>
    /// <returns>若找到欄位則回傳 <see cref="FormFieldViewModel"/>；否則回傳 null。</returns>
    Task<FormFieldViewModel?> GetFieldById(Guid fieldId);

    void UpsertField(FormFieldViewModel model, Guid formMasterId);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="req"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task MoveFieldAsync(MoveFormFieldRequest req, CancellationToken ct);
    
    /// <summary>
    /// 批次設定欄位的可編輯狀態。
    /// </summary>
    Task<string> SetAllEditable( Guid formMasterId, bool isEditable, CancellationToken ct );

    /// <summary>
    /// 批次設定欄位的必填狀態。
    /// </summary>
    Task<string> SetAllRequired( Guid formMasterId, bool isRequired, CancellationToken ct );

    bool CheckFieldExists(Guid fieldId);

    Task<List<FormFieldValidationRuleDto>> GetValidationRulesByFieldId(Guid fieldId, CancellationToken ct = default);

    bool HasValidationRules(Guid fieldId);

    FormFieldValidationRuleDto CreateEmptyValidationRule(Guid fieldConfigId);
    Task<bool> InsertValidationRule( FormFieldValidationRuleDto model, CancellationToken ct = default );
    int GetNextValidationOrder(Guid fieldId);

    FormControlType GetControlTypeByFieldId(Guid fieldId);

    Task<bool> SaveValidationRule( FormFieldValidationRuleDto model, CancellationToken ct = default );

    Task<bool> DeleteValidationRule( Guid id , CancellationToken ct = default );
    
    void EnsureDropdownCreated(Guid fieldId, bool? isUseSql = false, string? sql = null);

    Task<DropDownViewModel> GetDropdownSetting( Guid dropdownId, CancellationToken ct = default );

    Task<List<FormFieldDropdownOptionsDto>> GetDropdownOptions( Guid dropDownId, CancellationToken ct = default );

    Task SaveDropdownSql( Guid dropdownId, string sql, CancellationToken ct );
    
    Task SetDropdownMode( Guid dropdownId, bool isUseSql, CancellationToken ct );

    ValidateSqlResultViewModel ValidateDropdownSql( string sql );
    
    ValidateSqlResultViewModel ImportDropdownOptionsFromSql( string sql, Guid dropdownId );

    /// <summary>
    /// 匯入先前查詢的下拉選單值（欄位別名需為 NAME）。
    /// </summary>
    /// <param name="sql">僅允許 SELECT 的 SQL 語句</param>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <returns>匯入結果</returns>
    PreviousQueryDropdownImportResultViewModel ImportPreviousQueryDropdownValues(string sql, Guid dropdownId);

    Task ReplaceDropdownOptionsAsync(Guid dropdownId, IReadOnlyList<DropdownOptionItemViewModel> options, CancellationToken ct = default);

    Task<Guid> SaveFormHeader( FormHeaderViewModel model );

    Task<Guid> SaveMasterDetailFormHeader(MasterDetailFormHeaderViewModel model);
    
    /// <summary>
    /// 儲存多對多表單主檔設定，並回傳 FORM_FIELD_MASTER 主鍵。
    /// </summary>
    Task<Guid> SaveMultipleMappingFormHeader(MultipleMappingFormHeaderViewModel model);

    /// <summary>
    /// 檢查表格名稱與 View 名稱的組合是否已存在於 FORM_FIELD_MASTER
    /// </summary>
    /// <param name="baseTableId">資料表名稱</param>
    /// <param name="viewTableId">View 表名稱</param>
    /// <param name="excludeId">編輯時排除自身 ID</param>
    /// <returns>若存在相同組合則回傳 true</returns>
    bool CheckFormMasterExists(Guid baseTableId, Guid viewTableId, Guid? excludeId = null);

    Task<bool> CheckMasterDetailFormMasterExistsAsync(
        Guid masterTableId,
        Guid detailTableId,
        Guid viewTableId,
        Guid? excludeId = null);

    /// <summary>
    /// 取得刪除防呆 SQL 規則清單。
    /// </summary>
    /// <param name="formFieldMasterId">表單主檔 ID（可空）</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>規則清單</returns>
    Task<List<FormFieldDeleteGuardSqlDto>> GetDeleteGuardSqls(Guid? formFieldMasterId, CancellationToken ct = default);

    /// <summary>
    /// 取得單筆刪除防呆 SQL 規則。
    /// </summary>
    /// <param name="id">規則 ID</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>規則明細</returns>
    Task<FormFieldDeleteGuardSqlDto?> GetDeleteGuardSql(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 新增刪除防呆 SQL 規則。
    /// </summary>
    /// <param name="model">新增內容</param>
    /// <param name="currentUserId">目前登入使用者 ID</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>新增後的規則資料</returns>
    Task<FormFieldDeleteGuardSqlDto> CreateDeleteGuardSql(
        FormFieldDeleteGuardSqlCreateViewModel model,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// 更新刪除防呆 SQL 規則。
    /// </summary>
    /// <param name="id">規則 ID</param>
    /// <param name="model">更新內容</param>
    /// <param name="currentUserId">目前登入使用者 ID</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>更新後的規則資料（找不到則回傳 null）</returns>
    Task<FormFieldDeleteGuardSqlDto?> UpdateDeleteGuardSql(
        Guid id,
        FormFieldDeleteGuardSqlUpdateViewModel model,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// 刪除刪除防呆 SQL 規則（軟刪除）。
    /// </summary>
    /// <param name="id">規則 ID</param>
    /// <param name="currentUserId">目前登入使用者 ID</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>是否刪除成功</returns>
    Task<bool> DeleteDeleteGuardSql(Guid id, Guid? currentUserId, CancellationToken ct = default);
}
