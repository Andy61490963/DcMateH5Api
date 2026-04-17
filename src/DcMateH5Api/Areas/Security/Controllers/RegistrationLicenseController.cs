using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.RegistrationLicense;
using DcMateH5.Abstractions.RegistrationLicense.Model;
using DcMateH5Api.Controllers;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Security.Controllers;

/// <summary>
/// 註冊碼 API
/// </summary>
[Area("Security")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Security)]
[Route("[area]/[controller]")]
// [Authorize]
public sealed class RegistrationLicenseController : BaseController
{
    private readonly IRegistrationLicenseService _registrationLicenseService;

    public RegistrationLicenseController(IRegistrationLicenseService registrationLicenseService)
    {
        _registrationLicenseService = registrationLicenseService;
    }

    /// <summary>
    /// 產生註冊碼
    /// </summary>
    /// <param name="request">註冊碼產生請求</param>
    /// <returns>註冊碼產生結果</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(Result<LicenseGenerateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<LicenseGenerateResponse>), StatusCodes.Status400BadRequest)]
    public IActionResult Generate([FromBody] LicenseGenerateRequest request)
    {
        try
        {
            LicenseGenerateResponse response = _registrationLicenseService.Generate(request);
            return Ok(Result<LicenseGenerateResponse>.Ok(response));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Result<LicenseGenerateResponse>.Fail(
                RegistrationLicenseErrorCode.InvalidRequest,
                ex.Message));
        }
    }

    private enum RegistrationLicenseErrorCode
    {
        InvalidRequest
    }
}
