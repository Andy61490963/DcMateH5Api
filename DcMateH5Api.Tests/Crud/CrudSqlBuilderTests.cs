using Dapper;
using Xunit;

public class CrudSqlBuilderTests
{
    [Fact]
    public void SafeIdent_Valid_ReturnsBracketed()
    {
        var result = CrudSqlBuilder.SafeIdent("Users");
        Assert.Equal("[Users]", result);
    }

    [Fact]
    public void SafeIdent_Invalid_Throws()
    {
        Assert.Throws<ArgumentException>(() => CrudSqlBuilder.SafeIdent("bad name"));
    }

    [Fact]
    public void BuildDelete_ComposesCompositeWhere()
    {
        var builder = new CrudSqlBuilder();
        var (sql, _) = builder.BuildDelete("Users", new { Id = 1, Code = "A" });
        Assert.Contains("WHERE [Id]=@w_Id AND [Code]=@w_Code", sql);
    }

    [Fact]
    public void BuildUpdate_UsesPrefixedParameters()
    {
        var builder = new CrudSqlBuilder();
        var (sql, param) = builder.BuildUpdate("Users", new { Name = "B" }, new { Id = 1 });
        var dp = Assert.IsType<DynamicParameters>(param);
        Assert.Contains("set_Name", dp.ParameterNames);
        Assert.Contains("w_Id", dp.ParameterNames);
    }
}
