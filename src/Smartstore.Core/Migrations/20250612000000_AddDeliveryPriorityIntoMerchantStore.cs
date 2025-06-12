using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-12 01:00:00", "Add DeliveryPriority On ProductMerchantStoreMapping")]
    public class AddDeliveryOnProductMerchantStoreMapping : Migration
    {
        public override void Up()
        {
            Alter
                .Table("Product_MerchantStore_Mapping")
                .AddColumn("DeliveryPriority")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(1);
        }

        public override void Down()
        {
            Delete.Column("DeliveryPriority").FromTable("Product_MerchantStore_Mapping");
        }
    }
}
