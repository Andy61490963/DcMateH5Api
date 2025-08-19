using System;
using DynamicForm.Helper;
using Xunit;

namespace DynamicForm.Tests.HelperTest;

public class PkHelperTests
{
    [Fact]
    public void ConvertPkType_Guid_ReturnsGuid()
    {
        var guid = Guid.NewGuid();
        var result = ConvertToColumnTypeHelper.ConvertPkType(guid.ToString(), "uniqueidentifier");
        var value = Assert.IsType<Guid>(result);
        Assert.Equal(guid, value);
    }

    [Fact]
    public void ConvertPkType_Int_ReturnsInt()
    {
        var result = ConvertToColumnTypeHelper.ConvertPkType("123", "int");
        var value = Assert.IsType<int>(result);
        Assert.Equal(123, value);
    }

    [Fact]
    public void ConvertPkType_String_ReturnsString()
    {
        var result = ConvertToColumnTypeHelper.ConvertPkType("abc", "nvarchar");
        var value = Assert.IsType<string>(result);
        Assert.Equal("abc", value);
    }

    [Fact]
    public void GeneratePkValue_Uniqueidentifier_ReturnsGuid()
    {
        var result = GeneratePkValueHelper.GeneratePkValue("uniqueidentifier");
        Assert.IsType<Guid>(result);
    }

    [Fact]
    public void GeneratePkValue_Numeric_ReturnsDecimal()
    {
        var result = GeneratePkValueHelper.GeneratePkValue("numeric");
        Assert.IsType<decimal>(result);
    }

    [Fact]
    public void GeneratePkValue_Bigint_ReturnsLong()
    {
        var result = GeneratePkValueHelper.GeneratePkValue("bigint");
        Assert.IsType<long>(result);
    }

    [Fact]
    public void GeneratePkValue_Int_ReturnsInt()
    {
        var result = GeneratePkValueHelper.GeneratePkValue("int");
        Assert.IsType<int>(result);
    }

    [Fact]
    public void GeneratePkValue_Nvarchar_ReturnsString()
    {
        var result = GeneratePkValueHelper.GeneratePkValue("nvarchar");
        var value = Assert.IsType<string>(result);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }
}

