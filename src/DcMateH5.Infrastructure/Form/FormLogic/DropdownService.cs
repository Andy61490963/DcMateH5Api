using DbExtensions.DbExecutor.Interface;
using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Infrastructure.Form.FormLogic;

public class DropdownService : IDropdownService
{
    private readonly IDbExecutor _dbExecutor;

    public DropdownService(IDbExecutor dbExecutor)
    {
        _dbExecutor = dbExecutor;
    }

    /// <summary>
    /// 將原始資料列轉為可供表單渲染的結構，並回傳主鍵集合。
    /// </summary>
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

    /// <summary>
    /// 依答案中的 OptionId 批次查詢下拉顯示文字。
    /// </summary>
    public Dictionary<Guid, string> GetOptionTextMap(IEnumerable<DropdownAnswerDto> answers)
    {
        var optionIds = answers.Select(a => a.OptionId).Distinct().ToList();
        if (!optionIds.Any())
        {
            return new Dictionary<Guid, string>();
        }

        return _dbExecutor.QueryAsync<(Guid Id, string Text)>(
                "SELECT ID, OPTION_TEXT AS Text FROM FORM_FIELD_DROPDOWN_OPTIONS WHERE ID IN @Ids",
                new { Ids = optionIds })
            .GetAwaiter()
            .GetResult()
            .ToDictionary(x => x.Id, x => x.Text);
    }

    /// <summary>
    /// 將資料列中的下拉 OptionId 轉換為 OptionText，供前端直接顯示。
    /// </summary>
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

        var answerMap = answers
            .GroupBy(a => a.RowId?.ToString() ?? string.Empty)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.FieldId, x => x.OptionId),
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var rowIdStr = row.PkId?.ToString() ?? string.Empty;
            if (!answerMap.TryGetValue(rowIdStr, out var answerFields))
            {
                continue;
            }

            foreach (var (colName, configId) in dropdownColumns)
            {
                if (!answerFields.TryGetValue(configId, out var optionId) ||
                    !optionTextMap.TryGetValue(optionId, out var text))
                {
                    continue;
                }

                var cell = row.Cells.FirstOrDefault(c =>
                    string.Equals(c.ColumnName, colName, StringComparison.OrdinalIgnoreCase));
                if (cell != null)
                {
                    cell.Value = text;
                }
            }
        }
    }
}
