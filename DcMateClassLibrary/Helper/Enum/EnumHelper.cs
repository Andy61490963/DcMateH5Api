using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DcMateH5Api.Areas.Enum.Models;

namespace ClassLibrary;

/// <summary>
/// Enum 相關的輔助方法
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// 將 enum 值轉成整數
    /// </summary>
    /// <param name="obj">列舉值</param>
    /// <returns>對應的 int 值</returns>
    public static int ToInt(this Enum obj)
    {
        return Convert.ToInt32(obj);
    }

    /// <summary>
    /// 將指定的 enum 型別轉成「列舉選項清單」
    /// 給 Controller / Service 使用，
    /// 可以依 enum 型別動態產生下拉選單資料。
    /// </summary>
    /// <param name="enumType">enum 的 Type</param>
    /// <returns>列舉選項清單</returns>
    /// <exception cref="ArgumentException">當傳入的型別不是 enum 時拋出</exception>
    public static List<EnumOptionDto> ToDescriptionList(Type enumType)
    {
        // 防呆：確保傳進來的一定是 enum
        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"{enumType.Name} 不是 enum");
        }

        var result = new List<EnumOptionDto>();

        // 逐一處理 enum 中的每個值
        foreach (var value in Enum.GetValues(enumType))
        {
            // enum 的名稱
            var key = value!.ToString()!;

            // 取得對應的 enum 成員資訊
            var memberInfo = enumType.GetMember(key).FirstOrDefault();

            // 預設顯示文字使用 enum 名稱
            var text = key;
            string? description = null;

            // 嘗試讀取 DisplayAttribute
            var displayAttribute = memberInfo?.GetCustomAttribute<DisplayAttribute>();
            if (displayAttribute != null)
            {
                // Display(Name) 優先作為顯示文字
                if (!string.IsNullOrWhiteSpace(displayAttribute.GetName()))
                {
                    text = displayAttribute.GetName()!;
                }

                // Description 可選，用於 tooltip 或備註
                description = displayAttribute.GetDescription();
            }

            result.Add(new EnumOptionDto
            {
                Value       = Convert.ToInt32(value),
                Key         = key,
                Text        = text,
                Description = description
            });
        }

        return result;
    }
}
