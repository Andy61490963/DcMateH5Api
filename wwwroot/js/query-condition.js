/**
 * 依 QueryConditionType 建立對應的查詢元件。
 * Dropdown 類型會向後端 API 取得選項，避免前端硬編碼。
 */
function renderQueryCondition(field) {
    let $el;
    switch (field.queryConditionType) {
        case 'Dropdown':
            $el = $('<select/>').attr('name', field.column);
            fetch(`/api/Form/${field.fieldConfigId}/query-options`)
                .then(res => res.json())
                .then(opts => {
                    opts.forEach(o => $el.append(`<option value="${o.value}">${o.label}</option>`));
                });
            break;
        case 'Number':
            $el = $('<input/>').attr({ type: 'number', name: field.column });
            break;
        case 'Date':
            $el = $('<input/>').attr({ type: 'date', name: field.column });
            break;
        default:
            $el = $('<input/>').attr({ type: 'text', name: field.column });
            break;
    }
    return $el;
}
