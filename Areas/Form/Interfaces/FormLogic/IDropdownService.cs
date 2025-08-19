using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.ViewModels;

namespace DynamicForm.Areas.Form.Interfaces.FormLogic;

public interface IDropdownService
{
    List<FormDataRow> ToFormDataRows(
        IEnumerable<IDictionary<string, object?>> rawRows,
        string pkColumn,
        out List<object> rowIds);

    List<DropdownAnswerDto> GetAnswers(IEnumerable<object> rowIds);
    
    Dictionary<Guid, string> GetOptionTextMap(IEnumerable<DropdownAnswerDto> answers);

    void ReplaceDropdownIdsWithTexts(
        List<FormDataRow> rows,
        List<FormFieldConfigDto> fieldConfigs,
        List<DropdownAnswerDto> answers,
        Dictionary<Guid, string> optionTextMap);
}