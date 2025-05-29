using FluentMigrator;
using Smartstore.Core.Catalog;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Discounts;
using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.GiftCards;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Checkout.Shipping;
using Smartstore.Core.Checkout.Tax;
using Smartstore.Core.Common;
using Smartstore.Core.Common.Configuration;
using Smartstore.Core.Configuration;
using Smartstore.Core.DataExchange;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Rules;
using Smartstore.Core.Search;
using Smartstore.Core.Search.Facets;
using Smartstore.Core.Search.Indexing;
using Smartstore.Core.Seo;
using Smartstore.Data.Migrations;


namespace Smartstore.Core.Data.Migrations
{
    [MigrationVersion("2024-03-28 00:00:00", "Add RecurringOrderName column to Order")]
    public class AddRecurringOrderName : Migration
    {
        public override void Up()
        {
            Alter.Table("Order")
                .AddColumn("RecurringOrderName").AsString(100).Nullable();
        }

        public override void Down()
        {
            Delete.Column("RecurringOrderName").FromTable("Order");
        }
    }
}