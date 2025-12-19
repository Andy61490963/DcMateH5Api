using ClassLibrary;
using DcMateH5Api.Areas.Enum.Models;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

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
    /// <returns></returns>
    [HttpGet]
    public IActionResult GetList()
    {
        var result = EnumMap.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<EnumOptionDto>)EnumExtensions.ToDescriptionList(kv.Value)
        );
        return Ok(result);
    }

    /// <summary>
    /// 回單一列舉（大小寫不敏感）
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [HttpGet("{name}")]
    public IActionResult Get(string name)
    {
        if (!EnumMap.TryGetValue(name, out var enumType))
            return NotFound($"Enum '{name}' 不在白名單，請確認名稱或先加到 EnumMap。");

        var list = EnumExtensions.ToDescriptionList(enumType);
        return Ok(list);
    }
    
    /// <summary>
    /// 1) 不用反射全掃，只暴露想給前端用的 enum（安全、直覺）
    /// </summary>
    private static readonly Dictionary<string, Type> EnumMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["actionType"]     = typeof(ActionType),
            ["tableSchemaQueryType"] = typeof(TableSchemaQueryType),
            ["tableStatusType"] = typeof(TableStatusType),
            ["formControlType"] = typeof(FormControlType),
            ["queryConditionType"] = typeof(QueryComponentType),
            ["validationType"] = typeof(ValidationType),
            // 以後加一行就好：["yourEnumName"] = typeof(YourEnum)
        };
}