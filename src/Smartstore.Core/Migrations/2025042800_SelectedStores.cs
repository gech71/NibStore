using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-04 00:00:00", "Add SelectedStore column to ShoppingCartItem")]
    public class AddSelectedStoreToShoppingCartItem : Migration
    {
        public override void Up()
        {
            Alter.Table("ShoppingCartItem")
                .AddColumn("SelectedStore").AsString(400).Nullable();
        }

        public override void Down()
        {
            Delete.Column("SelectedStore").FromTable("ShoppingCartItem");
        }
    }
}
