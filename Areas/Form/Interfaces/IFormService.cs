using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.ViewModels;
using System.Collections.Generic;

namespace DynamicForm.Areas.Form.Interfaces;

public interface IFormService
{
    /// <summary>
    /// 取得所有表單的資料列表。
    /// </summary>
    /// <param name="conditions">查詢條件集合。</param>
    /// <returns>每個表單對應的欄位與資料列集合。</returns>
    List<FormListDataViewModel> GetFormList(IEnumerable<FormQueryCondition>? conditions = null);
    
    /// <summary>
    /// 取得 單一
    /// </summary>
    /// <param name="id"></param>
    /// <param name="pk"></param>
    /// <returns></returns>
    FormSubmissionViewModel GetFormSubmission(Guid id, string? pk = null);

    /// <summary>
    /// 儲存或更新表單資料
    /// </summary>
    void SubmitForm(FormSubmissionInputModel input);
}