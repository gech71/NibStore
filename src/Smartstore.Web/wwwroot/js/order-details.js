function confirmCreatorGroup(button, creatorId) {
        // Show loading state
        button.disabled = true;
        button.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Processing...';
        
        // Here you would typically make an AJAX call to your backend
        // For demonstration, we'll simulate a successful confirmation
        setTimeout(function() {
            button.classList.add('confirmed');
            button.innerHTML = '<i class="fa fa-check"></i> Confirmed';
            
            // You could also fade out the entire group or mark it visually
            // button.closest('.creator-group').style.opacity = '0.8';
            
            // In a real implementation, you would:
            // 1. Make an AJAX call to your backend to confirm
            // 2. Handle success/error responses
            // 3. Update the UI accordingly
            
            // Example AJAX call (commented out):
            /*
            fetch('/Order/ConfirmCreatorItems', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    orderId: @Model.Id,
                    creatorId: creatorId
                })
            })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    button.classList.add('confirmed');
                    button.innerHTML = '<i class="fa fa-check"></i> Confirmed';
                } else {
                    button.disabled = false;
                    button.innerHTML = 'Confirm Order';
                    alert('Confirmation failed: ' + (data.message || 'Unknown error'));
                }
            })
            .catch(error => {
                button.disabled = false;
                button.innerHTML = 'Confirm Order';
                alert('Network error: ' + error.message);
            });
            */
        }, 1000);
    }
