function buyNow(url) {
        if (!window.isRegistered) {
            localStorage.setItem("pendingBuyNowUrl", url);
            window.location.href = window.loginUrl;
            return;
        }
        executeBuyNow(url);
    }

    function executeBuyNow(url) {
        fetch('/ShoppingCart/ClearCart', {
            method: 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
        .then(() => {
            return fetch(url, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
        })
        .then(response => response.json())
        .then(data => {
            if (data.redirect) {
                window.location.href = data.redirect;
            } else if (data.success) {
                window.location.href = '/checkout';
            } else {
                alert(data.message || 'Could not add product to cart.');
            }
        })
        .catch(() => alert('An error occurred.'));
    }

    // Auto-check after login/register
    document.addEventListener("DOMContentLoaded", function () {
        const pendingUrl = localStorage.getItem("pendingBuyNowUrl");
        if (pendingUrl && window.isRegistered) {
            localStorage.removeItem("pendingBuyNowUrl");
            executeBuyNow(pendingUrl);
        }
    });



 function buyNowWithForm(url, formSelector) {
    if (!window.isRegistered) {
        localStorage.setItem("pendingBuyNowUrl", url);
        localStorage.setItem("pendingFormSelector", formSelector);
        window.location.href = window.loginUrl;
        return;
    }

    const form = document.querySelector(formSelector);
    if (!form) {
        alert("Form not found.");
        return;
    }

    fetch('/ShoppingCart/ClearCart', {
        method: 'POST',
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
    .then(response => {
        if (!response.ok) throw new Error("Failed to clear cart.");
        return fetch(url, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
    })
    .then(res => res.json())
    .then(data => {
        if (data.redirect) {
            window.location.href = data.redirect;
        } else if (data.success) {
            window.location.href = '/checkout';
        } else {
            alert(data.message || "Could not add product to cart.");
        }
    })
    .catch(error => {
        console.error(error);
        alert("An error occurred: " + error.message);
    });
}

document.addEventListener("DOMContentLoaded", function () {
    const pendingUrl = localStorage.getItem("pendingBuyNowUrl");
    const formSelector = localStorage.getItem("pendingFormSelector");

    if (pendingUrl && window.isRegistered) {
        localStorage.removeItem("pendingBuyNowUrl");
        localStorage.removeItem("pendingFormSelector");
        buyNowWithForm(pendingUrl, formSelector);
    }
});

