using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;
using DcMateH5Api.Areas.Form.ViewModels;
using System.Threading;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormDesignerService
{
    Task<List<FORM_FIELD_Master>> GetFormMasters(CancellationToken ct);
    void DeleteFormMaster(Guid id);
    
    Task<FormDesignerIndexViewModel> GetFormDesignerIndexViewModel(Guid? id, CancellationToken ct);
    
    /// <summary>
    /// 依名稱關鍵字查詢資料表或檢視表名稱清單。
    /// 支援前綴與模糊比對（使用 LIKE）。
    /// </summary>
    /// <param name="tableNamePattern">表名稱關鍵字或樣式</param>
    /// <param name="schemaType">欲搜尋的資料來源類型（主表或檢視表）</param>
    /// <returns>符合條件的表名稱集合</returns>
    List<string> SearchTables(string? tableNamePattern, TableSchemaQueryType schemaType);
    
    Guid GetOrCreateFormMasterId(FORM_FIELD_Master model);
    
    FormFieldListViewModel? EnsureFieldsSaved(
        string tableName,
        Guid? formMasterId,
        TableSchemaQueryType type,
        string? formName = null);
    FormFieldListViewModel GetFieldsByTableName(string tableName, Guid? formMasterId, TableSchemaQueryType schemaType);

    /// <summary>
    /// 依欄位設定 ID 取得單一欄位設定。
    /// </summary>
    /// <param name="fieldId">欄位設定唯一識別碼</param>
    /// <returns>若找到欄位則回傳 <see cref="FormFieldViewModel"/>；否則回傳 null。</returns>
    FormFieldViewModel? GetFieldById(Guid fieldId);

    void UpsertField(FormFieldViewModel model, Guid formMasterId);

    /// <summary>
    /// 批次設定欄位的可編輯狀態。
    /// </summary>
    void SetAllEditable(Guid formMasterId, string tableName, bool isEditable);

    /// <summary>
    /// 批次設定欄位的必填狀態。
    /// </summary>
    void SetAllRequired(Guid formMasterId, string tableName, bool isRequired);

    bool CheckFieldExists(Guid fieldId);
    
    List<FormFieldValidationRuleDto> GetValidationRulesByFieldId(Guid fieldId);

    bool HasValidationRules(Guid fieldId);

    FormFieldValidationRuleDto CreateEmptyValidationRule(Guid fieldConfigId);
    void InsertValidationRule(FormFieldValidationRuleDto model);
    int GetNextValidationOrder(Guid fieldId);

    FormControlType GetControlTypeByFieldId(Guid fieldId);

    bool SaveValidationRule(FormFieldValidationRuleDto rule);

    bool DeleteValidationRule(Guid id);

    /// <summary>
    /// 確保 FORM_FIELD_DROPDOWN 存在，
    /// 可依需求指定預設的 SQL 來源與是否使用 SQL。
    /// </summary>
    /// <param name="fieldId">欄位設定 ID</param>
    /// <param name="isUseSql">是否使用 SQL 為資料來源，預設為 false；OnlyView 可帶入 null</param>
    /// <param name="sql">預設 SQL 查詢語句，預設為 null</param>
    void EnsureDropdownCreated(Guid fieldId, bool? isUseSql = false, string? sql = null);
    
    DropDownViewModel GetDropdownSetting(Guid fieldId);

    List<FORM_FIELD_DROPDOWN_OPTIONS> GetDropdownOptions(Guid dropDownId);
    
    void SaveDropdownSql(Guid fieldId, string sql);
    Guid SaveDropdownOption(Guid? id, Guid dropdownId, string optionText, string optionValue, string? optionTable = null);

    void DeleteDropdownOption(Guid optionId);

    void SetDropdownMode(Guid dropdownId, bool isUseSql);

    ValidateSqlResultViewModel ValidateDropdownSql(string sql);

    /// <summary>
    /// 執行 SQL 並將結果匯入指定的下拉選單選項表
    /// </summary>
    /// <param name="sql">要執行的查詢語法（僅限 SELECT）</param>
    /// <param name="dropdownId">目標下拉選單 ID</param>
    /// <returns>SQL 驗證與匯入結果</returns>
    ValidateSqlResultViewModel ImportDropdownOptionsFromSql(string sql, Guid dropdownId);
    Guid SaveFormHeader( FormHeaderViewModel model );

    /// <summary>
    /// 檢查表格名稱與 View 名稱的組合是否已存在於 FORM_FIELD_Master
    /// </summary>
    /// <param name="baseTableId">資料表名稱</param>
    /// <param name="viewTableId">View 表名稱</param>
    /// <param name="excludeId">編輯時排除自身 ID</param>
    /// <returns>若存在相同組合則回傳 true</returns>
    bool CheckFormMasterExists(Guid baseTableId, Guid viewTableId, Guid? excludeId = null);
}