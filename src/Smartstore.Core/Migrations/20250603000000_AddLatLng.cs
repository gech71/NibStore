using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-03 00:00:00", "Add ByGroundLatLng column to Order")]
    public class AddByGroundAddressesLatLngToOrder : Migration
    {
        public override void Up()
        {
            Alter.Table("Order")
                .AddColumn("ByGroundLatitude").AsString(255).Nullable()
                .AddColumn("ByGroundLongitude").AsString(255).Nullable();
        }

        public override void Down()
        {
            Delete.Column("ByGroundLatitude").FromTable("Order");
            Delete.Column("ByGroundLongitude").FromTable("Order");
        }
    }
}