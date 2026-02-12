// using DcMateClassLibrary.Helper;
// using DcMateH5Api.Controllers;
//
// using DcMateH5Api.Models;
// using DllLibrary.Test.Interfaces;
// using Microsoft.AspNetCore.Mvc;
//
// namespace DcMateH5Api.Areas.Test.Controllers
// {
//     [Area(Routes.AreaName)]
//     [ApiController]
//     [ApiExplorerSettings(GroupName = SwaggerGroups.Test)]
//     [Route(Routes.Base)]
//     public class TestController : BaseController
//     {
//         private readonly ITestService _testService;
//
//         public TestController(ITestService testService)
//         {
//             _testService = testService;
//         }
//
//         [HttpGet(Routes.Index)]
//         public IActionResult Index()
//         {
//             var message = _testService.GetIndexMessage();
//             return Ok(Result<string>.Ok(message));
//         }
//
//         [HttpGet(Routes.AuthIndex)]
//         public IActionResult AuthIndex()
//         {
//             var message = _testService.GetAuthenticatedMessage(CurrentUser.Account);
//             return Ok(Result<string>.Ok(message));
//         }
//
//         private static class Routes
//         {
//             public const string AreaName = "Test";
//             public const string Base = "[area]/[controller]";
//             public const string Index = "Index";
//             public const string AuthIndex = "AuthIndex";
//         }
//     }
// }