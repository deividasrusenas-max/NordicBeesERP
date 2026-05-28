# NordicBeesERP — Dažniausių klaidų šablonai

## ⚠️ PRIEŠ RAŠANT KODĄ — perskaityk šį failą

---

## PATTERN 1 — MudChip visada turi T="string"

```razor
❌ BLOGAI:
<MudChip Color="Color.Error" Size="Size.Small">Tekstas</MudChip>

✅ TEISINGAI:
<MudChip T="string" Color="Color.Error" Size="Size.Small">Tekstas</MudChip>
```

---

## PATTERN 2 — MudSimpleTable NETURI Headers/Items

```razor
❌ BLOGAI:
<MudSimpleTable T="OcrLineDto">
    <Headers><th>Col</th></Headers>
    <Items>@foreach...</Items>
</MudSimpleTable>

✅ TEISINGAI — naudok plain HTML:
<table>
    <thead><tr><th>Col</th></tr></thead>
    <tbody>
        @foreach (var line in _lines)
        {
            <tr><td>@line.Description</td></tr>
        }
    </tbody>
    <tfoot><tr><td><b>Viso</b></td></tr></tfoot>
</table>
```

---

## PATTERN 3 — MudTable NEPALAIKO Footer/Column/Cell/RowCells

```razor
❌ BLOGAI:
<MudTable>
    <Column>...</Column>
    <RowCells>...</RowCells>
    <Footer>...</Footer>
</MudTable>

✅ TEISINGAI:
<MudTable T="ModelType" Items="_items" Dense="true" Hover="true" Striped="true">
    <HeaderContent>
        <MudTh>Stulpelis</MudTh>
    </HeaderContent>
    <RowTemplate Context="item">
        <MudTd>@item.Field</MudTd>
    </RowTemplate>
    <PagerContent>
        <MudTablePager PageSizeOptions="new int[]{25, 50, 100}" />
    </PagerContent>
</MudTable>
```

---

## PATTERN 4 — UI komponentai NIEKADA nerašo į DB tiesiogiai

```csharp
❌ BLOGAI — dialoge:
@inject IDbContextFactory<NordicBeesERPContext> DbFactory

private async Task SaveAsync()
{
    await using var ctx = DbFactory.CreateDbContext();
    var invoice = new ExpenseInvoice { ... };
    ctx.ExpenseInvoices.Add(invoice);
    await ctx.SaveChangesAsync();
}

✅ TEISINGAI — dialoge kviečia Service:
@inject IExpenseService ExpenseService

private async Task SaveAsync()
{
    var invoice = await ExpenseService.CreateFromOcrAsync(_ocrResult, "MANUAL");
    Snackbar.Add("Išsaugota", Severity.Success);
    MudDialog?.Close(DialogResult.Ok(invoice.Id));
}
```

**Išimtis:** `InvoiceDetailDialog` ir `AssignSupplierDialog` gali naudoti `DbFactory` TIK tiekėjų paieškai (read-only).

---

## PATTERN 5 — Interface metodų alias'ai: NEŠALINTI jei naudojami

```csharp
❌ BLOGAI — pašalini metodą ir sulaužai OcrQueueWorker:
// Pašalintas ExtractInvoiceDataAsync iš IExpenseOcrService

✅ TEISINGAI — palik alias'ą:
public interface IExpenseOcrService
{
    Task<OcrResultDto> ProcessAsync(string base64, string fileName);
    // Backward compatibility - OcrQueueWorker naudoja šį metodą
    Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName);
}

// Implementacijoje:
public async Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName)
    => await ProcessAsync(base64, fileName);
```

**Taisyklė:** Prieš šalinant interface metodą — visada patikrink `grep -rn "MethodName" .` ar kas nors jį kviečia.

---

## PATTERN 6 — Async metodai VISADA su Async sufiksu

```csharp
❌ BLOGAI:
private async Task AnalyzeInvoice() { }
private async Task SaveInvoice() { }

✅ TEISINGAI:
private async Task AnalyzeAsync() { }
private async Task SaveAsync() { }
```

---

## PATTERN 7 — [NotMapped] navigation properties — Include() NEVEIKIA

```csharp
❌ BLOGAI:
// ExpenseInvoiceLine yra [NotMapped] IR entity.Ignore() DbContext'e
// Include() NIEKADA neįkels šių eilučių
var invoice = await ctx.ExpenseInvoices
    .Include(i => i.ExpenseInvoiceLines)  // ← NEVEIKIA
    .FirstOrDefaultAsync(i => i.Id == id);

✅ TEISINGAI — krauk atskirai:
var invoice = await ExpenseService.GetInvoiceWithDetailsAsync(id);
var lines = await ExpenseService.GetInvoiceLinesAsync(id);  // ← atskiras query
```

---

## PATTERN 8 — Hardcoded reikšmės DRAUDŽIAMOS

```csharp
❌ BLOGAI:
var buyerVatCodes = new[] { "LT100013406816", "LT254724219" };
var companyName = "MB Lakštena";

✅ TEISINGAI — iš company_settings:
var settings = await _companySettingsService.GetSettingsAsync();
var buyerVatCode = settings.VatCode;
var companyName = settings.CompanyName;
```

---

## PATTERN 9 — MudDialog struktūra Razor faile

```razor
❌ BLOGAI — EditForm kaip tiesioginis MudDialog vaikas:
<MudDialog>
    <TitleContent>...</TitleContent>
    <EditForm ...>          ← KLAIDA
        <DialogContent>
        </DialogContent>
    </EditForm>
</MudDialog>

✅ TEISINGAI — EditForm VIDUJE DialogContent:
<MudDialog>
    <TitleContent>...</TitleContent>
    <DialogContent>
        <EditForm ...>
            <!-- laukai -->
        </EditForm>
    </DialogContent>
    <DialogActions>
        <MudButton ...>Atšaukti</MudButton>
        <MudButton ...>Išsaugoti</MudButton>
    </DialogActions>
</MudDialog>
```

---

## PATTERN 10 — @keyframes Razor faile reikia @@

```razor
❌ BLOGAI:
<style>
    @keyframes pulse { ... }
</style>

✅ TEISINGAI:
<style>
    @@keyframes pulse { ... }
</style>
```

---

## PATTERN 11 — payment_method išlaidose

```csharp
❌ BLOGAI:
payment_method = "bank_transfer"  // ← įplaukų modulio formatas

✅ TEISINGAI išlaidose:
payment_method = "BANK"   // arba "CASH" arba "OTHER"
```

---

## PATTERN 12 — performed_by visada iš IAuthService

```csharp
❌ BLOGAI:
var performedBy = "system";  // hardcoded

✅ TEISINGAI:
var user = await _authService.GetAuthenticatedUserAsync();
var performedBy = user?.FullName ?? user?.Email ?? "system";
```

---

## PATTERN 13 — MudChipSet ir MudChip T tipas turi sutapti

```razor
❌ BLOGAI:
<MudChipSet T="string">
    <MudChip>Tekstas</MudChip>  ← trūksta T="string"
</MudChipSet>

✅ TEISINGAI:
<MudChipSet T="string">
    <MudChip T="string">Tekstas</MudChip>
</MudChipSet>
```

---

## PATTERN 14 — Razor @if su @{ } blokais viduje else

```razor
❌ BLOGAI — sukels RZ1010 klaidą:
else
{
    @{
        var x = GetValue();
    }
    @if (x > 0) { ... }
}

✅ TEISINGAI — kintamieji tiesiai if bloke:
else
{
    @if (GetValue() > 0) { ... }
}

// Arba @{ } PRIEŠ else:
@{
    var x = GetValue();
}
@if (x > 0) { ... }
```

---

## PRIEŠ KIEKVIENĄ PAKEITIMĄ patikrink:

```bash
# Ar metodas naudojamas prieš šalinant?
grep -rn "MethodName" .

# MudChip be T=?
grep -n "MudChip" File.razor | grep -v "T="

# Tiesioginė DB prieiga UI?
grep -n "DbFactory\|new ExpenseInvoice\|CreateDbContext" Components/Dialogs/

# Hardcoded VAT kodai?
grep -rn "LT100013406816\|LT254724219" Services/

# Build klaidos (tik errors, ne warnings):
dotnet build 2>&1 | grep "error CS\|error RZ"
```
