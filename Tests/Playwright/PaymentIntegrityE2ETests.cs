using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace NordicBeesERP.Tests.Playwright;

/// <summary>
/// E2E for payment write integrity (double-submit guard, server validation, Lithuanian decimal input,
/// delete keeps the audit history, expense payment double click).
///
/// Prerequisites: same as <see cref="HoneyTypeE2ETests"/> - dev server (dev DB, never prod) at E2E_BASE_URL
/// (default http://localhost:5081), NORDICBEES_E2E_ADMIN_EMAIL / NORDICBEES_E2E_ADMIN_PASSWORD set.
/// DB access reuses <see cref="E2eDb"/> (refuses anything but the dev database). Every test seeds its own
/// customer/supplier + invoice (number prefix "E2E-PAY-") and removes everything by id in finally.
/// </summary>
[Trait("Category", "E2E")]
public class PaymentIntegrityE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5081";
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public PaymentIntegrityE2ETests(ITestOutputHelper output) => _output = output;

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

    // ───────────────────────── customer invoices (PaymentDialog) ─────────────────────────

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PaymentDialog_RapidMultiClickOnSave_CreatesExactlyOnePayment(int clicks)
    {
        var s = await SeedCustomerInvoiceAsync();
        try
        {
            await LoginAsync();
            var dialog = await OpenPaymentDialogAsync(s);
            await dialog.GetByLabel("Suma (€)").FillAsync("100,00");
            await dialog.GetByLabel("Suma (€)").PressAsync("Tab");

            await dialog.GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" })
                .ClickAsync(new() { ClickCount = clicks, Delay = 0 });
            await Assertions.Expect(dialog).ToBeHiddenAsync();
            await Task.Delay(2500); // let any (wrong) late duplicate land

            var payments = await E2eDb.RowsAsync("SELECT id, amount FROM payments WHERE customer_id=@c", ("c", s.PartnerId));
            Assert.Single(payments);
            Assert.Equal(100m, Convert.ToDecimal(payments[0]["amount"]));
            Assert.Equal(1L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payment_allocations WHERE invoice_id=@i", ("i", s.InvoiceId)));
            Assert.Equal(1L, await E2eDb.ScalarAsync<long>(
                "SELECT COUNT(*) FROM payment_audit_log WHERE action='create' AND payment_id IN (SELECT id FROM payments WHERE customer_id=@c)", ("c", s.PartnerId)));
        }
        finally { await CleanupAsync(s); }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task PaymentDialog_ZeroOrNegativeAmount_ShowsErrorAndWritesNothing(string amountText)
    {
        var s = await SeedCustomerInvoiceAsync();
        try
        {
            await LoginAsync();
            var dialog = await OpenPaymentDialogAsync(s);
            await dialog.GetByLabel("Suma (€)").FillAsync(amountText);
            await dialog.GetByLabel("Suma (€)").PressAsync("Tab");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" }).ClickAsync();

            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "Mokėjimo suma turi būti didesnė už 0." })).ToBeVisibleAsync();
            await Task.Delay(1000);
            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payments WHERE customer_id=@c", ("c", s.PartnerId)));
            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payment_allocations WHERE invoice_id=@i", ("i", s.InvoiceId)));
        }
        finally { await CleanupAsync(s); }
    }

    [Theory]
    [InlineData("1 234,56")]
    [InlineData("1234.56")]
    public async Task PaymentDialog_AmountWithCommaOrDot_StoresExactValue(string amountText)
    {
        var s = await SeedCustomerInvoiceAsync();
        try
        {
            await LoginAsync();
            var dialog = await OpenPaymentDialogAsync(s);
            await dialog.GetByLabel("Suma (€)").FillAsync(amountText);
            await dialog.GetByLabel("Suma (€)").PressAsync("Tab");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" }).ClickAsync();
            await Assertions.Expect(dialog).ToBeHiddenAsync();

            var amount = await E2eDb.ScalarAsync<decimal>("SELECT amount FROM payments WHERE customer_id=@c", ("c", s.PartnerId));
            Assert.Equal(1234.56m, amount);
            Assert.Equal(1234.56m, await E2eDb.ScalarAsync<decimal>("SELECT SUM(allocated_amount) FROM payment_allocations WHERE invoice_id=@i", ("i", s.InvoiceId)));
        }
        finally { await CleanupAsync(s); }
    }

    [Fact]
    public async Task PaymentDialog_DeletePayment_KeepsAuditRowsAndAddsDeleteRow()
    {
        var s = await SeedCustomerInvoiceAsync();
        try
        {
            await LoginAsync();
            var dialog = await OpenPaymentDialogAsync(s);
            await dialog.GetByLabel("Suma (€)").FillAsync("100,00");
            await dialog.GetByLabel("Suma (€)").PressAsync("Tab");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" }).ClickAsync();
            await Assertions.Expect(dialog).ToBeHiddenAsync();
            var paymentId = await E2eDb.ScalarAsync<int>("SELECT id FROM payments WHERE customer_id=@c", ("c", s.PartnerId));

            // reopen: the history table now lists the payment with a delete icon
            dialog = await OpenPaymentDialogAsync(s);
            await dialog.Locator("tbody button").First.ClickAsync();
            await _page.GetByRole(AriaRole.Button, new() { Name = "Ištrinti", Exact = true }).ClickAsync();
            await Assertions.Expect(dialog).ToBeHiddenAsync();
            await Task.Delay(1000);

            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payments WHERE id=@p", ("p", paymentId)));
            Assert.Equal(0L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM payment_allocations WHERE payment_id=@p", ("p", paymentId)));
            var actions = (await E2eDb.RowsAsync("SELECT action FROM payment_audit_log WHERE payment_id=@p ORDER BY id", ("p", paymentId)))
                .Select(r => (string)r["action"]!).ToList();
            Assert.Equal(new[] { "create", "delete" }, actions);
        }
        finally { await CleanupAsync(s); }
    }

    // ───────────────────────── expense invoices (ExpensePaymentDialog) ─────────────────────────

    [Fact]
    public async Task ExpensePaymentDialog_RapidDoubleClickOnSave_CreatesExactlyOneExpensePayment()
    {
        var tag = E2eDb.NewTag();
        var number = $"E2E-PAY-{tag}";
        var invoiceId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO expense_invoices (invoice_number, invoice_date, due_date, amount_excl_vat, amount_incl_vat, status, pending_supplier_name, created_at, updated_at) " +
            "VALUES (@n, CURDATE(), DATE_ADD(CURDATE(), INTERVAL 14 DAY), 1000, 1000, 'PENDING', @s, NOW(), NOW())",
            ("n", number), ("s", $"E2E Supplier {tag}"));
        try
        {
            await LoginAsync();
            await _page.GotoAsync($"{_baseUrl}/expense-invoices");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await _page.GetByLabel("Paieška pagal Nr. arba tiekėją").FillAsync(number);
            var row = _page.Locator("tr", new() { HasText = number });
            await Assertions.Expect(row).ToBeVisibleAsync();
            await row.First.ClickAsync(new() { Position = new Position { X = 120, Y = 10 } }); // FIX-002: a single click opens the detail dialog

            await _page.GetByRole(AriaRole.Button, new() { Name = "REGISTRUOTI MOKĖJIMĄ" }).ClickAsync();
            var dialog = _page.Locator(".mud-dialog", new() { HasText = "Įvesti mokėjimą" });
            await Assertions.Expect(dialog).ToBeVisibleAsync();

            await dialog.GetByLabel("Suma (€)").FillAsync("100,00");
            await dialog.GetByLabel("Suma (€)").PressAsync("Tab");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Išsaugoti" })
                .ClickAsync(new() { ClickCount = 2, Delay = 0 });
            await Assertions.Expect(dialog).ToBeHiddenAsync();
            await Task.Delay(2500);

            Assert.Equal(1L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM expense_payments WHERE invoice_id=@i", ("i", invoiceId)));
            Assert.Equal(100m, await E2eDb.ScalarAsync<decimal>("SELECT amount FROM expense_payments WHERE invoice_id=@i", ("i", invoiceId)));
        }
        finally
        {
            await E2eDb.ExecAsync("DELETE FROM expense_payments WHERE invoice_id=@i", ("i", invoiceId));
            await E2eDb.ExecAsync("DELETE FROM expense_invoices WHERE id=@i", ("i", invoiceId));
        }
    }

    // ───────────────────────── helpers ─────────────────────────

    private sealed record PaySeed(int PartnerId, int InvoiceId);

    private static async Task<PaySeed> SeedCustomerInvoiceAsync()
    {
        var tag = E2eDb.NewTag();
        var partnerId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO business_partners (partner_type, name, country, country_code, default_language, payment_term_days, default_vat_rate, is_active, created_at, updated_at) " +
            "VALUES ('customer', @n, 'Lithuania', 'LT', 'LT', 14, 21, 1, NOW(), NOW())",
            ("n", $"E2E-PAY Customer {tag}"));
        var invoiceId = (int)await E2eDb.ExecInsertAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id, language, invoice_type, total_incl_vat, status, created_at, updated_at) " +
            "VALUES (@n, CURDATE(), @c, 'lt', 'PVM SĄSKAITA FAKTŪRA', 5000, 'confirmed', NOW(), NOW())",
            ("n", $"E2E-PAY-{tag}"), ("c", partnerId));
        return new PaySeed(partnerId, invoiceId);
    }

    private static async Task CleanupAsync(PaySeed s)
    {
        await E2eDb.ExecAsync("DELETE FROM payment_audit_log WHERE payment_id IN (SELECT id FROM payments WHERE customer_id=@c)", ("c", s.PartnerId));
        await E2eDb.ExecAsync("DELETE FROM payment_allocations WHERE invoice_id=@i", ("i", s.InvoiceId));
        await E2eDb.ExecAsync("DELETE FROM payments WHERE customer_id=@c", ("c", s.PartnerId));
        await E2eDb.ExecAsync("DELETE FROM invoices WHERE id=@i", ("i", s.InvoiceId));
        await E2eDb.ExecAsync("DELETE FROM business_partners WHERE id=@c", ("c", s.PartnerId));
    }

    private async Task LoginAsync()
    {
        var email = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_EMAIL")
            ?? throw new Exception("NORDICBEES_E2E_ADMIN_EMAIL not set");
        var password = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_PASSWORD")
            ?? throw new Exception("NORDICBEES_E2E_ADMIN_PASSWORD not set");
        await _page.GotoAsync($"{_baseUrl}/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.FillAsync("input[name=\"email\"]", email);
        await _page.FillAsync("input[name=\"password\"]", password);
        await _page.ClickAsync("form[action=\"/auth/login\"] button[type=submit]");
        await _page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });
    }

    private async Task<ILocator> OpenPaymentDialogAsync(PaySeed s)
    {
        await _page.GotoAsync($"{_baseUrl}/invoices/{s.InvoiceId}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle); // Blazor circuit up: clicks on the prerendered page do nothing
        await _page.GetByRole(AriaRole.Button, new() { Name = "Registruoti mokėjimą" }).First.ClickAsync();
        var dialog = _page.Locator(".mud-dialog", new() { HasText = "Įvesti mokėjimą" });
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        _output.WriteLine($"dialog open for invoice {s.InvoiceId}");
        return dialog;
    }
}
