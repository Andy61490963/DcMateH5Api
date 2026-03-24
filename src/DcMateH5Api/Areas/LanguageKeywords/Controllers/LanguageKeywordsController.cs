using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.Language.Models;
using DcMateH5.Abstractions.LanguageKeywords;
using DcMateH5Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.LanguageKeywords.Controllers
{
    /// <summary>
    /// 處理多語系
    /// </summary>
    [Area("LanguageKeywords")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.LanguageKeywords)]
    [Route("[area]/[controller]")]
    public class LanguageKeywordsController : BaseController
    {
        private static class Routes
        {
            public const string Query = "query";
        }

        private readonly ILanguageKeywordService _languageKeywordService;

        public LanguageKeywordsController(ILanguageKeywordService languageKeywordService)
        {
            _languageKeywordService = languageKeywordService;
        }

        /// <summary>
        /// 取得指定語系列表的多語系關鍵字資料
        /// </summary>
        /// <param name="request">查詢條件</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>多語系關鍵字資料</returns>
        [HttpPost(Routes.Query)]
        [ProducesResponseType(typeof(IReadOnlyList<LanguageKeywordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Query([FromBody] LanguageKeywordsRequest request, CancellationToken cancellationToken)
        {
            IReadOnlyList<LanguageKeywordDto> result = await _languageKeywordService.GetKeywordsAsync(
                request.Languages,
                cancellationToken);

            return Ok(result);
        }

    }
}
