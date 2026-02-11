using Dapper;
using DbExtensions.DbExecutor.Interface;
using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.Options;
using DcMateH5.Abstractions.Form.Transaction;
using DcMateH5.Abstractions.Form.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DcMateH5.Infrastructure.Form.Form;

/// <summary>
/// 處理主表與明細表 CRUD 的服務。
/// </summary>
public class FormMasterDetailService : IFormMasterDetailService
{
    private readonly IDbExecutor _dbExecutor;
    private readonly IFormService _formService;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly IFormDataService _formDataService;
    private readonly ISchemaService _schemaService;
    private readonly ITransactionService _txService;
    private readonly IReadOnlyList<string> _relationColumnSuffixes;

    public FormMasterDetailService(
        IDbExecutor dbExecutor,
        IFormService formService,
        IFormFieldMasterService formFieldMasterService,
        IFormDataService formDataService,
        ISchemaService schemaService,
        ITransactionService txService,
        IOptions<FormSettings> formSettings)
    {
        _dbExecutor = dbExecutor;
        _formService = formService;
        _formFieldMasterService = formFieldMasterService;
        _formDataService = formDataService;
        _schemaService = schemaService;
        _txService = txService;
        var resolvedSettings = formSettings?.Value ?? new FormSettings();
        _relationColumnSuffixes = resolvedSettings.GetRelationColumnSuffixesOrDefault();
    }

    private string GetRelationColumn(string masterTable, string detailTable, SqlTransaction? tx = null)
    {
        var masterCols = _schemaService.GetFormFieldMaster(masterTable, tx);
        var detailCols = _schemaService.GetFormFieldMaster(detailTable, tx);
        var common = masterCols.Intersect(detailCols, StringComparer.OrdinalIgnoreCase);
        var relationColumn = common.FirstOrDefault(column =>
            _relationColumnSuffixes.MatchesRelationSuffix(column));

        if (relationColumn is null)
        {
            var suffixDisplay = string.Join("', '", _relationColumnSuffixes);
            throw new InvalidOperationException(
                $"No relation column ending with any of '{suffixDisplay}' found between {masterTable} and {detailTable}.");
        }

        return relationColumn;
    }

    /// <inheritdoc />
    public Task<List<FormListResponseViewModel>> GetFormListAsync(FormFunctionType funcType, FormSearchRequest? request = null, CancellationToken ct = default)
    {
        return _formService.GetFormListAsync(funcType, request, ct: ct);
    }

    /// <inheritdoc />
    public async Task<FormMasterDetailSubmissionViewModel> GetFormSubmissionAsync(Guid formMasterDetailId, string? pk = null, CancellationToken ct = default)
    {
        // 取得主明細表頭設定，包含主表與明細表的 FormId
        var header = _formFieldMasterService.GetFormFieldMasterFromId(formMasterDetailId);
        
        var masterTable = header.BASE_TABLE_NAME
                          ?? throw new InvalidOperationException("Master table name is missing in header configuration.");
        var detailTable = header.DETAIL_TABLE_NAME
                          ?? throw new InvalidOperationException("Detail table name is missing in header configuration.");
        
        var relationColumn = GetRelationColumn(masterTable, detailTable);

        var masterVm = await _formService.GetFormSubmissionAsync(header.BASE_TABLE_ID, pk);
        MarkRelationField(masterVm.Fields, relationColumn);

        var result = new FormMasterDetailSubmissionViewModel
        {
            Master = masterVm,
            Details = new List<FormSubmissionViewModel>()
        };

        // 若為新增，僅回傳空白的明細欄位模板
        if (string.IsNullOrEmpty(pk))
        {
            var emptyDetail = await _formService.GetFormSubmissionAsync(header.DETAIL_TABLE_ID);
            MarkRelationField(emptyDetail.Fields, relationColumn);
            result.Details.Add(emptyDetail);
            return result;
        }
        var detailPk = await _schemaService.GetPrimaryKeyColumnAsync(header.DETAIL_TABLE_NAME, ct)
            ?? throw new InvalidOperationException("Detail table has no primary key.");

        var (pkName, pkType, pkVal) = await _schemaService.ResolvePkAsync(header.BASE_TABLE_NAME, pk, ct: ct);
        var relationObj = _dbExecutor.Connection.ExecuteScalar<object?>(
            $"SELECT [{relationColumn}] FROM [{header.BASE_TABLE_NAME}] WHERE [{pkName}] = @id",
            new { id = pkVal });
        var relationValue = relationObj?.ToString();
        if (relationValue == null)
            return result;

        var detailColumnTypes = await _formDataService.LoadColumnTypesAsync(header.DETAIL_TABLE_NAME, ct);
        var rows = await _formDataService.GetRowsAsync(
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

        foreach (var row in rows)
        {
            var detailPkValue = row[detailPk]?.ToString();
            var detailVm = await _formService.GetFormSubmissionAsync(header.DETAIL_TABLE_ID, detailPkValue);
            MarkRelationField(detailVm.Fields, relationColumn);
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
    public async Task SubmitFormAsync(FormMasterDetailSubmissionInputModel input, CancellationToken ct = default)
    {
        await _txService.WithTransactionAsync(async (tx, innerCt) =>
        {
            // 1) 讀設定：找出主檔/明細要用的關聯欄位名稱
            var header = _formFieldMasterService.GetFormFieldMasterFromId(input.MasterId, tx)
                         ?? throw new InvalidOperationException($"Form master not found: {input.MasterId}");

            var relationColumn = GetRelationColumn(header.BASE_TABLE_NAME!, header.DETAIL_TABLE_NAME!, tx);

            // 2) 查出「關聯欄位」在 config 中對應的 ConfigId（Master / Detail 各一）
            var masterCfgId = _dbExecutor.ExecuteScalarInTx<Guid?>(tx.Connection!, tx, @"/**/
        SELECT ID FROM FORM_FIELD_CONFIG
        WHERE FORM_FIELD_MASTER_ID = @Id AND COLUMN_NAME = @Col",
                                  new { Id = header.BASE_TABLE_ID, Col = relationColumn })
                              ?? throw new InvalidOperationException("Master relation column not found.");

            var detailCfgId = _dbExecutor.ExecuteScalarInTx<Guid?>(tx.Connection!, tx, @"/**/
        SELECT ID FROM FORM_FIELD_CONFIG
        WHERE FORM_FIELD_MASTER_ID = @Id AND COLUMN_NAME = @Col",
                                  new { Id = header.DETAIL_TABLE_ID, Col = relationColumn })
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
                    var (pkName, _, pkVal) = await _schemaService.ResolvePkAsync(header.BASE_TABLE_NAME!, input.MasterPk, tx, innerCt);
                    relationValue = _dbExecutor.ExecuteScalarInTx<object?>(tx.Connection!, tx, $@"/**/
SELECT [{relationColumn}] FROM [{header.BASE_TABLE_NAME}] WHERE [{pkName}] = @id", new { id = pkVal })
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
                    await _formService.SubmitFormAsync(masterUpdate, tx, innerCt);
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
                    await _formService.SubmitFormAsync(masterInsert, tx, innerCt); // 沿用你現有方法
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
                await _formService.SubmitFormAsync(detailInput, tx, innerCt);
            }
        }, ct);
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

    private static void MarkRelationField(IEnumerable<FormFieldInputViewModel> fields, string relationColumn)
    {
        foreach (var field in fields)
        {
            field.IS_RELATION = string.Equals(field.Column, relationColumn, StringComparison.OrdinalIgnoreCase);
        }
    }

}
