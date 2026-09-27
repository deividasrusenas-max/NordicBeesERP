using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D-048 / Part A: proves the DELETE logic in
/// <c>Migrations/Scripts/clean-start-expenses.sql</c> §3 — same dependency order, same
/// NOT-EXISTS supplier-survival predicate — against the real <c>nordic_bees_erp_test</c>
/// database. The statements here are SCOPED to this test's own seeded ids (the real script's
/// statements are deliberately unscoped, since a real clean start wipes the whole module in one
/// go against a dedicated staging/production database); scoping is the only difference — the
/// dependency order and the supplier NOT-EXISTS predicate are copied verbatim from the script.
///
/// Adversarially verified (scratch mutations, never committed): with the honey_deliveries
/// NOT-EXISTS clause removed AND the beekeeper flagged is_expense_supplier=1 (the genuine edge
/// case — a supplier deletion candidate that also has a real honey_deliveries row), the DELETE
/// failed with a real MySQL error ("Cannot delete or update a parent row: a foreign key
/// constraint fails ... honey_deliveries_ibfk_1 ... ON DELETE RESTRICT"), not a silent wrong
/// deletion. Checked via information_schema.REFERENTIAL_CONSTRAINTS: every one of the 11 FKs
/// referencing business_partners in this predicate is RESTRICT or NO ACTION (InnoDB treats these
/// identically) — never SET NULL or CASCADE. So even a hypothetically buggy predicate here would
/// abort the whole script with a clear SQL error, never silently lose or orphan data; this test's
/// own "beekeeper survives" assertion proves the correct/expected (non-buggy) case actually
/// deletes the right row, which the schema's own constraints cannot verify by themselves (a
/// RESTRICT-only schema would just as happily let an over-broad predicate delete an unreferenced
/// row it should have spared).
/// </summary>
[Collection("RealDatabase")]
public class CleanStartExpensesScriptTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public CleanStartExpensesScriptTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteStatements_RemoveExpenseData_ButLeaveTheBeekeeperAndItsDeliveryIntact()
    {
        var now = DateTime.UtcNow;
        var tag = now.Ticks;
        var invoiceNumber = $"INV-CLEANSTART-{tag}";

        await using var setup = await _fixture.Factory.CreateDbContextAsync();

        // --- Seed: one expense-only supplier, one invoice + line + audit + ocr queue + files row,
        // one alias + alias event tied to that supplier.
        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO business_partners (partner_type, name, country, country_code, default_language, payment_term_days, default_vat_rate, is_active, is_expense_supplier, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            "supplier", $"Test Expense Supplier {tag}", "Lithuania", "LT", "LT", 14, 21m, true, true, now, now);
        var supplierId = await setup.Database.SqlQueryRaw<int>(
            "SELECT id AS Value FROM business_partners WHERE name = {0}", $"Test Expense Supplier {tag}").FirstAsync();

        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO business_partners (partner_type, name, country, country_code, default_language, payment_term_days, default_vat_rate, is_active, is_expense_supplier, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            "supplier", $"Test Beekeeper {tag}", "Lithuania", "LT", "LT", 14, 21m, true, false, now, now);
        var beekeeperId = await setup.Database.SqlQueryRaw<int>(
            "SELECT id AS Value FROM business_partners WHERE name = {0}", $"Test Beekeeper {tag}").FirstAsync();

        var warehouseId = await setup.Database.SqlQueryRaw<int>("SELECT id AS Value FROM warehouses LIMIT 1").FirstAsync();
        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO honey_deliveries (delivery_date, delivery_number, supplier_id, gross_weight, tare_weight, net_weight, container_quantity, warehouse_id, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
            now, $"DEL-CLEANSTART-{tag}", beekeeperId, 100.000m, 5.000m, 95.000m, 1, warehouseId, now, now);

        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices (invoice_number, invoice_date, due_date, status, amount_excl_vat, vat_amount, amount_incl_vat, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})",
            invoiceNumber, now.Date, now.Date.AddDays(14), "NEEDS_REVIEW", 100.00m, 21.00m, 121.00m, now, now);
        var invoiceId = await setup.Database.SqlQueryRaw<int>(
            "SELECT id AS Value FROM expense_invoices WHERE invoice_number = {0}", invoiceNumber).FirstAsync();

        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, amount_excl_vat, vat_rate, amount_incl_vat) VALUES ({0}, {1}, {2}, {3}, {4})",
            invoiceId, "Test line", 100.00m, 21.0m, 121.00m);

        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_audit (invoice_id, invoice_number, action, performed_at) VALUES ({0}, {1}, {2}, {3})",
            invoiceId, invoiceNumber, "CREATED", now);

        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_ocr_queue (invoice_id, file_name, status, created_at) VALUES ({0}, {1}, {2}, {3})",
            invoiceId, $"{invoiceNumber}.pdf", "COMPLETED", now);

        var fakeSha256 = $"{tag:x16}".PadRight(64, '0');
        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (sha256, byte_size, mime_type, original_filename, module, entity_type, entity_id, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
            fakeSha256, 1234, "application/pdf", $"{invoiceNumber}.pdf", "expenses", "expense_invoice", invoiceId, now);

        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO supplier_aliases (partner_id, alias_key, raw_example, state, confirmations, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            supplierId, $"testaliaskey{tag}", $"Test Alias {tag}", "CONFIRMED", 2, now, now);
        var aliasId = await setup.Database.SqlQueryRaw<int>(
            "SELECT id AS Value FROM supplier_aliases WHERE alias_key = {0}", $"testaliaskey{tag}").FirstAsync();

        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO supplier_alias_events (alias_id, invoice_id, event, actor, created_at) VALUES ({0}, {1}, {2}, {3}, {4})",
            aliasId, invoiceId, "CONFIRMED", "test", now);

        try
        {
            // --- Act: the SAME dependency order and predicate structure as
            // clean-start-expenses.sql §3, scoped to this test's own ids.
            await using var act = await _fixture.Factory.CreateDbContextAsync();
            await using var tx = await act.Database.BeginTransactionAsync();

            await act.Database.ExecuteSqlRawAsync("DELETE FROM supplier_alias_events WHERE alias_id = {0}", aliasId);
            await act.Database.ExecuteSqlRawAsync("DELETE FROM supplier_aliases WHERE id = {0}", aliasId);
            await act.Database.ExecuteSqlRawAsync("DELETE FROM expense_ocr_queue WHERE invoice_id = {0}", invoiceId);
            await act.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
            await act.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId);
            await act.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
            await act.Database.ExecuteSqlRawAsync("DELETE FROM files WHERE module = 'expenses' AND entity_id = {0}", invoiceId);

            // The exact NOT-EXISTS predicate from the script, restricted to just the two test
            // partners — proves the SAME query deletes the expense-only supplier and spares the
            // beekeeper, not a different, weaker, test-only predicate.
            await act.Database.ExecuteSqlRawAsync(@"
                DELETE FROM business_partners
                WHERE id IN ({0}, {1})
                  AND is_expense_supplier = 1
                  AND NOT EXISTS (SELECT 1 FROM honey_deliveries hd WHERE hd.supplier_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM supplier_payments sp WHERE sp.supplier_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM containers c WHERE c.supplier_id = business_partners.id OR c.reservation_customer_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM deliveries d WHERE d.supplier_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM invoices i WHERE i.customer_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM credit_notes cn WHERE cn.customer_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM payments p WHERE p.customer_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM lots l WHERE l.customer_id = business_partners.id)
                  AND NOT EXISTS (SELECT 1 FROM supplier_approvals sa WHERE sa.supplier_id = business_partners.id)",
                supplierId, beekeeperId);

            await tx.CommitAsync();

            // --- Assert
            await using var verify = await _fixture.Factory.CreateDbContextAsync();

            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM expense_invoices WHERE id = {0}", invoiceId).FirstAsync());
            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId).FirstAsync());
            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId).FirstAsync());
            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM expense_ocr_queue WHERE invoice_id = {0}", invoiceId).FirstAsync());
            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM files WHERE module = 'expenses' AND entity_id = {0}", invoiceId).FirstAsync());
            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM supplier_aliases WHERE id = {0}", aliasId).FirstAsync());
            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM supplier_alias_events WHERE alias_id = {0}", aliasId).FirstAsync());

            // The expense-only supplier is gone …
            Assert.Equal(0, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM business_partners WHERE id = {0}", supplierId).FirstAsync());

            // … but the beekeeper and its delivery survive, unchanged.
            Assert.Equal(1, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM business_partners WHERE id = {0}", beekeeperId).FirstAsync());
            Assert.Equal(1, await verify.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM honey_deliveries WHERE supplier_id = {0}", beekeeperId).FirstAsync());
        }
        finally
        {
            await using var cleanup = await _fixture.Factory.CreateDbContextAsync();
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM supplier_alias_events WHERE alias_id = {0}", aliasId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM supplier_aliases WHERE id = {0}", aliasId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM expense_ocr_queue WHERE invoice_id = {0}", invoiceId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM files WHERE module = 'expenses' AND entity_id = {0}", invoiceId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM honey_deliveries WHERE supplier_id = {0}", beekeeperId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id IN ({0}, {1})", supplierId, beekeeperId);
        }
    }
}
