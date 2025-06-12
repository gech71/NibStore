using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core;
using Smartstore.Web.Models.Customers;

public class CustomerContactInfoViewComponent : SmartViewComponent
{
    private readonly SmartDbContext _db;
    private readonly IWorkContext _workContext;

    public CustomerContactInfoViewComponent(SmartDbContext db, IWorkContext workContext)
    {
        _db = db;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
{
    var currentCustomer = _workContext.CurrentCustomer;

    var customer = await _db.Set<Customer>()
        .Where(c => c.Id == currentCustomer.Id)
        .Select(c => new CustomerContactInfoModel
        {
            Username = c.Username,
            Phone = c.Phone
        })
        .FirstOrDefaultAsync();

    if (customer == null)
    {
        return Content("Customer not found.");
    }

    return View("Default", customer);
}

}
