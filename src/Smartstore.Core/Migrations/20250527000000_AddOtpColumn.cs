using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2024-03-27 00:00:00", "Add OTP column to Customer")]
    public class AddOtpColumn : Migration
    {
        public override void Up()
        {
            Alter.Table("Customer")
                .AddColumn("OTPCode").AsString(20).Nullable()
                .AddColumn("OtpCodeCreationDate").AsDateTime().Nullable();
        }

        public override void Down()
        {
            Delete.Column("OTPCode").FromTable("Customer");
            Delete.Column("OtpCodeCreationDate").FromTable("Customer");
        }
    }
}