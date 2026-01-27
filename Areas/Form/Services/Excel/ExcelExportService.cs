using ClosedXML.Excel;
using DcMateH5Api.Areas.Form.Interfaces.Excel;
using DcMateH5Api.Areas.Form.Models.Excel;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Services.Excel;

public sealed class ExcelExportService : IExcelExportService
{
    private const string DefaultContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string SheetName = "Export";
    private const string EmptyText = "";
    private const string PkHeader = "Pk";

    // Header 欄寬設定
    private const double MinHeaderWidth = 10;
    private const double MaxHeaderWidth = 45;
    private const double HeaderPaddingWidth = 2;

    public ExportFileResult ExportFormList(IReadOnlyList<FormListDataViewModel> rows, bool includePk = false)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));
        if (rows.Count == 0) throw new ArgumentException("rows 不可為空", nameof(rows));

        var fileName = BuildFileName(rows[0]);

        var columns = BuildColumns(rows);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SheetName);

        WriteHeader(ws, columns, includePk);
        WriteRows(ws, rows, columns, includePk);

        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        return new ExportFileResult
        {
            FileName = fileName,
            ContentType = DefaultContentType,
            Content = ms.ToArray()
        };
    }

    private static string BuildFileName(FormListDataViewModel firstRow)
    {
        var rawName = (firstRow.FormName ?? "FormExport").Trim();
        var safeName = SanitizeFileName(rawName);
        return $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars);
        return result.Length > 80 ? result[..80] : result;
    }

    private static List<(string Column, string DisplayName)> BuildColumns(IReadOnlyList<FormListDataViewModel> rows)
    {
        var columns = new List<(string Column, string DisplayName)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddColumnsByFirstRowOrder(rows[0], columns, seen);
        AddExtraColumns(rows, columns, seen);

        return columns;
    }

    private static void AddColumnsByFirstRowOrder(
        FormListDataViewModel firstRow,
        List<(string Column, string DisplayName)> columns,
        HashSet<string> seen)
    {
        foreach (var f in firstRow.Fields)
        {
            var col = NormalizeColumn(f.Column);
            if (col is null) continue;
            if (!seen.Add(col)) continue;

            columns.Add((col, GetDisplayName(f, col)));
        }
    }

    private static void AddExtraColumns(
        IReadOnlyList<FormListDataViewModel> rows,
        List<(string Column, string DisplayName)> columns,
        HashSet<string> seen)
    {
        var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            foreach (var f in row.Fields)
            {
                var col = NormalizeColumn(f.Column);
                if (col is null) continue;
                if (seen.Contains(col)) continue;

                extra.TryAdd(col, GetDisplayName(f, col));
            }
        }

        foreach (var kv in extra.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            columns.Add((kv.Key, kv.Value));
        }
    }

    private static string? NormalizeColumn(string? column)
    {
        if (string.IsNullOrWhiteSpace(column)) return null;
        return column.Trim();
    }

    private static string GetDisplayName(FormFieldInputViewModel f, string fallbackColumn)
    {
        return string.IsNullOrWhiteSpace(f.DISPLAY_NAME) ? fallbackColumn : f.DISPLAY_NAME.Trim();
    }

    private static void WriteHeader(
        IXLWorksheet ws,
        IReadOnlyList<(string Column, string DisplayName)> columns,
        bool includePk)
    {
        var startCol = 1;

        // ✅ 只有 includePk=true 才匯出 Pk
        if (includePk)
        {
            ws.Cell(1, startCol).Value = PkHeader;
            ws.Column(startCol).Width = GetHeaderWidth(PkHeader);
            startCol++;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var excelCol = startCol + i;
            var header = columns[i].DisplayName;

            ws.Cell(1, excelCol).Value = header;
            ws.Column(excelCol).Width = GetHeaderWidth(header);
        }

        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRow.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRow.Height = 20;
    }

    private static double GetHeaderWidth(string headerText)
    {
        var len = string.IsNullOrWhiteSpace(headerText) ? 0 : headerText.Trim().Length;
        var width = len + HeaderPaddingWidth;

        if (width < MinHeaderWidth) return MinHeaderWidth;
        if (width > MaxHeaderWidth) return MaxHeaderWidth;
        return width;
    }

    private static void WriteRows(
        IXLWorksheet ws,
        IReadOnlyList<FormListDataViewModel> rows,
        IReadOnlyList<(string Column, string DisplayName)> columns,
        bool includePk)
    {
        var startCol = includePk ? 2 : 1;

        for (var r = 0; r < rows.Count; r++)
        {
            var excelRow = r + 2;
            var row = rows[r];

            // ✅ 只有 includePk=true 才寫入 Pk 欄
            if (includePk)
            {
                ws.Cell(excelRow, 1).Value = row.Pk ?? EmptyText;
            }

            var map = BuildRowValueMap(row);

            for (var i = 0; i < columns.Count; i++)
            {
                var excelCol = startCol + i;
                var columnName = columns[i].Column;

                map.TryGetValue(columnName, out var value);
                ws.Cell(excelRow, excelCol).Value = NormalizeExcelValue(value);
            }
        }
    }

    private static Dictionary<string, object?> BuildRowValueMap(FormListDataViewModel row)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in row.Fields)
        {
            var col = NormalizeColumn(f.Column);
            if (col is null) continue;

            dict.TryAdd(col, f.CurrentValue);
        }

        return dict;
    }

    private static XLCellValue NormalizeExcelValue(object? value)
    {
        if (value is null)
            return EmptyText;

        if (value is string s)
        {
            s = s.Trim();

            // 防 Excel 公式注入：= + - @ 開頭會被 Excel 當公式
            if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@')
                s = "'" + s;

            return s;
        }

        return value.ToString() ?? EmptyText;
    }
}
