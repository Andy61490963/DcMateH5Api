using ClassLibrary;
using DcMateH5Api.Areas.Security.Controllers;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.ViewModels.Password;
using DcMateH5Api.Areas.Security.ViewModels.Register;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DcMateH5ApiTest.Security;

public sealed class AccountControllerTests
{
    [Theory]
    [InlineData("PasswordResetRequestTooFrequent")]
    [InlineData("PasswordResetHourlyLimitReached")]
    public async Task ForgotPassword_WhenRequestIsRateLimited_ReturnsTooManyRequests(string errorCode)
    {
        var controller = new AccountController(new RateLimitedAccountService(errorCode));

        IActionResult result = await controller.ForgotPassword(
            new ForgotPasswordRequestViewModel { Account = "test-account" },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);

        var response = Assert.IsType<Result<ForgotPasswordResponseViewModel>>(objectResult.Value);
        Assert.Equal(errorCode, response.Code);
    }

    private sealed class RateLimitedAccountService : IAccountService
    {
        private readonly string _errorCode;

        public RateLimitedAccountService(string errorCode)
        {
            _errorCode = errorCode;
        }

        public Task<Result<RegisterResponseViewModel>> RegisterAsync(
            RegisterRequestViewModel request,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ResetUserPasswordResponseViewModel>> ResetPasswordAsync(
            ResetUserPasswordRequestViewModel request,
            string actor,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ForgotPasswordResponseViewModel>> ForgotPasswordAsync(
            ForgotPasswordRequestViewModel request,
            CancellationToken ct = default)
            => Task.FromResult(Result<ForgotPasswordResponseViewModel>.Fail(
                Enum.Parse<RateLimitErrorCode>(_errorCode),
                "rate limited"));

        public Task<Result<VerifyForgotPasswordTokenResponseViewModel>> VerifyForgotPasswordTokenAsync(
            string token,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ResetForgotPasswordResponseViewModel>> ResetForgotPasswordAsync(
            ResetForgotPasswordRequestViewModel request,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private enum RateLimitErrorCode
    {
        PasswordResetRequestTooFrequent,
        PasswordResetHourlyLimitReached
    }
}
