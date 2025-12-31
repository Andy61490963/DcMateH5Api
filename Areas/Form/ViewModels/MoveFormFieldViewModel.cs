namespace DcMateH5Api.Areas.Form.ViewModels;

public sealed class MoveFormFieldRequest
{
    /// <summary>
    /// 要移動的欄位設定 ID（FORM_FIELD_CONFIG.ID）
    /// </summary>
    public Guid MovingId { get; set; }

    /// <summary>
    /// 移動後的前一個節點 ID（FORM_FIELD_CONFIG.ID）（放最前 = null）
    /// </summary>
    public Guid? PrevId { get; set; }

    /// <summary>
    /// 移動後的後一個節點 ID（FORM_FIELD_CONFIG.ID）（放最後 = null）
    /// </summary>
    public Guid? NextId { get; set; }
}
