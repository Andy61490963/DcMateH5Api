using ClassLibrary;
using DcMateH5Api.Areas.Enum.Models;
using DcMateH5Api.Helper;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Enum.Controllers;

/// <summary>
/// 列舉定義列表 API
/// </summary>
[Area("Enum")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Enum)]
[Route("[area]/[controller]")]
public class EnumListController : ControllerBase
{
    /// <summary>
    /// 取得列舉定義列表
    /// </summary>
    [HttpGet]
    public IActionResult GetList()
    {
        var data = EnumMap.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<EnumOptionDto>)EnumExtensions.ToDescriptionList(kv.Value),
            StringComparer.OrdinalIgnoreCase);

        return Ok(Result<Dictionary<string, IReadOnlyList<EnumOptionDto>>>.Ok(data));
    }

    /// <summary>
    /// 回單一列舉（大小寫不敏感）
    /// </summary>
    [HttpGet("{name}")]
    public IActionResult Get(string name)
    {
        if (!EnumMap.TryGetValue(name, out var enumType))
        {
            return NotFound(Result<IReadOnlyList<EnumOptionDto>>.Fail(
                EnumErrorCode.EnumNotWhitelisted,
                EnumErrorCode.EnumNotWhitelisted.GetDescription()
            ));
        }

        var list = (IReadOnlyList<EnumOptionDto>)EnumExtensions.ToDescriptionList(enumType);
        return Ok(Result<IReadOnlyList<EnumOptionDto>>.Ok(list));
    }

    /// <summary>
    /// 只暴露想給前端用的 enum（安全、直覺）
    /// </summary>
    private static readonly Dictionary<string, Type> EnumMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["actionType"]           = typeof(ActionType),
            ["tableSchemaQueryType"] = typeof(TableSchemaQueryType),
            ["tableStatusType"]      = typeof(TableStatusType),
            ["formControlType"]      = typeof(FormControlType),
            ["queryConditionType"]   = typeof(QueryComponentType),
            ["validationType"]       = typeof(ValidationType),
        };
}
