using ClassLibrary;
using Xunit;

namespace DynamicForm.Tests.LogicTest;

public class ValidationRulesMapTests
{
    [Fact]
    public void GetValidations_Text_ReturnsRegex()
    {
        var result = ValidationRulesMap.GetValidations(FormControlType.Text);
        Assert.Equal(new[] { ValidationType.Regex }, result);
    }

    [Fact]
    public void GetValidations_Number_ReturnsMinAndMax()
    {
        var result = ValidationRulesMap.GetValidations(FormControlType.Number);
        Assert.Equal(new[] { ValidationType.Min, ValidationType.Max }, result);
    }

    [Fact]
    public void GetValidations_Checkbox_ReturnsEmpty()
    {
        var result = ValidationRulesMap.GetValidations(FormControlType.Checkbox);
        Assert.Empty(result);
    }

    [Fact]
    public void HasValidations_Text_ReturnsTrue()
    {
        Assert.True(ValidationRulesMap.HasValidations(FormControlType.Text));
    }

    [Fact]
    public void HasValidations_Dropdown_ReturnsFalse()
    {
        Assert.False(ValidationRulesMap.HasValidations(FormControlType.Dropdown));
    }

    [Fact]
    public void GetValidations_Unknown_ReturnsEmpty()
    {
        var result = ValidationRulesMap.GetValidations((FormControlType)999);
        Assert.Empty(result);
    }
}
