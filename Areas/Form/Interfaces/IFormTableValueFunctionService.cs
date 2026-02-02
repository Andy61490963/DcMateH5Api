using ClassLibrary;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormTableValueFunctionService
{
    Task<IEnumerable<TableValueFunctionConfigViewModel>> GetFormMasters(CancellationToken ct = default);

    Task<List<FormTvfListDataViewModel>> GetTvfFormList(FormFunctionType funcType, FormTvfSearchRequest? request = null, CancellationToken ct = default);
}