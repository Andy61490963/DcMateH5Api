using ClassLibrary;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormDesignerTableValueFunctionService
{
    Task<FormFieldListViewModel?> EnsureFieldsSaved( string tableName, Guid? formMasterId, TableSchemaQueryType schemaType, CancellationToken ct );

    Task<Guid> SaveTableValueFunctionFormHeader(FormHeaderTableValueFunctionViewModel model, CancellationToken ct);
}