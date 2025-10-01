using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.Options;

/// <summary>
/// 提供表單相關的組態設定，供服務類別以 Options Pattern 方式注入使用。
/// </summary>
public sealed class FormSettings
{
    /// <summary>
    /// 取得主表與明細表之間判定關聯欄位時可接受的結尾字串清單。
    /// </summary>
    public List<string> RelationColumnSuffixes { get; init; } = new();
}

