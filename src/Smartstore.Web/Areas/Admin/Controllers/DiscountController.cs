﻿﻿using System.Linq.Dynamic.Core;
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



namespace Smartstore.Admin.Controllers
{
    public class DiscountController : AdminController
    {
        private readonly SmartDbContext _db;
        private readonly IRuleService _ruleService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizedEntityService _localizedEntityService;
        private readonly ProductController _productController;
        private readonly IWorkContext _workContext;



        public DiscountController(
            SmartDbContext db,
            IRuleService ruleService,
            ICurrencyService currencyService,
            ILocalizedEntityService localizedEntityService,
            ProductController productController,
            IWorkContext workContext)
        {
            _db = db;
            _ruleService = ruleService;
            _currencyService = currencyService;
            _localizedEntityService = localizedEntityService;
            _productController = productController;
            _workContext = workContext;
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
            return RedirectToAction(nameof(ManageDiscount));
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
                    .Select(x => new DiscountAppliedToEntityModel { Id = x.Id, Name = x.GetLocalized(y => y.Name, language) })
                    .ToList();

                ViewBag.AppliedToManufacturers = discount.AppliedToManufacturers
                    .Where(x => x != null && !x.Deleted)
                    .Select(x => new DiscountAppliedToEntityModel { Id = x.Id, Name = x.GetLocalized(y => y.Name, language) })
                    .ToList();

                ViewBag.AppliedToProducts = discount.AppliedToProducts
                    .Where(x => x != null && !x.Deleted)
                    .Select(x => new DiscountAppliedToEntityModel { Id = x.Id, Name = x.GetLocalized(y => y.Name, language) })
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
        [HttpGet]
        [Permission(Permissions.Catalog.Product.Read)]
        public async Task<IActionResult> ApplyDiscount()
        {
            var model = new ProductListModel();

            // Call the method from ProductController
            await _productController.PrepareProductListModelAsync(model);

            return View(model);
        }

        [HttpPost]
        [Permission(Permissions.Catalog.Product.Update)]      
        public async Task<IActionResult> ApplyDiscountToProducts([FromBody]ApplyDiscountToSelectedModel model)
        {
            // Call the existing action method in ProductController
            return await _productController.ApplyDiscountToSelected(model);
        }

        [HttpGet]
        [Permission(Permissions.Promotion.Discount.Read)]
        public async Task<IActionResult> ManageDiscount()
        {
            var discountList = new DiscountListModel();
            var productList = new ProductListModel();
            await _productController.PrepareProductListModelAsync(productList);

            var model = new CombinedDiscountTabsViewModel
            {
                DiscountList = discountList,
                ProductList = productList,
            };

            return View(model);
        }

        [HttpGet, HttpPost]
        [Permission(Permissions.Catalog.Product.Read)]
        public async Task<IActionResult> ProductDiscountList(GridCommand command, string discountName = null, string productName = null)
        {
            var currentUser = _workContext.CurrentCustomer;

            // Check if the current user is a Merchant
            var isMerchant = await _db.CustomerRoleMappings.AnyAsync(m =>
                m.CustomerId == currentUser.Id &&
                m.CustomerRole.SystemName == "Merchant"
            );

            var query = _db.Products
                .Include(p => p.AppliedDiscounts)
                .Where(p => p.AppliedDiscounts.Any());

            // Apply discount name filter (matches any of the applied discounts)
            if (!string.IsNullOrEmpty(discountName))
            {
                query = query.Where(p =>
                    p.AppliedDiscounts.Any(d => d.Name.Contains(discountName)));
            }

            // Apply product name filter
            if (!string.IsNullOrEmpty(productName))
            {
                query = query.Where(p => p.Name.Contains(productName));
            }

            // Restrict to merchant-owned products if user is a merchant
            if (isMerchant)
            {
                var merchantProductIds = await _db.GenericAttributes
                    .Where(a =>
                        a.KeyGroup == "Product" &&
                        a.Key == "CreatedByUserId" &&
                        a.Value == currentUser.Id.ToString())
                    .Select(a => a.EntityId)
                    .ToListAsync();

                if (!merchantProductIds.Any())
                {
                    query = query.Where(p => false); // No results for merchant with no products
                }
                else
                {
                    query = query.Where(p => merchantProductIds.Contains(p.Id));
                }
            }

            var data = await query
                .Select(p => new ProductDiscountViewModel
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Price = p.Price,
                    DiscountName = string.Join(", ", p.AppliedDiscounts.Select(d => d.Name)),
                    DiscountPercentage = p.AppliedDiscounts.FirstOrDefault().DiscountPercentage,
                    DiscountAmount = p.AppliedDiscounts.FirstOrDefault().DiscountAmount,
                    StartDateUtc = p.AppliedDiscounts.FirstOrDefault().StartDateUtc,
                    EndDateUtc = p.AppliedDiscounts.FirstOrDefault().EndDateUtc
                })
                .ApplyGridCommand(command)
                .ToPagedList(command)
                .LoadAsync();

            return Json(new GridModel<ProductDiscountViewModel>
            {
                Rows = data,
                Total = data.TotalCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDiscountsForProduct(int productId)
        {
            try
            {                
                if (productId <= 0)
                {
                    return Json(new { success = false, message = "Invalid product ID" });
                }

                var product = await _db.Products
                    .Include(p => p.AppliedDiscounts)
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null)
                {
                    return Json(new { success = false, message = $"Product with ID {productId} not found" });
                }

                var discounts = product.AppliedDiscounts.Select(d => new
                {
                    discountId = d.Id,
                    discountName = d.Name
                }).ToList();

                return Json(new { success = true, data = discounts });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> RemoveDiscountFromSelectedProducts([FromBody] RemoveDiscountFromSelectedModel model)
        {
            try
            {
                if (model == null || model.SelectedIds == null || !model.SelectedIds.Any())
                {
                    return Json(new { success = false, message = "No products selected" });
                }

                var discount = await _db.Discounts.FindAsync(model.DiscountId);
                if (discount == null)
                {
                    return Json(new { success = false, message = "Discount not found" });
                }

                var products = await _db.Products
                    .Include(p => p.AppliedDiscounts)
                    .Where(p => model.SelectedIds.Contains(p.Id))
                    .ToListAsync();

                var affectedCount = 0;
                foreach (var product in products)
                {
                    if (product.AppliedDiscounts.Remove(discount))
                    {
                        affectedCount++;
                    }
                }

                await _db.SaveChangesAsync();

                return Json(new 
                { 
                    success = true,
                    message = $"Discount removed from {affectedCount} product(s)",
                    affectedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new 
                { 
                    success = false,
                    message = "Error removing discount",
                    error = ex.Message
                });
            }
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