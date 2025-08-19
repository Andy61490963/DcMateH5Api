using System;
using DynamicForm.Helper;
using Xunit;

namespace DynamicForm.Tests.HelperTest;

public class ConvertToColumnTypeHelperTests
{
    [Fact]
    public void Convert_Int_ReturnsLong()
    {
        var result = ConvertToColumnTypeHelper.Convert("int", "123");
        var value = Assert.IsType<long>(result);
        Assert.Equal(123L, value);
    }

    [Fact]
    public void Convert_Decimal_ReturnsDecimal()
    {
        var result = ConvertToColumnTypeHelper.Convert("decimal", "123.45");
        var value = Assert.IsType<decimal>(result);
        Assert.Equal(123.45m, value);
    }

    [Fact]
    public void Convert_Datetime_ReturnsDateTime()
    {
        var result = ConvertToColumnTypeHelper.Convert("datetime", "2024-01-01");
        var value = Assert.IsType<DateTime>(result);
        Assert.Equal(new DateTime(2024, 1, 1), value);
    }

    [Fact]
    public void Convert_Bool_ReturnsBoolean()
    {
        var result = ConvertToColumnTypeHelper.Convert("bit", "1");
        var value = Assert.IsType<bool>(result);
        Assert.True(value);
    }

    [Fact]
    public void Convert_Nvarchar_ReturnsString()
    {
        var result = ConvertToColumnTypeHelper.Convert("nvarchar", "hello");
        var value = Assert.IsType<string>(result);
        Assert.Equal("hello", value);
    }
}

