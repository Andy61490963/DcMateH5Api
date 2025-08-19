using System.Linq;
using ClassLibrary;
using DynamicForm.Helper;
using Xunit;

namespace DynamicForm.Tests.HelperTest;

public class FormFieldHelperTests
{
    [Fact]
    public void GetQueryConditionTypeWhitelist_Int()
    {
        var result = FormFieldHelper.GetQueryConditionTypeWhitelist("int");
        Assert.Contains(QueryConditionType.Number, result);
        Assert.Contains(QueryConditionType.Text, result);
        Assert.Contains(QueryConditionType.Dropdown, result);
        Assert.DoesNotContain(QueryConditionType.Date, result);
    }

    [Fact]
    public void GetQueryConditionTypeWhitelist_DateTime()
    {
        var result = FormFieldHelper.GetQueryConditionTypeWhitelist("datetime");
        Assert.Equal(new[] { QueryConditionType.Date }, result);
    }
}
