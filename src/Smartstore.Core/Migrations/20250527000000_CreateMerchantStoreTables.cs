using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-05-27 00:00:00", "Core: Create MerchantStore tables")]
    public class CreateMerchantStoreTables : Migration
    {
        public override void Up()
        {
            Create.Table("MerchantStore")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("Name").AsString(400).NotNullable()
                .WithColumn("Description").AsString(int.MaxValue).Nullable()
                .WithColumn("DisplayOrder").AsInt32().NotNullable()
                .WithColumn("Address").AsString(1000).Nullable()
                .WithColumn("OpeningHours").AsString(400).Nullable()
                .WithColumn("OpeningTime").AsTime().Nullable()
                .WithColumn("ClosingTime").AsTime().Nullable()
                .WithColumn("Published").AsBoolean().NotNullable()
                .WithColumn("MediaFileId").AsInt32().NotNullable();

            Create.Table("MerchantStoreMapping")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("EntityId").AsInt32().NotNullable()
                .WithColumn("EntityName").AsString(400).NotNullable()
                .WithColumn("MerchantStoreId").AsInt32().NotNullable();

            Create.ForeignKey("FK_MerchantStoreMapping_MerchantStore")
                .FromTable("MerchantStoreMapping").ForeignColumn("MerchantStoreId")
                .ToTable("MerchantStore").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            Create.Index("IX_MerchantStoreMapping_MerchantStoreId")
                .OnTable("MerchantStoreMapping")
                .OnColumn("MerchantStoreId");

            Create.Index("IX_MerchantStoreMapping_EntityId_EntityName")
                .OnTable("MerchantStoreMapping")
                .OnColumn("EntityId").Ascending()
                .OnColumn("EntityName").Ascending();
        }

        public override void Down()
        {
            Delete.ForeignKey("FK_MerchantStoreMapping_MerchantStore").OnTable("MerchantStoreMapping");
            Delete.Index("IX_MerchantStoreMapping_MerchantStoreId").OnTable("MerchantStoreMapping");
            Delete.Index("IX_MerchantStoreMapping_EntityId_EntityName").OnTable("MerchantStoreMapping");
            Delete.Table("MerchantStoreMapping");
            Delete.Table("MerchantStore");
        }
    }
}