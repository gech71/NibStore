// storePickupPersistence.js
document.addEventListener('DOMContentLoaded', () => {
    //console.log("Store Pickup Persistence initialized");
    const STORE_SELECTIONS_KEY = 'storePickupSelections';
    
    // Initialize localStorage if empty
    if (!localStorage.getItem(STORE_SELECTIONS_KEY)) {
        localStorage.setItem(STORE_SELECTIONS_KEY, '{}');
    }
    
    // Save store selections to localStorage
    function saveStoreSelections() {
        const selections = {};
        document.querySelectorAll('.store-select').forEach(select => {
            const itemId = select.dataset.itemId;
            const selectedOption = select.options[select.selectedIndex];
            if (selectedOption && selectedOption.value) {
                selections[itemId] = {
                    storeId: selectedOption.dataset.storeId,
                    storeName: selectedOption.text
                };
            }
        });
        localStorage.setItem(STORE_SELECTIONS_KEY, JSON.stringify(selections));
    }
    
    // Restore store selections from localStorage
    function restoreStoreSelections() {
        try {
            const savedSelections = JSON.parse(localStorage.getItem(STORE_SELECTIONS_KEY) || '{}');
            document.querySelectorAll('.store-select').forEach(select => {
                const itemId = select.dataset.itemId;
                const selection = savedSelections[itemId];
                if (selection) {
                    const optionToSelect = Array.from(select.options).find(
                        opt => opt.dataset.storeId === selection.storeId
                    );
                    if (optionToSelect) {
                        optionToSelect.selected = true;
                        updateProductDisplay(itemId, selection.storeName);
                    }
                }
            });
        } catch (e) {
            console.error("Failed to restore store selections:", e);
            // Clear corrupted data
            localStorage.setItem(STORE_SELECTIONS_KEY, '{}');
        }
    }
    
    // Update product display with store name
    function updateProductDisplay(itemId, storeName) {
        const productItem = document.querySelector(`.product-item[data-item-id="${itemId}"]`);
        if (!productItem) return;
        const productName = productItem.querySelector('.product-name');
        if (!productName) return;
        
        if (!productName.dataset.originalName) {
            productName.dataset.originalName = productName.textContent;
        }
        productName.textContent = `${productName.dataset.originalName} (${storeName})`;
    }
    
    // Remove store selection when item is removed
    function removeStoreSelection(itemId) {
        try {
            const selections = JSON.parse(localStorage.getItem(STORE_SELECTIONS_KEY) || '{}');
            delete selections[itemId];
            localStorage.setItem(STORE_SELECTIONS_KEY, JSON.stringify(selections));
        } catch (e) {
            console.error("Failed to remove store selection:", e);
        }
    }

    // Initialize store selections
    restoreStoreSelections();

    // Modify existing store select change handler
    $(document).off('change', '.store-select').on('change', '.store-select', function() {
        var $select = $(this);
        var storeName = $select.val();
        var itemId = $select.data('item-id');
        var storeId = $select.find('option:selected').data('store-id');
        var $feedback = $select.siblings('.store-feedback');

        $feedback.text('').removeClass('text-success text-danger');

        if (storeName && storeId) {
            $.ajax({
                url: '/ShoppingCart/UpdateCartItemStore',
                method: 'POST',
                data: {
                    itemId: itemId,
                    storeName: storeName,
                    storeId: storeId
                },
                success: function(response) {
                    if (response.success) {
                        $feedback.text('Store updated successfully').addClass('text-success').show();
                        // Save to localStorage
                        saveStoreSelections();
                        // Update UI
                        updateProductDisplay(itemId, storeName);
                    } else {
                        $feedback.text(response.message || 'Failed to update store').addClass('text-danger').show();
                    }
                },
                error: function() {
                    $feedback.text('Server error occurred.').addClass('text-danger').show();
                }
            });
        }
    });

    // Handle item removal
    $(document).on('click', '.remove-item-x', function() {
        const $container = $(this).closest('.no-store-no-stock');
        const itemId = $container.data('item-id');
        removeStoreSelection(itemId);
    });

    // Enhance toggleStorePickupProducts to restore selections when shown
    const originalToggleStorePickupProducts = window.toggleStorePickupProducts;
    window.toggleStorePickupProducts = function() {
        originalToggleStorePickupProducts();
        if (document.getElementById('storePickupProducts').style.display === 'block') {
            restoreStoreSelections();
        }
    };
});