$(function () {
    // Track checkbox selection
    function updateApplyButtonState() {
        var anyChecked = $('.product-checkbox:checked').length > 0;
        $('#apply-discount-btn').prop('disabled', !anyChecked);
    }

    // Select All Checkbox
    $('#select-all').on('change', function () {
        $('.product-checkbox').prop('checked', $(this).is(':checked'));
        updateApplyButtonState();
    });

    // Individual Checkbox Change
    $('.product-checkbox').on('change', function () {
        var allChecked = $('.product-checkbox:checked').length === $('.product-checkbox').length;
        $('#select-all').prop('checked', allChecked);
        updateApplyButtonState();
    });

    // Show Popup When Clicking Apply Discount
    $('#apply-discount-btn').on('click', function (e) {
        e.preventDefault();

        var selectedIds = $('.product-checkbox:checked')
            .map(function () { return $(this).val(); }).get();

        if (!selectedIds.length) {
            Smartstore.showToast('error', '@T("Admin.Promotions.Discounts.NoProductsSelected")');
            return;
        }

        $('#selected-product-ids').val(selectedIds.join(','));
        $('#discount-popup-overlay, #discount-popup-dialog').show();
    });

    // Close Popup by Clicking Outside or Pressing Close Button
    $('#discount-popup-overlay, #close-discount-popup').on('click', function () {
        $('#discount-popup-overlay, #discount-popup-dialog').hide();
    });

    // Handle Form Submission
    $('#discount-form').on('submit', function (e) {
        e.preventDefault();

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
                displayNotification('@T("Admin.Common.Error")', 'error');
            }
        });
    });

    updateApplyButtonState();
});