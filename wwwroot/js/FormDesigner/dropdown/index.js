function toggleDropdownMode() {
    const mode = $('input[name="mode"]:checked').val();
    if (mode === 'sql') {
        $('#dropdownSql').prop('disabled', false);
        $('#optionList').find('input').prop('disabled', true);
        $('#addOption').prop('disabled', true);
    } else {
        $('#dropdownSql').prop('disabled', true);
        $('#optionList').find('input').prop('disabled', false);
        $('#addOption').prop('disabled', false);
    }
}

$(document).on('click', '.setting-dropdown-btn', function () {
    const id = $(this).data('id');
    if (!id) return;

    $.post('/FormDesigner/DropdownSetting', { fieldId: id })
        .done(function (html) {
            $(".modal-title").text("下拉選單設定");
            $("#settingDropdownModalBody").html(html);
            $("#settingDropdownModal").modal({ backdrop: "static" }).modal('show');
            toggleDropdownMode();
        });
});

$(document).on('click', '.deleteOption', function () {
    const $li = $(this).closest('li');
    const optionId = $li.data('option-id');
    const dropdownId = $li.data('dropdown-id');

    if (!optionId || !dropdownId) {
        alert('無效的選項或下拉選單 ID');
        return;
    }

    $.post('/FormDesigner/DeleteOption', { optionId, dropdownId })
        .done(function (html) {
            $('#optionList').html(html); 
        })
        .fail(() => alert('刪除失敗'));
});


$(document).on('blur', '#dropdownSql', function () {
    const fieldId = $('#dropdownModal').data('field-id');
    const sql = $(this).val();

    if (!fieldId || !sql.trim()) return;

    $.post('/FormDesigner/SaveDropdownSql', { fieldId: fieldId, sql: sql })
        .done(() => {
            console.log('Dropdown SQL 已儲存（on blur）');
            // 可選：顯示提示訊息
        })
        .fail(() => {
            console.warn('Dropdown SQL 儲存失敗');
        });
});

$(document).on('click', '#addOption', () => {
    const dropdownId = $('#dropdownModal').data('dropdown-id');
    $.post('/FormDesigner/NewDropdownOption', { dropdownId })
        .done(html => $('#optionList').html(html));
});

let timer;
$(document).on('input', '.option-text, .option-value', function () {
    clearTimeout(timer);
    const $input      = $(this);
    const $li         = $input.closest('li');
    const optionId    = $li.data('option-id');
    const dropdownId  = $li.data('dropdown-id');

    timer = setTimeout(() => {
        const optionText  = $li.find('.option-text').val().trim();
        const optionValue = $li.find('.option-value').val().trim();

        if (!optionText) return;

        $.post('/FormDesigner/SaveDropdownOption', {
            id: optionId,
            dropdownId,
            optionText,
            optionValue
        }).fail(() => {
            console.warn(`✗ ${optionId} 儲存失敗`);
        });
    }, 300);
});

$(document).on('change', 'input[name="mode"]', function () {
    const isSql      = $('#modeSql').is(':checked');
    const $modal     = $('#dropdownModal');
    const dropdownId = $modal.data('dropdown-id');

    // UI 立即切換
    toggleDropdownMode();
    
    $.post('/FormDesigner/SetDropdownMode',
        { dropdownId, isUseSql: isSql })
        .fail(() => alert('切換模式失敗，請重試'));
});

$(document).on('click', '#validateSqlBtn', function () {
    const $modal = $('#dropdownModal');
    const sql    = $('#dropdownSql').val()?.trim();
    const $resultContainer = $('#validateSqlResultContainer');

    $.post('/FormDesigner/ValidateDropdownSql', { sql })
        .done(function (partialHtml) {
            $resultContainer.html(partialHtml);
        })
        .fail(function () {
        });
});

$(document).on('click', '#importOptionsBtn', function () {
    const dropdownId = $('#dropdownModal').data('dropdown-id');
    const sql = $('#dropdownSql').val()?.trim();
    if (!dropdownId || !sql) return;

    const tableMatch = sql.match(/from\s+([a-zA-Z0-9_]+)/i);
    const optionTable = tableMatch ? tableMatch[1] : '';

    $.post('/FormDesigner/ImportOptions',
        { dropdownId, sql, optionTable })
        .done(html => $('#optionList').html(html))
        .fail(xhr => alert(xhr.responseText || '匯入失敗'));
});
