using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-04 14:00:00", "Core: Add store information to OrderItem")]
    public class AddStoreToOrderItem : Migration
    {
        public override void Up()
        {
            Alter.Table("OrderItem")
                .AddColumn("StoreId").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("StoreName").AsString(400).NotNullable().WithDefaultValue(string.Empty);
        }

        public override void Down()
        {
            Delete.Column("StoreId").FromTable("OrderItem");
            Delete.Column("StoreName").FromTable("OrderItem");
        }
    }
}