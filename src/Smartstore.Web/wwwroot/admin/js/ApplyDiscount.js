$(function () {
    var isSubmitting = false;

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

        var grid = $("#products-grid").data("datagrid");
        var selectedRows = grid.getSelectedRows();

        if (!selectedRows.length) {
            const noProductsMsg = $('#no-products-msg').data('msg') || "No products selected.";
            Smartstore.showToast('error', noProductsMsg);
            return;
        }

        var selectedIds = selectedRows.map(x => x.Id).join(',');
        $('#selected-product-ids').val(selectedIds);

        $('#discount-popup-overlay, #discount-popup-dialog').show();
    });

    $('#discount-popup-overlay, #close-discount-popup').on('click', function () {
        $('#discount-popup-overlay, #discount-popup-dialog').hide();
    });

    function loadAvailableDiscounts() {
        $.get('@Url.Action("GetAvailableDiscounts")', function (data) {
            var select = $('#DiscountId').empty().append('<option value="">-- @T("Admin.Common.Select") --</option>');
            $.each(data, function (i, item) {
                select.append(`<option value="${item.id}">${item.name}</option>`);
            });
        });
    }

    $('#discount-form').on('submit', function (e) {
        e.preventDefault();

        if (isSubmitting) return;
        isSubmitting = true;

        var $form = $(this);
        var url = $form.attr('action');
        var data = $form.serialize();

        $.ajax({
            url: url,
            type: 'POST',
            data: data,
            success: function (response) {
                if (response.success) {
                    Smartstore.showToast('success', response.message);
                    $('#discount-popup-overlay, #discount-popup-dialog').hide();
                    window.location.reload();
                } else {
                    Smartstore.showToast('error', response.message);
                }
            },
            error: function () {
                const errorMsg = $('#error-msg').data('msg') || "An error occurred.";
                Smartstore.showToast('error', errorMsg);
            },
            complete: function () {
                isSubmitting = false;
            }
        });
    });

    // Optional: Close popup on ESC key
    $(document).on('keydown', function (e) {
        if (e.key === "Escape") {
            $('#discount-popup-overlay, #discount-popup-dialog').hide();
        }
    });

    // Load available discounts on page load
    loadAvailableDiscounts();

    updateApplyButtonState();
});