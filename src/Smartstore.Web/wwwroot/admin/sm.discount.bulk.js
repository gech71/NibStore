$(function() {
    // Select all checkbox
    $('#select-all').on('change', function() {
        $('.product-checkbox').prop('checked', $(this).is(':checked'));
        updateApplyButtonState();
    });
    
    // Individual checkbox change
    $('.product-checkbox').on('change', function() {
        if (!$(this).is(':checked')) {
            $('#select-all').prop('checked', false);
        }
        updateApplyButtonState();
    });
    
    // Apply discount button click
    $('#apply-discount').on('click', function() {
        var selectedIds = $('.product-checkbox:checked').map(function() {
            return $(this).val();
        }).get();
        
        if (selectedIds.length > 0) {
            $('#selected-product-ids').val(selectedIds.join(','));
            $('#apply-discount-modal').modal('show');
        }
    });
    
    // Update apply button state based on selection
    function updateApplyButtonState() {
        var anyChecked = $('.product-checkbox:checked').length > 0;
        $('#apply-discount').prop('disabled', !anyChecked);
    }
    
    // Discount form submission
    $('#discount-form').on('submit', function(e) {
        e.preventDefault();
        
        $.ajax({
            url: $(this).attr('action'),
            type: 'POST',
            data: $(this).serialize(),
            success: function(response) {
                if (response.success) {
                    $('#apply-discount-modal').modal('hide');
                    displayNotification(response.message, 'success');
                    
                    // Refresh discounts table
                    loadAppliedDiscounts();
                    
                    // Clear selection
                    $('.product-checkbox').prop('checked', false);
                    $('#select-all').prop('checked', false);
                    updateApplyButtonState();
                } else {
                    displayNotification(response.message, 'error');
                }
            },
            error: function() {
                displayNotification('An error occurred while applying the discount.', 'error');
            }
        });
    });
    
    // Load applied discounts
    function loadAppliedDiscounts() {
        $.get('@Url.Action("GetAppliedDiscounts", "Discount")', function(data) {
            $('#discounts-table-container').html(data);
        });
    }
    
    // Delete discount
    $(document).on('click', '.delete-discount', function(e) {
        e.preventDefault();
        var id = $(this).data('id');
        
        confirm2('@T("Admin.Common.AreYouSure")', function() {
            $.post('@Url.Action("Delete", "Discount")', { id: id }, function(response) {
                if (response.success) {
                    displayNotification(response.message, 'success');
                    loadAppliedDiscounts();
                } else {
                    displayNotification(response.message, 'error');
                }
            });
        });
    });
    
    // Initialize
    updateApplyButtonState();
    loadAppliedDiscounts();
});