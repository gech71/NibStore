using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Smartstore.Core.Data;

public class OrderCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public OrderCleanupService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SmartDbContext>();
                var cutoff = DateTime.UtcNow.AddMinutes(-5);
                var oldOrders = db.Orders
                    .Where(o => !o.IsRecurring && !o.Deleted && o.CreatedOnUtc < cutoff)
                    .ToList();

                foreach (var order in oldOrders)
                {
                    db.Orders.Remove(order); // Hard delete
                }
                db.SaveChanges();
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Runs every 1 minute
        }
    }
}