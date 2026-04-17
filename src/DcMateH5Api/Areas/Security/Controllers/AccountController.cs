using ClassLibrary;
using DcMateClassLibrary.Helper;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.ViewModels.Password;
using DcMateH5Api.Areas.Security.ViewModels.Register;
using DcMateH5Api.Controllers;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Security.Controllers;

/// <summary>
/// Account API.
/// </summary>
[Area("Security")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Security)]
[Route("[area]/[controller]")]
public sealed class AccountController : BaseController
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(Result<RegisterResponseViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<RegisterResponseViewModel>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<RegisterResponseViewModel>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestViewModel request, CancellationToken ct)
    {
        Result<RegisterResponseViewModel> result = await _accountService.RegisterAsync(request, ct);
        return ToRegisterActionResult(result);
    }

    [Authorize]
    [HttpPut("password/reset")]
    [ProducesResponseType(typeof(Result<ResetUserPasswordResponseViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ResetUserPasswordResponseViewModel>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<ResetUserPasswordResponseViewModel>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetUserPasswordRequestViewModel request, CancellationToken ct)
    {
        Result<ResetUserPasswordResponseViewModel> result = await _accountService.ResetPasswordAsync(
            request,
            CurrentUser.Account,
            ct);

        return ToResetPasswordActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("password/forgot")]
    [ProducesResponseType(typeof(Result<ForgotPasswordResponseViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ForgotPasswordResponseViewModel>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<ForgotPasswordResponseViewModel>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<ForgotPasswordResponseViewModel>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestViewModel request, CancellationToken ct)
    {
        Result<ForgotPasswordResponseViewModel> result = await _accountService.ForgotPasswordAsync(request, ct);
        return ToForgotPasswordActionResult(result);
    }

    [AllowAnonymous]
    [HttpGet("password/forgot/verify")]
    [ProducesResponseType(typeof(Result<VerifyForgotPasswordTokenResponseViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<VerifyForgotPasswordTokenResponseViewModel>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyForgotPasswordToken([FromQuery] string token, CancellationToken ct)
    {
        Result<VerifyForgotPasswordTokenResponseViewModel> result =
            await _accountService.VerifyForgotPasswordTokenAsync(token, ct);

        return ToForgotPasswordTokenActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("password/forgot/reset")]
    [ProducesResponseType(typeof(Result<ResetForgotPasswordResponseViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ResetForgotPasswordResponseViewModel>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetForgotPassword(
        [FromBody] ResetForgotPasswordRequestViewModel request,
        CancellationToken ct)
    {
        Result<ResetForgotPasswordResponseViewModel> result =
            await _accountService.ResetForgotPasswordAsync(request, ct);

        return ToForgotPasswordTokenActionResult(result);
    }

    private IActionResult ToRegisterActionResult(Result<RegisterResponseViewModel> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result);
        }

        if (string.Equals(result.Code, AuthenticationErrorCode.AccountAlreadyExists.ToString(), StringComparison.Ordinal))
        {
            return Conflict(result);
        }

        return BadRequest(result);
    }

    private IActionResult ToResetPasswordActionResult(Result<ResetUserPasswordResponseViewModel> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result);
        }

        if (string.Equals(result.Code, AuthenticationErrorCode.UserNotFound.ToString(), StringComparison.Ordinal))
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }

    private IActionResult ToForgotPasswordActionResult(Result<ForgotPasswordResponseViewModel> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result);
        }

        if (string.Equals(result.Code, AuthenticationErrorCode.UserNotFound.ToString(), StringComparison.Ordinal))
        {
            return NotFound(result);
        }

        if (string.Equals(result.Code, AccountResultCodes.EmailSendFailed, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }

        return BadRequest(result);
    }

    private IActionResult ToForgotPasswordTokenActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    private static class AccountResultCodes
    {
        public const string EmailSendFailed = "EmailSendFailed";
    }
}
