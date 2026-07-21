using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using DbExtensions;
using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper.Enums;
using DcMateClassLibrary.Helper.FormHelper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.Transaction;
using DcMateH5.Abstractions.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Form.Form;

/// <summary>
/// 多對多維護服務，負責提供設定檔、左右清單與批次關聯的核心邏輯。
/// </summary>
public class FormMultipleMappingService : IFormMultipleMappingService
{
    private readonly SqlConnection _con;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly IFormFieldConfigService _formFieldConfigService;
    private readonly ISchemaService _schemaService;
    private readonly ITransactionService _txService;
    private readonly IFormService _formService;
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ICurrentUserAccessor _currentUser;
    
    public FormMultipleMappingService(
        SqlConnection connection,
        IFormFieldMasterService formFieldMasterService,
        IFormFieldConfigService formFieldConfigService,
        ISchemaService schemaService,
        ITransactionService txService,
        IFormService formService,
        SQLGenerateHelper sqlHelper,
        ICurrentUserAccessor currentUser)
    {
        _con = connection;
        _formFieldMasterService = formFieldMasterService;
        _formFieldConfigService = formFieldConfigService;
        _schemaService = schemaService;
        _txService = txService;
        _formService = formService;
        _sqlHelper = sqlHelper;
        _currentUser = currentUser;
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
       MAPPING_DETAIL_FK_COLUMN AS MappingDetailFkColumn,
       MAPPING_BASE_COLUMN_NAME AS MappingBaseColumnName,
       MAPPING_DETAIL_COLUMN_NAME AS MappingDetailColumnName
  FROM FORM_FIELD_MASTER
 WHERE FUNCTION_TYPE = @funcType
   AND IS_DELETE = 0";

        return _con.Query<MultipleMappingConfigViewModel>(sql,
            new { funcType = FormFunctionType.MultipleMappingMaintenance.ToInt() });
    }

    /// <inheritdoc />
    public List<FormListResponseViewModel> GetForms(FormSearchRequest? request = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _formService.GetFormList(FormFunctionType.MultipleMappingMaintenance, request);
    }
    
    /// <inheritdoc />
    public MultipleMappingListViewModel GetMappingList(
        Guid formMasterId,
        MappingListQuery query,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var usePaging = query.Page.HasValue && query.PageSize.HasValue;

        if (query.Page.HasValue != query.PageSize.HasValue)
        {
            throw new InvalidOperationException("page 與 pageSize 必須同時提供，或同時為 null。");
        }

        if (usePaging)
        {
            if (query.Page!.Value <= 0)
            {
                throw new InvalidOperationException("page 必須大於 0");
            }

            if (query.PageSize!.Value <= 0)
            {
                throw new InvalidOperationException("pageSize 必須大於 0");
            }
        }

        var header = GetMappingHeader(formMasterId);

        var (basePkName, _, basePkValue) = _schemaService.ResolvePk(header.BASE_TABLE_NAME!, query.BaseId);
        var (detailPkName, _, _) = _schemaService.ResolvePk(header.DETAIL_TABLE_NAME!, null);

        EnsureRowExists(header.BASE_TABLE_NAME!, basePkName, basePkValue!);

        var baseDisplayText = GetBaseDisplayText(header, basePkValue!);

        var linked = new Dictionary<string, MultipleMappingItemViewModel>(StringComparer.OrdinalIgnoreCase);
        var unlinked = new Dictionary<string, MultipleMappingItemViewModel>(StringComparer.OrdinalIgnoreCase);

        var linkedTotalCount = 0;
        var unlinkedTotalCount = 0;

        if (query.Type == MappingListType.All)
        {
            var linkedResult = LoadLinkedDetailRowsPaged(
                header,
                detailPkName,
                basePkValue!,
                query.DetailConditions,
                query.MappingConditions,
                query.Page,
                query.PageSize,
                query.OrderBySeqAscending);

            var unlinkedResult = LoadUnlinkedRowsPaged(
                header,
                detailPkName,
                basePkValue!,
                baseDisplayText,
                query.DetailConditions,
                query.Page,
                query.PageSize);

            linked = ToDictionaryByDetailPk(linkedResult.Items);
            linkedTotalCount = linkedResult.TotalCount;
            unlinked = ToDictionaryByDetailPk(unlinkedResult.Items);
            unlinkedTotalCount = unlinkedResult.TotalCount;

            var allResult = BuildListViewModel(
                formMasterId,
                header,
                basePkName,
                basePkValue,
                detailPkName,
                linked,
                unlinked,
                linkedTotalCount,
                unlinkedTotalCount);

            allResult.ComponentsByMappingRowId = BuildRuntimeComponents(formMasterId, header, linked.Values);
            return allResult;
        }

        if (query.Type == MappingListType.LinkedOnly)
        {
            var result = LoadLinkedDetailRowsPaged(
                header,
                detailPkName,
                basePkValue!,
                query.DetailConditions,
                query.MappingConditions,
                query.Page,
                query.PageSize,
                query.OrderBySeqAscending);

            linked = ToDictionaryByDetailPk(result.Items);
            linkedTotalCount = result.TotalCount;
        }
        else if (query.Type == MappingListType.UnlinkedOnly)
        {
            EnsureNoMappingConditionsForUnlinked(query.MappingConditions);

            var result = LoadUnlinkedRowsPaged(
                header,
                detailPkName,
                basePkValue!,
                baseDisplayText,
                query.DetailConditions,
                query.Page,
                query.PageSize);

            unlinked = ToDictionaryByDetailPk(result.Items);
            unlinkedTotalCount = result.TotalCount;
        }
        else
        {
            throw new InvalidOperationException("Type 不可為空，使用分頁查詢時請指定 LinkedOnly 或 UnlinkedOnly。");
        }

        var response = new MultipleMappingListViewModel
        {
            FormMasterId = formMasterId,
            BasePkColumn = basePkName,
            BasePk = basePkValue?.ToString() ?? string.Empty,
            DetailPkColumn = detailPkName,
            MappingBaseFkColumn = header.MAPPING_BASE_FK_COLUMN!,
            MappingDetailFkColumn = header.MAPPING_DETAIL_FK_COLUMN!,
            MappingBaseColumnName = header.MAPPING_BASE_COLUMN_NAME,
            MappingDetailColumnName = header.MAPPING_DETAIL_COLUMN_NAME,
            TargetMappingColumnName = header.TARGET_MAPPING_COLUMN_NAME,
            MappingComponentTargetColumnName = header.MAPPING_COMPONENT_TARGET_COLUMN_NAME,
            SourceDetailColumnCode = header.SOURCE_DETAIL_COLUMN_CODE,
            TargetMappingColumnCode = header.TARGET_MAPPING_COLUMN_CODE,
            LinkedTotalCount = linkedTotalCount,
            UnlinkedTotalCount = unlinkedTotalCount,
            Linked = linked,
            Unlinked = unlinked
        };

        response.ComponentsByMappingRowId = BuildRuntimeComponents(formMasterId, header, linked.Values);
        return response;
    }

    /// <inheritdoc />
    public MappingComponentDesignerListViewModel GetMappingComponentConfigurations(
        Guid formMasterId,
        MappingListQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ct.ThrowIfCancellationRequested();

        var linkedQuery = new MappingListQuery
        {
            BaseId = query.BaseId,
            Type = MappingListType.LinkedOnly,
            DetailConditions = query.DetailConditions,
            MappingConditions = query.MappingConditions,
            Page = query.Page,
            PageSize = query.PageSize,
            OrderBySeqAscending = query.OrderBySeqAscending
        };

        var runtime = GetMappingList(formMasterId, linkedQuery, ct);
        var configs = LoadComponentConfigs(
            formMasterId,
            runtime.ComponentsByMappingRowId.Keys.ToArray());

        var result = new MappingComponentDesignerListViewModel
        {
            FormMasterId = formMasterId,
            MappingComponentTargetColumnName = runtime.MappingComponentTargetColumnName,
            TotalCount = runtime.LinkedTotalCount
        };

        foreach (var component in runtime.ComponentsByMappingRowId.Values)
        {
            configs.TryGetValue(component.MappingRowId, out var config);

            result.ComponentsByMappingRowId.Add(component.MappingRowId, new MappingComponentDesignerItemViewModel
            {
                MappingRowId = component.MappingRowId,
                DetailPk = component.DetailPk,
                ControlType = component.ControlType,
                CurrentValue = component.CurrentValue,
                IsUseSql = config?.IsUseSql ?? false,
                DropdownSql = config?.DropdownSql,
                Options = component.Options,
                IsConfigured = component.IsConfigured
            });
        }

        return result;
    }

    /// <inheritdoc />
    public void UpsertMappingComponent(
        Guid formMasterId,
        string mappingRowId,
        MappingComponentUpsertViewModel request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (!Enum.IsDefined(request.ControlType))
        {
            throw new InvalidOperationException("ControlType 不在支援範圍內。");
        }

        if (request.ControlType == FormControlType.None)
        {
            DeleteMappingComponent(formMasterId, mappingRowId, ct);
            return;
        }

        var requestedOptions = request.Options ?? new List<MappingComponentOptionViewModel>();

        _txService.WithTransaction(tx =>
        {
            var header = GetMappingHeader(formMasterId, tx);
            var context = ResolveComponentContext(header, mappingRowId, tx);
            EnsureRowExists(header.MAPPING_TABLE_NAME!, context.MappingPkColumn, context.MappingPkValue, tx);

            var allowedControlTypes = FormFieldHelper.GetControlTypeWhitelist(context.TargetColumnType);
            if (!allowedControlTypes.Contains(request.ControlType))
            {
                throw new InvalidOperationException(
                    $"元件 {request.ControlType} 不適用於欄位 {context.TargetColumn} ({context.TargetColumnType})。");
            }

            var isOptionControl = IsOptionControl(request.ControlType);
            if (!isOptionControl &&
                (request.IsUseSql || !string.IsNullOrWhiteSpace(request.DropdownSql) || requestedOptions.Count > 0))
            {
                throw new InvalidOperationException("只有 Dropdown 或 Radio 可以設定選項。");
            }

            IReadOnlyList<MappingComponentOptionViewModel> options;
            string? dropdownSql = null;

            if (!isOptionControl)
            {
                options = Array.Empty<MappingComponentOptionViewModel>();
            }
            else if (request.IsUseSql)
            {
                dropdownSql = request.DropdownSql?.Trim();
                if (string.IsNullOrWhiteSpace(dropdownSql))
                {
                    throw new InvalidOperationException("SQL Dropdown 必須提供 DropdownSql。");
                }

                options = LoadMappingComponentOptionsFromSql(dropdownSql, tx);
            }
            else
            {
                options = NormalizeComponentOptions(requestedOptions);
                if (options.Count == 0)
                {
                    throw new InvalidOperationException("Dropdown 或 Radio 至少需要一個選項。");
                }
            }

            var convertedOptionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in options)
            {
                if (!ConvertToColumnTypeHelper.TryConvertStrict(
                        context.TargetColumnType,
                        option.Value,
                        out var convertedOptionValue) ||
                    convertedOptionValue is null)
                {
                    throw new InvalidOperationException(
                        $"選項值 {option.Value} 無法轉換為目標欄位型別 {context.TargetColumnType}。");
                }

                if (!convertedOptionValues.Add(NormalizeScalarValue(convertedOptionValue)))
                {
                    throw new InvalidOperationException(
                        $"選項值 {option.Value} 轉換為 {context.TargetColumnType} 後發生重複。");
                }
            }

            var account = GetCurrentAccount();
            var configId = _con.ExecuteScalar<Guid>(@"/**/
MERGE dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_CONFIG WITH (HOLDLOCK) AS target
USING (SELECT @FormMasterId AS FORM_FIELD_MASTER_ID, @MappingRowId AS MAPPING_ROW_ID) AS source
   ON target.FORM_FIELD_MASTER_ID = source.FORM_FIELD_MASTER_ID
  AND target.MAPPING_ROW_ID = source.MAPPING_ROW_ID
  AND target.IS_DELETE = 0
WHEN MATCHED THEN
    UPDATE SET
        CONTROL_TYPE = @ControlType,
        IS_USE_SQL = @IsUseSql,
        DROPDOWN_SQL = @DropdownSql,
        EDIT_TIME = GETDATE(),
        EDIT_USER = @Account
WHEN NOT MATCHED THEN
    INSERT
    (
        ID, FORM_FIELD_MASTER_ID, MAPPING_ROW_ID, CONTROL_TYPE,
        IS_USE_SQL, DROPDOWN_SQL, CREATE_TIME, CREATE_USER, IS_DELETE
    )
    VALUES
    (
        NEWID(), @FormMasterId, @MappingRowId, @ControlType,
        @IsUseSql, @DropdownSql, GETDATE(), @Account, 0
    )
OUTPUT inserted.ID;",
                new
                {
                    FormMasterId = formMasterId,
                    MappingRowId = context.NormalizedMappingRowId,
                    ControlType = (int)request.ControlType,
                    IsUseSql = isOptionControl && request.IsUseSql,
                    DropdownSql = dropdownSql,
                    Account = account
                },
                transaction: tx);

            _con.Execute(@"/**/
UPDATE dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_OPTION
   SET IS_DELETE = 1,
       EDIT_TIME = GETDATE(),
       EDIT_USER = @Account
 WHERE COMPONENT_CONFIG_ID = @ConfigId
   AND IS_DELETE = 0;",
                new { ConfigId = configId, Account = account },
                transaction: tx);

            foreach (var option in options)
            {
                _con.Execute(@"/**/
INSERT INTO dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_OPTION
(
    ID, COMPONENT_CONFIG_ID, OPTION_VALUE, OPTION_TEXT, OPTION_ORDER,
    CREATE_TIME, CREATE_USER, IS_DELETE
)
VALUES
(
    NEWID(), @ConfigId, @Value, @Text, @Order,
    GETDATE(), @Account, 0
);",
                    new
                    {
                        ConfigId = configId,
                        option.Value,
                        option.Text,
                        option.Order,
                        Account = account
                    },
                    transaction: tx);
            }
        });
    }

    /// <inheritdoc />
    public void DeleteMappingComponent(
        Guid formMasterId,
        string mappingRowId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _txService.WithTransaction(tx =>
        {
            var header = GetMappingHeader(formMasterId, tx);
            var context = ResolveComponentContext(header, mappingRowId, tx);
            SoftDeleteComponentConfigs(
                formMasterId,
                new[] { context.NormalizedMappingRowId },
                GetCurrentAccount(),
                tx);
        });
    }

    /// <inheritdoc />
    public Task<int> UpdateMappingComponentValue(
        Guid formMasterId,
        string mappingRowId,
        MappingComponentValueUpdateViewModel request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _txService.WithTransactionAsync(async (_, tx, txCt) =>
        {
            var header = GetMappingHeader(formMasterId, tx);
            var context = ResolveComponentContext(header, mappingRowId, tx);
            EnsureRowExists(header.MAPPING_TABLE_NAME!, context.MappingPkColumn, context.MappingPkValue, tx);

            var configs = LoadComponentConfigs(
                formMasterId,
                new[] { context.NormalizedMappingRowId },
                tx);

            if (!configs.TryGetValue(context.NormalizedMappingRowId, out var config))
            {
                throw new InvalidOperationException("此 Mapping Row 尚未設定動態元件。");
            }

            var convertedValue = ValidateAndConvertComponentValue(
                config.ControlType,
                config.Options,
                context.TargetColumn,
                context.TargetColumnType,
                request.Value);

            var columns = LoadColumnTypes(header.MAPPING_TABLE_NAME!, tx)
                .Keys
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var parameters = new DynamicParameters();
            parameters.Add("Value", convertedValue);
            parameters.Add("MappingPk", context.MappingPkValue);

            var setFragments = new List<string> { $"[{context.TargetColumn}] = @Value" };
            FormAuditColumns.AddUpdateColumns(columns, setFragments, parameters, GetCurrentAccount());

            var command = new CommandDefinition(
                $@"/**/
UPDATE [{header.MAPPING_TABLE_NAME}]
   SET {string.Join(", ", setFragments)}
 WHERE [{context.MappingPkColumn}] = @MappingPk;",
                parameters,
                transaction: tx,
                cancellationToken: txCt);

            return await _con.ExecuteAsync(command);
        }, ct);
    }

    private static MultipleMappingListViewModel BuildListViewModel(
        Guid formMasterId,
        FormFieldMasterDto header,
        string basePkName,
        object? basePkValue,
        string detailPkName,
        Dictionary<string, MultipleMappingItemViewModel> linked,
        Dictionary<string, MultipleMappingItemViewModel> unlinked,
        int linkedTotalCount,
        int unlinkedTotalCount)
    {
        return new MultipleMappingListViewModel
        {
            FormMasterId = formMasterId,
            BasePkColumn = basePkName,
            BasePk = basePkValue?.ToString() ?? string.Empty,
            DetailPkColumn = detailPkName,
            MappingBaseFkColumn = header.MAPPING_BASE_FK_COLUMN!,
            MappingDetailFkColumn = header.MAPPING_DETAIL_FK_COLUMN!,
            MappingBaseColumnName = header.MAPPING_BASE_COLUMN_NAME,
            MappingDetailColumnName = header.MAPPING_DETAIL_COLUMN_NAME,
            TargetMappingColumnName = header.TARGET_MAPPING_COLUMN_NAME,
            MappingComponentTargetColumnName = header.MAPPING_COMPONENT_TARGET_COLUMN_NAME,
            SourceDetailColumnCode = header.SOURCE_DETAIL_COLUMN_CODE,
            TargetMappingColumnCode = header.TARGET_MAPPING_COLUMN_CODE,
            LinkedTotalCount = linkedTotalCount,
            UnlinkedTotalCount = unlinkedTotalCount,
            Linked = linked,
            Unlinked = unlinked
        };
    }

    private static (string WhereSql, DynamicParameters Params) BuildLikeWhere(
        Dictionary<string, string>? filters,
        IReadOnlyCollection<string> allowedColumns,
        string tableAlias,
        string paramPrefix)
    {
        var dp = new DynamicParameters();

        if (filters == null || filters.Count == 0)
            return (string.Empty, dp);

        // 白名單：避免欄位名注入（欄位名無法參數化，只能靠白名單擋）
        var whitelist = new HashSet<string>(allowedColumns, StringComparer.OrdinalIgnoreCase);

        var andParts = new List<string>();
        var pIndex = 0;

        foreach (var (key, value) in filters)
        {
            var col = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(col))
                continue;

            if (!whitelist.Contains(col))
                throw new InvalidOperationException($"不允許的查詢欄位：{col}");

            var raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            // 參數名要可控，避免跟其他段參數撞名
            var paramName = $"{paramPrefix}{pIndex++}";

            // 預設 contains：%keyword%（你要更快可改成 keyword%）
            dp.Add(paramName, $"%{EscapeLike(raw)}%");

            andParts.Add($"{tableAlias}.[{col}] LIKE @{paramName} ESCAPE '\\'");
        }

        if (andParts.Count == 0)
            return (string.Empty, dp);

        return ($" AND {string.Join(" AND ", andParts)}", dp);
    }
    
    private static string EscapeLike(string input)
    {
        return input
            .Replace(@"\", @"\\")
            .Replace("%",  @"\%")
            .Replace("_",  @"\_")
            .Replace("[",  @"\[");
    }

    private static (string WhereSql, DynamicParameters Params) BuildConditionWhere(
        IEnumerable<FormQueryConditionViewModel>? conditions,
        IReadOnlyDictionary<string, string> columnTypes,
        string tableAlias,
        string paramPrefix)
    {
        var dp = new DynamicParameters();

        if (conditions == null)
        {
            return (string.Empty, dp);
        }

        var andParts = new List<string>();
        var pIndex = 0;

        foreach (var condition in conditions)
        {
            var col = (condition.Column ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(col))
            {
                continue;
            }

            ValidateColumnName(col);

            if (!columnTypes.TryGetValue(col, out var dataType))
            {
                throw new InvalidOperationException($"不允許查詢欄位：{col}");
            }

            if (condition.ConditionType == null || condition.ConditionType == ConditionType.None)
            {
                continue;
            }

            var colSql = $"{tableAlias}.[{col}]";
            var p1 = $"{paramPrefix}{pIndex++}";

            switch (condition.ConditionType.Value)
            {
                case ConditionType.Equal:
                    andParts.Add($"{colSql} = @{p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, condition.Value));
                    break;
                case ConditionType.NotEqual:
                    andParts.Add($"{colSql} <> @{p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, condition.Value));
                    break;
                case ConditionType.Like:
                    andParts.Add($"{colSql} LIKE @{p1} ESCAPE '\\'");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, ToLikeParameter(condition.Value)));
                    break;
                case ConditionType.Between:
                    var p2 = $"{paramPrefix}{pIndex++}";
                    andParts.Add($"{colSql} BETWEEN @{p1} AND @{p2}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, condition.Value));
                    dp.Add(p2, ConvertToColumnTypeHelper.Convert(dataType, condition.Value2));
                    break;
                case ConditionType.GreaterThan:
                    andParts.Add($"{colSql} > @{p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, condition.Value));
                    break;
                case ConditionType.GreaterThanOrEqual:
                    andParts.Add($"{colSql} >= @{p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, condition.Value));
                    break;
                case ConditionType.LessThan:
                    andParts.Add($"{colSql} < @{p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, condition.Value));
                    break;
                case ConditionType.LessThanOrEqual:
                    andParts.Add($"{colSql} <= @{p1}");
                    dp.Add(p1, ConvertToColumnTypeHelper.Convert(dataType, condition.Value));
                    break;
                case ConditionType.In:
                    AppendConditionInClause(dp, andParts, colSql, dataType, condition.Values, isNotIn: false, paramPrefix, ref pIndex);
                    break;
                case ConditionType.NotIn:
                    AppendConditionInClause(dp, andParts, colSql, dataType, condition.Values, isNotIn: true, paramPrefix, ref pIndex);
                    break;
                case ConditionType.IsNull:
                    andParts.Add($"{colSql} IS NULL");
                    break;
                case ConditionType.IsNotNull:
                    andParts.Add($"{colSql} IS NOT NULL");
                    break;
            }
        }

        if (andParts.Count == 0)
        {
            return (string.Empty, dp);
        }

        return ($" AND {string.Join(" AND ", andParts)}", dp);
    }

    private static void AppendConditionInClause(
        DynamicParameters parameters,
        List<string> whereParts,
        string columnSql,
        string dataType,
        IReadOnlyList<object>? values,
        bool isNotIn,
        string paramPrefix,
        ref int paramIndex)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        var names = new List<string>(values.Count);

        foreach (var value in values)
        {
            var paramName = $"{paramPrefix}{paramIndex++}";
            names.Add($"@{paramName}");
            parameters.Add(paramName, ConvertToColumnTypeHelper.Convert(dataType, value));
        }

        whereParts.Add($"{columnSql} {(isNotIn ? "NOT IN" : "IN")} ({string.Join(", ", names)})");
    }

    private static object? ToLikeParameter(object? value)
    {
        var raw = value?.ToString();
        return raw == null ? null : $"%{EscapeLike(raw)}%";
    }

    private static void EnsureNoMappingConditionsForUnlinked(
        IReadOnlyCollection<FormQueryConditionViewModel>? mappingConditions)
    {
        if (mappingConditions is { Count: > 0 })
        {
            throw new InvalidOperationException("Unlinked 查詢沒有 Mapping 資料列，不可使用 MappingConditions。");
        }
    }
    
    /// <inheritdoc />
    public void AddMappings(
        Guid formMasterId,
        MultipleMappingUpsertViewModel request,
        bool isSeq,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateUpsertRequest(request);

        _txService.WithTransaction(tx =>
        {
            var header = GetMappingHeader(formMasterId, tx);

            var (basePkName, _, basePkValue) =
                _schemaService.ResolvePk(header.BASE_TABLE_NAME!, request.BaseId, tx);

            var (detailPkName, detailPkType, _) =
                _schemaService.ResolvePk(header.DETAIL_TABLE_NAME!, null, tx);

            EnsureRowExists(header.BASE_TABLE_NAME!, basePkName, basePkValue!, tx);

            var mappingPkColumn = header.MAPPING_PK_COLUMN
                ?? throw new InvalidOperationException("設定檔缺少 MAPPING_PK_COLUMN");

            ValidateColumnName(mappingPkColumn);
            EnsureColumnExists(header.MAPPING_TABLE_NAME!, mappingPkColumn, "Mapping 表缺少主鍵欄位", tx);

            var columnTypes = LoadColumnTypes(header.MAPPING_TABLE_NAME!, tx);
            var mappingPkType = columnTypes.TryGetValue(mappingPkColumn, out var pkType)
                ? pkType
                : throw new InvalidOperationException($"無法取得 Mapping PK 欄位型別：{mappingPkColumn}");

            var isIdentityPk = _schemaService.IsIdentityColumn(header.MAPPING_TABLE_NAME!, mappingPkColumn, tx);
            var currentAccount = GetCurrentAccount();
            var mappingColumns = columnTypes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var seq = 0;
            if (isSeq)
            {
                seq = _con.ExecuteScalar<int>(
                    $@"/**/
    SELECT ISNULL(MAX([SEQ]), 0)
    FROM [{header.MAPPING_TABLE_NAME}]
    WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId;",
                    new { BaseId = basePkValue },
                    transaction: tx);
            }

            foreach (var item in request.Items)
            {
                var detailId = ConvertToColumnTypeHelper.ConvertPkType(item.DetailId, detailPkType);

                EnsureRowExists(header.DETAIL_TABLE_NAME!, detailPkName, detailId!, tx);

                var normalizedExtraFields = NormalizeExtraFields(
                    header,
                    item.ExtraFields,
                    columnTypes,
                    mappingPkColumn,
                    tx);

                var insertColumns = new List<string>();
                var insertValues = new List<string>();
                var parameters = new DynamicParameters();

                if (!isIdentityPk)
                {
                    var pkValue = GeneratePkValueHelper.GeneratePkValue(mappingPkType);
                    insertColumns.Add($"[{mappingPkColumn}]");
                    insertValues.Add("@Pk");
                    parameters.Add("Pk", pkValue);
                }

                insertColumns.Add($"[{header.MAPPING_BASE_FK_COLUMN}]");
                insertValues.Add("@BaseId");
                parameters.Add("BaseId", basePkValue);

                insertColumns.Add($"[{header.MAPPING_DETAIL_FK_COLUMN}]");
                insertValues.Add("@DetailId");
                parameters.Add("DetailId", detailId);

                if (isSeq)
                {
                    insertColumns.Add("[SEQ]");
                    insertValues.Add("@Seq");
                    parameters.Add("Seq", ++seq);
                }

                foreach (var extraField in normalizedExtraFields)
                {
                    insertColumns.Add($"[{extraField.Key}]");
                    insertValues.Add($"@{extraField.Key}");
                    parameters.Add(extraField.Key, extraField.Value);
                }

                FormAuditColumns.AddInsertColumns(
                    mappingColumns,
                    insertColumns,
                    insertValues,
                    parameters,
                    currentAccount);

                var sql = $@"/**/
    IF NOT EXISTS (
        SELECT 1
        FROM [{header.MAPPING_TABLE_NAME}]
        WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
          AND [{header.MAPPING_DETAIL_FK_COLUMN}] = @DetailId
    )
    BEGIN
        INSERT INTO [{header.MAPPING_TABLE_NAME}]
        (
            {string.Join(", ", insertColumns)}
        )
        VALUES
        (
            {string.Join(", ", insertValues)}
        );
    END";

                _con.Execute(sql, parameters, transaction: tx);
            }
        });
    }

    private string GetCurrentAccount()
    {
        var account = _currentUser.Get().Account;

        if (string.IsNullOrWhiteSpace(account))
        {
            throw new InvalidOperationException("無法取得目前登入者帳號名稱");
        }

        return account;
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

            var detailIds = request.Items
                .Select(item => ConvertToColumnTypeHelper.ConvertPkType(item.DetailId, detailPkType))
                .ToList();

            if (!string.IsNullOrWhiteSpace(header.MAPPING_PK_COLUMN))
            {
                ValidateColumnName(header.MAPPING_PK_COLUMN);
                var mappingRowIds = _con.Query<MappingRowIdDbRow>(
                        $@"/**/
SELECT [{header.MAPPING_PK_COLUMN}] AS MappingRowId
  FROM [{header.MAPPING_TABLE_NAME}]
 WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
   AND [{header.MAPPING_DETAIL_FK_COLUMN}] IN @DetailIds;",
                        new { BaseId = basePkValue, DetailIds = detailIds },
                        transaction: tx)
                    .Select(row => NormalizeScalarValue(row.MappingRowId))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                SoftDeleteComponentConfigs(
                    formMasterId,
                    mappingRowIds,
                    GetCurrentAccount(),
                    tx);
            }

            foreach (var detailId in detailIds)
            {
                EnsureRowExists(header.DETAIL_TABLE_NAME!, detailPkName, detailId!, tx);

                _con.Execute($@"/**/
DELETE FROM [{header.MAPPING_TABLE_NAME}]
WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
  AND [{header.MAPPING_DETAIL_FK_COLUMN}] = @DetailId",
                    new
                    {
                        BaseId = basePkValue,
                        DetailId = detailId
                    },
                    transaction: tx);
            }
        });
    }

    /// <summary>
    /// 依設定的 欄位 順序重新整理 Mapping 表的 SEQ 欄位，僅針對同一 Base 主鍵的資料列。
    /// </summary>
    /// <param name="request">包含設定檔、排序後 欄位 SID 清單與 Base 主鍵值的請求模型。</param>
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

            var (basePkName, _, basePkValue) =
                _schemaService.ResolvePk(header.BASE_TABLE_NAME!, request.Scope.BasePkValue, tx);

            EnsureRowExists(header.BASE_TABLE_NAME!, basePkName, basePkValue!, tx);

            var pkColumn = header.MAPPING_PK_COLUMN!;
            var columnTypes = LoadColumnTypes(header.MAPPING_TABLE_NAME!, tx);
            var pkType = columnTypes[pkColumn];

            // string → 正確 PK 型別
            var orderedPks = request.OrderedIds
                .Select(id => ConvertToColumnTypeHelper.ConvertPkType(id, pkType))
                .ToList();

            EnsureMappingPkBelongToBase(header, basePkValue!, orderedPks, tx);

            var parameters = new DynamicParameters();
            parameters.Add("BaseId", basePkValue);

            var valueSql = new List<string>();
            for (var i = 0; i < orderedPks.Count; i++)
            {
                parameters.Add($"pk{i}", orderedPks[i]);
                parameters.Add($"seq{i}", i + 1);
                valueSql.Add($"(@pk{i}, @seq{i})");
            }

            var sql = $@"/**/
;WITH OrderedPk AS (
    SELECT v.Pk, v.Seq
    FROM (VALUES {string.Join(", ", valueSql)}) AS v (Pk, Seq)
)
UPDATE m
   SET m.[SEQ] = o.Seq
  FROM [{header.MAPPING_TABLE_NAME}] m
  JOIN OrderedPk o ON m.[{pkColumn}] = o.Pk
 WHERE m.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId;";

            return _con.Execute(sql, parameters, transaction: tx);
        });
    }

    private void EnsureMappingPkBelongToBase(
        FormFieldMasterDto header,
        object basePkValue,
        IReadOnlyCollection<object> pkValues,
        SqlTransaction tx)
    {
        var totalSql = $@"/**/
SELECT COUNT(1)
FROM [{header.MAPPING_TABLE_NAME}]
WHERE [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId;";

        var total = _con.ExecuteScalar<int>(
            totalSql,
            new { BaseId = basePkValue },
            transaction: tx);

        if (total != pkValues.Count)
            throw new InvalidOperationException("排序數量與 Base 範圍資料筆數不一致");

        var matchedSql = $@"/**/
SELECT COUNT(1)
FROM [{header.MAPPING_TABLE_NAME}]
WHERE [{header.MAPPING_PK_COLUMN}] IN @Ids
  AND [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId;";

        var matched = _con.ExecuteScalar<int>(
            matchedSql,
            new { Ids = pkValues, BaseId = basePkValue },
            transaction: tx);

        if (matched != pkValues.Count)
            throw new InvalidOperationException("排序清單中包含不屬於該 Base 的資料");
    }


    private static class DisplayAlias
    {
        public const string Base  = "__BaseDisplay";
        public const string Detail = "__DetailDisplay";
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

        var mappingHeader = GetMappingHeader(formMasterId);
        if (!string.IsNullOrWhiteSpace(mappingHeader.MAPPING_COMPONENT_TARGET_COLUMN_NAME) &&
            request.Fields.Keys.Any(column =>
                string.Equals(
                    column,
                    mappingHeader.MAPPING_COMPONENT_TARGET_COLUMN_NAME,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return await UpdateMappingTableDataWithComponentValidation(
                formMasterId,
                request,
                ct);
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

        FormAuditColumns.AddUpdateColumns(columnSet, setFragments, parameters, GetCurrentAccount());

        parameters.Add("Pk", pkValue);

        var sql = $@"/**/
    UPDATE [{mappingTableName}]
       SET {string.Join(", ", setFragments)}
     WHERE [{pkName}] = @Pk;";

        return await _con.ExecuteAsync(sql, parameters);
    }

    private Task<int> UpdateMappingTableDataWithComponentValidation(
        Guid formMasterId,
        MappingTableUpdateRequest request,
        CancellationToken ct)
    {
        return _txService.WithTransactionAsync(async (_, tx, txCt) =>
        {
            var header = GetMappingHeader(formMasterId, tx);
            var context = ResolveComponentContext(header, request.MappingRowId, tx);
            EnsureRowExists(
                header.MAPPING_TABLE_NAME!,
                context.MappingPkColumn,
                context.MappingPkValue,
                tx);

            var tableColumns = _schemaService
                .GetFormFieldMaster(header.MAPPING_TABLE_NAME!, tx);
            if (tableColumns.Count == 0)
            {
                throw new InvalidOperationException(
                    $"無法取得關聯表欄位資訊：{header.MAPPING_TABLE_NAME}");
            }

            var columnSet = tableColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var columnTypes = LoadColumnTypes(header.MAPPING_TABLE_NAME!, tx);
            var updatePairs = BuildUpdatePairs(
                header.MAPPING_TABLE_NAME!,
                context.MappingPkColumn,
                request.Fields,
                columnSet,
                columnTypes,
                tx);

            var targetField = request.Fields.First(pair =>
                string.Equals(
                    pair.Key,
                    context.TargetColumn,
                    StringComparison.OrdinalIgnoreCase));
            var configs = LoadComponentConfigs(
                formMasterId,
                new[] { context.NormalizedMappingRowId },
                tx);
            if (!configs.TryGetValue(context.NormalizedMappingRowId, out var config))
            {
                throw new InvalidOperationException("此 Mapping Row 尚未設定動態元件。");
            }

            var convertedTargetValue = ValidateAndConvertComponentValue(
                config.ControlType,
                config.Options,
                context.TargetColumn,
                context.TargetColumnType,
                targetField.Value);
            var targetPairIndex = updatePairs.FindIndex(pair =>
                string.Equals(
                    pair.Column,
                    context.TargetColumn,
                    StringComparison.OrdinalIgnoreCase));
            updatePairs[targetPairIndex] = (context.TargetColumn, convertedTargetValue);

            var parameters = new DynamicParameters();
            var setFragments = new List<string>(capacity: updatePairs.Count);

            for (var i = 0; i < updatePairs.Count; i++)
            {
                var parameterName = $"p{i}";
                parameters.Add(parameterName, updatePairs[i].Value);
                setFragments.Add($"[{updatePairs[i].Column}] = @{parameterName}");
            }

            FormAuditColumns.AddUpdateColumns(
                columnSet,
                setFragments,
                parameters,
                GetCurrentAccount());
            parameters.Add("Pk", context.MappingPkValue);

            var command = new CommandDefinition(
                $@"/**/
UPDATE [{header.MAPPING_TABLE_NAME}]
   SET {string.Join(", ", setFragments)}
 WHERE [{context.MappingPkColumn}] = @Pk;",
                parameters,
                transaction: tx,
                cancellationToken: txCt);

            return await _con.ExecuteAsync(command);
        }, ct);
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
        // if (string.IsNullOrWhiteSpace(header.MAPPING_BASE_COLUMN_NAME) ||
        //     string.IsNullOrWhiteSpace(header.MAPPING_DETAIL_COLUMN_NAME))
        // {
        //     throw new InvalidOperationException("多對多設定檔缺少關聯表外鍵顯示欄位設定");
        // }
        
        ValidateTableName(header.BASE_TABLE_NAME);
        ValidateTableName(header.DETAIL_TABLE_NAME);
        ValidateTableName(header.MAPPING_TABLE_NAME);
        
        ValidateColumnName(header.MAPPING_BASE_FK_COLUMN);
        ValidateColumnName(header.MAPPING_DETAIL_FK_COLUMN);
        
        // ValidateColumnName(header.MAPPING_BASE_COLUMN_NAME);
        // ValidateColumnName(header.MAPPING_DETAIL_COLUMN_NAME);

        EnsureColumnExists(header.MAPPING_TABLE_NAME!, header.MAPPING_BASE_FK_COLUMN!, "關聯表缺少指向主表的外鍵欄位", tx);
        EnsureColumnExists(header.MAPPING_TABLE_NAME!, header.MAPPING_DETAIL_FK_COLUMN!, "關聯表缺少指向明細表的外鍵欄位", tx);
        EnsureColumnExists(header.BASE_TABLE_NAME!, header.MAPPING_BASE_FK_COLUMN!, "主表缺少對應的鍵欄位", tx);
        EnsureColumnExists(header.DETAIL_TABLE_NAME!, header.MAPPING_DETAIL_FK_COLUMN!, "明細表缺少對應的鍵欄位", tx);

        // _schemaService.ResolvePk(header.BASE_TABLE_NAME!, null, tx);
        // _schemaService.ResolvePk(header.DETAIL_TABLE_NAME!, null, tx);

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

    private string? GetBaseDisplayText(FormFieldMasterDto header, object basePkValue)
    {
        var baseDisplayColumn = header.MAPPING_BASE_COLUMN_NAME;
        if (string.IsNullOrWhiteSpace(baseDisplayColumn))
            return null;

        var sql = $@"/**/
SELECT b.[{baseDisplayColumn}]
FROM [{header.BASE_TABLE_NAME}] b
WHERE b.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId;";

        return _con.QueryFirstOrDefault<string?>(sql, new { BaseId = basePkValue });
    }
    
    private Dictionary<string, string> GetDetailToRelationDefaultColumnMap(
        Guid? detailTableId,
        string mappingTableName,
        List<string> mappingColumns)
    {
        var rows = _formFieldConfigService.GetFormFieldConfig(detailTableId);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.COLUMN_NAME) ||
                string.IsNullOrWhiteSpace(r.DETAIL_TO_RELATION_DEFAULT_COLUMN))
                continue;

            var detailCol  = r.COLUMN_NAME.Trim();
            var mappingCol = r.DETAIL_TO_RELATION_DEFAULT_COLUMN.Trim();

            if (result.TryGetValue(detailCol, out var exist) &&
                !exist.Equals(mappingCol, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"DETAIL_TO_RELATION_DEFAULT_COLUMN 設定衝突：{detailCol}");
            }

            if (!mappingColumns.Contains(mappingCol, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Mapping 表 {mappingTableName} 不存在欄位 {mappingCol}");
            }

            result[detailCol] = mappingCol;
        }

        return result;
    }

    /// <summary>
    /// 這坨東西全是 GPT 生的，老闆要快速交付，只能等厲害的人來重構了 QQ
    /// </summary>
    /// <param name="header"></param>
    /// <param name="detailPkName"></param>
    /// <param name="basePkValue"></param>
    /// <param name="filters"></param>
    /// <param name="tx"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private PageQueryResult<MultipleMappingItemViewModel> LoadLinkedDetailRowsPaged(
        FormFieldMasterDto header,
        string detailPkName,
        object basePkValue,
        IReadOnlyList<FormQueryConditionViewModel>? detailConditions,
        IReadOnlyList<FormQueryConditionViewModel>? mappingConditions,
        int? page,
        int? pageSize,
        bool orderBySeqAscending,
        SqlTransaction? tx = null)
    {
        if (string.IsNullOrWhiteSpace(header.MAPPING_BASE_COLUMN_NAME))
        {
            throw new InvalidOperationException("缺少 Base 顯示欄位設定");
        }

        var mappingColumnTypes = LoadColumnTypes(header.MAPPING_TABLE_NAME!, tx);
        var detailColumnTypes = LoadColumnTypes(header.DETAIL_TABLE_NAME!, tx);
        var mappingColumns = mappingColumnTypes.Keys.ToList();
        var detailColumns = detailColumnTypes.Keys.ToList();

        if (!detailColumns.Contains(detailPkName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Detail PK 不存在");
        }

        EnsureColumnExists(header.MAPPING_TABLE_NAME!, MappingColumnNames.Sequence, "Mapping 表缺少 SEQ 欄位", tx);

        var defaultMap = GetDetailToRelationDefaultColumnMap(
            header.DETAIL_TABLE_ID,
            header.MAPPING_TABLE_NAME!,
            mappingColumns);

        var mappingDropdownMeta = BuildDropdownMetaMap(
            header.MAPPING_TABLE_ID,
            TableSchemaQueryType.OnlyMapping,
            header.MAPPING_TABLE_NAME!);

        var mappingSelect = string.Join(", ", mappingColumns.Select(c => $"m.[{c}] AS [m__{c}]"));
        var detailSelect = string.Join(", ", detailColumns.Select(c => $"d.[{c}] AS [d__{c}]"));

        var (detailWhereSql, detailParams) = BuildConditionWhere(detailConditions, detailColumnTypes, "d", "dWhere");
        var (mappingWhereSql, mappingParams) = BuildConditionWhere(mappingConditions, mappingColumnTypes, "m", "mWhere");

        var param = new DynamicParameters();
        param.AddDynamicParams(detailParams);
        param.AddDynamicParams(mappingParams);
        param.Add("BaseId", basePkValue);

        var countSql = $@"/**/
    SELECT COUNT(1)
    FROM [{header.MAPPING_TABLE_NAME}] m
    JOIN [{header.DETAIL_TABLE_NAME}] d
      ON m.[{header.MAPPING_DETAIL_FK_COLUMN}] = d.[{detailPkName}]
    JOIN [{header.BASE_TABLE_NAME}] b
      ON m.[{header.MAPPING_BASE_FK_COLUMN}] = b.[{header.MAPPING_BASE_FK_COLUMN}]
    WHERE m.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
    {detailWhereSql}
    {mappingWhereSql};";

        var totalCount = _con.ExecuteScalar<int>(countSql, param, transaction: tx);

        var orderBySql = BuildLinkedOrderBySql(header, orderBySeqAscending);
        var pagingSql = BuildPagingSql(page, pageSize, param);

        var dataSql = $@"/**/
    SELECT
        {mappingSelect},
        {detailSelect},
        b.[{header.MAPPING_BASE_COLUMN_NAME}] AS BaseText
    FROM [{header.MAPPING_TABLE_NAME}] m
    JOIN [{header.DETAIL_TABLE_NAME}] d
      ON m.[{header.MAPPING_DETAIL_FK_COLUMN}] = d.[{detailPkName}]
    JOIN [{header.BASE_TABLE_NAME}] b
      ON m.[{header.MAPPING_BASE_FK_COLUMN}] = b.[{header.MAPPING_BASE_FK_COLUMN}]
    WHERE m.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
    {detailWhereSql}
    {mappingWhereSql}
    {orderBySql}
    {pagingSql};";

        var rows = _con.Query(dataSql, param, transaction: tx)
            .Cast<IDictionary<string, object?>>()
            .ToList();

        var items = new List<MultipleMappingItemViewModel>(rows.Count);

        foreach (var row in rows)
        {
            var mappingRaw = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var detailRaw = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in row)
            {
                if (key.StartsWith("m__", StringComparison.OrdinalIgnoreCase))
                {
                    mappingRaw[key[3..]] = value;
                }
                else if (key.StartsWith("d__", StringComparison.OrdinalIgnoreCase))
                {
                    detailRaw[key[3..]] = value;
                }
            }

            var pkText = detailRaw.TryGetValue(detailPkName, out var pkVal)
                ? pkVal?.ToString() ?? string.Empty
                : string.Empty;

            var mappingRowId = ResolveMappingRowId(header, mappingRaw);

            items.Add(new MultipleMappingItemViewModel
            {
                MappingRowId = mappingRowId,
                DetailPk = pkText,
                BaseDisplayText = row["BaseText"]?.ToString(),
                DetailDisplayText = pkText,
                DetailToRelationDefaultColumn = defaultMap,
                MappingFields = BuildFieldValueDict(mappingRaw, mappingDropdownMeta),
                DetailFields = BuildFieldValueDict(detailRaw, null)
            });
        }

        return new PageQueryResult<MultipleMappingItemViewModel>
        {
            TotalCount = totalCount,
            Items = items
        };
    }
    
     private PageQueryResult<MultipleMappingItemViewModel> LoadUnlinkedRowsPaged(
        FormFieldMasterDto header,
        string detailPkName,
        object basePkValue,
        string? baseDisplayText,
        IReadOnlyList<FormQueryConditionViewModel>? detailConditions,
        int? page,
        int? pageSize,
        SqlTransaction? tx = null)
    {
        var detailColumnTypes = LoadColumnTypes(header.DETAIL_TABLE_NAME!, tx);
        var mappingColumns = _schemaService.GetFormFieldMaster(header.MAPPING_TABLE_NAME!, tx).ToList();
        var detailColumns = detailColumnTypes.Keys.ToList();

        var defaultMap = GetDetailToRelationDefaultColumnMap(
            header.DETAIL_TABLE_ID,
            header.MAPPING_TABLE_NAME!,
            mappingColumns);

        var detailSelect = string.Join(", ", detailColumns.Select(c => $"d.[{c}] AS [d__{c}]"));

        var (filterSql, param) = BuildConditionWhere(detailConditions, detailColumnTypes, "d", "dWhere");
        param.Add("BaseId", basePkValue);

        var countSql = $@"/**/
    SELECT COUNT(1)
    FROM [{header.DETAIL_TABLE_NAME}] d
    WHERE 1 = 1
    {filterSql}
    AND NOT EXISTS (
        SELECT 1
        FROM [{header.MAPPING_TABLE_NAME}] m
        WHERE m.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
          AND m.[{header.MAPPING_DETAIL_FK_COLUMN}] = d.[{detailPkName}]
    );";

        var totalCount = _con.ExecuteScalar<int>(countSql, param, transaction: tx);

        var pagingSql = BuildPagingSql(page, pageSize, param);

        var dataSql = $@"/**/
    SELECT
        {detailSelect}
    FROM [{header.DETAIL_TABLE_NAME}] d
    WHERE 1 = 1
    {filterSql}
    AND NOT EXISTS (
        SELECT 1
        FROM [{header.MAPPING_TABLE_NAME}] m
        WHERE m.[{header.MAPPING_BASE_FK_COLUMN}] = @BaseId
          AND m.[{header.MAPPING_DETAIL_FK_COLUMN}] = d.[{detailPkName}]
    )
    ORDER BY d.[{detailPkName}]
    {pagingSql};";

        var rows = _con.Query(dataSql, param, transaction: tx)
            .Cast<IDictionary<string, object?>>()
            .ToList();

        var items = new List<MultipleMappingItemViewModel>(rows.Count);

        foreach (var row in rows)
        {
            var detailRaw = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in row)
            {
                if (key.StartsWith("d__", StringComparison.OrdinalIgnoreCase))
                {
                    detailRaw[key[3..]] = value;
                }
            }

            var pkText = detailRaw.TryGetValue(detailPkName, out var pkVal)
                ? pkVal?.ToString() ?? string.Empty
                : string.Empty;

            items.Add(new MultipleMappingItemViewModel
            {
                DetailPk = pkText,
                BaseDisplayText = baseDisplayText,
                DetailDisplayText = pkText,
                DetailToRelationDefaultColumn = defaultMap,
                MappingFields = new Dictionary<string, FieldValueViewModel>(StringComparer.OrdinalIgnoreCase),
                DetailFields = BuildFieldValueDict(detailRaw, null)
            });
        }

        return new PageQueryResult<MultipleMappingItemViewModel>
        {
            TotalCount = totalCount,
            Items = items
        };
    }
    
    private static string BuildPagingSql(int? page, int? pageSize, DynamicParameters parameters)
    {
        if (!page.HasValue && !pageSize.HasValue)
        {
            return string.Empty;
        }

        if (!page.HasValue || !pageSize.HasValue)
        {
            throw new InvalidOperationException("page 與 pageSize 必須同時提供，或同時為 null。");
        }

        if (page.Value <= 0)
        {
            throw new InvalidOperationException("page 必須大於 0");
        }

        if (pageSize.Value <= 0)
        {
            throw new InvalidOperationException("pageSize 必須大於 0");
        }

        parameters.Add("Offset", (page.Value - 1) * pageSize.Value);
        parameters.Add("PageSize", pageSize.Value);

        return "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
    }
    
    private static Dictionary<string, FieldValueViewModel> BuildFieldValueDict(
        IReadOnlyDictionary<string, object?> raw,
        IReadOnlyDictionary<string, DropdownMeta>? dropdownMeta)
    {
        var dict = new Dictionary<string, FieldValueViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var (col, val) in raw)
        {
            IReadOnlyList<DropdownOptionViewModel>? options = null;

            if (dropdownMeta != null && dropdownMeta.TryGetValue(col, out var meta))
            {
                options = meta.Options;
            }

            dict[col] = new FieldValueViewModel
            {
                Value = val,
                Options = options
            };
        }

        return dict;
    }

    private sealed class DropdownMeta
    {
        public IReadOnlyList<DropdownOptionViewModel> Options { get; init; } = Array.Empty<DropdownOptionViewModel>();
        public IReadOnlyDictionary<string, string> ValueToText { get; init; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, DropdownMeta> BuildDropdownMetaMap(
        Guid? masterId,
        TableSchemaQueryType schemaType,
        string tableName)
    {
        var templates = _formService.GetFieldTemplates(masterId, schemaType, tableName);

        return templates
            .Where(t => !string.IsNullOrWhiteSpace(t.Column))
            // .Where(t => t.CONTROL_TYPE == FormControlType.Dropdown) // 用你們的 enum 判斷
            .Where(t => t.OptionList is { Count: > 0 })
            .ToDictionary(
                t => t.Column,
                t =>
                {
                    var options = t.OptionList
                        .Where(o => !string.IsNullOrWhiteSpace(o.OPTION_VALUE))
                        .Select(o => new DropdownOptionViewModel
                        {
                            Value = o.OPTION_VALUE.Trim(),
                            Text = (o.OPTION_TEXT ?? string.Empty).Trim()
                        })
                        .ToList();

                    var map = options
                        .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().Text, StringComparer.OrdinalIgnoreCase);

                    return new DropdownMeta
                    {
                        Options = options,
                        ValueToText = map
                    };
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, MultipleMappingComponentViewModel> BuildRuntimeComponents(
        Guid formMasterId,
        FormFieldMasterDto header,
        IEnumerable<MultipleMappingItemViewModel> linkedItems)
    {
        var items = linkedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.MappingRowId))
            .ToList();

        var rowIds = items
            .Select(item => item.MappingRowId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var configs = LoadComponentConfigs(formMasterId, rowIds);
        var result = new Dictionary<string, MultipleMappingComponentViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (result.ContainsKey(item.MappingRowId))
            {
                throw new InvalidOperationException($"Mapping PK 重複，無法作為元件 key：{item.MappingRowId}");
            }

            configs.TryGetValue(item.MappingRowId, out var config);

            object? currentValue = null;
            if (!string.IsNullOrWhiteSpace(header.MAPPING_COMPONENT_TARGET_COLUMN_NAME) &&
                item.MappingFields.TryGetValue(header.MAPPING_COMPONENT_TARGET_COLUMN_NAME, out var fieldValue))
            {
                currentValue = fieldValue.Value;
            }

            result[item.MappingRowId] = new MultipleMappingComponentViewModel
            {
                MappingRowId = item.MappingRowId,
                DetailPk = item.DetailPk,
                ControlType = config?.ControlType ?? FormControlType.None,
                CurrentValue = currentValue,
                Options = config?.Options
                    .Select(CloneComponentOption)
                    .ToList() ?? new List<MappingComponentOptionViewModel>(),
                IsConfigured = config != null
            };
        }

        return result;
    }

    private Dictionary<string, ComponentConfigAggregate> LoadComponentConfigs(
        Guid formMasterId,
        IReadOnlyCollection<string> mappingRowIds,
        SqlTransaction? tx = null)
    {
        if (mappingRowIds.Count == 0)
        {
            return new Dictionary<string, ComponentConfigAggregate>(StringComparer.OrdinalIgnoreCase);
        }

        const string configSql = @"/**/
SELECT ID AS Id,
       MAPPING_ROW_ID AS MappingRowId,
       CONTROL_TYPE AS ControlType,
       IS_USE_SQL AS IsUseSql,
       DROPDOWN_SQL AS DropdownSql
  FROM dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_CONFIG
 WHERE FORM_FIELD_MASTER_ID = @FormMasterId
   AND MAPPING_ROW_ID IN @MappingRowIds
   AND IS_DELETE = 0;";

        var configs = _con.Query<ComponentConfigAggregate>(
                configSql,
                new { FormMasterId = formMasterId, MappingRowIds = mappingRowIds },
                transaction: tx)
            .ToList();

        if (configs.Count == 0)
        {
            return new Dictionary<string, ComponentConfigAggregate>(StringComparer.OrdinalIgnoreCase);
        }

        const string optionSql = @"/**/
SELECT COMPONENT_CONFIG_ID AS ComponentConfigId,
       OPTION_VALUE AS Value,
       OPTION_TEXT AS Text,
       OPTION_ORDER AS [Order]
  FROM dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_OPTION
 WHERE COMPONENT_CONFIG_ID IN @ConfigIds
   AND IS_DELETE = 0
 ORDER BY COMPONENT_CONFIG_ID, OPTION_ORDER, ID;";

        var options = _con.Query<ComponentOptionDbRow>(
                optionSql,
                new { ConfigIds = configs.Select(config => config.Id).ToArray() },
                transaction: tx)
            .ToList();

        var optionsByConfigId = options
            .GroupBy(option => option.ComponentConfigId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(option => new MappingComponentOptionViewModel
                {
                    Value = option.Value,
                    Text = option.Text,
                    Order = option.Order
                }).ToList());

        foreach (var config in configs)
        {
            if (optionsByConfigId.TryGetValue(config.Id, out var configOptions))
            {
                config.Options = configOptions;
            }
        }

        return configs.ToDictionary(
            config => config.MappingRowId,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveMappingRowId(
        FormFieldMasterDto header,
        IReadOnlyDictionary<string, object?> mappingRaw)
    {
        if (string.IsNullOrWhiteSpace(header.MAPPING_PK_COLUMN) ||
            !mappingRaw.TryGetValue(header.MAPPING_PK_COLUMN, out var mappingPk) ||
            mappingPk is null)
        {
            return string.Empty;
        }

        return NormalizeScalarValue(mappingPk);
    }

    private ComponentContext ResolveComponentContext(
        FormFieldMasterDto header,
        string mappingRowId,
        SqlTransaction? tx = null)
    {
        if (string.IsNullOrWhiteSpace(mappingRowId))
        {
            throw new InvalidOperationException("MappingRowId 不可為空。");
        }

        if (string.IsNullOrWhiteSpace(header.MAPPING_PK_COLUMN))
        {
            throw new InvalidOperationException("設定檔缺少 MAPPING_PK_COLUMN。");
        }

        if (string.IsNullOrWhiteSpace(header.MAPPING_COMPONENT_TARGET_COLUMN_NAME))
        {
            throw new InvalidOperationException("設定檔缺少 MAPPING_COMPONENT_TARGET_COLUMN_NAME。");
        }

        ValidateColumnName(header.MAPPING_PK_COLUMN);
        ValidateColumnName(header.MAPPING_COMPONENT_TARGET_COLUMN_NAME);

        var columnTypes = LoadColumnTypes(header.MAPPING_TABLE_NAME!, tx);
        if (!columnTypes.TryGetValue(header.MAPPING_PK_COLUMN, out var mappingPkType))
        {
            throw new InvalidOperationException($"Mapping Table 缺少主鍵欄位：{header.MAPPING_PK_COLUMN}");
        }

        if (!columnTypes.TryGetValue(header.MAPPING_COMPONENT_TARGET_COLUMN_NAME, out var targetColumnType))
        {
            throw new InvalidOperationException(
                $"Mapping Table 缺少逐 SID 元件目標值欄位：{header.MAPPING_COMPONENT_TARGET_COLUMN_NAME}");
        }

        var mappingPkValue = ConvertToColumnTypeHelper.ConvertPkType(mappingRowId.Trim(), mappingPkType);

        return new ComponentContext(
            header.MAPPING_PK_COLUMN,
            mappingPkValue,
            NormalizeScalarValue(mappingPkValue),
            header.MAPPING_COMPONENT_TARGET_COLUMN_NAME,
            targetColumnType);
    }

    private IReadOnlyList<MappingComponentOptionViewModel> LoadMappingComponentOptionsFromSql(
        string sql,
        SqlTransaction tx)
    {
        if (!IsReadOnlyComponentOptionSql(sql))
        {
            throw new InvalidOperationException("DropdownSql 僅允許 SELECT 查詢。");
        }

        using var command = _con.CreateCommand();
        command.CommandText = sql;
        command.Transaction = tx;

        using var reader = command.ExecuteReader();
        var schema = reader.GetColumnSchema();
        var idColumn = FindColumn(schema, "ID")
                       ?? throw new InvalidOperationException("DropdownSql 必須回傳 ID 欄位。");
        var nameColumn = FindColumn(schema, "NAME")
                         ?? throw new InvalidOperationException("DropdownSql 必須回傳 NAME 欄位。");

        var result = new List<MappingComponentOptionViewModel>();
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            var value = ReadRequiredOptionValue(reader, idColumn, "ID");
            var text = ReadRequiredOptionValue(reader, nameColumn, "NAME");
            if (!values.Add(value))
            {
                throw new InvalidOperationException($"DropdownSql 回傳重複的 ID：{value}");
            }

            result.Add(new MappingComponentOptionViewModel
            {
                Value = value,
                Text = text,
                Order = result.Count + 1
            });
        }

        return result;
    }

    private static IReadOnlyList<MappingComponentOptionViewModel> NormalizeComponentOptions(
        IReadOnlyList<MappingComponentOptionViewModel> options)
    {
        var result = new List<MappingComponentOptionViewModel>(options.Count);
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < options.Count; index++)
        {
            var value = options[index].Value?.Trim();
            var text = options[index].Text?.Trim();
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Dropdown 選項的 Value 與 Text 不可為空。");
            }

            if (!values.Add(value))
            {
                throw new InvalidOperationException($"Dropdown 選項 Value 重複：{value}");
            }

            result.Add(new MappingComponentOptionViewModel
            {
                Value = value,
                Text = text,
                Order = options[index].Order > 0 ? options[index].Order : index + 1
            });
        }

        return result
            .OrderBy(option => option.Order)
            .ThenBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SoftDeleteComponentConfigs(
        Guid formMasterId,
        IReadOnlyCollection<string> mappingRowIds,
        string account,
        SqlTransaction tx)
    {
        if (mappingRowIds.Count == 0)
        {
            return;
        }

        _con.Execute(@"/**/
UPDATE componentOption
   SET componentOption.IS_DELETE = 1,
       componentOption.EDIT_TIME = GETDATE(),
       componentOption.EDIT_USER = @Account
  FROM dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_OPTION componentOption
  JOIN dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_CONFIG componentConfig
    ON componentConfig.ID = componentOption.COMPONENT_CONFIG_ID
 WHERE componentConfig.FORM_FIELD_MASTER_ID = @FormMasterId
   AND componentConfig.MAPPING_ROW_ID IN @MappingRowIds
   AND componentOption.IS_DELETE = 0;

UPDATE dbo.FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_CONFIG
   SET IS_DELETE = 1,
       EDIT_TIME = GETDATE(),
       EDIT_USER = @Account
 WHERE FORM_FIELD_MASTER_ID = @FormMasterId
   AND MAPPING_ROW_ID IN @MappingRowIds
   AND IS_DELETE = 0;",
            new
            {
                FormMasterId = formMasterId,
                MappingRowIds = mappingRowIds,
                Account = account
            },
            transaction: tx);
    }

    private static object? ValidateAndConvertComponentValue(
        FormControlType controlType,
        IReadOnlyList<MappingComponentOptionViewModel> options,
        string targetColumn,
        string targetColumnType,
        object? value)
    {
        if (IsOptionControl(controlType) && value is null)
        {
            throw new InvalidOperationException("Dropdown 或 Radio 的輸入值不可為 NULL。");
        }

        if (!ConvertToColumnTypeHelper.TryConvertStrict(
                targetColumnType,
                value,
                out var convertedValue))
        {
            throw new InvalidOperationException(
                $"輸入值無法轉換為欄位 {targetColumn} 的型別 {targetColumnType}。");
        }

        if (IsOptionControl(controlType))
        {
            var normalizedValue = NormalizeScalarValue(convertedValue!);
            var isValidOption = options.Any(option =>
                ConvertToColumnTypeHelper.TryConvertStrict(
                    targetColumnType,
                    option.Value,
                    out var convertedOptionValue) &&
                convertedOptionValue is not null &&
                string.Equals(
                    NormalizeScalarValue(convertedOptionValue),
                    normalizedValue,
                    StringComparison.OrdinalIgnoreCase));

            if (!isValidOption)
            {
                throw new InvalidOperationException("輸入值不在此元件的有效選項中。");
            }
        }

        return convertedValue;
    }

    private static bool IsOptionControl(FormControlType controlType) =>
        controlType is FormControlType.Dropdown or FormControlType.Radio;

    private static bool IsReadOnlyComponentOptionSql(string sql)
    {
        if (!FormDesignerPureLogic.IsSelectSql(sql))
        {
            return false;
        }

        var statement = sql.Trim();
        if (statement.EndsWith(';'))
        {
            statement = statement[..^1].TrimEnd();
        }

        if (statement.Contains(';'))
        {
            return false;
        }

        return !Regex.IsMatch(
            statement,
            @"\b(into|create|grant|revoke|deny|backup|restore|dbcc|waitfor|shutdown|use)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static MappingComponentOptionViewModel CloneComponentOption(
        MappingComponentOptionViewModel option) =>
        new()
        {
            Value = option.Value,
            Text = option.Text,
            Order = option.Order
        };

    private static string NormalizeScalarValue(object value)
    {
        var normalized = value switch
        {
            decimal decimalValue => decimalValue.ToString("G29", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        return normalized?.Trim()
               ?? throw new InvalidOperationException("Mapping PK 無法轉換為字串。");
    }

    private static DbColumn? FindColumn(IReadOnlyList<DbColumn> columns, string name) =>
        columns.FirstOrDefault(column =>
            string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase));

    private static string ReadRequiredOptionValue(
        SqlDataReader reader,
        DbColumn column,
        string columnName)
    {
        var ordinal = column.ColumnOrdinal
                      ?? throw new InvalidOperationException($"DropdownSql 的 {columnName} 欄位沒有 ordinal。");
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException($"DropdownSql 的 {columnName} 不可為 NULL。");
        }

        var value = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"DropdownSql 的 {columnName} 不可為空。");
        }

        return value;
    }

    private sealed class ComponentConfigAggregate
    {
        public Guid Id { get; set; }
        public string MappingRowId { get; set; } = string.Empty;
        public FormControlType ControlType { get; set; }
        public bool IsUseSql { get; set; }
        public string? DropdownSql { get; set; }
        public List<MappingComponentOptionViewModel> Options { get; set; } = new();
    }

    private sealed class ComponentOptionDbRow
    {
        public Guid ComponentConfigId { get; set; }
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    private sealed class MappingRowIdDbRow
    {
        public object MappingRowId { get; set; } = default!;
    }

    private sealed record ComponentContext(
        string MappingPkColumn,
        object MappingPkValue,
        string NormalizedMappingRowId,
        string TargetColumn,
        string TargetColumnType);

    private static void ValidateUpsertRequest(MultipleMappingUpsertViewModel request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.BaseId))
        {
            throw new InvalidOperationException("BaseId 不可為空");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Items 不可為空");
        }

        var detailIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            if (item == null)
            {
                throw new InvalidOperationException("Items 不可包含 null");
            }

            if (string.IsNullOrWhiteSpace(item.DetailId))
            {
                throw new InvalidOperationException("DetailId 不可為空");
            }

            if (!detailIds.Add(item.DetailId.Trim()))
            {
                throw new InvalidOperationException($"DetailId 不可重複：{item.DetailId}");
            }
        }
    }

    private static void ValidateReorderRequest(MappingSequenceReorderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.FormMasterId == Guid.Empty)
            throw new InvalidOperationException("FormMasterId 不可為空");

        if (request.Scope == null || string.IsNullOrWhiteSpace(request.Scope.BasePkValue))
            throw new InvalidOperationException("BasePkValue 不可為空");

        if (request.OrderedIds == null || request.OrderedIds.Count == 0)
            throw new InvalidOperationException("OrderedIds 不可為空");

        var normalized = request.OrderedIds
            .Select(x => x.Trim())
            .ToList();

        if (normalized.Count != normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidOperationException("OrderedIds 不可重複");
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

        if (string.IsNullOrWhiteSpace(header.MAPPING_PK_COLUMN))
            throw new InvalidOperationException("設定檔缺少 MAPPING_PK_COLUMN");

        if (!columns.Contains(header.MAPPING_PK_COLUMN))
            throw new InvalidOperationException($"Mapping 表缺少 {header.MAPPING_PK_COLUMN} 欄位");

        if (!columns.Contains("SEQ"))
            throw new InvalidOperationException("Mapping 表缺少 SEQ 欄位");

        if (!columns.Contains(header.MAPPING_BASE_FK_COLUMN!))
            throw new InvalidOperationException($"Mapping 表缺少 {header.MAPPING_BASE_FK_COLUMN} 欄位");
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
            $"/**/SELECT COUNT(1) FROM [{header.MAPPING_TABLE_NAME}] WHERE [{header.MAPPING_PK_COLUMN}] IN @Sids AND [{header.MAPPING_BASE_FK_COLUMN}] = @BaseId",
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
        IReadOnlyDictionary<string, string> columnTypes,
        SqlTransaction? tx = null)
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
            if (_schemaService.IsIdentityColumn(tableName, column, tx))
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

    private static Dictionary<string, MultipleMappingItemViewModel> ToDictionaryByDetailPk(
        IEnumerable<MultipleMappingItemViewModel> items)
    {
        var result = new Dictionary<string, MultipleMappingItemViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.DetailPk))
            {
                throw new InvalidOperationException("DetailPk 不可為空");
            }

            if (result.ContainsKey(item.DetailPk))
            {
                throw new InvalidOperationException($"DetailPk 重複：{item.DetailPk}");
            }

            result[item.DetailPk] = item;
        }

        return result;
    }
    
    private sealed class PageQueryResult<T>
    {
        public int TotalCount { get; init; }

        public List<T> Items { get; init; } = new();
    }
    
    private static class MappingColumnNames
    {
        public const string Sequence = "SEQ";
    }

    private static class SqlSortDirection
    {
        public const string Asc = "ASC";
        public const string Desc = "DESC";
    }

    private string BuildLinkedOrderBySql(FormFieldMasterDto header, bool orderBySeqAscending)
    {
        var direction = orderBySeqAscending
            ? SqlSortDirection.Asc
            : SqlSortDirection.Desc;

        return
            $"ORDER BY m.[{MappingColumnNames.Sequence}] {direction}, " +
            $"m.[{header.MAPPING_DETAIL_FK_COLUMN}] {direction}";
    }
    
    private Dictionary<string, object?> NormalizeExtraFields(
        FormFieldMasterDto header,
        IDictionary<string, object?>? extraFields,
        IReadOnlyDictionary<string, string> columnTypes,
        string mappingPkColumn,
        SqlTransaction tx)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (extraFields == null || extraFields.Count == 0)
        {
            return result;
        }

        var mappingTableName = header.MAPPING_TABLE_NAME
            ?? throw new InvalidOperationException("設定檔缺少 MAPPING_TABLE_NAME");

        var mappingColumns = _schemaService.GetFormFieldMaster(mappingTableName, tx)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var reservedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            mappingPkColumn,
            header.MAPPING_BASE_FK_COLUMN!,
            header.MAPPING_DETAIL_FK_COLUMN!,
            FormAuditColumns.CreateTime,
            FormAuditColumns.EditTime,
            FormAuditColumns.CreateUser,
            FormAuditColumns.EditUser
        };

        if (columnTypes.ContainsKey("SEQ"))
        {
            reservedColumns.Add("SEQ");
        }

        foreach (var pair in extraFields)
        {
            var columnName = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new InvalidOperationException("ExtraFields 欄位名稱不可為空");
            }

            ValidateColumnName(columnName);

            if (!mappingColumns.Contains(columnName))
            {
                throw new InvalidOperationException($"Mapping 表不存在欄位：{columnName}");
            }

            if (reservedColumns.Contains(columnName))
            {
                throw new InvalidOperationException($"欄位不可由 ExtraFields 指定：{columnName}");
            }

            if (_schemaService.IsIdentityColumn(mappingTableName, columnName, tx))
            {
                throw new InvalidOperationException($"不可寫入 Identity 欄位：{columnName}");
            }

            if (!columnTypes.TryGetValue(columnName, out var sqlType))
            {
                throw new InvalidOperationException($"無法取得欄位型別：{columnName}");
            }

            var convertedValue = ConvertToColumnTypeHelper.Convert(sqlType, pair.Value);
            result[columnName] = convertedValue;
        }

        return result;
    }
}
