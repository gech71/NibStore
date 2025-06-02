using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-05-14 00:00:00", "Add Latitude and Longitude to MerchantStore")]
    public class AddLatitudeLongitudeToMerchantStore : Migration
    {
        public override void Up()
        {
            Alter.Table("MerchantStore")
                .AddColumn("Latitude").AsDouble().Nullable()
                .AddColumn("Longitude").AsDouble().Nullable();
        }

        public override void Down()
        {
            Delete.Column("Latitude").FromTable("MerchantStore");
            Delete.Column("Longitude").FromTable("MerchantStore");
        }
    }
}
