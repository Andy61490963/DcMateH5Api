using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Abstractions.Form.Form;

public interface IFormTableValueFunctionService
{
    Task<IEnumerable<TableValueFunctionConfigViewModel>> GetFormMasters(CancellationToken ct = default);

    Task<List<FormTvfListDataViewModel>> GetTvfFormList(FormFunctionType funcType, FormTvfSearchRequest? request = null, CancellationToken ct = default);
}