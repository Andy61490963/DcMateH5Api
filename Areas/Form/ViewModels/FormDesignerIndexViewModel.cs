using DynamicForm.Areas.Form.Models;

namespace DynamicForm.Areas.Form.ViewModels;

public class FormDesignerIndexViewModel
{
    /// <summary>
    /// 表單主檔基本資訊
    /// </summary>
    public FORM_FIELD_Master FormHeader { get; set; } = new();

    /// <summary>
    /// 主表欄位設定清單
    /// </summary>
    public FormFieldListViewModel BaseFields { get; set; } = new();

    /// <summary>
    /// 檢視(View)欄位設定清單
    /// </summary>
    public FormFieldListViewModel ViewFields { get; set; } = new();

    /// <summary>
    /// 右側欄位設定編輯區所需的資料
    /// </summary>
    public FormFieldViewModel FieldSetting { get; set; } = new();
}

