using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-09 01:00:00", "Add NOT NULL unique Phone column to Customer table")]
    public class AddPhoneToCustomer : Migration
    {
        public override void Up()
        {
            Alter.Table("Customer").AddColumn("Phone").AsString(100).Nullable();

            Create
                .Index("IX_Customer_Phone")
                .OnTable("Customer")
                .OnColumn("Phone")
                .Unique()
                .WithOptions()
                .NonClustered();
        }

        public override void Down()
        {
            Delete.Index("IX_Customer_Phone").OnTable("Customer");
            Delete.Column("Phone").FromTable("Customer");
        }
    }
}