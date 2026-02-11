using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Abstractions.Form.Form;

public interface IFormDesignerTableValueFunctionService
{
    Task<FormFieldListViewModel?> EnsureFieldsSaved( string tvfName, Guid? formMasterId, TableSchemaQueryType schemaType, CancellationToken ct );

    Task<FormFieldListViewModel> GetFieldsByTableNameInTxAsync(
        string tvpName,
        Guid? formMasterId,
        TableSchemaQueryType schemaType,
        CancellationToken ct);
    
    Task<Guid> SaveTableValueFunctionFormHeader(FormHeaderTableValueFunctionViewModel model, CancellationToken ct);
}