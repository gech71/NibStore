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
        private readonly SmartDbContext _db;

        public MerchantStoreController(IMerchantStoreService merchantStoreService, SmartDbContext db)
        {
            _merchantStoreService = merchantStoreService;
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

        [HttpPost]
        [Permission(Permissions.Catalog.MerchantStore.Read)]
        public async Task<IActionResult> MerchantStoreList(GridCommand command)
        {
            var merchantStores = await _merchantStoreService.GetAllMerchantStoresAsync(true);
            var mapper = MapperFactory.GetMapper<MerchantStore, MerchantStoreModel>();
            var merchantStoreModels = await Task.WhenAll(
                merchantStores.Select(async x => await mapper.MapAsync(x)));

            var gridModel = new GridModel<MerchantStoreModel>
            {
                Rows = merchantStoreModels,
                Total = merchantStoreModels.Count()
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
                await MapperFactory.MapAsync(model, merchantStore);
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
        public async Task<IActionResult> Delete(int id)
        {
            var merchantStore = await _merchantStoreService.GetMerchantStoreByIdAsync(id);
            if (merchantStore == null)
            {
                return NotFound();
            }

            await _merchantStoreService.DeleteMerchantStoreAsync(merchantStore);

            NotifySuccess(T("Admin.Common.TaskSuccessfullyProcessed"));
            return RedirectToAction(nameof(List));
        }
    }
}