using System.ComponentModel.DataAnnotations;
using DcMateH5Api.Areas.Security.ViewModels.Register;
using Xunit;

namespace DcMateH5ApiTest.Security;

public sealed class RegisterRequestViewModelTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void Validate_Lv_EnforcesNonNegativeRange(int lv, bool expectedIsValid)
    {
        var request = new RegisterRequestViewModel
        {
            Account = "test-account",
            Password = "test-password",
            Lv = lv
        };
        var validationResults = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.Equal(expectedIsValid, isValid);
    }
}
