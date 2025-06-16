(function ($) {
    // 1. Wait for both document ready AND grid initialization
    $(document).ready(function() {
        // 2. Use Smartstore's event system to ensure grid is ready
        Smartstore.global.event.publish('grid.initialized', function (grid) {
            if (grid.element.is("#products-grid")) {
                initGridHandlers(grid);
            }
        });

        // 3. Fallback: Check periodically if grid exists
        const checkGridInterval = setInterval(() => {
            const grid = $("#products-grid").data("kendoGrid");
            if (grid) {
                clearInterval(checkGridInterval);
                initGridHandlers(grid);
            }
        }, 200);
    });

    function initGridHandlers(grid) {
        console.log("Grid initialized, setting up handlers...");

        function updateApplyButtonState() {
            const selected = grid.selectedKeyNames();
            $("#btn-apply-discount").prop("disabled", selected.length === 0);
        }

        // Bind to grid events
        grid.bind("change", updateApplyButtonState);
        grid.bind("dataBound", updateApplyButtonState);
        
        // Initial update
        updateApplyButtonState();
    }

    // 4. Make functions globally available (for button clicks)
    window.openDiscountModal = function() {
        const grid = $("#products-grid").data("kendoGrid");
        if (!grid) {
            console.error("Grid not found in openDiscountModal!");
            return;
        }
        
        const selected = grid.selectedKeyNames();
        if (!selected.length) {
            alert("Please select at least one product.");
            return;
        }
        $('#apply-discount-modal').modal('show');
    };

    window.applySelectedDiscount = function() {
        const grid = $("#products-grid").data("kendoGrid");
        if (!grid) {
            console.error("Grid not found in applySelectedDiscount!");
            return;
        }

        const selected = grid.selectedKeyNames();
        const discountId = $("#discountSelect").val();

        if (!selected.length) {
            alert("Please select at least one product.");
            return;
        }

        const $button = $('#apply-discount-modal .btn-primary');
        $button.prop('disabled', true).text('Applying...');

        $.ajax({
            url: '@Url.Action("ApplyDiscountToProducts", "Discount")',
            type: 'POST',
            data: {
                productIds: selected,
                discountId: discountId
            },
            success: function(response) {
                if (response.success) {
                    alert("Discount applied successfully!");
                    grid.dataSource.read(); // Refresh grid
                } else {
                    alert(response.message || "Error applying discount.");
                }
            },
            error: function() {
                alert("AJAX request failed.");
            },
            complete: function() {
                $button.prop('disabled', false).text('Apply Discount');
                $('#apply-discount-modal').modal('hide');
            }
        });
    };
})(jQuery);