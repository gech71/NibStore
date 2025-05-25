using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Mvc.Rendering;
using Smartstore.Admin.Models.Store;
using Smartstore.Admin.Models.Stores;
using Smartstore.ComponentModel;
using Smartstore.Core.Catalog.Search;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Security;
using Smartstore.Core.Stores;
using Smartstore.Utilities;
using Smartstore.Web.Models;
using Smartstore.Web.Models.DataGrid;

namespace Smartstore.Admin.Controllers
{
    public class StoreController : AdminController
    {
        private readonly SmartDbContext _db;
        private readonly ICatalogSearchService _catalogSearchService;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly ILogger<StoreController> _logger;

        public StoreController(
            SmartDbContext db,
            ICatalogSearchService catalogSearchService,
            ShoppingCartSettings shoppingCartSettings,
            ILogger<StoreController> logger
        )
        {
            _db = db;
            _catalogSearchService = catalogSearchService;
            _shoppingCartSettings = shoppingCartSettings;
            _logger = logger;
        }

        /// <summary>
        /// (AJAX) Gets a list of all available stores.
        /// </summary>
        /// <param name="label">Text for optional entry. If not null an entry with the specified label text and the Id 0 will be added to the list.</param>
        /// <param name="selectedIds">Ids of selected entities.</param>
        /// <returns>List of all stores as JSON.</returns>
        public IActionResult AllStores(string label, string selectedIds)
        {
            var stores = new List<Store>(Services.StoreContext.GetAllStores());
            var ids = selectedIds.ToIntArray();

            if (label.HasValue())
            {
                stores.Insert(0, new Store { Name = label, Id = 0 });
            }

            var list = stores
                .Select(x => new ChoiceListItem
                {
                    Id = x.Id.ToString(),
                    Text = x.Name,
                    Selected = ids.Contains(x.Id),
                })
                .ToList();

            return new JsonResult(list);
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(List));
        }

        [Permission(Permissions.Configuration.Store.Read)]
        public IActionResult List()
        {
            return View();
        }

        [HttpPost]
        [Permission(Permissions.Configuration.Store.Read)]
        public async Task<IActionResult> StoreList(GridCommand command)
        {
            var stores = Services.StoreContext.GetAllStores();
            var mapper = MapperFactory.GetMapper<Store, StoreModel>();

            var rows = await stores
                .AsQueryable()
                .ApplyGridCommand(command)
                .SelectAwait(async x =>
                {
                    var model = await mapper.MapAsync(x);
                    model.HostList = model.Hosts.Convert<string[]>();
                    model.EditUrl = Url.Action(nameof(Edit), "Store", new { id = x.Id });

                    return model;
                })
                .AsyncToList();

            return Json(new GridModel<StoreModel> { Rows = rows, Total = rows.Count });
        }

        [Permission(Permissions.Configuration.Store.Create)]
        public async Task<IActionResult> Create()
        {
            await PrepareViewBag(null);

            var model = new StoreModel
            {
                DefaultCurrencyId = Services.CurrencyService.PrimaryCurrency.Id,
            };

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [Permission(Permissions.Configuration.Store.Create)]
        public async Task<IActionResult> Create(StoreModel model, bool continueEditing)
        {
            if (ModelState.IsValid)
            {
                var store = await MapperFactory.MapAsync<StoreModel, Store>(model);

                _db.Stores.Add(store);
                await _db.SaveChangesAsync();

                NotifySuccess(T("Admin.Configuration.Stores.Added"));

                return continueEditing
                    ? RedirectToAction(nameof(Edit), new { id = store.Id })
                    : RedirectToAction(nameof(List));
            }

            await PrepareViewBag(null);

            return View(model);
        }

        [Permission(Permissions.Configuration.Store.Read)]
        public async Task<IActionResult> Edit(int id)
        {
            var store = Services.StoreContext.GetStoreById(id);
            if (store == null)
            {
                return RedirectToAction(nameof(List));
            }

            var model = await MapperFactory.MapAsync<Store, StoreModel>(store);

            await PrepareViewBag(store);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [FormValueRequired("save", "save-continue")]
        [Permission(Permissions.Configuration.Store.Update)]
        public async Task<IActionResult> Edit(StoreModel model, bool continueEditing)
        {
            var store = await _db.Stores.FindByIdAsync(model.Id);
            if (store == null)
            {
                return RedirectToAction(nameof(List));
            }

            if (ModelState.IsValid)
            {
                await MapperFactory.MapAsync(model, store);

                await _db.SaveChangesAsync();

                NotifySuccess(T("Admin.Configuration.Stores.Updated"));

                return continueEditing
                    ? RedirectToAction(nameof(Edit), new { id = store.Id })
                    : RedirectToAction(nameof(List));
            }

            await PrepareViewBag(store);

            return View(model);
        }

        [HttpPost]
        [Permission(Permissions.Configuration.Store.Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var store = await _db.Stores.FindByIdAsync(id);
            if (store == null)
            {
                return RedirectToAction(nameof(List));
            }

            try
            {
                _db.Stores.Remove(store);
                await _db.SaveChangesAsync();

                NotifySuccess(T("Admin.Configuration.Stores.Deleted"));
                return RedirectToAction(nameof(List));
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }

            return RedirectToAction(nameof(Edit), new { id = store.Id });
        }

        [SaveChanges<SmartDbContext>(false)]
        [Permission(Permissions.Configuration.Store.ReadStats, false)]
        public async Task<JsonResult> StoreDashboardReportAsync()
        {
            var primaryCurrency = Services.CurrencyService.PrimaryCurrency;
            var currentUser = Services.WorkContext.CurrentCustomer;
            var authorizedStoreIds = await Services.StoreMappingService.GetAuthorizedStoreIdsAsync(
                "Customer",
                currentUser.Id
            );

            var isMerchant = await _db.CustomerRoleMappings.AnyAsync(m =>
                m.CustomerId == currentUser.Id && m.CustomerRole.SystemName == "Merchant"
            );

            int productsCount = 0,
                attributesCount = 0,
                categoriesCount = 0,
                manufacturersCount = 0,
                customersCount = 0,
                ordersCount = 0,
                attributeCombinationsCount = 0;
            decimal sales = 0;

            int? mediaCount = 0,
                onlineCustomersCount = 0;
            decimal? mediaSize = 0,
                cartsValue = 0,
                wishlistsValue = 0;

            if (isMerchant)
            {
                var merchantProductIds = await _db
                    .GenericAttributes.Where(a =>
                        a.KeyGroup == "Product"
                        && a.Key == "CreatedByUserId"
                        && a.Value == currentUser.Id.ToString()
                    )
                    .Select(a => a.EntityId)
                    .ToListAsync();

                productsCount = await _db
                    .Products.Where(p => merchantProductIds.Contains(p.Id))
                    .CountAsync();
                attributesCount = await _db
                    .ProductVariantAttributes.Where(a => merchantProductIds.Contains(a.ProductId))
                    .CountAsync();
                attributeCombinationsCount = await _db
                    .ProductVariantAttributeCombinations.Where(ac =>
                        merchantProductIds.Contains(ac.ProductId) && ac.IsActive
                    )
                    .CountAsync();
                categoriesCount = await _db.Categories.CountAsync();
                manufacturersCount = await _db
                    .ProductManufacturers.Where(pm => merchantProductIds.Contains(pm.ProductId))
                    .Select(pm => pm.ManufacturerId)
                    .Distinct()
                    .CountAsync();
                customersCount = await _db
                    .OrderItems.Where(oi => merchantProductIds.Contains(oi.ProductId))
                    .Select(oi => oi.Order.CustomerId)
                    .Distinct()
                    .CountAsync();
                ordersCount = await _db
                    .OrderItems.Where(oi => merchantProductIds.Contains(oi.ProductId))
                    .Select(oi => oi.OrderId)
                    .Distinct()
                    .CountAsync();
                sales =
                    await _db
                        .OrderItems.Where(oi => merchantProductIds.Contains(oi.ProductId))
                        .SumAsync(oi => (decimal?)oi.PriceInclTax * oi.Quantity) ?? 0;
                mediaCount = null;
                mediaSize = null;
                onlineCustomersCount = null;
                cartsValue = null;
                wishlistsValue = null;
            }
            else
            {
                productsCount = await _catalogSearchService
                    .PrepareQuery(new CatalogSearchQuery())
                    .CountAsync();
                attributesCount = await _db.ProductAttributes.CountAsync();
                attributeCombinationsCount =
                    await _db.ProductVariantAttributeCombinations.CountAsync(x => x.IsActive);
                categoriesCount = await _db.Categories.CountAsync();
                manufacturersCount = await _db.Manufacturers.CountAsync();

                var registeredRole = await _db
                    .CustomerRoles.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SystemName == SystemCustomerRoleNames.Registered);

                var registeredCustomersQuery = _db
                    .Customers.AsNoTracking()
                    .ApplyRolesFilter([registeredRole.Id]);

                customersCount = await registeredCustomersQuery.CountAsync();

                var ordersQuery = _db.Orders.ApplyCustomerFilter(authorizedStoreIds).AsNoTracking();
                ordersCount = await ordersQuery.CountAsync();
                sales = await ordersQuery.SumAsync(x => (decimal?)x.OrderTotal) ?? 0;

                mediaCount = await Services.MediaService.CountFilesAsync(
                    new MediaSearchQuery { Deleted = false }
                );
                mediaSize = await _db.MediaFiles.SumAsync(x => (long)x.Size);
                onlineCustomersCount = await _db
                    .Customers.ApplyOnlineCustomersFilter(15)
                    .CountAsync();
                cartsValue = await _db.ShoppingCartItems.GetOpenCartTypeSubTotalAsync(
                    ShoppingCartType.ShoppingCart,
                    _shoppingCartSettings.AllowActivatableCartItems ? true : null
                );
                wishlistsValue = await _db.ShoppingCartItems.GetOpenCartTypeSubTotalAsync(
                    ShoppingCartType.Wishlist
                );
            }

            var model = new StoreDashboardReportModel
            {
                ProductsCount = productsCount.ToString("N0"),
                CategoriesCount = categoriesCount.ToString("N0"),
                ManufacturersCount = manufacturersCount.ToString("N0"),
                AttributesCount = attributesCount.ToString("N0"),
                AttributeCombinationsCount = attributeCombinationsCount.ToString("N0"),
                MediaCount = mediaCount != null ? mediaCount.Value.ToString("N0") : null,
                MediaSize =
                    mediaSize != null ? Prettifier.HumanizeBytes((long)mediaSize.Value) : null,
                OrdersCount = ordersCount.ToString("N0"),
                OnlineCustomersCount =
                    onlineCustomersCount != null ? onlineCustomersCount.Value.ToString("N0") : null,
                Sales = Services.CurrencyService.CreateMoney(sales, primaryCurrency).ToString(),
                CartsValue =
                    cartsValue != null
                        ? Services
                            .CurrencyService.CreateMoney(cartsValue.Value, primaryCurrency)
                            .ToString()
                        : null,
                WishlistsValue =
                    wishlistsValue != null
                        ? Services
                            .CurrencyService.CreateMoney(wishlistsValue.Value, primaryCurrency)
                            .ToString()
                        : null,
            };

            return new JsonResult(new { model });
        }

        private async Task PrepareViewBag(Store store)
        {
            var currencies = await _db
                .Currencies.AsNoTracking()
                .ApplyStandardFilter(false, store?.Id ?? 0)
                .ToListAsync();

            ViewBag.Currencies = currencies
                .Select(x => new SelectListItem
                {
                    Text = x.GetLocalized(y => y.Name),
                    Value = x.Id.ToString(),
                })
                .ToList();
        }
    }
}
