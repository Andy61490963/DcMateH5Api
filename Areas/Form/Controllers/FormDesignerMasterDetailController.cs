using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Areas.Security.Interfaces;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;
using System.Threading;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Form)]
[Route("[area]/[controller]")]
[Produces("application/json")]
public class FormDesignerMasterDetailController : ControllerBase
{
    private readonly IFormDesignerService _formDesignerService;

    public FormDesignerMasterDetailController(IFormDesignerService formDesignerService)
    {
        _formDesignerService = formDesignerService;
    }
}
