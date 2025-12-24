using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormDesignerService
{
    Task<List<FormFieldMasterDto>> GetFormMasters( FormFunctionType functionType, string? q, CancellationToken ct );
    
    Task UpdateFormName( UpdateFormNameViewModel model, CancellationToken ct );

    Task DeleteFormMaster(Guid id, CancellationToken ct = default);
    
    Task<FormDesignerIndexViewModel> GetFormDesignerIndexViewModel( FormFunctionType functionType, Guid? id, CancellationToken ct );
    
    List<string> SearchTables( string? tableName, TableSchemaQueryType schemaType );
    
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

    Task<DropDownViewModel> GetDropdownSetting( Guid fieldId, CancellationToken ct = default );

    Task<List<FormFieldDropdownOptionsDto>> GetDropdownOptions( Guid dropDownId, CancellationToken ct = default );

    Task SaveDropdownSql( Guid dropdownId, string sql, CancellationToken ct );
    Guid SaveDropdownOption(Guid? id, Guid dropdownId, string optionText, string optionValue, string? optionTable = null);

    Task<bool> DeleteDropdownOption(Guid optionId, CancellationToken ct = default);

    Task SetDropdownMode( Guid dropdownId, bool isUseSql, CancellationToken ct );

    ValidateSqlResultViewModel ValidateDropdownSql( string sql );
    
    ValidateSqlResultViewModel ImportDropdownOptionsFromSql( string sql, Guid dropdownId );
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
}
