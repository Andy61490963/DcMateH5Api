using DcMateH5Api.Areas.Form.Models.Excel;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces.Excel;

public interface IExcelExportService
{
    /// <summary>
    /// 將 FormListDataViewModel 清單匯出成 Excel（xlsx）
    /// </summary>
    /// <param name="rows">列表資料</param>
    /// <returns>匯出檔案內容</returns>
    ExportFileResult ExportFormList(IReadOnlyList<FormListDataViewModel> rows, bool includePk = false);
}