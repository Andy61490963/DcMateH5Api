using DbExtensions.DbExecutor.Interface;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.Models;

namespace DcMateH5.Infrastructure.Form.FormLogic;

public class FormFieldConfigService : IFormFieldConfigService
{
    private readonly IDbExecutor _dbExecutor;

    public FormFieldConfigService(IDbExecutor dbExecutor)
    {
        _dbExecutor = dbExecutor;
    }

    /// <summary>
    /// 依表單主檔 ID 取得欄位設定清單。
    /// </summary>
    /// <param name="id">表單主檔識別碼。</param>
    /// <returns>欄位設定列表。</returns>
    public Task<List<FormFieldConfigDto>> GetFormFieldConfigAsync(Guid? id, CancellationToken ct = default)
    {
        return _dbExecutor.QueryAsync<FormFieldConfigDto>(
            "/**/SELECT * FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id",
            new { id },
            ct: ct);
    }

    /// <summary>
    /// 一次載入欄位設定、驗證規則、下拉設定與選項資料。
    /// </summary>
    /// <param name="masterId">表單主檔識別碼。</param>
    /// <returns>組合後的欄位設定資料。</returns>
    /// <remarks>
    /// 業務邏輯：維持原本資料集合內容與排序邏輯，僅調整為經由 DbExecutor 進行資料存取。
    /// </remarks>
    public async Task<FieldConfigData> LoadFieldConfigDataAsync(Guid? masterId, CancellationToken ct = default)
    {
        var configs = await _dbExecutor.QueryAsync<FormFieldConfigDto>(@"SELECT FFC.*, FFM.FORM_NAME
                    FROM FORM_FIELD_CONFIG FFC
                    JOIN FORM_FIELD_MASTER FFM ON FFM.ID = FFC.FORM_FIELD_MASTER_ID
                    WHERE FFM.ID = @ID
                    ORDER BY FIELD_ORDER;", new { ID = masterId }, ct: ct);

        var rules = await _dbExecutor.QueryAsync<FormFieldValidationRuleDto>(@"SELECT R.*
                    FROM FORM_FIELD_VALIDATION_RULE R
                    JOIN FORM_FIELD_CONFIG C ON R.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_MASTER_ID = @ID;", new { ID = masterId }, ct: ct);

        var dropdowns = await _dbExecutor.QueryAsync<FormFieldDropDownDto>(@"SELECT D.*
                    FROM FORM_FIELD_DROPDOWN D
                    JOIN FORM_FIELD_CONFIG C ON D.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_MASTER_ID = @ID;", new { ID = masterId }, ct: ct);

        var options = await _dbExecutor.QueryAsync<FormFieldDropdownOptionsDto>(@"SELECT O.*
                    FROM FORM_FIELD_DROPDOWN_OPTIONS O
                    JOIN FORM_FIELD_DROPDOWN D ON O.FORM_FIELD_DROPDOWN_ID = D.ID
                    JOIN FORM_FIELD_CONFIG C ON D.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_MASTER_ID = @ID;", new { ID = masterId }, ct: ct);

        return new FieldConfigData(configs, rules, dropdowns, options);
    }

    /// <summary>
    /// 同步版本（相容舊呼叫端）。
    /// </summary>
    public List<FormFieldConfigDto> GetFormFieldConfig(Guid? id)
    {
        return _dbExecutor.Query<FormFieldConfigDto>(
            "/**/SELECT * FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_MASTER_ID = @id",
            new { id });
    }

    /// <summary>
    /// 同步版本（相容舊呼叫端）。
    /// </summary>
    public FieldConfigData LoadFieldConfigData(Guid? masterId)
    {
        var configs = _dbExecutor.Query<FormFieldConfigDto>(@"SELECT FFC.*, FFM.FORM_NAME
                    FROM FORM_FIELD_CONFIG FFC
                    JOIN FORM_FIELD_MASTER FFM ON FFM.ID = FFC.FORM_FIELD_MASTER_ID
                    WHERE FFM.ID = @ID
                    ORDER BY FIELD_ORDER;", new { ID = masterId });

        var rules = _dbExecutor.Query<FormFieldValidationRuleDto>(@"SELECT R.*
                    FROM FORM_FIELD_VALIDATION_RULE R
                    JOIN FORM_FIELD_CONFIG C ON R.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_MASTER_ID = @ID;", new { ID = masterId });

        var dropdowns = _dbExecutor.Query<FormFieldDropDownDto>(@"SELECT D.*
                    FROM FORM_FIELD_DROPDOWN D
                    JOIN FORM_FIELD_CONFIG C ON D.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_MASTER_ID = @ID;", new { ID = masterId });

        var options = _dbExecutor.Query<FormFieldDropdownOptionsDto>(@"SELECT O.*
                    FROM FORM_FIELD_DROPDOWN_OPTIONS O
                    JOIN FORM_FIELD_DROPDOWN D ON O.FORM_FIELD_DROPDOWN_ID = D.ID
                    JOIN FORM_FIELD_CONFIG C ON D.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_MASTER_ID = @ID;", new { ID = masterId });

        return new FieldConfigData(configs, rules, dropdowns, options);
    }

}