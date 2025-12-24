using System;
using System.Text.RegularExpressions;
using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
using DcMateH5Api.SqlHelper;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services;

/// <summary>
/// 多對多維護服務，負責提供設定檔、左右清單與批次關聯的核心邏輯。
/// </summary>
public class FormMultipleMappingService : IFormMultipleMappingService
{
    private readonly SqlConnection _con;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly ISchemaService _schemaService;
    private readonly ITransactionService _txService;
    private readonly IFormService _formService;
    private readonly SQLGenerateHelper _sqlHelper;
    
    public FormMultipleMappingService(
        SqlConnection connection,
        IFormFieldMasterService formFieldMasterService,
        ISchemaService schemaService,
        ITransactionService txService,
        IFormService formService,
        SQLGenerateHelper sqlHelper)
    {
        _con = connection;
        _formFieldMasterService = formFieldMasterService;
        _schemaService = schemaService;
        _txService = txService;
        _formService = formService;
        _sqlHelper = sqlHelper;
    }

    /// <inheritdoc />
    public IEnumerable<MultipleMappingConfigViewModel> GetFormMasters(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        const string sql = @"/**/
SELECT ID AS Id,
       FORM_NAME AS FormName,
       BASE_TABLE_NAME AS BaseTableName,
       DETAIL_TABLE_NAME AS DetailTableName,
       MAPPING_TABLE_NAME AS MappingTableName,
       MAPPING_BASE_FK_COLUMN AS MappingBaseFkColumn,
       MAPPING_DETAIL_FK_COLUMN AS MappingDetailFkColumn
  FROM FORM_FIELD_MASTER
 WHERE FUNCTION_TYPE = @funcType
   AND IS_DELETE = 0";

        return _con.Query<MultipleMappingConfigViewModel>(sql,
            new { funcType = FormFunctionType.MultipleMappingMaintenance.ToInt() });
    }

    /// <inheritdoc />
    public List<FormListDataViewModel> GetForms(FormSearchRequest? request = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _formService.GetFormList(FormFunctionType.MultipleMappingMaintenance, request);
    }

    /// <inheritdoc />
    public MultipleMappingListViewModel GetMappingList(Guid formMasterId, string baseId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var header = GetMappingHeader(formMasterId);

        var (basePkName, _, basePkValue) = _schemaService.ResolvePk(header.BASE_TABLE_NAME!, baseId);
        var (detailPkName, detailPkType, _) = _schemaService.ResolvePk(header.DETAIL_TABLE_NAME!, null);

        EnsureRowExists(header.BASE_TABLE_NAME!, basePkName, basePkValue!);

        // 這裡改成 scalar：先抓字串，再用既有 helper 轉成正確 PK 型別
        var linkedDetailIds = _con.Query<string>($@"/**/
SELECT CAST([{header.MAPPING_DETAIL_FK_COLUMN}] AS NVARCHAR(4000)) AS Id
  FROM [{header.MAPPING_TABLE_NAME}]
 WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId",
                new { BaseId = basePkValue })
            .Select(x => ConvertToColumnTypeHelper.ConvertPkType(x, detailPkType))
            .ToList();

        var linkedItems = LoadDetailRows(header.DETAIL_TABLE_NAME!, detailPkName, linkedDetailIds);
        var unlinkedItems = LoadUnlinkedRows(header, detailPkName, basePkValue);

        return new MultipleMappingListViewModel
        {
            FormMasterId = formMasterId,
            BasePkColumn = basePkName,
            BasePk = basePkValue?.ToString() ?? string.Empty,
            DetailPkColumn = detailPkName,
            MappingBaseFkColumn = header.MAPPING_BASE_FK_COLUMN!,
            MappingDetailFkColumn = header.MAPPING_DETAIL_FK_COLUMN!,
            Linked = linkedItems,
            Unlinked = unlinkedItems
        };
    }

    /// <inheritdoc />
    public void AddMappings(Guid formMasterId, MultipleMappingUpsertViewModel request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateUpsertRequest(request);

        _txService.WithTransaction(tx =>
        {
            var header = GetMappingHeader(formMasterId, tx);

            var (basePkName, _, basePkValue) = _schemaService.ResolvePk(header.BASE_TABLE_NAME!, request.BaseId, tx);
            var (detailPkName, detailPkType, _) = _schemaService.ResolvePk(header.DETAIL_TABLE_NAME!, null, tx);

            EnsureRowExists(header.BASE_TABLE_NAME!, basePkName, basePkValue!, tx);
            var detailIds = ConvertDetailIds(request.DetailIds, detailPkType);

            foreach (var detailId in detailIds)
            {
                EnsureRowExists(header.DETAIL_TABLE_NAME!, detailPkName, detailId!, tx);

                _con.Execute($@"/**/
IF NOT EXISTS (
    SELECT 1 FROM [{header.MAPPING_TABLE_NAME}]
    WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
      AND [{header.MAPPING_DETAIL_FK_COLUMN}] = @DetailId)
BEGIN
    INSERT INTO [{header.MAPPING_TABLE_NAME}]
        (SID, [{header.MAPPING_BASE_FK_COLUMN}], [{header.MAPPING_DETAIL_FK_COLUMN}], CREATE_TIME, EDIT_TIME, IS_DELETE)
    VALUES (@SID, @BaseId, @DetailId, @CreateTime, @EditTime, @IsDelete);
END",
                    new { SID = RandomHelper.GenerateRandomDecimal(), BaseId = basePkValue, DetailId = detailId, CreateTime = DateTime.Now, EditTime = DateTime.Now, IsDelete = 0  }, transaction: tx);
            }
        });
    }

    /// <inheritdoc />
    public void RemoveMappings(Guid formMasterId, MultipleMappingUpsertViewModel request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateUpsertRequest(request);

        _txService.WithTransaction(tx =>
        {
            var header = GetMappingHeader(formMasterId, tx);
            var (basePkName, _, basePkValue) = _schemaService.ResolvePk(header.BASE_TABLE_NAME!, request.BaseId, tx);
            var (detailPkName, detailPkType, _) = _schemaService.ResolvePk(header.DETAIL_TABLE_NAME!, null, tx);

            EnsureRowExists(header.BASE_TABLE_NAME!, basePkName, basePkValue!, tx);
            var detailIds = ConvertDetailIds(request.DetailIds, detailPkType);

            foreach (var detailId in detailIds)
            {
                EnsureRowExists(header.DETAIL_TABLE_NAME!, detailPkName, detailId!, tx);
                _con.Execute($@"/**/
DELETE FROM [{header.MAPPING_TABLE_NAME}]
WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
  AND [{header.MAPPING_DETAIL_FK_COLUMN}] = @DetailId",
                    new { BaseId = basePkValue, DetailId = detailId }, transaction: tx);
            }
        });
    }

    /// <summary>
    /// 依指定的 SID 順序重新整理 Mapping 表的 SEQ 欄位，僅針對同一 Base 主鍵的資料列。
    /// </summary>
    /// <param name="request">包含設定檔、排序後 SID 清單與 Base 主鍵值的請求模型。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>實際更新的筆數。</returns>
    public int ReorderMappingSequence(MappingSequenceReorderRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateReorderRequest(request);

        return _txService.WithTransaction(tx =>
        {
            var header = GetMappingHeader(request.FormMasterId, tx);
            EnsureReorderColumns(header, tx);

            var (basePkName, _, basePkValue) = _schemaService.ResolvePk(header.BASE_TABLE_NAME!, request.Scope.BasePkValue, tx);
            EnsureRowExists(header.BASE_TABLE_NAME!, basePkName, basePkValue!, tx);

            EnsureSidsBelongToBase(header, basePkValue!, request.OrderedSids, tx);

            var parameters = new DynamicParameters();
            parameters.Add("BaseId", basePkValue);

            var valueFragments = new List<string>();
            for (var i = 0; i < request.OrderedSids.Count; i++)
            {
                var sidParam = $"sid{i}";
                var seqParam = $"seq{i}";
                parameters.Add(sidParam, request.OrderedSids[i]);
                parameters.Add(seqParam, i + 1);
                valueFragments.Add($"(@{sidParam}, @{seqParam})");
            }

            var valuesSql = string.Join(", ", valueFragments);

            var sql = $@"/**/
;WITH OrderedSids AS (
    SELECT v.SID, v.Seq
    FROM (VALUES {valuesSql}) AS v (SID, Seq)
)
UPDATE m
   SET m.[SEQ] = o.Seq
  FROM [{header.MAPPING_TABLE_NAME}] AS m
  JOIN OrderedSids AS o ON m.SID = o.SID
 WHERE m.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId;";

            return _con.Execute(sql, parameters, transaction: tx);
        });
    }

    /// <summary>
    /// 依據 MAPPING_TABLE_ID 查詢關聯表所有資料列，並將欄位名稱與值以模型封裝返回。
    /// </summary>
    /// <param name="formMasterId">FORM_FIELD_MASTER 的 MAPPING_TABLE_ID。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>包含關聯表名稱與完整資料列集合的模型。</returns>
    public async Task<MappingTableDataViewModel> GetMappingTableData(Guid formMasterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (formMasterId == Guid.Empty)
        {
            throw new InvalidOperationException("FormMasterId 不可為空。");
        }
        
        var mappingTableName = await GetMappingTableNameAsync(formMasterId, ct);

        var columns = _schemaService.GetFormFieldMaster(mappingTableName!);
        if (columns.Count == 0)
        {
            throw new InvalidOperationException($"無法取得關聯表欄位資訊：{mappingTableName}");
        }

        var key = _schemaService.GetPrimaryKeyColumn(mappingTableName!);
        
        var rows = _con.Query($"SELECT * FROM [{mappingTableName}]")
            .Cast<IDictionary<string, object?>>()
            .Select(row => new MappingTableRowViewModel
            {
                Columns = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)
            })
            .ToList();

        return new MappingTableDataViewModel
        {
            FormMasterId = formMasterId,
            MappingTableName = mappingTableName!,
            MappingTableKey = key!,
            Rows = rows
        };
    }

    /// <summary>
    /// 依 FormMasterId + MappingRowId 更新關聯表指定欄位資料。
    /// </summary>
    /// <remarks>
    /// 說明：
    /// 1) 用 MappingRowId 精準定位要更新的那一筆
    /// 2) 用 Fields(key:value)  
    /// </remarks>
    public async Task<int> UpdateMappingTableData(Guid formMasterId, MappingTableUpdateRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateUpdateMappingRequestV2(formMasterId, request);

        if (request.Fields.Count == 0)
        {
            return 0;
        }

        var mappingTableName = await GetMappingTableNameAsync(formMasterId, ct);

        // 1) 取得欄位白名單（無 tx）
        var tableColumns = _schemaService.GetFormFieldMaster(mappingTableName);
        if (tableColumns.Count == 0)
        {
            throw new InvalidOperationException($"無法取得關聯表欄位資訊：{mappingTableName}");
        }

        var columnSet = tableColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2) PK 欄位
        var pkName = _schemaService.GetPrimaryKeyColumn(mappingTableName);
        if (string.IsNullOrWhiteSpace(pkName))
        {
            throw new InvalidOperationException($"關聯表缺少主鍵欄位：{mappingTableName}");
        }

        // 3) 取 PK 型別（只取 meta）
        var (_, pkType, _) = _schemaService.ResolvePk(mappingTableName, null);

        // 4) 轉 PK 值（支援 int/decimal/guid）
        var pkValue = ConvertToColumnTypeHelper.ConvertPkType(request.MappingRowId, pkType);

        // 5) 確保 row 存在（無 tx）
        EnsureRowExists(mappingTableName, pkName, pkValue!);

        // 6) 欄位型別（無 tx）
        var columnTypes = LoadColumnTypes(mappingTableName);

        // 7) 組 updatePairs（Identity 判斷若有 tx overload 就用無 tx 版）
        var updatePairs = BuildUpdatePairs(mappingTableName, pkName, request.Fields, columnSet, columnTypes);

        if (updatePairs.Count == 0)
        {
            return 0;
        }

        // 8) 參數化 UPDATE
        var parameters = new DynamicParameters();
        var setFragments = new List<string>(capacity: updatePairs.Count);

        for (var i = 0; i < updatePairs.Count; i++)
        {
            var paramName = $"p{i}";
            parameters.Add(paramName, updatePairs[i].Value);
            setFragments.Add($"[{updatePairs[i].Column}] = @{paramName}");
        }

        parameters.Add("Pk", pkValue);

        var sql = $@"/**/
    UPDATE [{mappingTableName}]
       SET {string.Join(", ", setFragments)}
     WHERE [{pkName}] = @Pk;";

        return await _con.ExecuteAsync(sql, parameters);
    }

    private FormFieldMasterDto GetMappingHeader(Guid formMasterId, SqlTransaction? tx = null)
    {
        var header = _formFieldMasterService.GetFormFieldMasterFromId(formMasterId, tx)
                     ?? throw new InvalidOperationException($"查無設定檔：{formMasterId}");

        if (header.FUNCTION_TYPE != FormFunctionType.MultipleMappingMaintenance)
        {
            throw new InvalidOperationException("設定檔功能類型不符，多對多維護僅接受 FUNCTION_TYPE = MultipleMappingMaintenance。");
        }

        if (string.IsNullOrWhiteSpace(header.BASE_TABLE_NAME) ||
            string.IsNullOrWhiteSpace(header.DETAIL_TABLE_NAME) ||
            string.IsNullOrWhiteSpace(header.MAPPING_TABLE_NAME))
        {
            throw new InvalidOperationException("多對多設定檔缺少必要的資料表名稱");
        }

        if (string.IsNullOrWhiteSpace(header.MAPPING_BASE_FK_COLUMN) ||
            string.IsNullOrWhiteSpace(header.MAPPING_DETAIL_FK_COLUMN))
        {
            throw new InvalidOperationException("多對多設定檔缺少關聯表外鍵欄位設定");
        }

        ValidateColumnName(header.MAPPING_BASE_FK_COLUMN);
        ValidateColumnName(header.MAPPING_DETAIL_FK_COLUMN);

        EnsureColumnExists(header.MAPPING_TABLE_NAME!, header.MAPPING_BASE_FK_COLUMN!, "關聯表缺少指向主表的外鍵欄位", tx);
        EnsureColumnExists(header.MAPPING_TABLE_NAME!, header.MAPPING_DETAIL_FK_COLUMN!, "關聯表缺少指向明細表的外鍵欄位", tx);
        EnsureColumnExists(header.BASE_TABLE_NAME!, header.MAPPING_BASE_FK_COLUMN!, "主表缺少對應的鍵欄位", tx);
        EnsureColumnExists(header.DETAIL_TABLE_NAME!, header.MAPPING_DETAIL_FK_COLUMN!, "明細表缺少對應的鍵欄位", tx);

        _schemaService.ResolvePk(header.BASE_TABLE_NAME!, null, tx);
        _schemaService.ResolvePk(header.DETAIL_TABLE_NAME!, null, tx);

        return header;
    }

    private async Task<string> GetMappingTableNameAsync(Guid formMasterId, CancellationToken ct)
    {
        var where = new WhereBuilder<FormFieldMasterDto>()
            .AndEq(x => x.ID, formMasterId)
            .AndNotDeleted();

        var res = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);

        if (res == null || string.IsNullOrWhiteSpace(res.MAPPING_TABLE_NAME))
        {
            throw new InvalidOperationException("查無對應的關聯表設定，請確認 FormMasterId 是否正確。");
        }

        var mappingTableName = res.MAPPING_TABLE_NAME;
        ValidateTableName(mappingTableName);

        return mappingTableName;
    }

    private List<MultipleMappingItemViewModel> LoadDetailRows(string detailTableName, string detailPkName, IEnumerable<object> detailIds)
    {
        if (!detailIds.Any()) return new();

        var rows = _con.Query($"SELECT * FROM [{detailTableName}] WHERE [{detailPkName}] IN @Ids",
                new { Ids = detailIds })
            .Cast<IDictionary<string, object?>>()
            .ToList();

        return rows.Select(row => ToItem(detailPkName, row)).ToList();
    }

    private List<MultipleMappingItemViewModel> LoadUnlinkedRows(FormFieldMasterDto header, string detailPkName, object? basePkValue)
    {
        var rows = _con.Query($@"/**/
SELECT * FROM [{header.DETAIL_TABLE_NAME}] d
WHERE NOT EXISTS (
    SELECT 1 FROM [{header.MAPPING_TABLE_NAME}] m
    WHERE m.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
      AND m.[{header.MAPPING_DETAIL_FK_COLUMN}] = d.[{detailPkName}]
)", new { BaseId = basePkValue })
            .Cast<IDictionary<string, object?>>()
            .ToList();

        return rows.Select(row => ToItem(detailPkName, row)).ToList();
    }

    private static MultipleMappingItemViewModel ToItem(string pkName, IDictionary<string, object?> row)
    {
        row.TryGetValue(pkName, out var pkVal);
        return new MultipleMappingItemViewModel
        {
            DetailPk = pkVal?.ToString() ?? string.Empty,
            Fields = row.ToDictionary(k => k.Key, v => v.Value)
        };
    }

    private static void ValidateUpsertRequest(MultipleMappingUpsertViewModel request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.BaseId))
        {
            throw new InvalidOperationException("BaseId 不可為空");
        }

        if (request.DetailIds == null || request.DetailIds.Count == 0)
        {
            throw new InvalidOperationException("DetailIds 不可為空");
        }
    }

    private static void ValidateReorderRequest(MappingSequenceReorderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.FormMasterId == Guid.Empty)
        {
            throw new InvalidOperationException("FormMasterId 不可為空");
        }

        if (request.Scope == null || string.IsNullOrWhiteSpace(request.Scope.BasePkValue))
        {
            throw new InvalidOperationException("BasePkValue 不可為空");
        }

        if (request.OrderedSids == null || request.OrderedSids.Count == 0)
        {
            throw new InvalidOperationException("orderedSids 不可為空");
        }

        if (request.OrderedSids.Count != request.OrderedSids.Distinct().Count())
        {
            throw new InvalidOperationException("orderedSids 不可重複");
        }
    }

    private static IEnumerable<object> ConvertDetailIds(IEnumerable<string> detailIds, string detailPkType)
    {
        foreach (var id in detailIds)
        {
            yield return ConvertToColumnTypeHelper.ConvertPkType(id, detailPkType);
        }
    }

    private void EnsureRowExists(string tableName, string pkName, object pkValue, SqlTransaction? tx = null)
    {
        var count = _con.ExecuteScalar<int>(
            $"/**/SELECT COUNT(1) FROM [{tableName}] WHERE [{pkName}] = @Pk",
            new { Pk = pkValue }, transaction: tx);

        if (count == 0)
        {
            throw new InvalidOperationException($"資料不存在：{tableName}.[{pkName}]={pkValue}");
        }
    }

    private static void ValidateUpdateMappingRequestV2(Guid formMasterId, MappingTableUpdateRequest request)
    {
        if (formMasterId == Guid.Empty)
        {
            throw new InvalidOperationException("FormMasterId 不可為空。");
        }

        if (request == null)
        {
            throw new InvalidOperationException("Request 不可為空。");
        }

        if (string.IsNullOrWhiteSpace(request.MappingRowId))
        {
            throw new InvalidOperationException("MappingRowId 不可為空。");
        }

        if (request.Fields == null)
        {
            throw new InvalidOperationException("Fields 不可為空。");
        }
    }

    private static void ValidateColumnName(string columnName)
    {
        if (!Regex.IsMatch(columnName, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"欄位名稱僅允許英數與底線：{columnName}");
        }
    }

    private static void ValidateTableName(string tableName)
    {
        if (!Regex.IsMatch(tableName, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"資料表名稱僅允許英數與底線：{tableName}");
        }
    }

    private Dictionary<string, string> LoadColumnTypes(string tableName, SqlTransaction? tx = null)
    {
        return _con.Query<(string COLUMN_NAME, string DATA_TYPE)>(
                "/**/SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName",
                new { TableName = tableName }, transaction: tx)
            .ToDictionary(x => x.COLUMN_NAME, x => x.DATA_TYPE, StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureColumnExists(string tableName, string columnName, string errorMessage, SqlTransaction? tx)
    {
        var columns = _schemaService.GetFormFieldMaster(tableName, tx)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains(columnName))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private void EnsureReorderColumns(FormFieldMasterDto header, SqlTransaction? tx)
    {
        var columns = _schemaService.GetFormFieldMaster(header.MAPPING_TABLE_NAME!, tx)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains("SID"))
        {
            throw new InvalidOperationException("Mapping 表缺少 SID 欄位，無法執行排序。");
        }

        if (!columns.Contains("SEQ"))
        {
            throw new InvalidOperationException("Mapping 表缺少 SEQ 欄位，無法執行排序。");
        }

        if (!columns.Contains(header.MAPPING_BASE_FK_COLUMN!))
        {
            throw new InvalidOperationException($"Mapping 表缺少 {header.MAPPING_BASE_FK_COLUMN} 欄位，無法依 Base 範圍排序。");
        }
    }

    private void EnsureSidsBelongToBase(FormFieldMasterDto header, object basePkValue, IReadOnlyCollection<decimal> sids, SqlTransaction tx)
    {
        var totalForBase = _con.ExecuteScalar<int>(
            $"/**/SELECT COUNT(1) FROM [{header.MAPPING_TABLE_NAME}] WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId",
            new { BaseId = basePkValue }, transaction: tx);

        if (totalForBase != sids.Count)
        {
            throw new InvalidOperationException("傳入的 orderedSids 與 Base 篩選的資料筆數不符，無法保證 SEQ 唯一性。");
        }

        var matchedCount = _con.ExecuteScalar<int>(
            $"/**/SELECT COUNT(1) FROM [{header.MAPPING_TABLE_NAME}] WHERE SID IN @Sids AND [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId",
            new { Sids = sids, BaseId = basePkValue }, transaction: tx);

        if (matchedCount != sids.Count)
        {
            throw new InvalidOperationException("orderedSids 中至少有一筆不存在或不屬於指定的 BasePkValue。");
        }
    }
    
    private List<(string Column, object? Value)> BuildUpdatePairs(
        string tableName,
        string pkName,
        IDictionary<string, object?> fields,
        ISet<string> columnSet,
        IReadOnlyDictionary<string, string> columnTypes)
    {
        // 用 List 保持順序穩定；用 HashSet 防止重複欄位
        var result = new List<(string Column, object? Value)>(fields.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (column, rawValue) in fields)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                throw new InvalidOperationException("欄位名稱不可為空。");
            }

            // 防 injection：欄位名只允許英數底線（你原本就有）
            ValidateColumnName(column);

            // 防 injection：欄位必須存在於白名單 schema
            if (!columnSet.Contains(column))
            {
                throw new InvalidOperationException($"關聯表不存在欄位：{column}");
            }

            // 防同欄位重複更新（避免後面誰覆蓋誰很難 debug）
            if (!seen.Add(column))
            {
                throw new InvalidOperationException($"欄位名稱重複：{column}");
            }

            // 禁止更新 PK
            if (string.Equals(column, pkName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"不可更新主鍵欄位：{column}");
            }

            // 禁止更新 Identity（你原本就有）
            if (_schemaService.IsIdentityColumn(tableName, column))
            {
                throw new InvalidOperationException($"不可更新 Identity 欄位：{column}");
            }

            // 型別轉換：把前端送來的 object 轉成該 column 的 SQL type 對應型別
            columnTypes.TryGetValue(column, out var sqlType);
            var convertedValue = ConvertToColumnTypeHelper.Convert(sqlType, rawValue);

            result.Add((column, convertedValue));
        }

        return result;
    }

}
