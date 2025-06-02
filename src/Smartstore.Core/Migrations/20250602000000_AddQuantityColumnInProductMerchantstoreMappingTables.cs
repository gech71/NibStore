using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-02 00:01:00", "Core: Add Quantity to ProductMerchantStoreMapping")]
    public class AddQuantityToProductMerchantStoreMapping : Migration
    {
        public override void Up()
        {
            Alter.Table("Product_MerchantStore_Mapping")
                .AddColumn("Quantity").AsInt32().NotNullable().WithDefaultValue(0);
        }

        public override void Down()
        {
            Delete.Column("Quantity").FromTable("Product_MerchantStore_Mapping");
        }
    }
}