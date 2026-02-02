using ClassLibrary;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormTableValueFunctionService
{
    IEnumerable<TableValueFunctionConfigViewModel> GetFormMasters(CancellationToken ct = default);
    
    List<FormTvfListDataViewModel> GetTvfFormList(FormFunctionType funcType, FormTvfSearchRequest? request = null);
}