$(document).ready(function () {
    // Initialize modal properly
    var $modal = $('#apply-bulk-discount-modal');
    $modal.modal({
        show: false,
        backdrop: true,
        keyboard: true
    });

    // Checkbox handling
    function updateSelections() {
        var anyChecked = $('.product-checkbox:checked').length > 0;
        $('#apply-discount').prop('disabled', !anyChecked);
        $('#select-all').prop('checked', 
            anyChecked && $('.product-checkbox:checked').length === $('.product-checkbox').length
        );
    }

    $('.product-checkbox, #select-all').on('change', function () {
        if (this.id === 'select-all') {
            $('.product-checkbox').prop('checked', this.checked);
        }
        updateSelections();
    });

    // Apply discount button
    $('#apply-discount').on('click', function (e) {
        e.preventDefault();
        var selectedIds = $('.product-checkbox:checked').map(function () {
            return this.value;
        }).get();

        if (selectedIds.length === 0) {
            Smartstore.showToast('error', '@T("Admin.Promotions.Discounts.NoProductsSelected")');
            return;
        }

        $('#selected-product-ids').val(selectedIds.join(','));
        
        // Force modal to top layer
        $modal.css('z-index', '99999');
        $modal.modal('show');
        
        // Emergency fallback
        setTimeout(function() {
            if ($modal.hasClass('show')) return;
            
            $modal.addClass('show');
            $modal.show();
            $('body').addClass('modal-open');
            $('<div class="modal-backdrop fade show"></div>').appendTo('body');
        }, 100);
    });

    // Form submission
    $('#discount-form').on('submit', function (e) {
        e.preventDefault();
        var $form = $(this);
        var $submitBtn = $form.find('[type="submit"]');
        
        $submitBtn.prop('disabled', true);
        
        $.ajax({
            url: $form.attr('action'),
            type: 'POST',
            data: $form.serialize(),
            success: function(response) {
                if (response.success) {
                    Smartstore.showToast('success', response.message);
                    $modal.modal('hide');
                    
                    // Reset selections
                    $('.product-checkbox, #select-all').prop('checked', false);
                    $('#apply-discount').prop('disabled', true);
                } else {
                    Smartstore.showToast('error', response.message);
                }
            },
            error: function() {
                Smartstore.showToast('error', '@T("Admin.Common.Error")');
            },
            complete: function() {
                $submitBtn.prop('disabled', false);
            }
        });
    });

    // Cleanup on modal close
    $modal.on('hidden.bs.modal', function() {
        $('.modal-backdrop').remove();
    });
});