using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services;

/// <summary>
/// 處理主表與明細表 CRUD 的服務。
/// </summary>
public class FormMasterDetailService : IFormMasterDetailService
{
    private readonly SqlConnection _con;
    private readonly IFormService _formService;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly IFormDataService _formDataService;
    private readonly ISchemaService _schemaService;
    private readonly ITransactionService _txService;
    private readonly string _relationColumnSuffix;

    public FormMasterDetailService(
        SqlConnection connection,
        IFormService formService,
        IFormFieldMasterService formFieldMasterService,
        IFormDataService formDataService,
        ISchemaService schemaService,
        ITransactionService txService,
        IConfiguration configuration)
    {
        _con = connection;
        _formService = formService;
        _formFieldMasterService = formFieldMasterService;
        _formDataService = formDataService;
        _schemaService = schemaService;
        _txService = txService;
        _relationColumnSuffix =
            configuration.GetValue<string>("FormSettings:RelationColumnSuffix") ?? "_NO";
    }

    private string GetRelationColumn(string masterTable, string detailTable, SqlTransaction? tx = null)
    {
        var masterCols = _schemaService.GetFormFieldMaster(masterTable, tx);
        var detailCols = _schemaService.GetFormFieldMaster(detailTable, tx);
        var common = masterCols.Intersect(detailCols, StringComparer.OrdinalIgnoreCase);
        var col = common.FirstOrDefault(c =>
            c.EndsWith(_relationColumnSuffix, StringComparison.OrdinalIgnoreCase));
        return col ?? throw new InvalidOperationException(
            $"No relation column ending with '{_relationColumnSuffix}' found between {masterTable} and {detailTable}.");
    }

    /// <inheritdoc />
    public List<FormListDataViewModel> GetFormList(FormFunctionType funcType, FormSearchRequest? request = null)
    {
        return _formService.GetFormList(funcType, request);
    }

    /// <inheritdoc />
    public FormMasterDetailSubmissionViewModel GetFormSubmission(Guid formMasterDetailId, string? pk = null)
    {
        // 取得主明細表頭設定，包含主表與明細表的 FormId
        var header = _formFieldMasterService.GetFormFieldMasterFromId(formMasterDetailId);
        var masterVm = _formService.GetFormSubmission(header.BASE_TABLE_ID, pk);

        var result = new FormMasterDetailSubmissionViewModel
        {
            Master = masterVm,
            Details = new List<FormSubmissionViewModel>()
        };

        var detailTemplate = _formService.GetFormSubmission(header.DETAIL_TABLE_ID);

        // 若為新增，僅回傳空白的明細欄位模板
        if (string.IsNullOrEmpty(pk))
        {
            result.Details.Add(detailTemplate);
            return result;
        }

        var relationColumn = GetRelationColumn(header.BASE_TABLE_NAME, header.DETAIL_TABLE_NAME);
        var detailPk = _schemaService.GetPrimaryKeyColumn(header.DETAIL_TABLE_NAME)
            ?? throw new InvalidOperationException("Detail table has no primary key.");

        var (pkName, pkType, pkVal) = _schemaService.ResolvePk(header.BASE_TABLE_NAME, pk);
        var relationObj = _con.ExecuteScalar<object?>(
            $"SELECT [{relationColumn}] FROM [{header.BASE_TABLE_NAME}] WHERE [{pkName}] = @id",
            new { id = pkVal });
        var relationValue = relationObj?.ToString();
        if (relationValue == null)
            return result;

        var detailColumnTypes = _formDataService.LoadColumnTypes(header.DETAIL_TABLE_NAME);
        var rows = _formDataService.GetRows(
            header.DETAIL_TABLE_NAME,
            new List<FormQueryConditionViewModel>
            {
                new FormQueryConditionViewModel
                {
                    Column = relationColumn,
                    ConditionType = ConditionType.Equal,
                    Value = relationValue,
                    DataType = detailColumnTypes.GetValueOrDefault(relationColumn, "string")
                }
            });

        var detailPkValues = rows
            .Select(row => row.TryGetValue(detailPk, out var pkValue) ? pkValue?.ToString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dropdownAnswersByRow = LoadDetailDropdownAnswers(detailPkValues);

        foreach (var row in rows)
        {
            row.TryGetValue(detailPk, out var detailPkObj);
            var detailPkValue = detailPkObj?.ToString();
            var detailVm = BuildDetailSubmission(
                detailTemplate,
                row,
                detailPkValue,
                detailPkValue != null &&
                dropdownAnswersByRow.TryGetValue(detailPkValue, out var dropdowns)
                    ? dropdowns
                    : null);
            result.Details.Add(detailVm);
        }

        return result;
    }
    
    /// <summary>
    /// 新增/更新 主+明細 的 Orchestrator（原子性）
    /// 支援：
    /// 1) MasterPk 有值 → 只處理 Master 更新（若有欄位） + 新增/更新 Detail
    /// 2) MasterPk 空 → 先新增 Master 取 relationValue → 灌入所有 Detail 後再寫 Detail
    /// </summary>
    public void SubmitForm(FormMasterDetailSubmissionInputModel input)
    {
        _txService.WithTransaction(tx =>
        {
            // 1) 讀設定：找出主檔/明細要用的關聯欄位名稱
            var header = _formFieldMasterService.GetFormFieldMasterFromId(input.MasterId, tx)
                         ?? throw new InvalidOperationException($"Form master not found: {input.MasterId}");

            var relationColumn = GetRelationColumn(header.BASE_TABLE_NAME!, header.DETAIL_TABLE_NAME!, tx);

            // 2) 查出「關聯欄位」在 config 中對應的 ConfigId（Master / Detail 各一）
            var masterCfgId = _con.ExecuteScalar<Guid?>(@"/**/
        SELECT ID FROM FORM_FIELD_CONFIG
        WHERE FORM_FIELD_Master_ID = @Id AND COLUMN_NAME = @Col",
                                  new { Id = header.BASE_TABLE_ID, Col = relationColumn }, transaction: tx)
                              ?? throw new InvalidOperationException("Master relation column not found.");

            var detailCfgId = _con.ExecuteScalar<Guid?>(@"/**/
        SELECT ID FROM FORM_FIELD_CONFIG
        WHERE FORM_FIELD_Master_ID = @Id AND COLUMN_NAME = @Col",
                                  new { Id = header.DETAIL_TABLE_ID, Col = relationColumn }, transaction: tx)
                              ?? throw new InvalidOperationException("Detail relation column not found.");

            // 3) 優先從 MasterFields 嘗試取得 relationValue（若前端已送）
            object? relationValue = input.MasterFields.FirstOrDefault(f => f.FieldConfigId == masterCfgId)?.Value;

            // 4) 分兩種情境
            if (!string.IsNullOrEmpty(input.MasterPk))
            {
                // Case A：Master 已存在（MasterPk 有值）
                // 若 relationValue 還沒有，就用 MasterPk 回查 DB 的 relationColumn
                if (relationValue is null)
                {
                    var (pkName, _, pkVal) = _schemaService.ResolvePk(header.BASE_TABLE_NAME!, input.MasterPk, tx);
                    relationValue = _con.ExecuteScalar<object?>($@"/**/
SELECT [{relationColumn}] FROM [{header.BASE_TABLE_NAME}] WHERE [{pkName}] = @id", new { id = pkVal }, transaction: tx)
                                    ?? throw new InvalidOperationException($"Master not found by PK: {input.MasterPk}");
                }

                // 若有 Master 欄位要改，交給既有單表 Submit
                if (input.MasterFields.Count > 0)
                {
                    var masterUpdate = new FormSubmissionInputModel
                    {
                        BaseId = header.BASE_TABLE_ID,
                        Pk = input.MasterPk,
                        InputFields = input.MasterFields
                    };
                    _formService.SubmitForm(masterUpdate, tx);
                }
            }
            else
            {
                // Case B：Master 要新增（MasterPk 空）
                if (relationValue is null)
                {
                    throw new InvalidOperationException($"關聯鍵的FieldConfigId遺失，無法插入關聯欄位");
                }
                else
                {
                    // 前端已送 relationValue（例如非 PK 的自然鍵）
                    // 直接新增 Master（不需要再取 PK），用既有單表 Submit
                    var masterInsert = new FormSubmissionInputModel
                    {
                        BaseId = header.BASE_TABLE_ID,
                        Pk = null,
                        InputFields = input.MasterFields
                    };
                    _formService.SubmitForm(masterInsert, tx); // 沿用你現有方法
                }
            }

            // 5) 明細：若前端未提供 relationValue，才自動回填 Master 的 relationValue
            foreach (var row in input.DetailRows)
            {
                var relationField = row.Fields.FirstOrDefault(f => f.FieldConfigId == detailCfgId);
                var shouldFallbackToMaster =
                    relationField is null || string.IsNullOrWhiteSpace(relationField.Value);

                if (shouldFallbackToMaster)
                {
                    UpsertField(row.Fields, detailCfgId, relationValue, overwrite: relationField != null);
                }

                var detailInput = new FormSubmissionInputModel
                {
                    BaseId = header.DETAIL_TABLE_ID,
                    Pk = string.IsNullOrWhiteSpace(row.Pk) ? null : row.Pk,
                    InputFields = row.Fields
                };
                _formService.SubmitForm(detailInput, tx);
            }
        });
    }

    private Dictionary<string, Dictionary<Guid, Guid>> LoadDetailDropdownAnswers(IEnumerable<string> rowIds)
    {
        var idList = rowIds?.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        if (idList.Count == 0)
        {
            return new Dictionary<string, Dictionary<Guid, Guid>>(StringComparer.OrdinalIgnoreCase);
        }

        var answers = _con.Query<(string RowId, Guid FieldId, Guid OptionId)>(
            @"/**/SELECT ROW_ID AS RowId,
                    FORM_FIELD_CONFIG_ID AS FieldId,
                    FORM_FIELD_DROPDOWN_OPTIONS_ID AS OptionId
              FROM FORM_FIELD_DROPDOWN_ANSWER
              WHERE ROW_ID IN @RowIds",
            new { RowIds = idList });

        return answers
            .GroupBy(a => a.RowId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.FieldId, x => x.OptionId),
                StringComparer.OrdinalIgnoreCase);
    }

    private static FormSubmissionViewModel BuildDetailSubmission(
        FormSubmissionViewModel template,
        IDictionary<string, object?> row,
        string? pk,
        IReadOnlyDictionary<Guid, Guid>? dropdownAnswers)
    {
        var fieldValues = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);

        var fields = template.Fields
            .Select(field => CloneField(field, fieldValues, dropdownAnswers))
            .ToList();

        return new FormSubmissionViewModel
        {
            FormId = template.FormId,
            FormName = template.FormName,
            TargetTableToUpsert = template.TargetTableToUpsert,
            Pk = pk,
            Fields = fields
        };
    }

    private static FormFieldInputViewModel CloneField(
        FormFieldInputViewModel source,
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyDictionary<Guid, Guid>? dropdownAnswers)
    {
        var cloned = new FormFieldInputViewModel
        {
            FieldConfigId = source.FieldConfigId,
            Column = source.Column,
            DATA_TYPE = source.DATA_TYPE,
            CONTROL_TYPE = source.CONTROL_TYPE,
            DefaultValue = source.DefaultValue,
            IS_REQUIRED = source.IS_REQUIRED,
            IS_EDITABLE = source.IS_EDITABLE,
            ValidationRules = source.ValidationRules != null
                ? new List<FormFieldValidationRuleDto>(source.ValidationRules)
                : null,
            ISUSESQL = source.ISUSESQL,
            DROPDOWNSQL = source.DROPDOWNSQL,
            QUERY_COMPONENT = source.QUERY_COMPONENT,
            QUERY_CONDITION = source.QUERY_CONDITION,
            CAN_QUERY = source.CAN_QUERY,
            OptionList = new List<FormFieldDropdownOptionsDto>(source.OptionList),
            SOURCE_TABLE = source.SOURCE_TABLE
        };

        if (source.CONTROL_TYPE == FormControlType.Dropdown &&
            dropdownAnswers?.TryGetValue(source.FieldConfigId, out var optionId) == true)
        {
            cloned.CurrentValue = optionId;
        }
        else if (values.TryGetValue(source.Column, out var value))
        {
            cloned.CurrentValue = value;
        }
        else
        {
            cloned.CurrentValue = null;
        }

        return cloned;
    }

    /// <summary>
    /// 把 relationValue 塞進某 Fields 清單（若已存在就視需求覆蓋）
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="cfgId"></param>
    /// <param name="value"></param>
    /// <param name="overwrite"></param>
    private static void UpsertField(List<FormInputField> fields, Guid cfgId, object? value, bool overwrite)
    {
        var f = fields.FirstOrDefault(x => x.FieldConfigId == cfgId);
        var stringValue = value?.ToString();

        if (f == null)
        {
            fields.Add(new FormInputField { FieldConfigId = cfgId, Value = stringValue });
        }
        else if (overwrite)
        {
            f.Value = stringValue;
        }
    }

}