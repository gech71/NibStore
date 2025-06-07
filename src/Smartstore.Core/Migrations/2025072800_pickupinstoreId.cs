using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-07 00:00:00", "Add PickupStoreId column to ShoppingCartItem")]
    public class AddPickupStoreIdToShoppingCartItem : Migration
    {
        public override void Up()
        {
            Alter.Table("ShoppingCartItem")
                .AddColumn("PickupStoreId").AsInt32().Nullable();
        }

        public override void Down()
        {
            Delete.Column("PickupStoreId").FromTable("ShoppingCartItem");
        }
    }
}
