using DcMateH5Api.Areas.Security.ViewModels.Password;
using DcMateH5Api.Areas.Security.ViewModels.Register;
using DcMateH5Api.Models;

namespace DcMateH5Api.Areas.Security.Interfaces;

/// <summary>
/// Account service.
/// </summary>
public interface IAccountService
{
    Task<Result<RegisterResponseViewModel>> RegisterAsync(
        RegisterRequestViewModel request,
        CancellationToken ct = default);

    Task<Result<ResetUserPasswordResponseViewModel>> ResetPasswordAsync(
        ResetUserPasswordRequestViewModel request,
        string actor,
        CancellationToken ct = default);

    Task<Result<ForgotPasswordResponseViewModel>> ForgotPasswordAsync(
        ForgotPasswordRequestViewModel request,
        CancellationToken ct = default);

    Task<Result<VerifyForgotPasswordTokenResponseViewModel>> VerifyForgotPasswordTokenAsync(
        string token,
        CancellationToken ct = default);

    Task<Result<ResetForgotPasswordResponseViewModel>> ResetForgotPasswordAsync(
        ResetForgotPasswordRequestViewModel request,
        CancellationToken ct = default);
}
