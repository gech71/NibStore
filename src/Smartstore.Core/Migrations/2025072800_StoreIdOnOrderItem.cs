using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-07 01:00:00", "Add StorePickupId column to OrderItem")]
    public class AddStorePickupIdToOrderItem : Migration
    {
        public override void Up()
        {
            Alter.Table("OrderItem")
                .AddColumn("StorePickupId").AsInt32().Nullable()
                .AddColumn("StoreName").AsString(400).Nullable();
        }

        public override void Down()
        {
            Delete.Column("StorePickupId").FromTable("OrderItem");
            Delete.Column("StoreName").FromTable("OrderItem");
        }
    }
}
