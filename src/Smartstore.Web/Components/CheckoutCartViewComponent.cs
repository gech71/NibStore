using Microsoft.AspNetCore.Mvc;
using Smartstore.Web.Models.Cart;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Identity;

public class CheckoutCartViewComponent : SmartViewComponent
{
    private readonly IShoppingCartService _shoppingCartService;

    public CheckoutCartViewComponent(IShoppingCartService shoppingCartService)
    {
        _shoppingCartService = shoppingCartService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // Get the current customer's shopping cart
        var cart = await _shoppingCartService.GetCartAsync(
            customer: null, // Will use WorkContext.CurrentCustomer
            cartType: ShoppingCartType.ShoppingCart,
            storeId: 0,     // Will use StoreContext.CurrentStore.Id
            activeOnly: true);

        // Map the cart to a ShoppingCartModel
        var model = await cart.MapAsync(
            isEditable: true,
            prepareEstimateShippingIfEnabled: true);

        return View(model);
    }
}