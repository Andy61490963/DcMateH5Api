using ClassLibrary;
using Dapper;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces.FormLogic;
using DynamicForm.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Services.FormLogic;

public class DropdownService : IDropdownService
{
    private readonly SqlConnection _con;
    
    public DropdownService(SqlConnection connection)
    {
        _con = connection;
    }
    
    public List<FormDataRow> ToFormDataRows(
        IEnumerable<IDictionary<string, object?>> rawRows,
        string pkColumn,
        out List<object> rowIds)
    {
        var rows = new List<FormDataRow>();
        rowIds = new List<object>();

        foreach (var row in rawRows)
        {
            var vmRow = new FormDataRow();
            foreach (var (col, val) in row)
            {
                if (string.Equals(col, pkColumn, StringComparison.OrdinalIgnoreCase))
                {
                    vmRow.PkId = val!;
                    rowIds.Add(val!);
                }
                vmRow.Cells.Add(new FormDataCell { ColumnName = col, Value = val });
            }
            rows.Add(vmRow);
        }
        return rows;
    }
    
    public List<DropdownAnswerDto> GetAnswers(IEnumerable<object> rowIds)
    {
        var stringIds = rowIds.Select(id => id.ToString()!).ToList();
        if (!stringIds.Any()) return new();
        return _con.Query<DropdownAnswerDto>(
            @"SELECT ROW_ID AS RowId, FORM_FIELD_CONFIG_ID AS FieldId, FORM_FIELD_DROPDOWN_OPTIONS_ID AS OptionId
              FROM FORM_FIELD_DROPDOWN_ANSWER WHERE ROW_ID IN @RowIds",
            new { RowIds = stringIds }).ToList();
    }

    public Dictionary<Guid, string> GetOptionTextMap(IEnumerable<DropdownAnswerDto> answers)
    {
        var optionIds = answers.Select(a => a.OptionId).Distinct().ToList();
        if (!optionIds.Any()) return new();

        return _con.Query<(Guid Id, string Text)>(
            "SELECT ID, OPTION_TEXT AS Text FROM FORM_FIELD_DROPDOWN_OPTIONS WHERE ID IN @Ids",
            new { Ids = optionIds }
        ).ToDictionary(x => x.Id, x => x.Text);
    }

    public void ReplaceDropdownIdsWithTexts(
        List<FormDataRow> rows,
        List<FormFieldConfigDto> fieldConfigs,
        List<DropdownAnswerDto> answers,
        Dictionary<Guid, string> optionTextMap)
    {
        var dropdownColumns = fieldConfigs
            .Where(f => (FormControlType)f.CONTROL_TYPE == FormControlType.Dropdown)
            .Select(f => (f.COLUMN_NAME, f.ID))
            .ToList();

        // Group Answer by RowId(string)
        var answerMap = answers
            .GroupBy(a => a.RowId?.ToString() ?? string.Empty)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.FieldId, x => x.OptionId),
                StringComparer.OrdinalIgnoreCase
            );

        foreach (var row in rows)
        {
            var rowIdStr = row.PkId?.ToString() ?? string.Empty;
            if (!answerMap.TryGetValue(rowIdStr, out var answerFields)) continue;

            foreach (var (colName, configId) in dropdownColumns)
            {
                if (answerFields.TryGetValue(configId, out var optionId) &&
                    optionTextMap.TryGetValue(optionId, out var text))
                {
                    var cell = row.Cells.FirstOrDefault(c => string.Equals(c.ColumnName, colName, StringComparison.OrdinalIgnoreCase));
                    if (cell != null) cell.Value = text;
                }
            }
        }
    }

}