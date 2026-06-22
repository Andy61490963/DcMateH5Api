using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper.FormHelper;
using Xunit;

namespace DcMateH5ApiTest.Form;

public class QueryComponentTypeTests
{
    [Fact]
    public void Radio_KeepsStableContractValue()
    {
        Assert.Equal(7, (int)QueryComponentType.Radio);
    }

    [Theory]
    [InlineData("bit")]
    [InlineData("int")]
    [InlineData("nvarchar")]
    [InlineData("date")]
    [InlineData("datetime")]
    public void GetQueryConditionTypeWhitelist_IncludesRadioForOptionBasedSearch(string dataType)
    {
        var whitelist = FormFieldHelper.GetQueryConditionTypeWhitelist(dataType);

        Assert.Contains(QueryComponentType.Radio, whitelist);
    }

    [Fact]
    public void Radio_UsesEqualQueryCondition()
    {
        Assert.Equal(ConditionType.Equal, QueryComponentType.Radio.ToConditionType());
    }
}
