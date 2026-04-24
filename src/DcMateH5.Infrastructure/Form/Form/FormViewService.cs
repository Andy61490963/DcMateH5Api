using System.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using DcMateClassLibrary.Enums;
using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper.FormHelper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Form.Form;

public class FormViewService : IFormViewService
{
    private const FormFunctionType FunctionType = FormFunctionType.ViewQueryMaintenance;
    private const TableSchemaQueryType SchemaType = TableSchemaQueryType.OnlyView;

    private static readonly Regex SafeSqlIdentifierRegex =
        new("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly SqlConnection _con;
    private readonly IFormFieldConfigService _formFieldConfigService;
    private readonly IDropdownSqlSyncService _dropdownSqlSyncService;

    public FormViewService(
        SqlConnection connection,
        IFormFieldConfigService formFieldConfigService,
        IDropdownSqlSyncService dropdownSqlSyncService)
    {
        _con = connection;
        _formFieldConfigService = formFieldConfigService;
        _dropdownSqlSyncService = dropdownSqlSyncService;
    }

    public async Task<IEnumerable<ViewFormConfigViewModel>> GetFormMasters(CancellationToken ct = default)
    {
        return await _con.QueryAsync<ViewFormConfigViewModel>(new CommandDefinition(
            Sql.GetMasters,
            new { FunctionType = (int)FunctionType },
            cancellationToken: ct));
    }

    public async Task<List<FormListResponseViewModel>> GetForms(FormSearchRequest request, CancellationToken ct = default)
    {
        var masters = await GetTargetMastersAsync(request.FormMasterId, ct);
        var responses = new List<FormListResponseViewModel>(masters.Count);

        foreach (var master in masters)
        {
            var (schemaName, objectName) = ParseObjectName(master.VIEW_TABLE_NAME!);
            var qualifiedName = QuoteQualifiedName(schemaName, objectName);
            var pkInfo = await TryGetPrimaryKeyInfoAsync(schemaName, objectName, ct);
            var templates = BuildFieldTemplates(master.VIEW_TABLE_ID!.Value, schemaName, objectName);

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(request.Conditions, parameters);
            var orderByClause = BuildOrderByClause(request.OrderBys, request.Page, request.PageSize, pkInfo?.Name);
            var pagingClause = BuildPagingClause(request.Page, request.PageSize, parameters);

            var rawRows = (await _con.QueryAsync(
                    new CommandDefinition(
                        $"SELECT * FROM {qualifiedName}{whereClause}{orderByClause}{pagingClause}",
                        parameters,
                        cancellationToken: ct)))
                .Cast<IDictionary<string, object?>>()
                .ToList();

            var totalCount = await _con.ExecuteScalarAsync<int>(new CommandDefinition(
                $"SELECT COUNT(*) FROM {qualifiedName}{whereClause}",
                parameters,
                cancellationToken: ct));

            var items = rawRows.Select(row => new FormListRowViewModel
            {
                Pk = pkInfo != null && TryGetValueIgnoreCase(row, pkInfo.Name, out var pkValue)
                    ? pkValue?.ToString()
                    : null,
                Fields = templates.Select(template => CloneWithValue(template, ResolveDisplayValue(template, row))).ToList()
            }).ToList();

            responses.Add(new FormListResponseViewModel
            {
                FormMasterId = master.ID,
                FormName = master.FORM_NAME,
                BaseId = master.VIEW_TABLE_ID,
                TotalPageSize = totalCount,
                Items = items
            });
        }

        return responses;
    }

    public async Task<FormSubmissionViewModel> GetForm(Guid formId, string? pk = null, CancellationToken ct = default)
    {
        var master = await GetHeaderAsync(formId, ct);
        var (schemaName, objectName) = ParseObjectName(master.VIEW_TABLE_NAME!);
        var qualifiedName = QuoteQualifiedName(schemaName, objectName);
        var templates = BuildFieldTemplates(master.VIEW_TABLE_ID!.Value, schemaName, objectName);

        IDictionary<string, object?>? row = null;
        if (!string.IsNullOrWhiteSpace(pk))
        {
            var pkInfo = await TryGetPrimaryKeyInfoAsync(schemaName, objectName, ct);
            if (pkInfo == null)
            {
                throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "此 View 未定義可供單筆讀取的主鍵。");
            }

            var typedPk = ConvertToColumnTypeHelper.ConvertPkType(pk, pkInfo.Type);
            row = (await _con.QueryAsync(
                    new CommandDefinition(
                        $"SELECT * FROM {qualifiedName} WHERE [{pkInfo.Name}] = @id",
                        new { id = typedPk },
                        cancellationToken: ct)))
                .Cast<IDictionary<string, object?>>()
                .FirstOrDefault();

            if (row == null)
            {
                throw new HttpStatusCodeException(HttpStatusCode.NotFound, "查無指定的 View 資料。");
            }
        }

        var fields = templates
            .Select(template => CloneWithValue(template, row == null ? null : ResolveDisplayValue(template, row)))
            .ToList();

        return new FormSubmissionViewModel
        {
            FormId = master.ID,
            Pk = pk,
            TargetTableToUpsert = master.VIEW_TABLE_NAME,
            FormName = master.FORM_NAME,
            Fields = fields
        };
    }

    private async Task<List<FormFieldMasterDto>> GetTargetMastersAsync(Guid formMasterId, CancellationToken ct)
    {
        if (formMasterId != Guid.Empty)
        {
            var header = await GetHeaderAsync(formMasterId, ct);
            return new List<FormFieldMasterDto> { header };
        }

        return (await _con.QueryAsync<FormFieldMasterDto>(new CommandDefinition(
                Sql.GetHeaderEntities,
                new { FunctionType = (int)FunctionType },
                cancellationToken: ct)))
            .ToList();
    }

    private async Task<FormFieldMasterDto> GetHeaderAsync(Guid formId, CancellationToken ct)
    {
        var master = await _con.QueryFirstOrDefaultAsync<FormFieldMasterDto>(new CommandDefinition(
            Sql.GetHeaderById,
            new { Id = formId, FunctionType = (int)FunctionType },
            cancellationToken: ct));

        if (master == null || master.VIEW_TABLE_ID == null || string.IsNullOrWhiteSpace(master.VIEW_TABLE_NAME))
        {
            throw new HttpStatusCodeException(HttpStatusCode.NotFound, "查無 View 查詢設定。");
        }

        return master;
    }

    private List<FormFieldInputViewModel> BuildFieldTemplates(Guid masterId, string schemaName, string objectName)
    {
        var data = _formFieldConfigService.LoadFieldConfigData(masterId);
        var columnTypes = LoadColumnTypes(schemaName, objectName);
        var primaryKeys = LoadPrimaryKeys(schemaName, objectName);
        var dynamicOptionCache = new Dictionary<Guid, List<FormFieldDropdownOptionsDto>>();

        return data.FieldConfigs
            .Select(field => BuildFieldViewModel(field, data, columnTypes, primaryKeys, dynamicOptionCache))
            .ToList();
    }

    private FormFieldInputViewModel BuildFieldViewModel(
        FormFieldConfigDto field,
        FieldConfigData data,
        IReadOnlyDictionary<string, string> columnTypes,
        HashSet<string> primaryKeys,
        Dictionary<Guid, List<FormFieldDropdownOptionsDto>> dynamicOptionCache)
    {
        var dropdown = data.DropdownConfigs.FirstOrDefault(item => item.FORM_FIELD_CONFIG_ID == field.ID);
        var finalOptions = ResolveDropdownOptions(dropdown, data, dynamicOptionCache, field.COLUMN_NAME);

        var rules = data.ValidationRules
            .Where(rule => rule.FORM_FIELD_CONFIG_ID == field.ID)
            .OrderBy(rule => rule.VALIDATION_ORDER)
            .ToList();

        var dataType = columnTypes.TryGetValue(field.COLUMN_NAME, out var resolvedType)
            ? resolvedType
            : field.DATA_TYPE;

        return new FormFieldInputViewModel
        {
            FieldConfigId = field.ID,
            Column = field.COLUMN_NAME,
            DISPLAY_NAME = field.DISPLAY_NAME,
            DATA_TYPE = dataType,
            CONTROL_TYPE = field.CONTROL_TYPE,
            DefaultValue = field.QUERY_DEFAULT_VALUE,
            IS_REQUIRED = field.IS_REQUIRED,
            IS_EDITABLE = false,
            IS_DISPLAYED = field.IS_DISPLAYED,
            IS_PK = primaryKeys.Contains(field.COLUMN_NAME),
            IS_RELATION = false,
            ValidationRules = rules,
            ISUSESQL = dropdown?.ISUSESQL ?? false,
            DROPDOWNSQL = dropdown?.DROPDOWNSQL ?? string.Empty,
            QUERY_COMPONENT = field.QUERY_COMPONENT,
            QUERY_CONDITION = field.QUERY_CONDITION,
            CAN_QUERY = field.CAN_QUERY,
            OptionList = finalOptions,
            SOURCE_TABLE = SchemaType,
            DETAIL_TO_RELATION_DEFAULT_COLUMN = field.DETAIL_TO_RELATION_DEFAULT_COLUMN
        };
    }

    private List<FormFieldDropdownOptionsDto> ResolveDropdownOptions(
        FormFieldDropDownDto? dropdown,
        FieldConfigData data,
        Dictionary<Guid, List<FormFieldDropdownOptionsDto>> dynamicOptionCache,
        string columnNameForErrorMessage)
    {
        if (dropdown == null || dropdown.ID == Guid.Empty)
        {
            return new List<FormFieldDropdownOptionsDto>();
        }

        if (dropdown.ISUSESQL && dropdown.IS_QUERY_DROPDOWN && !string.IsNullOrWhiteSpace(dropdown.DROPDOWNSQL))
        {
            return BuildQueryDropdownOptions(dropdown.ID, GetPreviousQueryList(dropdown));
        }

        if (dropdown.ISUSESQL && !string.IsNullOrWhiteSpace(dropdown.DROPDOWNSQL))
        {
            return GetOrSyncSqlDropdownOptions(dropdown.ID, dropdown.DROPDOWNSQL, dynamicOptionCache, columnNameForErrorMessage);
        }

        return data.DropdownOptions
            .Where(option => option.FORM_FIELD_DROPDOWN_ID == dropdown.ID)
            .Where(option => string.IsNullOrWhiteSpace(option.OPTION_TABLE))
            .ToList();
    }

    private List<FormFieldDropdownOptionsDto> GetOrSyncSqlDropdownOptions(
        Guid dropdownId,
        string dropdownSql,
        Dictionary<Guid, List<FormFieldDropdownOptionsDto>> dynamicOptionCache,
        string columnNameForErrorMessage)
    {
        if (dynamicOptionCache.TryGetValue(dropdownId, out var cached))
        {
            return cached;
        }

        try
        {
            var options = _dropdownSqlSyncService.Sync(dropdownId, dropdownSql).Options;
            dynamicOptionCache[dropdownId] = options;
            return options;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"同步下拉選項失敗（欄位 {columnNameForErrorMessage}）：{ex.Message}", ex);
        }
    }

    private List<string> GetPreviousQueryList(FormFieldDropDownDto dropdown)
    {
        if (!dropdown.IS_QUERY_DROPDOWN || string.IsNullOrWhiteSpace(dropdown.DROPDOWNSQL))
        {
            return new List<string>();
        }

        var wrappedSql = $@"
SELECT src.[NAME]
FROM (
{dropdown.DROPDOWNSQL}
) AS src;";

        var shouldClose = _con.State != ConnectionState.Open;
        if (shouldClose)
        {
            _con.Open();
        }

        try
        {
            return _con.Query<string>(wrappedSql, commandTimeout: 10)
                .Select(value => value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList()!;
        }
        catch
        {
            return new List<string>();
        }
        finally
        {
            if (shouldClose)
            {
                _con.Close();
            }
        }
    }

    private static List<FormFieldDropdownOptionsDto> BuildQueryDropdownOptions(Guid dropdownId, List<string> names)
    {
        return names
            .Select(name => name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new FormFieldDropdownOptionsDto
            {
                ID = Guid.Empty,
                FORM_FIELD_DROPDOWN_ID = dropdownId,
                OPTION_TEXT = name!,
                OPTION_VALUE = name!
            })
            .ToList();
    }

    private static FormFieldInputViewModel CloneWithValue(FormFieldInputViewModel template, object? currentValue)
    {
        return new FormFieldInputViewModel
        {
            FieldConfigId = template.FieldConfigId,
            Column = template.Column,
            DISPLAY_NAME = template.DISPLAY_NAME,
            DATA_TYPE = template.DATA_TYPE,
            CONTROL_TYPE = template.CONTROL_TYPE,
            DefaultValue = template.DefaultValue,
            IS_REQUIRED = template.IS_REQUIRED,
            IS_EDITABLE = template.IS_EDITABLE,
            IS_DISPLAYED = template.IS_DISPLAYED,
            IS_PK = template.IS_PK,
            IS_RELATION = template.IS_RELATION,
            ValidationRules = template.ValidationRules,
            ISUSESQL = template.ISUSESQL,
            DROPDOWNSQL = template.DROPDOWNSQL,
            QUERY_COMPONENT = template.QUERY_COMPONENT,
            QUERY_CONDITION = template.QUERY_CONDITION,
            CAN_QUERY = template.CAN_QUERY,
            OptionList = template.OptionList,
            SOURCE_TABLE = template.SOURCE_TABLE,
            CurrentValue = currentValue,
            DETAIL_TO_RELATION_DEFAULT_COLUMN = template.DETAIL_TO_RELATION_DEFAULT_COLUMN
        };
    }

    private static object? ResolveDisplayValue(FormFieldInputViewModel template, IDictionary<string, object?> row)
    {
        if (!TryGetValueIgnoreCase(row, template.Column, out var rawValue))
        {
            return null;
        }

        if (rawValue == null || template.OptionList.Count == 0)
        {
            return rawValue;
        }

        var rawText = rawValue.ToString();
        var option = template.OptionList.FirstOrDefault(item =>
            string.Equals(item.OPTION_VALUE, rawText, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.ID.ToString(), rawText, StringComparison.OrdinalIgnoreCase));

        return option?.OPTION_TEXT ?? rawValue;
    }

    private Dictionary<string, string> LoadColumnTypes(string schemaName, string objectName)
    {
        return _con.Query<(string COLUMN_NAME, string DATA_TYPE)>(
                Sql.LoadColumnTypes,
                new { SchemaName = schemaName, ObjectName = objectName })
            .ToDictionary(item => item.COLUMN_NAME, item => item.DATA_TYPE, StringComparer.OrdinalIgnoreCase);
    }

    private HashSet<string> LoadPrimaryKeys(string schemaName, string objectName)
    {
        return _con.Query<string>(
                Sql.LoadPrimaryKeys,
                new { SchemaName = schemaName, ObjectName = objectName })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<PkInfo?> TryGetPrimaryKeyInfoAsync(string schemaName, string objectName, CancellationToken ct)
    {
        var rows = (await _con.QueryAsync<PkInfo>(new CommandDefinition(
                Sql.LoadPrimaryKeyInfo,
                new { SchemaName = schemaName, ObjectName = objectName },
                cancellationToken: ct)))
            .ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        if (rows.Count > 1)
        {
            throw new InvalidOperationException($"View [{schemaName}].[{objectName}] 使用複合主鍵，FormView 模組目前不支援。");
        }

        return rows[0];
    }

    private static string BuildWhereClause(IEnumerable<FormQueryConditionViewModel>? conditions, DynamicParameters parameters)
    {
        if (conditions == null)
        {
            return string.Empty;
        }

        var whereList = new List<string>();
        var index = 0;

        foreach (var condition in conditions)
        {
            var column = NormalizeAndValidateColumn(condition.Column);
            if (column == null || condition.ConditionType == null)
            {
                continue;
            }

            var p1 = $"p{index++}";

            switch (condition.ConditionType.Value)
            {
                case ConditionType.Equal:
                    whereList.Add($"[{column}] = @{p1}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value));
                    break;
                case ConditionType.NotEqual:
                    whereList.Add($"[{column}] <> @{p1}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value));
                    break;
                case ConditionType.Like:
                    whereList.Add($"[{column}] LIKE @{p1}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value == null ? null : $"%{condition.Value}%"));
                    break;
                case ConditionType.Between:
                    var p2 = $"p{index++}";
                    whereList.Add($"[{column}] BETWEEN @{p1} AND @{p2}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value));
                    parameters.Add(p2, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value2));
                    break;
                case ConditionType.GreaterThan:
                    whereList.Add($"[{column}] > @{p1}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value));
                    break;
                case ConditionType.GreaterThanOrEqual:
                    whereList.Add($"[{column}] >= @{p1}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value));
                    break;
                case ConditionType.LessThan:
                    whereList.Add($"[{column}] < @{p1}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value));
                    break;
                case ConditionType.LessThanOrEqual:
                    whereList.Add($"[{column}] <= @{p1}");
                    parameters.Add(p1, ConvertToColumnTypeHelper.Convert(condition.DataType, condition.Value));
                    break;
                case ConditionType.In:
                    AppendInClause(whereList, parameters, column, condition, ref index, false);
                    break;
                case ConditionType.NotIn:
                    AppendInClause(whereList, parameters, column, condition, ref index, true);
                    break;
            }
        }

        return whereList.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", whereList)}";
    }

    private static void AppendInClause(
        List<string> whereList,
        DynamicParameters parameters,
        string column,
        FormQueryConditionViewModel condition,
        ref int index,
        bool isNotIn)
    {
        if (condition.Values == null || condition.Values.Count == 0)
        {
            return;
        }

        var inParams = new List<string>(condition.Values.Count);
        foreach (var value in condition.Values)
        {
            var parameterName = $"p{index++}";
            inParams.Add($"@{parameterName}");
            parameters.Add(parameterName, ConvertToColumnTypeHelper.Convert(condition.DataType, value));
        }

        whereList.Add($"[{column}] {(isNotIn ? "NOT IN" : "IN")} ({string.Join(", ", inParams)})");
    }

    private static string BuildOrderByClause(IEnumerable<FormOrderBy>? orderBys, int? page, int? pageSize, string? fallbackPk)
    {
        var clauses = new List<string>();

        if (orderBys != null)
        {
            foreach (var orderBy in orderBys)
            {
                var column = NormalizeAndValidateColumn(orderBy.Column);
                if (column == null)
                {
                    continue;
                }

                clauses.Add($"[{column}] {(orderBy.Direction == SortType.Desc ? "DESC" : "ASC")}");
            }
        }

        if (clauses.Count == 0 && !string.IsNullOrWhiteSpace(fallbackPk))
        {
            clauses.Add($"[{fallbackPk}] ASC");
        }

        if (clauses.Count == 0 && page.HasValue && pageSize.HasValue)
        {
            clauses.Add("(SELECT NULL)");
        }

        return clauses.Count == 0 ? string.Empty : $" ORDER BY {string.Join(", ", clauses)}";
    }

    private static string BuildPagingClause(int? page, int? pageSize, DynamicParameters parameters)
    {
        if (!page.HasValue || !pageSize.HasValue)
        {
            return string.Empty;
        }

        var currentPage = Math.Max(page.Value, 1);
        var size = Math.Max(pageSize.Value, 1);
        parameters.Add("offset", (currentPage - 1) * size);
        parameters.Add("pageSize", size);
        return " OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
    }

    private static bool TryGetValueIgnoreCase(IDictionary<string, object?> row, string key, out object? value)
    {
        foreach (var pair in row)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static (string SchemaName, string ObjectName) ParseObjectName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "View 名稱不可為空。");
        }

        var parts = fullName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => ("dbo", ValidateIdentifier(parts[0], "object")),
            2 => (ValidateIdentifier(parts[0], "schema"), ValidateIdentifier(parts[1], "object")),
            _ => throw new HttpStatusCodeException(HttpStatusCode.BadRequest, "View 名稱格式錯誤。")
        };
    }

    private static string ValidateIdentifier(string value, string name)
    {
        if (!SafeSqlIdentifierRegex.IsMatch(value))
        {
            throw new HttpStatusCodeException(HttpStatusCode.BadRequest, $"{name} 名稱包含非法字元。");
        }

        return value;
    }

    private static string QuoteQualifiedName(string schemaName, string objectName) => $"[{schemaName}].[{objectName}]";

    private static string? NormalizeAndValidateColumn(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            return null;
        }

        return SafeSqlIdentifierRegex.IsMatch(column) ? column : null;
    }

    private sealed class PkInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
    }

    private static class Sql
    {
        public const string GetMasters = @"
SELECT
    ID AS Id,
    FORM_NAME AS FormName,
    VIEW_TABLE_ID AS ViewTableId,
    VIEW_TABLE_NAME AS ViewTableName
FROM FORM_FIELD_MASTER
WHERE FUNCTION_TYPE = @FunctionType
  AND IS_DELETE = 0
ORDER BY EDIT_TIME DESC, CREATE_TIME DESC;";

        public const string GetHeaderEntities = @"
SELECT *
FROM FORM_FIELD_MASTER
WHERE FUNCTION_TYPE = @FunctionType
  AND IS_DELETE = 0
ORDER BY EDIT_TIME DESC, CREATE_TIME DESC;";

        public const string GetHeaderById = @"
SELECT TOP (1) *
FROM FORM_FIELD_MASTER
WHERE ID = @Id
  AND FUNCTION_TYPE = @FunctionType
  AND IS_DELETE = 0;";

        public const string LoadColumnTypes = @"
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @ObjectName;";

        public const string LoadPrimaryKeys = @"
SELECT KU.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
 AND TC.TABLE_SCHEMA = KU.TABLE_SCHEMA
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_SCHEMA = @SchemaName
  AND TC.TABLE_NAME = @ObjectName
ORDER BY KU.ORDINAL_POSITION;";

        public const string LoadPrimaryKeyInfo = @"
SELECT
    KU.COLUMN_NAME AS Name,
    COL.DATA_TYPE AS Type
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
 AND TC.TABLE_SCHEMA = KU.TABLE_SCHEMA
JOIN INFORMATION_SCHEMA.COLUMNS COL
  ON COL.TABLE_SCHEMA = KU.TABLE_SCHEMA
 AND COL.TABLE_NAME = KU.TABLE_NAME
 AND COL.COLUMN_NAME = KU.COLUMN_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND TC.TABLE_SCHEMA = @SchemaName
  AND TC.TABLE_NAME = @ObjectName
ORDER BY KU.ORDINAL_POSITION;";
    }
}
