using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Services.FormLogic;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DynamicForm.Tests.LogicTest;

public class DropdownServiceTests
{
    [Fact]
    public void ToFormDataRows_MapsCellsAndIdsCorrectly()
    {
        // Arrange: 模擬資料列，包含主鍵與其他欄位
        var rawRows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { { "Id", 1 }, { "Name", "Alice" } },
            new Dictionary<string, object?> { { "Id", 2 }, { "Name", "Bob" } }
        };
        var service = new DropdownService(new SqlConnection());

        // Act: 轉換成 ViewModel 並收集主鍵
        var rows = service.ToFormDataRows(rawRows, "Id", out var rowIds);

        // Assert: 驗證資料列數量、主鍵集合與欄位內容
        Assert.Equal(2, rows.Count);
        Assert.Equal(new object[] { 1, 2 }, rowIds);

        var firstRow = rows[0];
        Assert.Equal(1, firstRow.PkId);
        Assert.Contains(firstRow.Cells, c => c.ColumnName == "Id" && (int)c.Value! == 1);
        Assert.Contains(firstRow.Cells, c => c.ColumnName == "Name" && (string)c.Value! == "Alice");
    }

    [Fact]
    public void ToFormDataRows_RespectsPkColumnCaseInsensitively()
    {
        // Arrange: 主鍵欄位名稱大小寫不一致也應被辨識
        var rawRows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { { "id", 99 }, { "Name", "Charlie" } }
        };
        var service = new DropdownService(new SqlConnection());

        // Act
        var rows = service.ToFormDataRows(rawRows, "ID", out var rowIds);

        // Assert
        Assert.Single(rows);
        Assert.Single(rowIds);
        Assert.Equal(99, rows[0].PkId);
    }
}
