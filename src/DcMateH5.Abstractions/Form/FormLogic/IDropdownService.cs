using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Abstractions.Form.FormLogic;

public interface IDropdownService
{
    List<FormDataRow> ToFormDataRows(
        IEnumerable<IDictionary<string, object?>> rawRows,
        string pkColumn,
        out List<object> rowIds);

    Task<Dictionary<Guid, string>> GetOptionTextMapAsync(
        IEnumerable<DropdownAnswerDto> answers,
        CancellationToken ct = default);

    // 相容舊呼叫端（逐步淘汰）
    Dictionary<Guid, string> GetOptionTextMap(IEnumerable<DropdownAnswerDto> answers);

    void ReplaceDropdownIdsWithTexts(
        List<FormDataRow> rows,
        List<FormFieldConfigDto> fieldConfigs,
        List<DropdownAnswerDto> answers,
        Dictionary<Guid, string> optionTextMap);
}
