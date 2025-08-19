/*
* 搜尋特定表
* */
function handleTableSearch(inputSelector, nameKey, targetSelector) {
    const $input = $(inputSelector);
    const value = $input.val();
    const dataType = $input.data('type');

    if (!value) {
        alert(nameKey === 'tableName' ? '請輸入表格名稱' : '請輸入檢視表格名稱');
        return;
    }

    const data = {};
    data['tableName'] = value;
    data['schemaType'] = dataType;

    $.ajax({
        url: '/FormDesigner/QueryFields',
        type: 'GET',
        data: data,
        success: function (partialHtml) {
            $(targetSelector).html(partialHtml); // 指定要更新的區塊
            const masterId = $(targetSelector).find('[data-master-id]').data('master-id');
            if (nameKey === 'tableName') {
                $('#baseTableId').val(masterId);
            } else {
                $('#viewTableId').val(masterId);
            }
        },
        error: function () {
            alert('查詢失敗，請確認表格名稱');
        }
    });
}

// 綁定事件：主表
$('#btnSearchTable').click(function () {
    handleTableSearch('#tableNameInput', 'tableName', '#formFieldList');
});
// 綁定事件：View
$('#btnSearchViewTable').click(function () {
    handleTableSearch('#viewTableNameInput', 'viewTableName', '#formViewFieldList');
});

// 儲存 Form Header
$('#btnSaveFormHeader').click(function () {
    const data = {
        ID: $('#formMasterId').val(),
        FORM_NAME: $('#FORM_NAME').val(),
        TABLE_NAME: $('#tableNameInput').val(),
        VIEW_TABLE_NAME: $('#viewTableNameInput').val(),
        BASE_TABLE_ID: $('#baseTableId').val(),
        VIEW_TABLE_ID: $('#viewTableId').val()
    };

    $.ajax({
        url: '/FormDesigner/SaveFormHeader',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(data),
        success: function (res) {
            if (res && res.id) {
                $('#formMasterId').val(res.id);
            }
            alert('儲存成功');
        },
        error: function (xhr) {
            alert(xhr.responseText || '儲存失敗');
        }
    });
});


/*
* 載入單一欄位詳細資料設定
* */
function loadFieldSetting(tableName, columnName, schemaType) {
    $.ajax({
        url: '/FormDesigner/GetFieldSetting',
        type: 'GET',
        data: { tableName: tableName, columnName: columnName, schemaType: schemaType },
        success: function (html) {
            $('#formFieldSetting').html(html);
            toggleDropdownButton();
            syncEditableRequired();
        },
        error: function () {
            alert('載入欄位設定失敗');
        }
    });
}

function toggleDropdownButton() {
    const val = $('#CONTROL_TYPE').val();
    if (val == '5') {
        $('.setting-dropdown-btn').removeClass('d-none');
    } else {
        $('.setting-dropdown-btn').addClass('d-none');
    }
}

// 當欄位不可編輯時，必填選項應一併取消並禁用
function syncEditableRequired() {
    const $editable = $('#editableCheck');
    const $required = $('#isRequiredCheck');
    if ($editable.length === 0 || $required.length === 0) return;

    if (!$editable.prop('checked')) {
        $required.prop('checked', false).prop('disabled', true);
    } else {
        $required.prop('disabled', false);
    }
}

$(document).on('change', '#CONTROL_TYPE', toggleDropdownButton);
$(document).on('change', '#editableCheck', syncEditableRequired);

/*
* 更新設定
* */
$(document).on('change', '#field-setting-form input, #field-setting-form select', function () {
    $('#field-setting-form').submit(); // 交由 MVC unobtrusive Ajax 處理
});

/*
* 設定單一屬性
* */
$(document).on('click', '.setting-rule-btn', function () {
    const id = $(this).data('id');
    if (!id) return;

    // 先檢查是否存在
    $.get('/FormDesigner/CheckFieldExists', { fieldId: id })
        .done(function (exists) {
            if (!exists) {
                Swal.fire({
                    icon: 'warning',
                    title: '請先儲存欄位設定',
                    text: '要先有控制元件，才能新增限制條件。',
                    confirmButtonText: '確認'
                });
                return;
            }

            // 存在才打開 Modal
            $.post('/FormDesigner/SettingRule', { fieldId: id })
                .done(function (response) {
                    $(".modal-title").text("欄位限制條件設定");
                    $("#settingRuleModalBody").html(response);
                    $("#settingRuleModal").modal({ backdrop: "static" }).modal('show');
                })
                .fail(function (xhr) {
                    alert(xhr.responseText || "載入限制條件失敗！");
                });
        })
        .fail(function () {
            alert("檢查欄位是否存在時發生錯誤");
        });
});


// 編輯按鈕
$(document).on('click', '.edit-rule', function () {
    const $row = $(this).closest('tr');

    // 啟用整列 input/select 欄位
    $row.find('input, select').prop('disabled', false);

    // 立即觸發驗證類型的邏輯（例如 Required 要禁用值）
    $row.find('.validation-type').trigger('change');

    // 顯示「儲存」，隱藏「編輯」
    $row.find('.save-rule').removeClass('d-none');
    $(this).addClass('d-none');
});

$(document).on('change', '.validation-type', function () {
    const $row = $(this).closest('tr');
    const selectedType = $(this).val();
    const $valueInput = $row.find('.validation-value');

    if (selectedType === '0' || selectedType === '4' || selectedType === '5') {
        $valueInput.prop('disabled', true).val('');
    } else {
        $valueInput.prop('disabled', false);
    }
});


// 新增按鈕
$(document).on('click', '.btnAddRule', function () {
    const fieldConfigId = $('#ID').val();

    $.ajax({
        url: '/FormDesigner/CreateEmptyValidationRule',
        type: 'POST',
        data: { fieldConfigId: fieldConfigId },
        success: function (response) {
            $("#validationRuleRow").html(response);
        },
        error: function (xhr) {
            alert('新增失敗：' + xhr.responseText);
        }
    });
});

// 儲存按鈕
$(document).on('click', '.save-rule', function () {
    const $row = $(this).closest('tr');
    const id = $row.data('id');
    const data = {
        ID: id,
        VALIDATION_TYPE: parseInt($row.find('select[name="VALIDATION_TYPE"]').val()),
        VALIDATION_VALUE: $row.find('input[name="VALIDATION_VALUE"]').val(),
        MESSAGE_ZH: $row.find('input[name="MESSAGE_ZH"]').val(),
        MESSAGE_EN: $row.find('input[name="MESSAGE_EN"]').val()
        // VALIDATION_ORDER: parseInt($row.find('input[name="VALIDATION_ORDER"]').val())
    };

    $.ajax({
        url: '/FormDesigner/SaveValidationRule',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(data),
        success: function () {
            $row.find('input, select').prop('disabled', true);
            $row.find('.save-rule').addClass('d-none');
            $row.find('.edit-rule').removeClass('d-none');
            Swal.fire({
                icon: 'success',
                title: '儲存成功',
                showConfirmButton: false,
                timer: 1500
            });
        },
        error: function (xhr) {
            alert('儲存失敗：' + xhr.responseText);
        }
    });
});

// 刪除按鈕
$(document).on('click', '.delete-rule', function () {
    const $row = $(this).closest('tr');
    const id = $row.data('id');
    const fieldConfigId = $('#ID').val();

    Swal.fire({
        title: '確定要刪除嗎？',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: '確認',
        cancelButtonText: '取消'
    }).then((result) => {
        if (!result.isConfirmed) return;

        $.ajax({
            url: '/FormDesigner/DeleteValidationRule',
            type: 'POST',
            data: { id: id, fieldConfigId: fieldConfigId },
            success: function (response) {
                $('#validationRuleRow').html(response);
                Swal.fire({
                    icon: 'success',
                    title: '刪除成功',
                    showConfirmButton: false,
                    timer: 1500
                });
            },
            error: function (xhr) {
                alert('刪除失敗：' + xhr.responseText);
            }
        });
    });
});

$(document).on('click', '.closeModal', function () {
    $(this).closest('.modal').modal('hide');
});

/*
 * 批次設定欄位的可編輯與必填
 */
function toggleAllEditable(btn) {
    const $btn = $(btn);
    const targetEditable = $btn.data('editable');
    const $container = $btn.closest('.field-list-container');
    const formMasterId = $container.data('master-id');
    const tableName = $container.data('table-name');
    const schemaType = $container.data('schema-type');

    $.ajax({
        url: '/FormDesigner/SetAllEditable',
        type: 'POST',
        data: { formMasterId, tableName, isEditable: targetEditable, schemaType },
        success: function (html) {
            $container.replaceWith(html);
        },
        error: function () {
            alert('批次更新可編輯狀態失敗');
        }
    });
}

function toggleAllRequired(btn) {
    const $btn = $(btn);
    const targetRequired = $btn.data('required');
    const $container = $btn.closest('.field-list-container');
    const formMasterId = $container.data('master-id');
    const tableName = $container.data('table-name');
    const schemaType = $container.data('schema-type');

    $.ajax({
        url: '/FormDesigner/SetAllRequired',
        type: 'POST',
        data: { formMasterId, tableName, isRequired: targetRequired, schemaType },
        success: function (html) {
            $container.replaceWith(html);
        },
        error: function () {
            alert('批次更新必填狀態失敗');
        }
    });
}