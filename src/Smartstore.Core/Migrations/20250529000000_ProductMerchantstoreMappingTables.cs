using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-05-29 00:00:00", "Core: Create Product MerchantStore Mapping tables")]
    public class CreateProductMerchantStoreMappingTables : Migration
    {
        public override void Up()
        {
            Create
                .Table("Product_MerchantStore_Mapping")
                .WithColumn("Id")
                .AsInt32()
                .PrimaryKey()
                .Identity()
                .WithColumn("ProductId")
                .AsInt32()
                .NotNullable()
                .WithColumn("MerchantStoreId")
                .AsInt32()
                .NotNullable()
                .WithColumn("DisplayOrder")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0);

            Create
                .ForeignKey("FK_ProductMerchantStoreMapping_Product")
                .FromTable("Product_MerchantStore_Mapping")
                .ForeignColumn("ProductId")
                .ToTable("Product")
                .PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            Create
                .ForeignKey("FK_ProductMerchantStoreMapping_MerchantStore")
                .FromTable("Product_MerchantStore_Mapping")
                .ForeignColumn("MerchantStoreId")
                .ToTable("MerchantStore")
                .PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            Create
                .Index("IX_ProductMerchantStoreMapping_ProductId")
                .OnTable("Product_MerchantStore_Mapping")
                .OnColumn("ProductId");

            Create
                .Index("IX_ProductMerchantStoreMapping_MerchantStoreId")
                .OnTable("Product_MerchantStore_Mapping")
                .OnColumn("MerchantStoreId");
        }

        public override void Down()
        {
            Delete
                .ForeignKey("FK_ProductMerchantStoreMapping_Product")
                .OnTable("Product_MerchantStore_Mapping");
            Delete
                .ForeignKey("FK_ProductMerchantStoreMapping_MerchantStore")
                .OnTable("Product_MerchantStore_Mapping");
            Delete
                .Index("IX_ProductMerchantStoreMapping_ProductId")
                .OnTable("Product_MerchantStore_Mapping");
            Delete
                .Index("IX_ProductMerchantStoreMapping_MerchantStoreId")
                .OnTable("Product_MerchantStore_Mapping");
            Delete.Table("Product_MerchantStore_Mapping");
        }
    }
}
