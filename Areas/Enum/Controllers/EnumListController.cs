using ClassLibrary;
using DynamicForm.Areas.Enum.Interfaces;
using DynamicForm.Areas.Enum.Models;
using Microsoft.AspNetCore.Mvc;
using DynamicForm.Helper;

namespace DynamicForm.Areas.Enum.Controllers;

/// <summary>
/// 列舉定義列表 API
/// </summary>
[Area("Enum")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Enum)]
[Route("[area]/[controller]")]
public class EnumListController : ControllerBase
{
    private readonly IEnumListService _service;

    /// <summary>
    /// 建構式注入 FormListService。
    /// </summary>
    /// <param name="service">表單清單服務介面</param>
    public EnumListController(IEnumListService service)
    {
        _service = service;
    }
    
    /// <summary>
    /// 1) 不用反射全掃，只暴露你想給前端用的 enum（安全、直覺）
    /// 2) StringComparer.OrdinalIgnoreCase -> 路由大小寫不敏感
    /// </summary>
    private static readonly Dictionary<string, Type> EnumMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["actionType"]     = typeof(ActionType),
            ["tableSchemaQueryType"] = typeof(TableSchemaQueryType),
            ["tableStatusType"] = typeof(TableStatusType),
            ["formControlType"] = typeof(FormControlType),
            ["queryConditionType"] = typeof(QueryConditionType),
            ["validationType"] = typeof(ValidationType),
            // 以後加一行就好：["yourEnumName"] = typeof(YourEnum)
        };

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
}