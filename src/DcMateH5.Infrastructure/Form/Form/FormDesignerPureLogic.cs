using System.Text.RegularExpressions;
using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper.FormHelper;
using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Infrastructure.Form.Form;

internal static class FormDesignerPureLogic
{
    public static IReadOnlyList<(string Text, string Value)> NormalizeAndValidateOptions(
        IReadOnlyList<DropdownOptionItemViewModel> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var list = options
            .Select(x => (
                Text: x.OptionText?.Trim(),
                Value: x.OptionValue?.Trim()
            ))
            .ToList();

        if (list.Any(x => string.IsNullOrWhiteSpace(x.Text) || string.IsNullOrWhiteSpace(x.Value)))
        {
            throw new InvalidOperationException("OptionText / OptionValue 不可空白");
        }

        var duplicateValue = list
            .GroupBy(x => x.Value!, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateValue is not null)
        {
            throw new InvalidOperationException($"OptionValue 重複：{duplicateValue.Key}");
        }

        return list
            .Select(x => (x.Text!, x.Value!))
            .ToList();
    }

    public static void ValidateColumnName(string columnName)
    {
        if (!Regex.IsMatch(columnName, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"欄位名稱僅允許英數與底線：{columnName}");
        }
    }

    public static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException("資料表名稱不可為空");
        }

        if (!Regex.IsMatch(tableName, @"^[A-Za-z0-9_\.]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"資料表名稱僅允許英數、底線與點：{tableName}");
        }
    }

    public static bool IsSelectSql(string sql)
    {
        if (!Regex.IsMatch(sql, @"^\s*select\b", RegexOptions.IgnoreCase))
        {
            return false;
        }

        return !Regex.IsMatch(
            sql,
            @"\b(insert|update|delete|drop|alter|truncate|exec|merge)\b",
            RegexOptions.IgnoreCase);
    }

    public static FormFieldViewModel CreateDefaultFieldConfig(
        string columnName,
        string dataType,
        bool sourceIsNullable,
        bool isTvfQueryParameter,
        Guid masterId,
        string tableName,
        long index,
        TableSchemaQueryType schemaType)
    {
        return new FormFieldViewModel
        {
            ID = Guid.NewGuid(),
            FORM_FIELD_MASTER_ID = masterId,
            TableName = tableName,
            COLUMN_NAME = columnName,
            DATA_TYPE = dataType,
            IsNullable = sourceIsNullable,
            IS_TVF_QUERY_PARAMETER = isTvfQueryParameter,
            CONTROL_TYPE = FormFieldHelper.GetDefaultControlType(dataType),
            IS_REQUIRED = false,
            IS_EDITABLE = true,
            IS_DISPLAYED = true,
            FIELD_ORDER = index,
            QUERY_DEFAULT_VALUE = null,
            SchemaType = schemaType,
            QUERY_COMPONENT = QueryComponentType.None,
            QUERY_CONDITION = ConditionType.Like,
            CAN_QUERY = false
        };
    }
}
