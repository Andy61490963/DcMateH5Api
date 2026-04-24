using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Abstractions.Form.Form;

public interface IFormViewDesignerService
{
    Task<List<FormFieldMasterDto>> GetFormMasters(string? q, CancellationToken ct = default);

    Task<FormDesignerIndexViewModel> GetDesigner(Guid id, CancellationToken ct = default);

    List<string> SearchViews(string? viewName);

    Task<FormFieldListViewModel?> EnsureFieldsSaved(string viewName, Guid? formMasterId, CancellationToken ct = default);

    Task<FormFieldViewModel?> GetFieldById(Guid fieldId);

    Task UpsertFieldAsync(FormFieldViewModel model, CancellationToken ct = default);

    Task<FormFieldListViewModel> GetFieldsByViewName(string viewName, Guid formMasterId, CancellationToken ct = default);

    Task MoveFieldAsync(MoveFormFieldRequest req, CancellationToken ct = default);

    Task UpdateFormName(UpdateFormNameViewModel model, CancellationToken ct = default);

    Task Delete(Guid id, CancellationToken ct = default);

    Task<Guid> SaveViewFormHeader(FormViewHeaderViewModel model, CancellationToken ct = default);
}
