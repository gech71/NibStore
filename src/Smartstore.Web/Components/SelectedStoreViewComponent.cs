using Smartstore.Core.Checkout.Cart;

public class SelectedStoreViewComponent : SmartViewComponent
{
    private readonly SmartDbContext _db;

    public SelectedStoreViewComponent(SmartDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync(int cartItemId)
    {
        var selectedStore = await _db.Set<ShoppingCartItem>()
            .Where(c => c.Id == cartItemId)
            .Select(c => c.SelectedStore)
            .FirstOrDefaultAsync();

        var storeName = selectedStore ?? "No selected store";

        return View("Default", storeName);
    }
}
