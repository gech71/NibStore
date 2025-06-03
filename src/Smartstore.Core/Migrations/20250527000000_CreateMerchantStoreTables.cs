using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-05-27 00:00:00", "Core: Create MerchantStore tables")]
    public class CreateMerchantStoreTables : Migration
    {
        public override void Up()
        {
            Create
                .Table("MerchantStore")
                .WithColumn("Id")
                .AsInt32()
                .PrimaryKey()
                .Identity()
                .WithColumn("Name")
                .AsString(400)
                .NotNullable()
                .WithColumn("DisplayOrder")
                .AsInt32()
                .NotNullable()
                .WithColumn("Address")
                .AsString(1000)
                .Nullable()
                .WithColumn("OpeningHours")
                .AsString(400)
                .Nullable()
                .WithColumn("OpeningTime")
                .AsTime()
                .Nullable()
                .WithColumn("ClosingTime")
                .AsTime()
                .Nullable()
                .WithColumn("Published")
                .AsBoolean()
                .NotNullable()
                .WithColumn("MediaFileId")
                .AsInt32()
                .NotNullable();
        }

        public override void Down()
        {
            Delete.Table("MerchantStore");
        }
    }
}
