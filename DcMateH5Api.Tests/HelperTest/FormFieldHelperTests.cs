using System.Linq;
using ClassLibrary;
using DcMateH5Api.Helper;
using Xunit;

namespace DcMateH5Api.Tests.HelperTest;

public class FormFieldHelperTests
{
    [Fact]
    public void GetQueryConditionTypeWhitelist_Int()
    {
        var result = FormFieldHelper.GetQueryConditionTypeWhitelist("int");
        Assert.Contains(QueryComponentType.Number, result);
        Assert.Contains(QueryComponentType.Text, result);
        Assert.Contains(QueryComponentType.Dropdown, result);
        Assert.DoesNotContain(QueryComponentType.Date, result);
    }

    [Fact]
    public void GetQueryConditionTypeWhitelist_DateTime()
    {
        var result = FormFieldHelper.GetQueryConditionTypeWhitelist("datetime");
        Assert.Equal(new[] { QueryComponentType.Date }, result);
    }
}
