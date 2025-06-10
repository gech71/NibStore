using System.Linq.Dynamic.Core;
using Smartstore.Admin.Models.Discounts;
using Smartstore.ComponentModel;
using Smartstore.Core.Catalog.Discounts;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Localization;
using Smartstore.Core.Logging;
using Smartstore.Core.Rules;
using Smartstore.Core.Rules.Filters;
using Smartstore.Core.Security;
using Smartstore.Web.Models;
using Smartstore.Web.Models.DataGrid;
using Smartstore.Admin.Models.Catalog;
using Microsoft.AspNetCore.Mvc.Rendering;
using Smartstore.Core.Catalog.Pricing;


namespace Smartstore.Admin.Controllers
{
    public class DiscountController : AdminController
    {
        private readonly SmartDbContext _db;
        private readonly IRuleService _ruleService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizedEntityService _localizedEntityService;


        public DiscountController(
            SmartDbContext db,
            IRuleService ruleService,
            ICurrencyService currencyService,
            ILocalizedEntityService localizedEntityService)
        {
            _db = db;
            _ruleService = ruleService;
            _currencyService = currencyService;
            _localizedEntityService = localizedEntityService;
        }

        /// <summary>
        /// (AJAX) Gets a list of all available discounts. 
        /// </summary>
        /// <param name="label">Text for optional entry. If not null an entry with the specified label text and the Id 0 will be added to the list.</param>
        /// <param name="selectedIds">Ids of selected entities.</param>
        /// <param name="DiscountType">Specifies the <see cref="DiscountType"/>.</param>
        /// <returns>List of all discounts as JSON.</returns>
        public async Task<IActionResult> AllDiscounts(string label, string selectedIds, DiscountType? type)
        {
            var discounts = await _db.Discounts
                .AsNoTracking()
                .Where(x => x.DiscountTypeId == (int)type)
                .ToListAsync();

            var selectedArr = selectedIds.ToIntArray();

            if (label.HasValue())
            {
                discounts.Insert(0, new Discount { Name = label, Id = 0 });
            }

            var data = discounts
                .Select(x => new ChoiceListItem
                {
                    Id = x.Id.ToString(),
                    Text = x.Name,
                    Selected = selectedArr.Contains(x.Id)
                })
                .ToList();

            return new JsonResult(data);
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(List));
        }

        [Permission(Permissions.Promotion.Discount.Read)]
        public IActionResult List()
        {
            return View(new DiscountListModel());
        }

        [Permission(Permissions.Promotion.Discount.Read)]
        public async Task<IActionResult> DiscountList(GridCommand command, DiscountListModel model)
        {
            var query = _db.Discounts.AsNoTracking();

            if (model.SearchName.HasValue())
            {
                query = query.ApplySearchFilterFor(x => x.Name, model.SearchName);
            }
            if (model.SearchDiscountTypeId.HasValue)
            {
                query = query.Where(x => x.DiscountTypeId == model.SearchDiscountTypeId);
            }
            if (model.SearchUsePercentage.HasValue)
            {
                query = query.Where(x => x.UsePercentage == model.SearchUsePercentage.Value);
            }
            if (model.SearchRequiresCouponCode.HasValue)
            {
                query = query.Where(x => x.RequiresCouponCode == model.SearchRequiresCouponCode.Value);
            }

            var discounts = await query
                .Include(x => x.RuleSets)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ApplyGridCommand(command)
                .ToPagedList(command)
                .LoadAsync();

            var mapper = MapperFactory.GetMapper<Discount, DiscountModel>();
            var rows = await discounts
                .SelectAwait(async x => await mapper.MapAsync(x))
                .AsyncToList();

            return Json(new GridModel<DiscountModel>
            {
                Rows = rows,
                Total = discounts.TotalCount
            });
        }

        [HttpPost]
        [Permission(Permissions.Promotion.Discount.Delete)]
        public async Task<IActionResult> DiscountDelete(GridSelection selection)
        {
            var entities = await _db.Discounts.GetManyAsync(selection.GetEntityIds(), true);
            if (entities.Count > 0)
            {
                _db.Discounts.RemoveRange(entities);
                await _db.SaveChangesAsync();

                Services.ActivityLogger.LogActivity(
                    KnownActivityLogTypes.DeleteDiscount, 
                    T("ActivityLog.DeleteDiscount"), 
                    string.Join(", ", entities.Select(x => x.Name)));
            }

            return Json(new { Success = true, entities.Count });
        }

        [Permission(Permissions.Promotion.Discount.Create)]
        public IActionResult Create()
        {
            var model = new DiscountModel
            {
                LimitationTimes = 1
            };

            AddLocales(model.Locales);
            PrepareDiscountModel(model, null);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [Permission(Permissions.Promotion.Discount.Create)]
        public async Task<IActionResult> Create(DiscountModel model, bool continueEditing)
        {
            if (ModelState.IsValid)
            {
                var discount = await MapperFactory.MapAsync<DiscountModel, Discount>(model);
                discount.CouponCode = discount.CouponCode?.Trim();

                _db.Discounts.Add(discount);
                await _db.SaveChangesAsync();

                await ApplyLocales(model, discount);
                await _ruleService.ApplyRuleSetMappingsAsync(discount, model.SelectedRuleSetIds);
                await _db.SaveChangesAsync();

                Services.ActivityLogger.LogActivity(KnownActivityLogTypes.AddNewDiscount, T("ActivityLog.AddNewDiscount"), discount.Name);
                NotifySuccess(T("Admin.Promotions.Discounts.Added"));

                return continueEditing
                    ? RedirectToAction(nameof(Edit), new { id = discount.Id })
                    : RedirectToAction("Index");
            }

            PrepareDiscountModel(model, null);
            return View(model);
        }

        [Permission(Permissions.Promotion.Discount.Read)]
        public async Task<IActionResult> Edit(int id)
        {
            // INFO: CacheableEntity! Always load "tracked" otherwise RuleSets loaded with old values after saving.
            var discount = await _db.Discounts
                .AsSplitQuery()
                .Include(x => x.RuleSets)
                .Include(x => x.AppliedToCategories)
                .Include(x => x.AppliedToManufacturers)
                .Include(x => x.AppliedToProducts)
                .FindByIdAsync(id);

            if (discount == null)
            {
                return NotFound();
            }

            var model = await MapperFactory.MapAsync<Discount, DiscountModel>(discount);

            PrepareDiscountModel(model, discount);

            AddLocales(model.Locales, (locale, languageId) =>
            {
                locale.OfferBadgeLabel = discount.GetLocalized(x => x.OfferBadgeLabel, languageId, false, false);
            });

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [Permission(Permissions.Promotion.Discount.Update)]
        public async Task<IActionResult> Edit(DiscountModel model, bool continueEditing)
        {
            var discount = await _db.Discounts
                .AsSplitQuery()
                .Include(x => x.RuleSets)
                .Include(x => x.AppliedToCategories)
                .Include(x => x.AppliedToManufacturers)
                .Include(x => x.AppliedToProducts)
                .FindByIdAsync(model.Id);

            if (discount == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                await MapperFactory.MapAsync(model, discount);

                discount.CouponCode = discount.CouponCode?.Trim();

                await ApplyLocales(model, discount);
                await _ruleService.ApplyRuleSetMappingsAsync(discount, model.SelectedRuleSetIds);

                await _db.SaveChangesAsync();

                Services.ActivityLogger.LogActivity(KnownActivityLogTypes.EditDiscount, T("ActivityLog.EditDiscount"), discount.Name);
                NotifySuccess(T("Admin.Promotions.Discounts.Updated"));

                return continueEditing
                    ? RedirectToAction(nameof(Edit), new { id = discount.Id })
                    : RedirectToAction("Index");
            }

            PrepareDiscountModel(model, discount);
            return View(model);
        }

        [HttpPost]
        [Permission(Permissions.Promotion.Discount.Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var discount = await _db.Discounts.FindByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }

            _db.Discounts.Remove(discount);
            await _db.SaveChangesAsync();

            Services.ActivityLogger.LogActivity(KnownActivityLogTypes.DeleteDiscount, T("ActivityLog.DeleteDiscount"), discount.Name);
            NotifySuccess(T("Admin.Promotions.Discounts.Deleted"));

            return RedirectToAction(nameof(List));
        }

        #region Discount usage history

        [Permission(Permissions.Promotion.Discount.Read)]
        public async Task<IActionResult> DiscountUsageHistoryList(GridCommand command, int discountId)
        {
            var historyEntries = await _db.DiscountUsageHistory
                .AsNoTracking()
                .Where(x => x.DiscountId == discountId)
                .OrderByDescending(x => x.CreatedOnUtc)
                .ApplyGridCommand(command, false)
                .ToPagedList(command)
                .LoadAsync();

            var rows = historyEntries
                .Select(x => new DiscountUsageHistoryModel
                {
                    Id = x.Id,
                    DiscountId = x.DiscountId,
                    OrderId = x.OrderId,
                    CreatedOnUtc = x.CreatedOnUtc,
                    CreatedOn = Services.DateTimeHelper.ConvertToUserTime(x.CreatedOnUtc, DateTimeKind.Utc),
                    OrderEditUrl = Url.Action("Edit", "Order", new { id = x.OrderId, area = "Admin" }),
                    OrderEditLinkText = T("Admin.Common.ViewObject", x.OrderId)
                })
                .ToList();

            return Json(new GridModel<DiscountUsageHistoryModel>
            {
                Rows = rows,
                Total = historyEntries.TotalCount
            });
        }

        [Permission(Permissions.Promotion.Discount.Update)]
        public async Task<IActionResult> DiscountUsageHistoryDelete(GridSelection selection)
        {
            var success = false;
            var numDeleted = 0;
            var ids = selection.GetEntityIds();

            if (ids.Any())
            {
                var historyEntries = await _db.DiscountUsageHistory.GetManyAsync(ids, true);

                _db.DiscountUsageHistory.RemoveRange(historyEntries);

                numDeleted = await _db.SaveChangesAsync();
                success = true;
            }

            return Json(new { Success = success, Count = numDeleted });
        }

        #endregion

        private void PrepareDiscountModel(DiscountModel model, Discount discount)
        {
            if (discount != null)
            {
                var language = Services.WorkContext.WorkingLanguage;

                model.SelectedRuleSetIds = discount.RuleSets.Select(x => x.Id).ToArray();

                ViewBag.AppliedToCategories = discount.AppliedToCategories
                    .Where(x => x != null && !x.Deleted)
                    .Select(x => new DiscountAppliedToEntityModel { Id = x.Id, Name = x.Name })
                    .ToList();

                ViewBag.AppliedToManufacturers = discount.AppliedToManufacturers
                    .Where(x => x != null && !x.Deleted)
                    .Select(x => new DiscountAppliedToEntityModel { Id = x.Id, Name = x.Name })
                    .ToList();

                ViewBag.AppliedToProducts = discount.AppliedToProducts
                    .Where(x => x != null && !x.Deleted)
                    .Select(x => new DiscountAppliedToEntityModel { Id = x.Id, Name = x.Name })
                    .ToList();
            }
            else
            {
                ViewBag.AppliedToCategories = new List<DiscountAppliedToEntityModel>();
                ViewBag.AppliedToManufacturers = new List<DiscountAppliedToEntityModel>();
                ViewBag.AppliedToProducts = new List<DiscountAppliedToEntityModel>();
            }

            ViewBag.PrimaryStoreCurrencyCode = _currencyService.PrimaryCurrency.CurrencyCode;
        }

        [Permission(Permissions.Promotion.Discount.Read)]
        public IActionResult ManageDiscounts()
        {
            return View();
        }

       [HttpGet]
       [Permission(Permissions.Catalog.Product.Read)]
       public async Task<IActionResult> GetAllProducts(GridCommand command)
       {
           var query = _db.Products.AsNoTracking();

    var products = await query
        .OrderBy(p => p.Name)
        .ApplyGridCommand(command)
        .ToPagedList(command)
        .LoadAsync();

    var gridModel = new GridModel<ProductModel>
    {
        Rows = products.Select(x => new ProductModel
        {
            Id = x.Id,
            Name = x.Name,
            Sku = x.Sku,
            Price = x.Price,
            EditUrl = Url.Action("Edit", "Product", new { id = x.Id })
        }).ToList(),
        Total = products.TotalCount
    };

    return Json(gridModel);
}
        [HttpPost]
        [Permission(Permissions.Promotion.Discount.Update)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscountToProducts(ApplyDiscountModel model)
        {
            if (model.SelectedProductIds == null || !model.SelectedProductIds.Any())
            {
            return Json(new { success = false, message = T("Admin.Promotions.Discounts.NoSelection") });
            }

            if (model.SelectedDiscountId <= 0)
            {
            return Json(new { success = false, message = T("Admin.Promotions.Discounts.NoDiscountSelected") });
            }

            var discount = await _db.Discounts
                .Include(d => d.AppliedToProducts)
                .FirstOrDefaultAsync(d => d.Id == model.SelectedDiscountId);

            if (discount == null)
            {
            return Json(new { success = false, message = T("Admin.Promotions.Discounts.NotFound") });
            }

            var products = await _db.Products
            .Where(p => model.SelectedProductIds.Contains(p.Id))
            .ToListAsync();

            foreach (var product in products)
            {
            if (!discount.AppliedToProducts.Any(ap => ap.Id == product.Id))
            {
                discount.AppliedToProducts.Add(product);
            }
            }

            await _db.SaveChangesAsync();

            // Fetch updated product-discount info to pass to the view if needed
            // Example: ViewData["UpdatedProducts"] = products.Select(p => new { p.Id, p.Name }).ToList();

            return Json(new { success = true, message = T("Admin.Promotions.Discounts.AppliedSuccessfully") });
        }

        [HttpGet]
        [Permission(Permissions.Promotion.Discount.Read)]
        public async Task<IActionResult> GetProductsWithDiscounts(GridCommand command)
        {
            var query = from product in _db.Products.AsNoTracking()
                join discountProduct in _db.Discounts.AsNoTracking().SelectMany(d => d.AppliedToProducts, (d, p) => new { d, p })
                    on product.Id equals discountProduct.p.Id into dpj
                from discountProduct in dpj.DefaultIfEmpty()
                select new ProductWithDiscountModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    DiscountName = discountProduct != null ? discountProduct.d.Name : T("Admin.Promotions.Discounts.None").ToString(),
                    StartDate = discountProduct != null ? discountProduct.d.StartDateUtc : null,
                    EndDate = discountProduct != null ? discountProduct.d.EndDateUtc : null,
                    DiscountPercentage = discountProduct != null && discountProduct.d.UsePercentage == true ? discountProduct.d.DiscountPercentage : null,
                    DiscountAmount = discountProduct != null && discountProduct.d.UsePercentage == false ? discountProduct.d.DiscountAmount : null
                };

    // No filter applied since GridCommand does not have a Filter property
    // If you want to filter by product name, add a search parameter to your model and use it here.
    // Example:
    // if (!string.IsNullOrEmpty(model.SearchProductName))
    // {
    //     query = query.Where(x => x.ProductName.Contains(model.SearchProductName));
    // }

    var pagedRows = await query
        .OrderBy(x => x.ProductName)
        .ApplyGridCommand(command)
        .ToPagedList(command)
        .LoadAsync();

    return Json(new GridModel<ProductWithDiscountModel>
    {
        Rows = pagedRows.ToList(),
        Total = pagedRows.TotalCount
    });
}
        private async Task ApplyLocales(DiscountModel model, Discount discount)
        {
            foreach (var localized in model.Locales)
            {
                await _localizedEntityService.ApplyLocalizedValueAsync(discount, x => x.OfferBadgeLabel, localized.OfferBadgeLabel, localized.LanguageId);
            }
        }
    }
}
