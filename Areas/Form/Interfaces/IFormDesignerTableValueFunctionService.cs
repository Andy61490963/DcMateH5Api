using ClassLibrary;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

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