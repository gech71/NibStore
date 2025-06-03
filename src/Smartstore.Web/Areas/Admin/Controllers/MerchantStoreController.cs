using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Admin.Models.MerchantStores;
using Smartstore.ComponentModel;
using Smartstore.Core.Content.MerchantStores;
using Smartstore.Core.Security;
using Smartstore.Web.Modelling;
using Smartstore.Web.Models.DataGrid;

namespace Smartstore.Admin.Controllers
{
    public class MerchantStoreController : AdminController
    {
        private readonly IMerchantStoreService _merchantStoreService;
        private readonly IWorkContext _workContext;
        private readonly SmartDbContext _db;

        public MerchantStoreController(IMerchantStoreService merchantStoreService, IWorkContext workContext, SmartDbContext db)
        {
            _merchantStoreService = merchantStoreService;
            _workContext = workContext;
            _db = db;
        }

        [Permission(Permissions.Catalog.MerchantStore.Read)]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(List));
        }

        [Permission(Permissions.Catalog.MerchantStore.Read)]
        public IActionResult List()
        {
            return View();
        }

        [HttpGet]
        [Permission(Permissions.Catalog.MerchantStore.Read)]
        public async Task<IActionResult> AllMerchantStores()
        {
            var stores = await _merchantStoreService.GetAllMerchantStoresAsync();

            var result = stores.Select(x => new
            {
                id = x.Id,
                text = x.Name
            });

            return Json(result);
        }

        [HttpPost]
        [Permission(Permissions.Catalog.MerchantStore.Read)]
        public async Task<IActionResult> MerchantStoreList(GridCommand command, MerchantStoreModel model)
        {
            var currentUser = _workContext.CurrentCustomer;

            var isMerchant = await _db.CustomerRoleMappings.AnyAsync(m =>
                            m.CustomerId == currentUser.Id && m.CustomerRole.SystemName == "Merchant"
                        );

            var merchantStores = await _merchantStoreService.GetAllMerchantStoresAsync(true);
            var mapper = MapperFactory.GetMapper<MerchantStore, MerchantStoreModel>();
            var merchantStoreModels = (await Task.WhenAll(merchantStores.Select(x => mapper.MapAsync(x)))).ToList();

            foreach (var storeModel in merchantStoreModels)
            {
                storeModel.EditUrl = Url.Action("Edit", "MerchantStore", new { area = "Admin", id = storeModel.Id });
            }

            if (isMerchant)
            {
                var merchantStoreIds = await _db
                        .GenericAttributes.Where(a =>
                            a.KeyGroup == "MerchantStore"
                            && a.Key == "CreatedByUserId"
                            && a.Value == currentUser.Id.ToString()
                        )
                        .Select(a => a.EntityId)
                        .ToListAsync();

                merchantStoreModels = merchantStoreModels
                    .Where(x => merchantStoreIds.Contains(x.Id))
                    .ToList();
            }

            if (model.SearchMerchantStoreName.HasValue())
            {
                merchantStoreModels = merchantStoreModels
            .Where(x => !string.IsNullOrEmpty(x.Name) &&
                        x.Name.IndexOf(model.SearchMerchantStoreName, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
            }

            var gridModel = new GridModel<MerchantStoreModel>
            {
                Rows = merchantStoreModels,
                Total = merchantStoreModels.Count
            };
            return Json(gridModel);
        }

        [Permission(Permissions.Catalog.MerchantStore.Create)]
        public IActionResult Create()
        {
            var model = new MerchantStoreModel();
            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [Permission(Permissions.Catalog.MerchantStore.Create)]
        public async Task<IActionResult> Create(MerchantStoreModel model, bool continueEditing)
        {
            if (ModelState.IsValid)
            {
                var merchantStore = await MapperFactory.MapAsync<MerchantStoreModel, MerchantStore>(model);
                await _merchantStoreService.InsertMerchantStoreAsync(merchantStore);

                var currentUser = _workContext.CurrentCustomer;

                var attribute = new GenericAttribute
                {
                    EntityId = merchantStore.Id,
                    KeyGroup = "MerchantStore",
                    Key = "CreatedByUserId",
                    Value = currentUser.Id.ToString(),
                };

                _db.GenericAttributes.Add(attribute);
                await _db.SaveChangesAsync();

                NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
                return continueEditing
                    ? RedirectToAction(nameof(Edit), new { id = merchantStore.Id })
                    : RedirectToAction(nameof(List));
            }

            return View(model);
        }

        [Permission(Permissions.Catalog.MerchantStore.Read)]
        public async Task<IActionResult> Edit(int id)
        {
            var merchantStore = await _merchantStoreService.GetMerchantStoreByIdAsync(id);
            if (merchantStore == null)
            {
                return NotFound();
            }

            var model = await MapperFactory.MapAsync<MerchantStore, MerchantStoreModel>(merchantStore);
            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [Permission(Permissions.Catalog.MerchantStore.Update)]
        public async Task<IActionResult> Edit(MerchantStoreModel model, bool continueEditing)
        {
            var merchantStore = await _merchantStoreService.GetMerchantStoreByIdAsync(model.Id);
            if (merchantStore == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Map basic properties
                await MapperFactory.MapAsync(model, merchantStore);

                // Handle map/location specific data
                merchantStore.Latitude = model.Latitude;
                merchantStore.Longitude = model.Longitude;
                merchantStore.Address = model.Address; // Make sure this is mapped

                await _merchantStoreService.UpdateMerchantStoreAsync(merchantStore);

                NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
                return continueEditing
                    ? RedirectToAction(nameof(Edit), new { id = merchantStore.Id })
                    : RedirectToAction(nameof(List));
            }

            return View(model);
        }

        [HttpPost]
        [Permission(Permissions.Catalog.MerchantStore.Delete)]
        public async Task<IActionResult> Delete(GridSelection selection)
        {
            var ids = selection.GetEntityIds();

            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "No merchant store IDs provided." });
            }

            foreach (var id in ids)
            {
                var merchantStore = await _merchantStoreService.GetMerchantStoreByIdAsync(id);
                if (merchantStore != null)
                {
                    await _merchantStoreService.DeleteMerchantStoreAsync(merchantStore);
                }
            }
            return Json(new { success = true });
        }
    }
}