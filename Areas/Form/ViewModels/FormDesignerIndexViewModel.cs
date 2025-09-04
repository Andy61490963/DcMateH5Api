using DcMateH5Api.Areas.Form.Models;

namespace DcMateH5Api.Areas.Form.ViewModels;

public class FormDesignerIndexViewModel
{
    /// <summary>
    /// 表單主檔基本資訊
    /// </summary>
    public FORM_FIELD_Master FormHeader { get; set; } = new();

    /// <summary>
    /// 主檔欄位設定清單
    /// </summary>
    public FormFieldListViewModel BaseFields { get; set; } = new();
    
    /// <summary>
    /// 主檔欄位設定清單
    /// </summary>
    public FormFieldListViewModel DetailFields { get; set; } = new();

    /// <summary>
    /// 檢視表欄位設定清單
    /// </summary>
    public FormFieldListViewModel ViewFields { get; set; } = new();
}

