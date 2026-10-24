using System.Text.RegularExpressions;
using Microsoft.Playwright;
using UglyToad.PdfPig;
using Xunit;
using Xunit.Abstractions;

namespace NordicBeesERP.Tests.Playwright;

/// <summary>Skips (visibly) while the dev DB lacks the settlement tables / OFFSET enum values - a human applies the DDL.</summary>
public sealed class FactRequiresDevSettlementTablesAttribute : FactAttribute
{
    public FactRequiresDevSettlementTablesAttribute()
    {
        try
        {
            var ok = E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('settlement_offsets','settlement_offset_lines')").GetAwaiter().GetResult() == 2
                && E2eDb.RowsAsync("SHOW COLUMNS FROM expense_payments LIKE 'payment_method'").GetAwaiter().GetResult().Any(r => ((string)r["Type"]!).Contains("OFFSET"));
            if (!ok) Skip = "Dev DB lacks settlement_offsets / OFFSET enum values: apply Migrations/Scripts/20261024_settlement_offsets.sql to nordic_bees_erp";
        }
        catch (Exception ex) { Skip = "Dev DB not reachable: " + ex.GetType().Name; }
    }
}

/// <summary>
/// FIX-004: E2E for the settlement act (užskaita) and the FIX-002 UI rules.
/// Prerequisites as for <see cref="PaymentIntegrityE2ETests"/>: own server on a free port against the DEV DB (never 5081), E2E_BASE_URL,
/// NORDICBEES_E2E_ADMIN_EMAIL / NORDICBEES_E2E_ADMIN_PASSWORD. The "persisting" tests need Migrations/Scripts/20261024_settlement_offsets.sql
/// applied to the dev DB (a human applies DDL) and are skipped until then. Seeded rows carry the number tag E2E-UZS and are removed in finally.
/// </summary>
[Trait("Category", "E2E")]
public class SettlementOffsetE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5199";
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public SettlementOffsetE2ETests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1" });
        _page = await (await _browser.NewContextAsync(new() { AcceptDownloads = true })).NewPageAsync();
        _page.SetDefaultTimeout(15_000);
    }

    public async Task DisposeAsync()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    // ───────────────────────── seed / cleanup ─────────────────────────

    private sealed record World(string Tag, int PartnerId, string PartnerName, int LakId, string LakNumber, int ExpenseId, string ExpenseNumber);

    private static async Task<World> SeedAsync(decimal lak = 120m, decimal expense = 20m)
    {
        var tag = E2eDb.NewTag();
        var name = $"E2E-UZS Partner {tag}";
        var partnerId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO business_partners (partner_type, name, country, country_code, default_language, payment_term_days, default_vat_rate, is_active, created_at, updated_at) " +
            "VALUES ('customer', @n, 'Lithuania', 'LT', 'LT', 14, 21, 1, NOW(), NOW())", ("n", name));
        var lakNumber = $"LAK-E2E-UZS-{tag}";
        var lakId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, payment_due_date, customer_id, language, invoice_type, subtotal_excl_vat, total_incl_vat, status, created_at, updated_at) " +
            "VALUES (@n, CURDATE(), DATE_ADD(CURDATE(), INTERVAL 14 DAY), @c, 'lt', 'PVM SĄSKAITA FAKTŪRA', @t, @t, 'confirmed', NOW(), NOW())",
            ("n", lakNumber), ("c", partnerId), ("t", lak));
        var expNumber = $"E2E-UZS-EXP-{tag}";
        var expId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO expense_invoices (supplier_id, invoice_number, invoice_date, due_date, amount_excl_vat, amount_incl_vat, status, created_at, updated_at) " +
            "VALUES (@s, @n, CURDATE(), DATE_ADD(CURDATE(), INTERVAL 14 DAY), @t, @t, 'PENDING', NOW(), NOW())",
            ("s", partnerId), ("n", expNumber), ("t", expense));
        return new World(tag, partnerId, name, lakId, lakNumber, expId, expNumber);
    }

    private static async Task CleanupAsync(World w)
    {
        await TryExec("DELETE FROM settlement_offset_lines WHERE offset_id IN (SELECT id FROM settlement_offsets WHERE partner_id=@p)", w);
        await TryExec("DELETE FROM settlement_offsets WHERE partner_id=@p", w);
        await E2eDb.ExecAsync("UPDATE payment_audit_log SET payment_id = NULL WHERE payment_id IN (SELECT id FROM payments WHERE customer_id=@p)", ("p", w.PartnerId));
        await E2eDb.ExecAsync("DELETE FROM payment_audit_log WHERE invoice_id=@i", ("i", w.LakId));
        await E2eDb.ExecAsync("DELETE FROM payment_allocations WHERE invoice_id=@i", ("i", w.LakId));
        await E2eDb.ExecAsync("DELETE FROM payments WHERE customer_id=@p", ("p", w.PartnerId));
        await E2eDb.ExecAsync("DELETE FROM expense_payments WHERE invoice_id=@i", ("i", w.ExpenseId));
        await E2eDb.ExecAsync("DELETE FROM expense_invoice_audit WHERE invoice_id=@i", ("i", w.ExpenseId));
        await E2eDb.ExecAsync("DELETE FROM expense_invoices WHERE id=@i", ("i", w.ExpenseId));
        await E2eDb.ExecAsync("DELETE FROM invoices WHERE id=@i", ("i", w.LakId));
        await E2eDb.ExecAsync("DELETE FROM business_partners WHERE id=@p", ("p", w.PartnerId));
    }

    private static async Task TryExec(string sql, World w)
    {
        try { await E2eDb.ExecAsync(sql, ("p", w.PartnerId)); }
        catch (MySqlConnector.MySqlException) { /* settlement tables not on this DB */ }
    }

    private static async Task<bool> SettlementTablesExistAsync() =>
        await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('settlement_offsets','settlement_offset_lines')") == 2
        && (await E2eDb.RowsAsync("SHOW COLUMNS FROM expense_payments LIKE 'payment_method'")).Any(r => ((string)r["Type"]!).Contains("OFFSET"));

    // ───────────────────────── page helpers ─────────────────────────

    private async Task LoginAsync()
    {
        var email = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_EMAIL") ?? throw new Exception("NORDICBEES_E2E_ADMIN_EMAIL not set");
        var password = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_PASSWORD") ?? throw new Exception("NORDICBEES_E2E_ADMIN_PASSWORD not set");
        await _page.GotoAsync($"{_baseUrl}/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.FillAsync("input[name=\"email\"]", email);
        await _page.FillAsync("input[name=\"password\"]", password);
        await _page.ClickAsync("form[action=\"/auth/login\"] button[type=submit]");
        await _page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });
    }

    private async Task OpenNewActAndPickPartnerAsync(World w)
    {
        await _page.GotoAsync($"{_baseUrl}/settlements/new");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle); // Blazor circuit up
        await _page.GetByLabel("Partneris").ClickAsync();
        await _page.Locator(".mud-popover-open .mud-list-item", new() { HasText = w.PartnerName }).ClickAsync();
    }

    private ILocator Row(string documentNumber) => _page.Locator("tr", new() { HasText = documentNumber });

    private async Task SetAmountAsync(string documentNumber, string text)
    {
        var input = Row(documentNumber).Locator("input").Last;
        await input.FillAsync(text);
        await input.PressAsync("Tab");
    }

    private ILocator SettleButton => _page.GetByRole(AriaRole.Button, new() { Name = "Sudengti" });
    private static readonly Regex Money20 = new(@"20,00\s€");
    private static readonly Regex Money120 = new(@"120,00\s€");

    // ───────────────────────── a) + b) validation in the UI (works without the settlement tables) ─────────────────────────

    [Fact]
    public async Task NewAct_ListsPartner_ShowsOpenAmounts_AndValidatesInTheUi()
    {
        var w = await SeedAsync();
        try
        {
            await LoginAsync();
            await _page.GotoAsync($"{_baseUrl}/settlements/new");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            // FIX-004: nothing selected → empty select with the placeholder, never "0"
            await Assertions.Expect(_page.GetByPlaceholder("Pasirinkite partnerį")).ToBeVisibleAsync();
            Assert.DoesNotContain("0", (await _page.GetByLabel("Partneris").InputValueAsync()).Trim());

            await OpenNewActAndPickPartnerAsync(w);
            // a) both tables show the documents with their open amounts
            await Assertions.Expect(Row(w.LakNumber)).ToContainTextAsync(Money120);
            await Assertions.Expect(Row(w.ExpenseNumber)).ToContainTextAsync(Money20);

            // b) unequal sums → "Sudengti" disabled
            await SetAmountAsync(w.LakNumber, "10");
            await SetAmountAsync(w.ExpenseNumber, "20");
            await Assertions.Expect(SettleButton).ToBeDisabledAsync();

            // equal sums but above the expense invoice's open balance → server error
            await SetAmountAsync(w.LakNumber, "25");
            await SetAmountAsync(w.ExpenseNumber, "25");
            await Assertions.Expect(SettleButton).ToBeEnabledAsync();
            await SettleButton.ClickAsync();
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "viršija sąskaitos" })).ToBeVisibleAsync();
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "likutį" })).ToBeVisibleAsync();

            // three decimals
            await SetAmountAsync(w.LakNumber, "10,005");
            await SetAmountAsync(w.ExpenseNumber, "10,005");
            await SettleButton.ClickAsync();
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "Sumos gali turėti ne daugiau kaip 2 skaitmenis po kablelio." })).ToBeVisibleAsync();

            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payments WHERE customer_id=@p", ("p", w.PartnerId)));
            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM expense_payments WHERE invoice_id=@i", ("i", w.ExpenseId)));
        }
        finally { await CleanupAsync(w); }
    }

    // ───────────────────────── c) – g) the full act lifecycle ─────────────────────────

    [FactRequiresDevSettlementTables]
    public async Task Act_Confirm_DbRows_InvoiceView_Pdf_Cancel()
    {
        var w = await SeedAsync();
        try
        {
            await LoginAsync();
            await OpenNewActAndPickPartnerAsync(w);
            await SetAmountAsync(w.LakNumber, "20,00");
            await SetAmountAsync(w.ExpenseNumber, "20,00");
            await SettleButton.ClickAsync();
            await _page.WaitForURLAsync(new Regex(@"/settlements/\d+$"));

            // c) act created, list shows it with status "Patvirtinta"
            var actNumber = await E2eDb.ScalarAsync<string>("SELECT offset_number FROM settlement_offsets WHERE partner_id=@p", ("p", w.PartnerId));
            Assert.Matches(@"^UZS-\d{2}-\d{4}$", actNumber);
            await _page.GotoAsync($"{_baseUrl}/settlements");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var listRow = _page.Locator("tr", new() { HasText = actNumber });
            await Assertions.Expect(listRow).ToContainTextAsync("Patvirtinta");
            await Assertions.Expect(listRow).ToContainTextAsync(Money20);

            // d) DB rows
            var pay = (await E2eDb.RowsAsync("SELECT id, amount, payment_method, source, reference_number, bank_confirmed FROM payments WHERE customer_id=@p", ("p", w.PartnerId))).Single();
            Assert.Equal("offset", pay["payment_method"]); Assert.Equal("offset", pay["source"]); Assert.Equal(actNumber, pay["reference_number"]);
            Assert.Equal(20m, Convert.ToDecimal(pay["amount"]));
            Assert.Equal(20m, await E2eDb.ScalarAsync<decimal>("SELECT SUM(allocated_amount) FROM payment_allocations WHERE invoice_id=@i", ("i", w.LakId)));
            var lak = (await E2eDb.RowsAsync("SELECT paid_amount, payment_status FROM invoices WHERE id=@i", ("i", w.LakId))).Single();
            Assert.Equal(20m, Convert.ToDecimal(lak["paid_amount"])); Assert.Equal("partial", lak["payment_status"]);
            var exp = (await E2eDb.RowsAsync("SELECT amount, payment_method, source, reference FROM expense_payments WHERE invoice_id=@i", ("i", w.ExpenseId))).Single();
            Assert.Equal("OFFSET", exp["payment_method"]); Assert.Equal("offset", exp["source"]); Assert.Equal(20m, Convert.ToDecimal(exp["amount"]));
            Assert.Equal("PAID", await E2eDb.ScalarAsync<string>("SELECT status FROM expense_invoices WHERE id=@i", ("i", w.ExpenseId)));

            // e) InvoiceView history label + edit/delete blocked
            await _page.GotoAsync($"{_baseUrl}/invoices/{w.LakId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Assertions.Expect(_page.Locator("tr", new() { HasText = $"Užskaita {actNumber}" })).ToBeVisibleAsync();
            var expected = $"Užskaitos mokėjimą galima atšaukti tik atšaukus užskaitos aktą {actNumber}.";
            var paymentRow = _page.Locator("tr", new() { HasText = $"Užskaita {actNumber}" });
            await paymentRow.Locator("button").Nth(1).ClickAsync(); // edit
            await _page.Locator(".mud-dialog").GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" }).ClickAsync();
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = expected })).ToBeVisibleAsync();
            await _page.Locator(".mud-dialog").GetByRole(AriaRole.Button, new() { Name = "Atšaukti" }).ClickAsync();
            await paymentRow.Locator("button").Last.ClickAsync(); // delete
            await _page.GetByRole(AriaRole.Button, new() { Name = "Ištrinti", Exact = true }).ClickAsync();
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = expected }).Last).ToBeVisibleAsync();
            Assert.Equal(1L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payments WHERE customer_id=@p", ("p", w.PartnerId)));

            // f) PDF
            await _page.GotoAsync($"{_baseUrl}/settlements/{await E2eDb.ScalarAsync<int>("SELECT id FROM settlement_offsets WHERE partner_id=@p", ("p", w.PartnerId))}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var download = await _page.RunAndWaitForDownloadAsync(() => _page.GetByRole(AriaRole.Button, new() { Name = "Atsisiųsti PDF" }).ClickAsync());
            Assert.EndsWith(".pdf", download.SuggestedFilename);
            var bytes = await File.ReadAllBytesAsync((await download.PathAsync())!);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            using (var doc = PdfDocument.Open(bytes))
            {
                var text = Regex.Replace(string.Join(" ", doc.GetPages().Select(p => p.Text)), @"\s+", "");
                Assert.Contains("TARPUSAVIOUŽSKAITOSAKTAS", text);
                Assert.Contains(actNumber, text);
                Assert.Contains(w.LakNumber.Replace(" ", ""), text);
                Assert.Contains(w.ExpenseNumber.Replace(" ", ""), text);
                Assert.Contains("20,00", text);
            }

            // g) cancel: no reason → error; with reason → "Atšaukta", rows gone, balances restored, act kept
            await _page.GetByRole(AriaRole.Button, new() { Name = "Atšaukti aktą" }).ClickAsync();
            var confirmCancel = _page.GetByRole(AriaRole.Button, new() { Name = "Patvirtinti atšaukimą" });
            await confirmCancel.ClickAsync(); // no reason typed
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "Nurodykite atšaukimo priežastį." })).ToBeVisibleAsync();
            Assert.Equal("confirmed", await E2eDb.ScalarAsync<string>("SELECT status FROM settlement_offsets WHERE partner_id=@p", ("p", w.PartnerId)));
            await _page.GetByLabel("Atšaukimo priežastis").FillAsync("E2E testas");
            await confirmCancel.ClickAsync();
            await Assertions.Expect(_page.GetByText("Atšaukta").First).ToBeVisibleAsync();
            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payments WHERE customer_id=@p", ("p", w.PartnerId)));
            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM expense_payments WHERE invoice_id=@i", ("i", w.ExpenseId)));
            var lakAfter = (await E2eDb.RowsAsync("SELECT paid_amount, payment_status FROM invoices WHERE id=@i", ("i", w.LakId))).Single();
            Assert.Equal(0m, Convert.ToDecimal(lakAfter["paid_amount"])); Assert.Equal("unpaid", lakAfter["payment_status"]);
            Assert.Equal("PENDING", await E2eDb.ScalarAsync<string>("SELECT status FROM expense_invoices WHERE id=@i", ("i", w.ExpenseId)));
            Assert.Equal("cancelled", await E2eDb.ScalarAsync<string>("SELECT status FROM settlement_offsets WHERE partner_id=@p", ("p", w.PartnerId)));
            Assert.Equal(2L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM settlement_offset_lines WHERE offset_id IN (SELECT id FROM settlement_offsets WHERE partner_id=@p)", ("p", w.PartnerId)));
        }
        finally { await CleanupAsync(w); }
    }

    // ───────────────────────── h) offset 20 + bank payment 100 → LAK paid ─────────────────────────

    [FactRequiresDevSettlementTables]
    public async Task Act20_ThenRegularPayment100_LakPaid()
    {
        var w = await SeedAsync();
        try
        {
            await LoginAsync();
            await OpenNewActAndPickPartnerAsync(w);
            await SetAmountAsync(w.LakNumber, "20,00");
            await SetAmountAsync(w.ExpenseNumber, "20,00");
            await SettleButton.ClickAsync();
            await _page.WaitForURLAsync(new Regex(@"/settlements/\d+$"));

            await _page.GotoAsync($"{_baseUrl}/invoices/{w.LakId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Registruoti mokėjimą" }).First.ClickAsync();
            var dialog = _page.Locator(".mud-dialog", new() { HasText = "Įvesti mokėjimą" });
            await dialog.GetByLabel("Suma (€)").FillAsync("100,00");
            await dialog.GetByLabel("Suma (€)").PressAsync("Tab");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" }).ClickAsync();
            await Assertions.Expect(dialog).ToBeHiddenAsync();

            Assert.Equal("paid", await E2eDb.ScalarAsync<string>("SELECT payment_status FROM invoices WHERE id=@i", ("i", w.LakId)));
            Assert.Equal(120m, await E2eDb.ScalarAsync<decimal>("SELECT SUM(allocated_amount) FROM payment_allocations WHERE invoice_id=@i", ("i", w.LakId)));
        }
        finally { await CleanupAsync(w); }
    }
}


/// <summary>FIX-004 item 4: FIX-002 rules on real pages (method labels, money format, zero amount rejected). Works on the current dev DB.</summary>
[Trait("Category", "E2E")]
public class Fix002UiE2ETests : IAsyncLifetime
{
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5199";
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1" });
        _page = await _browser.NewPageAsync();
        _page.SetDefaultTimeout(15_000);
    }

    public async Task DisposeAsync()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private async Task LoginAsync()
    {
        var email = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_EMAIL") ?? throw new Exception("NORDICBEES_E2E_ADMIN_EMAIL not set");
        var password = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_PASSWORD") ?? throw new Exception("NORDICBEES_E2E_ADMIN_PASSWORD not set");
        await _page.GotoAsync($"{_baseUrl}/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.FillAsync("input[name=\"email\"]", email);
        await _page.FillAsync("input[name=\"password\"]", password);
        await _page.ClickAsync("form[action=\"/auth/login\"] button[type=submit]");
        await _page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task PaymentHistory_ShowsLithuanianMethodAndMoneyFormat()
    {
        var tag = E2eDb.NewTag();
        var name = $"E2E-UZS Hist {tag}";
        var partnerId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO business_partners (partner_type, name, country, country_code, default_language, payment_term_days, default_vat_rate, is_active, created_at, updated_at) " +
            "VALUES ('customer', @n, 'Lithuania', 'LT', 'LT', 14, 21, 1, NOW(), NOW())", ("n", name));
        var invoiceId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id, language, invoice_type, total_incl_vat, paid_amount, status, created_at, updated_at) " +
            "VALUES (@n, CURDATE(), @c, 'lt', 'PVM SĄSKAITA FAKTŪRA', 5000, 1234.5, 'confirmed', NOW(), NOW())", ("n", $"LAK-E2E-UZS-{tag}"), ("c", partnerId));
        var paymentId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO payments (payment_date, customer_id, invoice_id, amount, payment_method, source, created_at, updated_at) " +
            "VALUES (CURDATE(), @c, @i, 1234.5, 'bank_transfer', 'manual', NOW(), NOW())", ("c", partnerId), ("i", invoiceId));
        await E2eDb.ExecAsync("INSERT INTO payment_allocations (payment_id, invoice_id, allocated_amount, allocated_at) VALUES (@p, @i, 1234.5, NOW())", ("p", paymentId), ("i", invoiceId));
        try
        {
            await LoginAsync();
            await _page.GotoAsync($"{_baseUrl}/payments/history");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var row = _page.Locator("tr", new() { HasText = name });
            await Assertions.Expect(row).ToBeVisibleAsync();
            await Assertions.Expect(row).ToContainTextAsync("Banko pavedimas");
            await Assertions.Expect(row).Not.ToContainTextAsync("BankTransfer");
            await Assertions.Expect(row).ToContainTextAsync(new Regex(@"1\s234,50\s€"));
        }
        finally
        {
            await E2eDb.ExecAsync("DELETE FROM payment_allocations WHERE invoice_id=@i", ("i", invoiceId));
            await E2eDb.ExecAsync("DELETE FROM payment_audit_log WHERE payment_id=@p", ("p", paymentId));
            await E2eDb.ExecAsync("DELETE FROM payments WHERE id=@p", ("p", paymentId));
            await E2eDb.ExecAsync("DELETE FROM invoices WHERE id=@i", ("i", invoiceId));
            await E2eDb.ExecAsync("DELETE FROM business_partners WHERE id=@c", ("c", partnerId));
        }
    }

    [Fact]
    public async Task ExpensePaymentDialog_ShowsLithuanianMethod_AndRejectsZeroWithoutSaving()
    {
        var tag = E2eDb.NewTag();
        var number = $"E2E-UZS-EXP-{tag}";
        var invoiceId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO expense_invoices (invoice_number, invoice_date, due_date, amount_excl_vat, amount_incl_vat, status, pending_supplier_name, created_at, updated_at) " +
            "VALUES (@n, CURDATE(), DATE_ADD(CURDATE(), INTERVAL 14 DAY), 1234.5, 1234.5, 'PENDING', @s, NOW(), NOW())", ("n", number), ("s", $"E2E-UZS Supplier {tag}"));
        await E2eDb.ExecAsync(
            "INSERT INTO expense_payments (invoice_id, payment_date, amount, payment_method, source, created_at) VALUES (@i, CURDATE(), 34.5, 'BANK', 'manual', NOW())", ("i", invoiceId));
        try
        {
            await LoginAsync();
            await _page.GotoAsync($"{_baseUrl}/expense-invoices");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await _page.GetByLabel("Paieška pagal Nr. arba tiekėją").FillAsync(number);
            var row = _page.Locator("tr", new() { HasText = number });
            await Assertions.Expect(row).ToBeVisibleAsync();
            await Assertions.Expect(row).ToContainTextAsync(new Regex(@"1\s234,50\s€")); // list money format
            await row.First.ClickAsync(new() { Position = new Position { X = 120, Y = 10 } }); // FIX-002: one click opens the detail

            await _page.GetByRole(AriaRole.Button, new() { Name = "REGISTRUOTI MOKĖJIMĄ" }).ClickAsync();
            var dialog = _page.Locator(".mud-dialog", new() { HasText = "Įvesti mokėjimą" });
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            await Assertions.Expect(dialog).ToContainTextAsync("Banko pavedimas"); // history row, not "BANK"
            await Assertions.Expect(dialog.GetByText("BANK", new() { Exact = true })).ToHaveCountAsync(0);

            await dialog.GetByLabel("Suma (€)").FillAsync("0");
            await dialog.GetByLabel("Suma (€)").PressAsync("Tab");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" }).ClickAsync();
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "Mokėjimo suma turi būti didesnė už 0." })).ToBeVisibleAsync();
            await Task.Delay(1000);
            Assert.Equal(1L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM expense_payments WHERE invoice_id=@i", ("i", invoiceId))); // only the seeded one
        }
        finally
        {
            await E2eDb.ExecAsync("DELETE FROM expense_payments WHERE invoice_id=@i", ("i", invoiceId));
            await E2eDb.ExecAsync("DELETE FROM expense_invoice_audit WHERE invoice_id=@i", ("i", invoiceId));
            await E2eDb.ExecAsync("DELETE FROM expense_invoices WHERE id=@i", ("i", invoiceId));
        }
    }
}
