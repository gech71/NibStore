using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-05-28 00:00:00", "Add ByGroundAddress column to Order")]
    public class AddByGroundAddressToOrder : Migration
    {
        public override void Up()
        {
            Alter.Table("Order")
                .AddColumn("ByGroundAddress").AsString(255).Nullable();
        }

        public override void Down()
        {
            Delete.Column("ByGroundAddress").FromTable("Order");
        }
    }
}
