using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations
{
    [MigrationVersion("2025-06-07 01:00:00", "Add StorePickupId and StoreName columns to OrderItem")]
    public class AddStorePickupIdToOrderItem : Migration
    {
        public override void Up()
        {
            IfDatabase("postgres")
                .Execute.Sql(@"
                    DO $$
                    BEGIN
                        -- Add StorePickupId column if it does not exist
                        IF NOT EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_name = 'OrderItem'
                              AND column_name = 'StorePickupId'
                        ) THEN
                            ALTER TABLE ""OrderItem"" ADD COLUMN ""StorePickupId"" INTEGER;
                        END IF;

                        -- Add StoreName column if it does not exist
                        IF NOT EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_name = 'OrderItem'
                              AND column_name = 'StoreName'
                        ) THEN
                            ALTER TABLE ""OrderItem"" ADD COLUMN ""StoreName"" VARCHAR(400);
                        END IF;
                    END;
                    $$;
                ");
        }

        public override void Down()
        {
            Delete.Column("StorePickupId").FromTable("OrderItem");
            Delete.Column("StoreName").FromTable("OrderItem");
        }
    }
}
