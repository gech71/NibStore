using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-07 00:00:00", "Add StorePickupId and StorePickupName to OrderItem")]
    public class AddStorePickupToOrderItem : Migration
    {
        public override void Up()
        {
            Alter.Table("OrderItem")
              
                .AddColumn("StoreName").AsString(400).Nullable();
        }

        public override void Down()
        {
    
            Delete.Column("StoreName").FromTable("OrderItem");
        }
    }
}
