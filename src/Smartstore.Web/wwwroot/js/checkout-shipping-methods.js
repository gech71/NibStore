// checkout-shipping-methods.js

// Main function that runs when DOM is loaded
function initializeShippingMethods() {
    // Get store pickup method ID from data attribute
    const scriptElement = document.querySelector('script[src*="checkout-shipping-methods.js"]');
    const storePickupMethodId = scriptElement ? parseInt(scriptElement.getAttribute('data-store-pickup-method-id')) : 0;

    // DOM elements
    const storePickupProducts = document.getElementById('storePickupProducts');
    let map, marker;
    let debounceTimer;
    const addressInput = document.getElementById('addressInput');
    const addressSuggestions = document.getElementById('addressSuggestions');

    // Initialize the page
    initPage();

    function initPage() {
        toggleStorePickupProducts();
        initEventListeners();
        restoreSavedData();
        initLeafletMap();
    }

    function toggleStorePickupProducts() {
        const selectedRadio = document.querySelector('input[name="shippingoption"]:checked');
        const selectedId = selectedRadio ? parseInt(selectedRadio.dataset.shippingmethodid) : null;

        if (selectedId === storePickupMethodId && storePickupProducts) {
            storePickupProducts.style.display = 'block';
        } else if (storePickupProducts) {
            storePickupProducts.style.display = 'none';
        }
    }

    function initEventListeners() {
        // Shipping method radio buttons
        document.querySelectorAll('input[name="shippingoption"]').forEach(radio => {
            radio.addEventListener('change', () => {
                toggleStorePickupProducts();
                toggleByGroundMap(radio);
            });
            if (radio.checked) toggleByGroundMap(radio);
        });

        // Map toggle button
        document.querySelector('.map-toggle-btn')?.addEventListener('click', function() {
            const mapElement = document.getElementById('leafletMap');
            if (mapElement) {
                const isVisible = mapElement.style.display !== 'none';
                mapElement.style.display = isVisible ? 'none' : 'block';
                
                if (!isVisible && map) {
                    setTimeout(() => {
                        map.invalidateSize();
                        const lat = document.getElementById('latitudeField').value;
                        const lng = document.getElementById('longitudeField').value;
                        if (lat && lng) {
                            map.setView([parseFloat(lat), parseFloat(lng)], map.getZoom());
                        }
                    }, 100);
                }
            }
        });

        // Store selection dropdowns
        document.querySelectorAll('.store-select-dropdown').forEach(dropdown => {
            dropdown.addEventListener('change', function() {
                if (this.value) {
                    this.style.display = 'none';
                    const productItem = this.closest('.product-item');
                    const productName = productItem.querySelector('.product-name');
                    if (!productName.innerText.includes('(')) {
                        productName.innerText += ` (${this.value})`;
                    }
                }
            });
        });

        // Address input handling
        if (addressInput) {
            addressInput.addEventListener('input', handleAddressInput);
            addressInput.addEventListener('blur', validateAddressInput);
            addressInput.addEventListener('focus', showAddressSuggestionsIfAvailable);
        }

        // Current location button
        document.querySelector('.current-location-btn')?.addEventListener('click', handleCurrentLocation);
    }

    function restoreSavedData() {
        const savedAddress = localStorage.getItem('byGroundAddress');
        if (savedAddress && addressInput) {
            addressInput.value = savedAddress;
        }
    }

    function toggleByGroundMap(selectedRadio) {
        document.querySelectorAll('.by-ground-extra').forEach(el => el.style.display = 'none');
        if (selectedRadio.dataset.shippingName.toLowerCase() === 'by ground') {
            const container = selectedRadio.closest('[data-index]');
            const extra = container.querySelector('.by-ground-extra');
            if (extra) extra.style.display = 'flex';
        } else {
            document.getElementById('latitudeField').value = '';
            document.getElementById('longitudeField').value = '';
            document.getElementById('addressInput').value = '';
        }
    }

    function initLeafletMap() {
        const mapContainer = document.getElementById('leafletMap');
        if (!mapContainer) return;

        let lat = 9.03, lng = 38.74;
        map = L.map('leafletMap').setView([lat, lng], 13);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        marker = L.marker([lat, lng], { draggable: true }).addTo(map);
        marker.on('dragend', async () => {
            const pos = marker.getLatLng();
            const address = await reverseGeocode(pos.lat, pos.lng);
            updateAddressAndCoordinates(address, pos.lat, pos.lng);
        });

        map.on('click', async (e) => {
            marker.setLatLng(e.latlng);
            const address = await reverseGeocode(e.latlng.lat, e.latlng.lng);
            updateAddressAndCoordinates(address, e.latlng.lat, e.latlng.lng);
        });
    }

    let suppressNextInput = false;

    function handleAddressInput(e) {
        if (suppressNextInput) {
            suppressNextInput = false;
            return;
        }
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            if (e.target.value.length > 3) {
                searchAddress(e.target.value);
            } else {
                hideAddressSuggestions();
            }
        }, 500);
    }

    function searchAddress(query) {
        setLoading(true, addressInput);
        fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}`)
            .then(res => res.json())
            .then(data => {
                if (data.length > 0) {
                    showAddressSuggestions(data);
                } else {
                    hideAddressSuggestions();
                }
            })
            .catch(() => hideAddressSuggestions())
            .finally(() => setLoading(false, addressInput));
    }

    function showAddressSuggestions(addresses) {
        console.log('[showAddressSuggestions] called:', addresses);
        if (!addressSuggestions) {
            return;
        }
        addressSuggestions.innerHTML = '';
        addresses.slice(0, 5).forEach(addr => {
            const item = document.createElement('div');
            item.className = 'dropdown-item';
            item.textContent = addr.display_name;
            item.addEventListener('mousedown', () => {
                suppressNextInput = true;
            });
            item.addEventListener('touchstart', () => {
                suppressNextInput = true;
            });
            item.addEventListener('click', () => {
                const lat = parseFloat(addr.lat);
                const lng = parseFloat(addr.lon);
                map.setView([lat, lng], 15);
                marker.setLatLng([lat, lng]);
                updateAddressAndCoordinates(addr.display_name, lat, lng);
                hideAddressSuggestions();
            });
            addressSuggestions.appendChild(item);
        });
        addressSuggestions.style.display = 'block';
    }

    function showAddressSuggestionsIfAvailable() {
        if (addressSuggestions.innerHTML.trim() !== '') {
            addressSuggestions.style.display = 'block';
        }
    }

    function hideAddressSuggestions() {
        if (addressSuggestions) {
            addressSuggestions.style.display = 'none';
        }
    }

    function validateAddressInput() {
        setTimeout(() => {
            hideAddressSuggestions();
            if (addressInput.value.trim() && !document.getElementById('latitudeField').value) {
                showAddressError('Please select a valid address from the suggestions or use the map');
                return false;
            }
            hideAddressError();
            return true;
        }, 350);
    }

    function handleCurrentLocation() {
        if (navigator.geolocation) {
            setLoading(true, document.querySelector('.current-location-btn'));
            navigator.geolocation.getCurrentPosition(async (pos) => {
                const { latitude, longitude } = pos.coords;
                map.setView([latitude, longitude], 15);
                marker.setLatLng([latitude, longitude]);
                const address = await reverseGeocode(latitude, longitude);
                updateAddressAndCoordinates(address, latitude, longitude);
                setLoading(false, document.querySelector('.current-location-btn'));
            }, () => {
                alert('Could not get your location. Please make sure location services are enabled.');
                setLoading(false, document.querySelector('.current-location-btn'));
            });
        } else {
            alert('Geolocation is not supported by your browser');
        }
    }

    function updateAddressAndCoordinates(address, lat, lng) {
        if (addressInput) {
            addressInput.value = address;
            localStorage.setItem('byGroundAddress', address);
            const inputEvent = new Event('input', { bubbles: true });
            addressInput.dispatchEvent(inputEvent);
        }
        document.getElementById('mapCoordinates').value = `${lat},${lng}`;
        document.getElementById('latitudeField').value = lat;
        document.getElementById('longitudeField').value = lng;
        
        const googleMapLink = document.querySelector('.google-map-link');
        if (googleMapLink) {
            googleMapLink.href = `https://maps.google.com/?q=${lat},${lng}`;
            googleMapLink.style.display = 'inline';
        }
    }

    async function reverseGeocode(lat, lng) {
        try {
            const response = await fetch(`https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}`);
            const data = await response.json();
            return data.display_name || "Address not found";
        } catch (error) {
            console.error('Reverse geocoding error:', error);
            return "Address not found";
        }
    }

    function setLoading(state, element) {
        if (!element) return;

        if (element.classList.contains('current-location-btn')) {
            element.disabled = state;
            if (state) {
                element.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Locating...';
            } else {
                element.innerHTML = '<i class="fa fa-location-dot"></i>';
            }
        }
    }

    function setupFormValidation() {
        const form = addressInput?.closest('form');
        if (form) {
            form.addEventListener('submit', function(e) {
                const selectedRadio = document.querySelector('input[name="shippingoption"]:checked');
                if (selectedRadio && selectedRadio.dataset.shippingName.toLowerCase() === 'by ground') {
                    if (!document.getElementById('latitudeField').value) {
                        showAddressError('Please select a valid address from the suggestions or use the map');
                        e.preventDefault();
                        return false;
                    }
                }
                hideAddressError();
            });
        }

        if (addressInput) {
            addressInput.addEventListener('input', hideAddressError);
        }
    }

    function showAddressError(message) {
        const errorDiv = document.getElementById('addressError');
        if (errorDiv) {
            errorDiv.textContent = message;
            errorDiv.style.display = 'block';
        }
        if (addressInput) {
            addressInput.focus();
        }
    }

    function hideAddressError() {
        const errorDiv = document.getElementById('addressError');
        if (errorDiv) {
            errorDiv.textContent = '';
            errorDiv.style.display = 'none';
        }
    }

    // Initialize form validation
    setupFormValidation();

    // Store selection change handler
    $(document).on('change', '.store-select', function () {
        const $select = $(this);
        const storeName = $select.val();
        const itemId = $select.data('item-id');
        const storeId = $select.find('option:selected').data('store-id');
        const $feedback = $select.siblings('.store-feedback');

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
                success: function (response) {
                    if (response.success) {
                        $feedback.text('Store updated successfully').addClass('text-success').show();
                    } else {
                        $feedback.text(response.message || 'Failed to update store').addClass('text-danger').show();
                    }
                },
                error: function () {
                    $feedback.text('Server error occurred.').addClass('text-danger').show();
                }
            });
        }
    });

    // Order totals update functionality
    function initializeOrderTotalsUpdates() {
        $('#addressInput').on('input', function () {
            $('#liveByGroundAddress').text($(this).val());
            $('#latitudeField').val('');
            $('#longitudeField').val('');
        });

        function updateOrderTotalsUI() {
            const $selectedRadio = $('input[name="shippingoption"]:checked');
            if (!$selectedRadio.length) return;

            let shippingName = $selectedRadio.data('shipping-name');
            const originalShippingName = shippingName;

            if (shippingName && shippingName.trim().toLowerCase() === "by ground") {
                shippingName = "Delivery";
            }
            const $shippingLabel = $('.cart-summary-shipping .cart-summary-label span.font-weight-medium');
            if (shippingName) {
                $shippingLabel.text(shippingName).show();
            } else {
                $shippingLabel.hide();
            }

            if (originalShippingName && originalShippingName.trim().toLowerCase() === "by ground") {
                $('.by-ground-address-display').show();
            } else {
                $('.by-ground-address-display').hide();
            }
        }

        updateOrderTotalsUI();
        $('input[name="shippingoption"]').on('change', updateOrderTotalsUI);
    }

    // Initialize order totals updates
    initializeOrderTotalsUpdates();
}

// Wait for DOM to be fully loaded and jQuery to be available
document.addEventListener('DOMContentLoaded', initializeShippingMethods);