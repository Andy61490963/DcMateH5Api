using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Abstractions.Form.Form;

public interface IFormViewService
{
    Task<IEnumerable<ViewFormConfigViewModel>> GetFormMasters(CancellationToken ct = default);

    Task<List<FormListResponseViewModel>> GetForms(FormSearchRequest request, CancellationToken ct = default);

    Task<FormSubmissionViewModel> GetForm(Guid formId, string? pk = null, CancellationToken ct = default);
}
