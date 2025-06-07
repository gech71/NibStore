$(function () {
    function updateApplyButtonState() {
        var anyChecked = $('.product-checkbox:checked').length > 0;
        $('#apply-discount-btn').prop('disabled', !anyChecked);
    }

    $('#select-all').on('change', function () {
        $('.product-checkbox').prop('checked', $(this).is(':checked'));
        updateApplyButtonState();
    });

    $('.product-checkbox').on('change', function () {
        var allChecked = $('.product-checkbox:checked').length === $('.product-checkbox').length;
        $('#select-all').prop('checked', allChecked);
        updateApplyButtonState();
    });

    $('#apply-discount-btn').on('click', function (e) {
        e.preventDefault();

        var selectedIds = $('.product-checkbox:checked')
            .map(function () { return $(this).val(); }).get();

        if (!selectedIds.length) {
            const noProductsMsg = $('#no-products-msg').data('msg') || "No products selected.";
            Smartstore.showToast('error', noProductsMsg);
            return;
        }

        $('#selected-product-ids').val(selectedIds.join(','));
        $('#discount-popup-overlay, #discount-popup-dialog').show();
    });

    $('#discount-popup-overlay, #close-discount-popup').on('click', function () {
        $('#discount-popup-overlay, #discount-popup-dialog').hide();
    });

    $('#discount-form').on('submit', function (e) {

        var $form = $(this);
        var url = $form.attr('action');
        var data = $form.serialize();

        $.ajax({
            url: url,
            type: 'POST',
            data: data,
            success: function (response) {
                if (response.success) {
                    displayNotification(response.message, 'success');
                    $('#discount-popup-overlay, #discount-popup-dialog').hide();
                    window.location.reload();
                } else {
                    displayNotification(response.message, 'error');
                }
            },
            error: function () {
                const errorMsg = $('#error-msg').data('msg') || "An error occurred.";
                displayNotification(errorMsg, 'error');
            }
        });
    });

    // Optional: Close popup on ESC key
    $(document).on('keydown', function (e) {
        if (e.key === "Escape") {
            $('#discount-popup-overlay, #discount-popup-dialog').hide();
        }
    });

    updateApplyButtonState();
});
