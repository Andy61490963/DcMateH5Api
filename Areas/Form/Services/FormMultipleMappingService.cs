using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
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

    public FormMultipleMappingService(
        SqlConnection connection,
        IFormFieldMasterService formFieldMasterService,
        ISchemaService schemaService,
        ITransactionService txService)
    {
        _con = connection;
        _formFieldMasterService = formFieldMasterService;
        _schemaService = schemaService;
        _txService = txService;
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
  FROM FORM_FIELD_Master
 WHERE FUNCTION_TYPE = @funcType
   AND IS_DELETE = 0";

        return _con.Query<MultipleMappingConfigViewModel>(sql,
            new { funcType = FormFunctionType.MultipleMappingMaintenance.ToInt() });
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

    private static void ValidateColumnName(string columnName)
    {
        if (!Regex.IsMatch(columnName, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"欄位名稱僅允許英數與底線：{columnName}");
        }
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
}
