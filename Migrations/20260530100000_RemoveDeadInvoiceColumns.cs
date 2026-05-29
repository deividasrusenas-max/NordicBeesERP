using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Removes dead columns from invoices table (v0.9.3.6):
    ///   - created_by (FK invoices_ibfk_2 → users.id, also dropped)
    ///   - creator_id
    ///   - PaymentTermId1
    ///   - payment_term_id
    /// </summary>
    public partial class RemoveDeadInvoiceColumns : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // 1. Drop FK constraint on invoices.created_by (invoices_ibfk_2 → users)
            mb.Sql("ALTER TABLE `invoices` DROP FOREIGN KEY `invoices_ibfk_2`");

            // 2. Drop column invoices.created_by
            mb.Sql("ALTER TABLE `invoices` DROP COLUMN `created_by`");

            // 3. Drop column invoices.creator_id
            mb.Sql("ALTER TABLE `invoices` DROP COLUMN `creator_id`");

            // 4. Drop column invoices.PaymentTermId1
            mb.Sql("ALTER TABLE `invoices` DROP COLUMN `PaymentTermId1`");

            // 5. Drop column invoices.payment_term_id
            mb.Sql("ALTER TABLE `invoices` DROP COLUMN `payment_term_id`");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // 1. Recreate column invoices.created_by (int, nullable)
            mb.Sql("ALTER TABLE `invoices` ADD `created_by` int DEFAULT NULL AFTER `issued_by`");

            // 2. Recreate FK constraint invoices_ibfk_2 → users(id) ON DELETE SET NULL
            mb.Sql("ALTER TABLE `invoices` ADD CONSTRAINT `invoices_ibfk_2` FOREIGN KEY (`created_by`) REFERENCES `users` (`id`) ON DELETE SET NULL");

            // 3. Recreate column invoices.creator_id
            mb.Sql("ALTER TABLE `invoices` ADD `creator_id` int DEFAULT NULL AFTER `created_by`");

            // 4. Recreate column invoices.PaymentTermId1
            mb.Sql("ALTER TABLE `invoices` ADD `PaymentTermId1` int DEFAULT NULL");

            // 5. Recreate column invoices.payment_term_id
            mb.Sql("ALTER TABLE `invoices` ADD `payment_term_id` int DEFAULT NULL AFTER `created_at`");
        }
    }
}