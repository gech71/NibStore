﻿using Microsoft.AspNetCore.Mvc.Rendering;
using Smartstore.Core.Catalog;
using Smartstore.Core.Catalog.Brands;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Catalog.Search;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Security;
using Smartstore.Core.Stores;
using Smartstore.Data;
using Smartstore.Web.Rendering;

namespace Smartstore.Web.Controllers
{
    public partial class EntityController : PublicController
    {
        private readonly SmartDbContext _db;
        private readonly ICatalogSearchService _catalogSearchService;
        private readonly CatalogSettings _catalogSettings;
        private readonly MediaSettings _mediaSettings;
        private readonly SearchSettings _searchSettings;
        private readonly CustomerSettings _customerSettings;
        private readonly IMediaService _mediaService;
        private readonly ICategoryService _categoryService;
        private readonly Lazy<ILocalizedEntityService> _locEntityService;

        public EntityController(
            SmartDbContext db,
            ICatalogSearchService catalogSearchService,
            CatalogSettings catalogSettings,
            MediaSettings mediaSettings,
            SearchSettings searchSettings,
            CustomerSettings customerSettings,
            IMediaService mediaService,
            ICategoryService categoryService,
            Lazy<ILocalizedEntityService> locEntityService
        )
        {
            _db = db;
            _catalogSearchService = catalogSearchService;
            _catalogSettings = catalogSettings;
            _mediaSettings = mediaSettings;
            _searchSettings = searchSettings;
            _customerSettings = customerSettings;
            _mediaService = mediaService;
            _categoryService = categoryService;
            _locEntityService = locEntityService;
        }

        public async Task<IActionResult> Picker(EntityPickerModel model)
        {
            if (model.EntityType.EqualsNoCase("product"))
            {
                ViewBag.AvailableCategories = (
                    await _categoryService.GetCategoryTreeAsync(includeHidden: true)
                )
                    .FlattenNodes(false)
                    .Select(x => new SelectListItem
                    {
                        Text = x.GetCategoryNameIndented(),
                        Value = x.Id.ToString(),
                    })
                    .ToList();

                var manufacturers = await _db
                    .Manufacturers.ApplyStandardFilter(includeHidden: true)
                    .ToListAsync();

                ViewBag.AvailableManufacturers = manufacturers
                    .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() })
                    .ToList();

                ViewBag.AvailableStores = Services
                    .StoreContext.GetAllStores()
                    .ToSelectListItems(Array.Empty<int>());
            }
            else if (model.EntityType.EqualsNoCase("customer"))
            {
                ViewBag.AvailableCustomerSearchTypes = new List<SelectListItem>
                {
                    new()
                    {
                        Text = "Name",
                        Value = "Name",
                        Selected = true,
                    },
                    new() { Text = "Email", Value = "Email" },
                };

                if (_customerSettings.CustomerNumberMethod != CustomerNumberMethod.Disabled)
                {
                    ViewBag.AvailableCustomerSearchTypes.Add(
                        new SelectListItem
                        {
                            Text = T("Account.Fields.CustomerNumber"),
                            Value = "CustomerNumber",
                        }
                    );
                }
            }

            return PartialView(model);
        }

        [HttpPost]
        [ActionName("Picker")]
        public async Task<IActionResult> PickerPost(EntityPickerModel model)
        {
            try
            {
                var form = Request.Form;
                var disableIf = model
                    .DisableIf.SplitSafe(',')
                    .Select(x => x.ToLower().Trim())
                    .ToList();
                var disableIds = model.DisableIds.SplitSafe(',').Select(x => x.ToInt()).ToList();
                var selected = model.Selected.SplitSafe(',');
                var returnSku = model.ReturnField.EqualsNoCase("sku");

                using var scope = new DbContextScope(
                    Services.DbContext,
                    autoDetectChanges: false,
                    forceNoTracking: true
                );
                if (model.EntityType.EqualsNoCase("product"))
                {
                    model.SearchTerm = model.SearchTerm.TrimSafe();

                    var hasPermission = await Services.Permissions.AuthorizeAsync(
                        Permissions.Catalog.Product.Read
                    );
                    var disableIfNotSimpleProduct = disableIf.Contains("notsimpleproduct");
                    var disableIfGroupedProduct = disableIf.Contains("groupedproduct");
                    var labelTextGrouped = T(
                        "Admin.Catalog.Products.ProductType.GroupedProduct.Label"
                    ).Value;
                    var labelTextBundled = T(
                        "Admin.Catalog.Products.ProductType.BundledProduct.Label"
                    ).Value;
                    var sku = T("Products.Sku");

                    var currentUser = Services.WorkContext.CurrentCustomer;
                    var isMerchant = await _db.CustomerRoleMappings.AnyAsync(m =>
                        m.CustomerId == currentUser.Id && m.CustomerRole.SystemName == "Merchant"
                    );

                    List<int> merchantProductIds = null;

                    if (isMerchant)
                    {
                        merchantProductIds = await _db
                            .GenericAttributes.Where(a =>
                                a.KeyGroup == "Product"
                                && a.Key == "CreatedByUserId"
                                && a.Value == currentUser.Id.ToString()
                            )
                            .Select(a => a.EntityId)
                            .ToListAsync();

                        if (!merchantProductIds.Any())
                        {
                            model.SearchResult = new List<EntityPickerModel.SearchResultModel>();
                            return PartialView("Entity/_EntityPickerList", model);
                        }
                    }

                    var fields = new List<string> { "name" };
                    if (_searchSettings.SearchFields.Contains("sku"))
                    {
                        fields.Add("sku");
                    }
                    if (_searchSettings.SearchFields.Contains("shortdescription"))
                    {
                        fields.Add("shortdescription");
                    }

                    var searchQuery = new CatalogSearchQuery(
                        fields.ToArray(),
                        model.SearchTerm
                    ).HasStoreId(model.StoreId);

                    if (!hasPermission)
                    {
                        searchQuery = searchQuery.VisibleOnly(currentUser);
                    }

                    if (model.ProductTypeId > 0)
                    {
                        searchQuery = searchQuery.IsProductType((ProductType)model.ProductTypeId);
                    }

                    if (model.ManufacturerId != 0)
                    {
                        searchQuery = searchQuery.WithManufacturerIds(null, model.ManufacturerId);
                    }

                    if (model.CategoryId != 0)
                    {
                        var node = await _categoryService.GetCategoryTreeAsync(
                            model.CategoryId,
                            true
                        );
                        if (node != null)
                        {
                            searchQuery = searchQuery.WithCategoryTreePath(
                                node.GetTreePath(),
                                null
                            );
                        }
                    }

                    List<EntityPickerProduct> products;
                    var skip = model.PageIndex * model.PageSize;

                    if (_searchSettings.UseCatalogSearchInBackend)
                    {
                        searchQuery = searchQuery
                            .Slice(skip, model.PageSize)
                            .SortBy(ProductSortingEnum.NameAsc);

                        var searchResult = await _catalogSearchService.SearchAsync(searchQuery);
                        products = (await searchResult.GetHitsAsync())
                            .Where(x => !isMerchant || merchantProductIds.Contains(x.Id))
                            .Select(x => new EntityPickerProduct
                            {
                                Id = x.Id,
                                Sku = x.Sku,
                                Name = x.Name,
                                Published = x.Published,
                                ProductTypeId = x.ProductTypeId,
                                MainPictureId = x.MainPictureId,
                            })
                            .ToList();
                    }
                    else
                    {
                        var query = _catalogSearchService.PrepareQuery(searchQuery).AsNoTracking();

                        if (isMerchant)
                        {
                            query = query.Where(x => merchantProductIds.Contains(x.Id));
                        }

                        products = await query
                            .Select(x => new EntityPickerProduct
                            {
                                Id = x.Id,
                                Sku = x.Sku,
                                Name = x.Name,
                                Published = x.Published,
                                ProductTypeId = x.ProductTypeId,
                                MainPictureId = x.MainPictureId,
                            })
                            .OrderBy(x => x.Name)
                            .Skip(skip)
                            .Take(model.PageSize)
                            .ToListAsync();
                    }

                    var fileIds = products
                        .Select(x => x.MainPictureId ?? 0)
                        .Where(x => x != 0)
                        .Distinct()
                        .ToArray();

                    var files = (await _mediaService.GetFilesByIdsAsync(fileIds)).ToDictionarySafe(
                        x => x.Id
                    );

                    model.SearchResult = products
                        .Select(x =>
                        {
                            var item = new EntityPickerModel.SearchResultModel
                            {
                                Id = x.Id,
                                Title = x.Name,
                                Summary = x.Sku,
                                SummaryTitle = $"{sku}: {x.Sku.NaIfEmpty()}",
                                Published = hasPermission ? x.Published : null,
                                ReturnValue = returnSku ? x.Sku : x.Id.ToString(),
                            };

                            item.Selected = selected.Contains(item.ReturnValue);

                            if (disableIfNotSimpleProduct)
                            {
                                item.Disable = x.ProductTypeId != (int)ProductType.SimpleProduct;
                            }
                            else if (disableIfGroupedProduct)
                            {
                                item.Disable = x.ProductTypeId == (int)ProductType.GroupedProduct;
                            }

                            if (!item.Disable && disableIds.Contains(x.Id))
                            {
                                item.Disable = true;
                            }

                            if (x.ProductTypeId == (int)ProductType.GroupedProduct)
                            {
                                item.LabelText = labelTextGrouped;
                                item.LabelClassName = "badge-success";
                            }
                            else if (x.ProductTypeId == (int)ProductType.BundledProduct)
                            {
                                item.LabelText = labelTextBundled;
                                item.LabelClassName = "badge-info";
                            }

                            files.TryGetValue(x.MainPictureId ?? 0, out var file);
                            item.ImageUrl = _mediaService.GetUrl(
                                file,
                                _mediaSettings.ProductThumbPictureSizeOnProductDetailsPage,
                                null,
                                !_catalogSettings.HideProductDefaultPictures
                            );

                            return item;
                        })
                        .ToList();
                }
                else if (model.EntityType.EqualsNoCase("category"))
                {
                    var categoryQuery = _db
                        .Categories.AsNoTracking()
                        .ApplyStandardFilter(includeHidden: true)
                        .AsQueryable();

                    if (model.SearchTerm.HasValue())
                    {
                        categoryQuery = categoryQuery.Where(c =>
                            c.Name.Contains(model.SearchTerm)
                            || c.FullName.Contains(model.SearchTerm)
                        );
                    }

                    var categories = await categoryQuery.ToListAsync();

                    var fileIds = categories
                        .Select(x => x.MediaFileId ?? 0)
                        .Where(x => x != 0)
                        .Distinct()
                        .ToArray();

                    var files = (await _mediaService.GetFilesByIdsAsync(fileIds)).ToDictionarySafe(
                        x => x.Id
                    );

                    model.SearchResult = await categories
                        .SelectAwait(async x =>
                        {
                            var path = await _categoryService.GetCategoryPathAsync(
                                x,
                                Services.WorkContext.WorkingLanguage.Id,
                                "({0})"
                            );
                            var item = new EntityPickerModel.SearchResultModel
                            {
                                Id = x.Id,
                                Title = x.Name,
                                Summary = path,
                                SummaryTitle = path,
                                Published = x.Published,
                                ReturnValue = x.Id.ToString(),
                                Selected = selected.Contains(x.Id.ToString()),
                                Disable = disableIds.Contains(x.Id),
                            };

                            if (x.Alias.HasValue())
                            {
                                item.LabelText = x.Alias;
                                item.LabelClassName = "badge-secondary";
                            }

                            files.TryGetValue(x.MediaFileId ?? 0, out var file);
                            item.ImageUrl = _mediaService.GetUrl(
                                file,
                                _mediaSettings.ProductThumbPictureSizeOnProductDetailsPage,
                                null,
                                !_catalogSettings.HideProductDefaultPictures
                            );

                            return item;
                        })
                        .AsyncToList();
                }
                else if (model.EntityType.EqualsNoCase("manufacturer"))
                {
                    var manufacturerQuery = _db
                        .Manufacturers.AsNoTracking()
                        .ApplyStandardFilter(includeHidden: true)
                        .AsQueryable();

                    if (model.SearchTerm.HasValue())
                    {
                        manufacturerQuery = manufacturerQuery.Where(c =>
                            c.Name.Contains(model.SearchTerm)
                        );
                    }

                    var manufacturers = await manufacturerQuery
                        .ApplyPaging(model.PageIndex, model.PageSize)
                        .ToListAsync();

                    var fileIds = manufacturers
                        .Select(x => x.MediaFileId ?? 0)
                        .Where(x => x != 0)
                        .Distinct()
                        .ToArray();

                    var files = (await _mediaService.GetFilesByIdsAsync(fileIds)).ToDictionarySafe(
                        x => x.Id
                    );

                    model.SearchResult = manufacturers
                        .Select(x =>
                        {
                            var item = new EntityPickerModel.SearchResultModel
                            {
                                Id = x.Id,
                                Title = x.Name,
                                Published = x.Published,
                                ReturnValue = x.Id.ToString(),
                                Selected = selected.Contains(x.Id.ToString()),
                                Disable = disableIds.Contains(x.Id),
                            };

                            files.TryGetValue(x.MediaFileId ?? 0, out var file);
                            item.ImageUrl = _mediaService.GetUrl(
                                file,
                                _mediaSettings.ProductThumbPictureSizeOnProductDetailsPage,
                                null,
                                !_catalogSettings.HideProductDefaultPictures
                            );

                            return item;
                        })
                        .ToList();
                }
                else if (model.EntityType.EqualsNoCase("customer"))
                {
                    var registeredRole = await _db
                        .CustomerRoles.AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.SystemName == SystemCustomerRoleNames.Registered
                        );

                    var registeredRoleId = registeredRole.Id;

                    var customerQuery = _db.Customers.AsNoTracking().AsQueryable();

                    if (model.SearchTerm.HasValue())
                    {
                        if (model.CustomerSearchType.EqualsNoCase("Name"))
                        {
                            customerQuery = customerQuery.ApplySearchTermFilter(model.SearchTerm);
                        }
                        else if (model.CustomerSearchType.EqualsNoCase("Email"))
                        {
                            customerQuery = customerQuery.ApplyIdentFilter(
                                email: model.SearchTerm,
                                userName: model.SearchTerm
                            );
                        }
                        else if (model.CustomerSearchType.EqualsNoCase("CustomerNumber"))
                        {
                            customerQuery = customerQuery.ApplyIdentFilter(
                                customerNumber: model.SearchTerm
                            );
                        }
                    }

                    var customers = await customerQuery
                        .ApplyRolesFilter([registeredRoleId])
                        .ApplyPaging(model.PageIndex, model.PageSize)
                        .ToListAsync();

                    model.SearchResult = customers
                        .Select(x =>
                        {
                            var fullName = x.GetFullName();

                            var item = new EntityPickerModel.SearchResultModel
                            {
                                Id = x.Id,
                                ReturnValue = x.Id.ToString(),
                                Title = x.Username.NullEmpty() ?? x.Email,
                                Summary = fullName,
                                SummaryTitle = fullName,
                                Published = true,
                                Selected = selected.Contains(x.Id.ToString()),
                                Disable = disableIds.Contains(x.Id),
                            };

                            return item;
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                NotifyError(ex.ToAllMessages());
            }

            return PartialView("Picker.List", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Permission(Permissions.System.AccessBackend)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Patch(
            string entityName,
            int entityId,
            string propertyName,
            [FromBody] object value
        )
        {
            var success = false;

            try
            {
                var patch = new EntityPatch(entityName, entityId);
                patch.Properties.Add(propertyName, value);

                if (await _db.PatchEntityAsync(patch) is not null)
                {
                    await _db.SaveChangesAsync();
                    success = true;
                }
            }
            catch (Exception ex)
            {
                NotifyError(ex.ToAllMessages());
            }

            return Json(
                new
                {
                    success,
                    entityName,
                    entityId,
                    propertyName,
                }
            );
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Permission(Permissions.System.AccessBackend)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PatchLocalized(
            int languageId,
            string entityName,
            int entityId,
            string propertyName,
            [FromBody] object value
        )
        {
            var success = false;

            try
            {
                var localizedProperty = await _db
                    .LocalizedProperties.ApplyStandardFilter(
                        languageId,
                        entityId,
                        entityName,
                        propertyName
                    )
                    .FirstOrDefaultAsync();

                _locEntityService.Value.ApplyLocalizedValue(
                    localizedProperty,
                    entityId,
                    entityName,
                    propertyName,
                    value,
                    languageId
                );

                await _db.SaveChangesAsync();
                success = true;
            }
            catch (Exception ex)
            {
                NotifyError(ex.ToAllMessages());
            }

            return Json(
                new
                {
                    success,
                    languageId,
                    entityName,
                    entityId,
                    propertyName,
                }
            );
        }
    }
}
