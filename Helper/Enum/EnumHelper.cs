using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DynamicForm.Areas.Enum.Models;
using DynamicForm.Areas.Form.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClassLibrary;

public static class EnumExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj">列舉物件</param>
    /// <returns></returns>
    public static int ToInt( this Enum obj )
    {
        return Convert.ToInt32( obj );
    }
    
    public static List<EnumOptionDto> ToDescriptionList<TEnum>() where TEnum : Enum
    {
        return Enum.GetValues(typeof(TEnum))
            .Cast<TEnum>()
            .Select(e => new EnumOptionDto
            {
                Value = Convert.ToInt32(e),
                Key   = e.ToString(),
                Text  = GetDisplayName(e)
            })
            .ToList();
    }
    
    /// <summary>
    /// 非泛型版本，給 Controller 用（用名字/字典決定要哪個 enum）
    /// </summary>
    /// <param name="enumType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static List<EnumOptionDto> ToDescriptionList(Type enumType)
    {
        if (!enumType.IsEnum) throw new ArgumentException($"{enumType.Name} 不是 enum");

        var list = new List<EnumOptionDto>();
        foreach (var val in Enum.GetValues(enumType))
        {
            var key = val!.ToString()!;
            var member = enumType.GetMember(key).FirstOrDefault();

            var text = key;
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            var name = display?.GetName(); 
            var description = display?.GetDescription();
            if (!string.IsNullOrWhiteSpace(name)) text = name!;

            list.Add(new EnumOptionDto
            {
                Value = Convert.ToInt32(val), 
                Key   = key,
                Text  = text,
                Description = description
            });
        }
        return list;
    }
    
    public static List<SelectListItem> ToSelectList<TEnum>() where TEnum : Enum
    {
        return Enum.GetValues(typeof(TEnum))
            .Cast<TEnum>()
            .Select(e => new SelectListItem
            {
                Value = Convert.ToInt32(e).ToString(),
                Text = GetDisplayName(e)
            })
            .ToList();
    }

    public static List<ValidationTypeOptionDto> ToSelectList<TEnum>(IEnumerable<TEnum> values) where TEnum : Enum
    {
        return values
            .Select(e => new ValidationTypeOptionDto
            {
                Value = Convert.ToInt32(e).ToString(),
                Text = GetDisplayName(e)
            })
            .ToList();
    }
    
    /// <summary>
    /// 取得列舉描述
    /// </summary>
    /// <param name="enumValue"></param>
    /// <typeparam name="TEnum"></typeparam>
    /// <returns></returns>
    public static string GetDisplayName<TEnum>(TEnum enumValue) where TEnum : Enum
    {
        var member = typeof(TEnum).GetMember(enumValue.ToString()).FirstOrDefault();
        if (member != null)
        {
            var displayAttr = member.GetCustomAttribute<DisplayAttribute>();
            if (displayAttr != null)
                return displayAttr.Name ?? enumValue.ToString();
        }
        return enumValue.ToString();
    }
}
