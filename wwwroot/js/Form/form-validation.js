document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('dynamic-form');
    if (!form) return;

    form.addEventListener('submit', function (e) {
        e.preventDefault(); // 阻止表單送出（改用 JS 控制驗證流程）

        let isValid = true;

        // 1. 清空所有錯誤訊息與紅框
        document.querySelectorAll('.validation-msg').forEach(span => span.innerText = '');
        document.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));

        // 2. 驗證所有欄位
        const fields = form.querySelectorAll('input, textarea, select');
        fields.forEach(field => {
            if (field.disabled || !field.name) return;

            const bindName = field.getAttribute('name');
            const errorSpan = document.querySelector(`.validation-msg[data-for="${bindName}"]`);
            let errorMessage = '';

            // 2-1: HTML5 驗證（required, pattern, maxlength）
            if (!field.checkValidity()) {
                isValid = false;

                if (field.validity.valueMissing) {
                    errorMessage = '此欄位為必填';
                } else if (field.validity.patternMismatch) {
                    errorMessage = field.getAttribute('data-val-regex') || '格式不正確';
                } else if (field.validity.tooLong) {
                    const maxMsg = field.getAttribute('data-maxlength-message');
                    errorMessage = maxMsg || '超出長度限制';
                } else {
                    errorMessage = '請修正此欄位';
                }

                if (errorSpan) errorSpan.innerText = errorMessage;
                field.classList.add('is-invalid');
            }
        });

        // 3. 如果驗證未通過，捲動至第一個錯誤欄位
        if (!isValid) {
            const firstErrorField = form.querySelector('.is-invalid');
            if (firstErrorField) {
                firstErrorField.scrollIntoView({ behavior: 'smooth', block: 'center' });

                // 延遲 focus 避免 scroll 衝突
                setTimeout(() => {
                    firstErrorField.focus();
                }, 300);
            }

            return; // 不送出表單
        }

        // 4. 驗證通過，送出表單
        form.submit();
    });
});
