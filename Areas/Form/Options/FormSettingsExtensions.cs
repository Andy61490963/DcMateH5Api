using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DcMateH5Api.Areas.Form.Options;

/// <summary>
/// 提供 <see cref="FormSettings"/> 相關的擴充方法，集中處理組態邏輯。
/// </summary>
public static class FormSettingsExtensions
{
    private const string DefaultRelationSuffix = "_NO";

    /// <summary>
    /// 取得整理後的關聯欄位結尾字串清單，若設定未提供則回傳預設值。
    /// </summary>
    /// <param name="settings">表單設定實例，可為 <c>null</c>。</param>
    /// <returns>整理後且唯讀的結尾字串集合。</returns>
    public static IReadOnlyList<string> GetRelationColumnSuffixesOrDefault(this FormSettings? settings)
    {
        var normalized = settings?.RelationColumnSuffixes?
            .Where(static suffix => !string.IsNullOrWhiteSpace(suffix))
            .Select(static suffix => suffix.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalized.Count == 0)
        {
            normalized.Add(DefaultRelationSuffix);
        }

        return new ReadOnlyCollection<string>(normalized);
    }

    /// <summary>
    /// 判斷指定欄位名稱是否與任何關聯欄位結尾字串相符。
    /// </summary>
    /// <param name="suffixes">可使用的關聯欄位結尾字串集合。</param>
    /// <param name="columnName">要檢查的欄位名稱。</param>
    /// <returns>若符合任一結尾字串則為 <c>true</c>，否則為 <c>false</c>。</returns>
    public static bool MatchesRelationSuffix(this IEnumerable<string> suffixes, string columnName)
    {
        ArgumentNullException.ThrowIfNull(suffixes);

        if (string.IsNullOrWhiteSpace(columnName))
        {
            return false;
        }

        return suffixes.Any(suffix =>
            columnName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}

