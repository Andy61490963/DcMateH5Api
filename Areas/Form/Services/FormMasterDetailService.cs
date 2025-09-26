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
    private const int MaxDetailPageSize = 200;
    private static readonly Regex IdentifierRegex = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
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

        // 若為新增，僅回傳空白的明細欄位模板
        if (string.IsNullOrEmpty(pk))
        {
            var emptyDetail = _formService.GetFormSubmission(header.DETAIL_TABLE_ID);
            result.Details.Add(emptyDetail);
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

        foreach (var row in rows)
        {
            var detailPkValue = row[detailPk]?.ToString();
            var detailVm = _formService.GetFormSubmission(header.DETAIL_TABLE_ID, detailPkValue);
            result.Details.Add(detailVm);
        }

        return result;
    }

    /// <summary>
    /// 依 FormFieldMasterId 分頁取得明細資料列表
    /// 支援：
    /// - 檢查設定是否完整
    /// - 驗證欄位是否合法 SQL 識別字
    /// - 讀取 FORM_FIELD_CONFIG 做欄位對應
    /// - 分頁查詢 Detail Table
    /// - 投影成 ViewModel，附帶欄位值與原始資料
    /// </summary>
    public FormDetailRowPageViewModel GetDetailRows(Guid formMasterDetailId, int page, int pageSize)
    {
        // 1) 取得主檔/明細表設定（包含 BASE_TABLE_NAME, DETAIL_TABLE_NAME 等）   
        var header = _formFieldMasterService.GetFormFieldMasterFromId(formMasterDetailId)
                     ?? throw new InvalidOperationException($"Form master not found: {formMasterDetailId}");

        if (string.IsNullOrWhiteSpace(header.BASE_TABLE_NAME))
        {
            throw new InvalidOperationException("Master table name is not configured.");
        }

        if (string.IsNullOrWhiteSpace(header.DETAIL_TABLE_NAME) || header.DETAIL_TABLE_ID is null)
        {
            throw new InvalidOperationException("Detail table setting is incomplete.");
        }

        // 2) 頁碼與 pageSize 合法化，避免被傳入 0、負數或過大值
        var safePage = page < 1 ? 1 : page;
        var trimmedPageSize = pageSize < 1 ? 1 : pageSize;
        if (trimmedPageSize > MaxDetailPageSize)
        {
            trimmedPageSize = MaxDetailPageSize;
        }

        // 3) 驗證 SQL 識別字（避免注入）
        ValidateSqlIdentifier(header.DETAIL_TABLE_NAME!, nameof(header.DETAIL_TABLE_NAME));

        // 4) 找到主檔與明細共用的關聯欄位（例如 *_NO）
        var relationColumn = GetRelationColumn(header.BASE_TABLE_NAME!, header.DETAIL_TABLE_NAME!);
        ValidateSqlIdentifier(relationColumn, nameof(relationColumn));

        // 5) 取得明細表的主鍵欄位
        var detailPk = _schemaService.GetPrimaryKeyColumn(header.DETAIL_TABLE_NAME!)
                       ?? throw new InvalidOperationException("Detail table has no primary key.");
        ValidateSqlIdentifier(detailPk, nameof(detailPk));

        // 6) 從 FORM_FIELD_CONFIG 讀取明細欄位的設定（欄位名稱 / 型別 / 顯示順序）
        var configs = _con.Query<(Guid Id, string Column, string DataType, int Order)>(@"/**/
SELECT ID, COLUMN_NAME, DATA_TYPE, FIELD_ORDER AS [Order]
FROM FORM_FIELD_CONFIG
WHERE FORM_FIELD_Master_ID = @Id
ORDER BY FIELD_ORDER", new { Id = header.DETAIL_TABLE_ID })
            .ToList();

        if (configs.Count == 0)
        {
            // 若沒設定欄位 → 回傳空結果
            return new FormDetailRowPageViewModel
            {
                Page = safePage,
                PageSize = trimmedPageSize,
                TotalCount = 0,
                RelationColumn = relationColumn,
                Rows = new List<FormDetailRowViewModel>()
            };
        }

        // 7) 檢查明細的 config 內是否有 relationColumn
        if (!configs.Any(c => c.Column.Equals(relationColumn, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Detail relation column '{relationColumn}' has no matching FORM_FIELD_CONFIG entry.");
        }

        // 8) 計算明細總數（for 分頁 UI）
        var totalCount = _con.ExecuteScalar<int>($"/**/SELECT COUNT(1) FROM [{header.DETAIL_TABLE_NAME}]");
        if (totalCount == 0)
        {
            return new FormDetailRowPageViewModel
            {
                Page = safePage,
                PageSize = trimmedPageSize,
                TotalCount = 0,
                RelationColumn = relationColumn,
                Rows = new List<FormDetailRowViewModel>()
            };
        }

        // 9) 載入明細表每個欄位的 SQL 資料型別
        var columnTypes = _formDataService.LoadColumnTypes(header.DETAIL_TABLE_NAME!);

        // 10) 計算分頁 offset（OFFSET/FETCH NEXT 語法用）
        var offset = (long)(safePage - 1) * trimmedPageSize;
        if (offset < 0)
        {
            offset = 0;
        }

        // 11) 撈取分頁的明細資料
        var rows = _con.Query(
                $"/**/SELECT * FROM [{header.DETAIL_TABLE_NAME}] ORDER BY [{detailPk}] OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, PageSize = trimmedPageSize })
            .Cast<IDictionary<string, object?>>()
            .ToList();

        // 12) 將資料投影成 ViewModel（包含 config → FieldConfigId 映射）
        var projectedRows = ProjectDetailRows(rows, detailPk, configs, columnTypes);

        // 13) 回傳分頁結果
        return new FormDetailRowPageViewModel
        {
            Page = safePage,
            PageSize = trimmedPageSize,
            TotalCount = totalCount,
            RelationColumn = relationColumn,
            Rows = projectedRows
        };
    }

    /// <summary>
    /// 將 DB 撈到的明細資料 rows 映射為 ViewModel 列表。
    /// 規則：
    /// 1) RawData：保留每欄原始值（DBNull→null）供前端/除錯用
    /// 2) Fields：依 FORM_FIELD_CONFIG 的順序與型別輸出 (FieldConfigId, ColumnName, Value)
    /// 3) Pk：以 detailPk 取值（若缺失則為 null）
    /// </summary>
    private List<FormDetailRowViewModel> ProjectDetailRows(
        IEnumerable<IDictionary<string, object?>> rows,
        string detailPk,
        IReadOnlyList<(Guid Id, string Column, string DataType, int Order)> configs,
        IReadOnlyDictionary<string, string> columnTypes)
    {
        // 1) 建立大小寫不敏感的 config map，加速查找型別與輸出欄位順序
        //    （同時把最終要輸出的欄位清單先組好，避免每筆 row 重複 work）
        var columnsInOrder = new List<(Guid Id, string Column, string SqlType)>(configs.Count);
        var typeByColumnCI = new Dictionary<string, string>(columnTypes, StringComparer.OrdinalIgnoreCase);

        foreach (var (id, column, configType, _) in configs)
        {
            // 若 columnTypes 有覆寫就用覆寫，否則用 config 的型別
            var sqlType = typeByColumnCI.TryGetValue(column, out var mapped)
                ? mapped
                : configType;

            columnsInOrder.Add((id, column, sqlType));
        }

        var result = new List<FormDetailRowViewModel>();

        // 2) 逐列處理
        foreach (var row in rows)
        {
            // 2-1) 規一化為大小寫不敏感的字典，便於後續查值
            var normalized = CreateCaseInsensitiveRow(row);

            // 2-2) 取 PK（可能不存在）
            normalized.TryGetValue(detailPk, out var pkRaw);

            // 2-3) 先建立 RawData（DBNull→null）
            var rawData = new Dictionary<string, object?>(normalized.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (col, val) in normalized)
            {
                rawData[col] = NormalizeRawValue(val);
            }

            // 2-4) 依 config 順序輸出 Fields
            var fields = new List<FormInputField>(columnsInOrder.Count);
            foreach (var (id, column, sqlType) in columnsInOrder)
            {
                normalized.TryGetValue(column, out var rawVal);
                fields.Add(new FormInputField
                {
                    FieldConfigId = id,
                    ColumnName    = column,
                    Value         = SerializeValue(rawVal, sqlType)
                });
            }

            // 2-5) 組合 Row VM
            result.Add(new FormDetailRowViewModel
            {
                Pk     = pkRaw?.ToString(),
                Fields = fields,
                RawData = rawData
            });
        }

        return result;
    }

    private static Dictionary<string, object?> CreateCaseInsensitiveRow(IDictionary<string, object?> source)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            normalized[key] = value;
        }

        return normalized;
    }

    private static void ValidateSqlIdentifier(string identifier, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !IdentifierRegex.IsMatch(identifier))
        {
            throw new InvalidOperationException($"{parameterName} contains invalid characters.");
        }
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

    private static object? NormalizeRawValue(object? value)
    {
        return value is DBNull ? null : value;
    }

    private static string? SerializeValue(object? value, string? sqlType)
    {
        if (value is DBNull)
        {
            return null;
        }

        if (value is null)
        {
            return null;
        }

        var normalized = sqlType?.ToLowerInvariant();

        return normalized switch
        {
            "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real" =>
                (value as IFormattable)?.ToString(null, CultureInfo.InvariantCulture)
                ?? Convert.ToString(value, CultureInfo.InvariantCulture),
            "int" or "bigint" or "smallint" or "tinyint" =>
                (value as IFormattable)?.ToString(null, CultureInfo.InvariantCulture)
                ?? Convert.ToString(value, CultureInfo.InvariantCulture),
            "bit" => value switch
            {
                bool b => b ? "1" : "0",
                byte bt => bt != 0 ? "1" : "0",
                short s => s != 0 ? "1" : "0",
                int i => i != 0 ? "1" : "0",
                long l => l != 0 ? "1" : "0",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            },
            "datetime" or "smalldatetime" or "datetime2" or "date" =>
                value is DateTime dt
                    ? dt.ToString("o", CultureInfo.InvariantCulture)
                    : Convert.ToString(value, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

}
