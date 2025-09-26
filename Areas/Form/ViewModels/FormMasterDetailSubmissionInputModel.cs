using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 主明細表提交的輸入模型。
/// </summary>
public class FormMasterDetailSubmissionInputModel
{
    /// <summary>主表的 FORM_FIELD_Master.ID。</summary>
    public Guid MasterId { get; set; }

    /// <summary>主表資料主鍵，新增時可為 null。</summary>
    public string? MasterPk { get; set; }

    /// <summary>主表欄位資料。</summary>
    public List<FormInputField> MasterFields { get; set; } = new();

    /// <summary>明細表列資料。</summary>
    public List<FormDetailRowInputModel> DetailRows { get; set; } = new();
}

/// <summary>
/// 明細表單列資料的輸入模型。
/// </summary>
public class FormDetailRowInputModel
{
    /// <summary>明細資料主鍵，新增時可為 null。</summary>
    public string? Pk { get; set; }

    /// <summary>明細欄位資料。</summary>
    public List<FormInputField> Fields { get; set; } = new();
}
