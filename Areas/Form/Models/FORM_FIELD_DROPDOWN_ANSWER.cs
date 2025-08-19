using System;

namespace DynamicForm.Areas.Form.Models;

public class FORM_FIELD_DROPDOWN_ANSWER
{
    /// <summary>
    /// 排序用序號
    /// </summary>
    public int SEQNO { get; set; }

    public Guid ID { get; set; }

    /// <summary>
    /// 對應 FORM_FIELD_CONFIG.ID，得知此答案隸屬於哪個欄位
    /// </summary>
    public Guid FORM_FIELD_CONFIG_ID { get; set; }

    /// <summary>
    /// 對應主資料表的紀錄識別
    /// </summary>
    public Guid ROW_ID { get; set; }

    /// <summary>
    /// 對應 FORM_FIELD_DROPDOWN_OPTIONS.ID，使用者實際選擇的選項
    /// </summary>
    public Guid FORM_FIELD_DROPDOWN_OPTIONS_ID { get; set; }
}

