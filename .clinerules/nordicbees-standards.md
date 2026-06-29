# NordicBeesERP — Pilni UI/UX ir Kodo Standartai

## ⚠️ PRIVALOMA perskaityti prieš bet kokį kūrimą ar pakeitimą

---

## 1. PROJEKTO KONTEKSTAS

**Stack:** .NET 10, Blazor Server, MudBlazor, MariaDB  
**Verslas:** MB Lakštena — bitininkystės produktų gamyba ir pardavimas  
**Kalba:** UI lietuviškai, kodas angliškai, commit'ai angliškai  
**Stilius:** Vienodas visame projekte — bazė yra įplaukų modulis (`PaymentsDashboard.razor`)

---

## 2. PRANEŠIMŲ SISTEMA (Snackbar)

**VISADA naudoti Snackbar** — ne alert, ne dialogas teksto pranešimams:

```csharp
Snackbar.Add("Sąskaita išsaugota sėkmingai", Severity.Success);   // žalia
Snackbar.Add("Klaida išsaugant: " + ex.Message, Severity.Error);  // raudona
Snackbar.Add("Sąskaita gali būti dublikatas", Severity.Warning);  // geltona
Snackbar.Add("Duomenys atnaujinti", Severity.Info);               // mėlyna
```

**Taisyklė:** NIEKADA nerodyti klaidų tik į Console. Visada Snackbar vartotojui. Visi pranešimai **lietuviškai**.

---

## 3. TRYNIMO LOGIKA

**VISADA** trinimas reikalauja patvirtinimo dialogo:

```csharp
private async Task DeleteItemAsync(int id, string itemName)
{
    var confirmed = await DialogService.ShowMessageBox(
        "Patvirtinti trynimą",
        $"Ar tikrai norite ištrinti \"{itemName}\"? Šio veiksmo negalima atšaukti.",
        yesText: "Ištrinti",
        cancelText: "Atšaukti");

    if (confirmed != true) return;

    try
    {
        await Service.DeleteAsync(id);
        Snackbar.Add($"\"{itemName}\" ištrinta sėkmingai", Severity.Success);
        await LoadDataAsync();
    }
    catch (Exception ex)
    {
        Snackbar.Add("Klaida trinant: " + ex.Message, Severity.Error);
    }
}
```

**Finansiniai įrašai** (sąskaitos, mokėjimai) — niekada neištrinti fiziškai. Tik `soft delete` arba `CANCELLED` statusas su audit log.

---

## 4. JOKIŲ HARDCODED REIKŠMIŲ

**Viskas iš DB arba nustatymų:**

```csharp
// ✅ TEISINGAI — Lakštenos rekvizitai iš company_settings
var settings = await _companySettingsService.GetSettingsAsync();
var buyerVatCode = settings.VatCode;
var buyerCompanyCode = settings.CompanyCode;
var buyerName = settings.CompanyName;

// ❌ DRAUDŽIAMA
var buyerVatCodes = new[] { "LT100013406816", "LT254724219" };
```

**Kategorijos** → `expense_categories` lentelė  
**Projektai** → `expense_projects` lentelė  
**Azure DI kredencialai** → `app_settings` lentelė  
**Visi konfigūruojami dalykai** → nustatymų puslapiai

---

## 5. DB SUDERINAMUMAS

**Prieš rašant kodą** — visada patikrinti DB schemą.

### expense_invoices — esami laukai:
```
id, supplier_id, invoice_type, pending_supplier_name, pending_supplier_vat,
pending_supplier_address, pending_supplier_company_code, pending_supplier_bank_account,
pending_supplier_city, pending_supplier_postal_code, pending_supplier_country_code,
invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount,
amount_incl_vat, paid_amount, status, ocr_status, ocr_confidence, ocr_flags,
ocr_raw_json, notes, approved_by, approved_at, rejected_reason, source,
original_filename, ocr_pipeline, created_at, updated_at, currency,
original_file_path, supplier_vat_verified, supplier_vat_verified_name, category_id
```

### expense_invoice_lines — esami laukai:
```
id, invoice_id, category_id, description, quantity, unit_price,
unit_of_measure, amount_excl_vat, vat_rate, amount_incl_vat, sort_order
```

### expense_invoice_audit — esami laukai:
```
id, invoice_id, invoice_number, action, action_details,
old_status VARCHAR(30), new_status VARCHAR(30), performed_by, performed_at
```

### expense_payments — payment_method ENUM:
```sql
payment_method ENUM('BANK','CASH','OTHER')
```
⚠️ NE `'bank_transfer'` — tai įplaukų modulio formatas!

### company_settings — Lakštenos rekvizitai:
```
company_name, company_code, vat_code, address, bank_name, bank_iban
```

---

## 6. AUDIT LOG

**Kiekvienas statusų pakeitimas** turi būti įrašytas į `expense_invoice_audit`.  
`performed_by` **visada iš autentifikacijos** — ne hardcoded:

```csharp
var currentUser = await _authService.GetAuthenticatedUserAsync();
var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "system";
```

**Audit actions:** `CREATED`, `STATUS_CHANGED`, `SUPPLIER_ASSIGNED`, `PAYMENT_ADDED`, `APPROVED`, `REJECTED`, `EDITED`

---

## 7. NAVIGATION PROPERTIES — SVARBU

`ExpenseInvoice` turi `[NotMapped]` ant visų navigation properties IR `entity.Ignore()` DbContext'e:

**TAISYKLĖ:** NIEKADA nenaudoti `Include()` eilutėms — neveikia!

```csharp
// ❌ BLOGAI — Include() su [NotMapped] NEVEIKIA
var invoice = await ctx.ExpenseInvoices
    .Include(i => i.ExpenseInvoiceLines)  // ← NIEKO NEĮKELIA
    .FirstOrDefaultAsync(i => i.Id == id);

// ✅ TEISINGAI — krauti eilutes atskirai
var invoice = await ExpenseService.GetInvoiceWithDetailsAsync(id);
var lines = await ExpenseService.GetInvoiceLinesAsync(id);
```

---

## 8. OCR CONFIDENCE — SVERTINIS VIDURKIS

```csharp
// Weights: Amounts=30%, InvoiceNumber=25%, SupplierName=25%, InvoiceDate=20%
// DueDate — bonus, neįeina į Overall
public int Overall
{
    get
    {
        var weighted = new (int value, int weight)[]
        {
            (Amounts, 30), (InvoiceNumber, 25), (SupplierName, 25), (InvoiceDate, 20)
        };
        var relevant = weighted.Where(x => x.value > 0).ToList();
        if (!relevant.Any()) return 0;
        return relevant.Sum(x => x.value * x.weight) / relevant.Sum(x => x.weight);
    }
}
```

---

## 9. DUBLIKATŲ TIKRINIMAS

```csharp
// Tikrinti pagal invoice_number + amount_incl_vat, nepriklausomai nuo supplier
// Tas pats invoice_number + ta pati suma = dublikatas
var existing = await ctx.ExpenseInvoices
    .Where(e => e.InvoiceNumber == invoiceNumber
             && e.InvoiceNumber != ""
             && e.Status != "REJECTED"
             && e.Status != "DUPLICATE_PENDING"
             && Math.Abs(e.AmountInclVat - amountInclVat) < 0.01m)
    .Select(e => e.Id)
    .FirstOrDefaultAsync();
```

---

## 10. PUSLAPIŲ STRUKTŪRA

```razor
@page "/puslapis"
@rendermode InteractiveServer
@using NordicBeesERP.Models
@using NordicBeesERP.Services
@using MudBlazor
@inject IService Service
@inject ISnackbar Snackbar
@inject IDialogService DialogService

<PageTitle>Pavadinimas</PageTitle>
<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h5" Class="mb-4">Puslapio pavadinimas</MudText>
    @if (_loading) { <MudProgressCircular Indeterminate="true" /> }
    else { <!-- KPI kortelės, filtrai, lentelė --> }
</MudContainer>
```

---

## 11. KPI KORTELIŲ SPALVŲ SISTEMA

```razor
<MudGrid Class="mb-4">
    <!-- MĖLYNA: bendri duomenys -->
    <MudItem xs="6" md="3">
        <MudPaper Elevation="0" Class="pa-4" Style="background:#dbeafe; border-radius:12px">
            <MudText Typo="Typo.caption">Antraštė</MudText>
            <MudText Typo="Typo.h4" Style="font-weight:bold">@_suma.ToString("N2")</MudText>
        </MudPaper>
    </MudItem>
    <!-- GELTONA: įspėjimai -->    background:#fef3c7  tekstas:#854d0e
    <!-- VIOLETINĖ: secondary -->  background:#f5f3ff  tekstas:#5b21b6
    <!-- RAUDONA: klaidos -->      background:#fef2f2  tekstas:#991b1b
    <!-- ŽALIA: sėkmė -->          background:#f0fdf4  border:#86efac
    <!-- ORANŽINĖ: perspėjimai --> background:#fff7ed  border:#fdba74
</MudGrid>
```

---

## 12. FILTRAI — INSTANT SEARCH

```razor
<MudPaper Class="pa-3 mb-4" Elevation="0" Style="background:#f8fafc; border-radius:8px">
    <MudGrid>
        <MudItem xs="12" sm="6">
            <MudTextField Value="@_filterSearch"
                          ValueChanged="@(async (string val) => { _filterSearch = val; await LoadDataAsync(); })"
                          Label="Paieška..." Variant="Variant.Outlined"
                          Immediate="true" FullWidth="@true" Clearable="true"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          Adornment="Adornment.Start" />
        </MudItem>
    </MudGrid>
</MudPaper>
```

---

## 13. LENTELIŲ STILIUS

```razor
<MudTable T="ModelType" Items="_items" Dense="true" Hover="true" Striped="true" RowsPerPage="25">
    <HeaderContent><MudTh>Stulpelis</MudTh></HeaderContent>
    <RowTemplate Context="item"><MudTd>@item.Field</MudTd></RowTemplate>
    <PagerContent><MudTablePager PageSizeOptions="new int[]{25, 50, 100}" /></PagerContent>
    <NoRecordsContent><MudText Class="text-center py-4">Įrašų nėra</MudText></NoRecordsContent>
</MudTable>
```

---

## 14. DIALOGŲ STANDARTAS

```razor
<MudDialog>
    <TitleContent>
        <div class="d-flex align-center gap-2">
            <MudIcon Icon="@Icons.Material.Filled.ICON" Color="Color.Primary" />
            <MudText Typo="Typo.h6">Pavadinimas</MudText>
        </div>
    </TitleContent>
    <DialogContent>
        @if (_isLoading) { <MudProgressCircular Indeterminate="true" /> }
        else { <div style="min-width:480px;"><!-- turinys --></div> }
    </DialogContent>
    <DialogActions>
        <MudButton Variant="Variant.Text" Color="Color.Default" OnClick="CancelAsync">Atšaukti</MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="SaveAsync" StartIcon="@Icons.Material.Filled.Save">Išsaugoti</MudButton>
    </DialogActions>
</MudDialog>
```

### Sekcijų spalvos:
| Spalva | Style | Naudojimas |
|--------|-------|-----------|
| Mėlyna | `background:#f0f4ff; border-left:4px solid #1976d2` | Informacija |
| Geltona | `background:#fff8e1; border-left:4px solid #f9a825` | OCR/AI duomenys |
| Žalia | `background:#e8f5e9; border-left:4px solid #388e3c` | Sėkmė |
| Oranžinė | `background:#fff3e0; border-left:4px solid #f57c00` | Įspėjimai |
| Raudona | `background:#fef2f2; border-left:4px solid #dc2626` | Klaidos |

---

## 15. IKONŲ SISTEMA

| Kontekstas | Ikona |
|-----------|-------|
| Tiekėjas | `Icons.Material.Filled.Business` |
| Mokėjimas | `Icons.Material.Filled.Payments` |
| Sąskaita | `Icons.Material.Filled.Receipt` |
| Įkelti | `Icons.Material.Filled.CloudUpload` |
| AI/OCR | `Icons.Material.Filled.SmartToy` |
| Patvirtinti | `Icons.Material.Filled.CheckCircle` |
| PDF | `Icons.Material.Filled.PictureAsPdf` |

---

## 16. STATUSŲ SPALVOS (Išlaidos)

```csharp
public static Color GetColor(string? status) => status switch
{
    "PENDING"           => Color.Warning,
    "PENDING_SUPPLIER"  => Color.Error,
    "NEEDS_REVIEW"      => Color.Warning,
    "DUPLICATE_PENDING" => Color.Error,
    "REJECTED"          => Color.Dark,
    "PARTIAL"           => Color.Info,
    "PAID"              => Color.Success,
    "APPROVED"          => Color.Tertiary,
    "APPROVED_PAID"     => Color.Success,
    _                   => Color.Default
};
```

---

## 17. METŲ FILTRAS

```csharp
private bool _showAllYears = false;
var y1 = new DateTime(DateTime.Today.Year, 1, 1);
var filtered = _showAllYears ? all : all.Where(i => i.Date >= y1).ToList();
```

---

## 18. IŠLAIDŲ MODULIO SPECIFIKA

### Sąskaitos tipai:
- `STANDARD` — įmonės pirkimo sąskaitos (kuras, paslaugos, medžiagos ir kt.)
- `ULAK` — bičių produktų pirkimo sąskaitos iš ūkininkų

### ⚠️ ULAK sąskaitų taisyklės (SVARBU):
- ULAK sąskaitos yra **bičių produktų pirkimas iš fizinių asmenų (bitininkų)**
- ULAK PVM tarifas yra **6%** — NE 0%
- ULAK tiekėjai yra fiziniai asmenys, ne įmonės
- ULAK sąskaitos generuojamos sistemoje specialiu būdu — NEKEISTI esamos logikos
- `ZERO_VAT` flag NEREIKIA generuoti ULAK sąskaitoms — tai nėra klaida
- ULAK modulis jau veikia — nekeisti be specialaus leidimo

### Statusų workflow:
```
GAVIMAS → validacija → PENDING_SUPPLIER / NEEDS_REVIEW / DUPLICATE_PENDING / REJECTED / PENDING
         → mokėjimas → PARTIAL / PAID
         → patvirtinimas → APPROVED / APPROVED_PAID
```

### OCR Flags:
```
VENDOR_NOT_FOUND    → "Nežinomas tiekėjas"      (raudona)
WRONG_RECIPIENT     → "Ne MB Lakštenai"          (raudona) ← KRITINIS
MISSING_AMOUNT      → "Trūksta sumos"            (geltona)
MISSING_INV_NUMBER  → "Trūksta numerio"          (geltona)
MISSING_DUE_DATE    → "Trūksta termino"          (pilka)
ZERO_VAT            → "PVM = 0%"                 (geltona) ← NE ULAK sąskaitoms
LINES_NOT_FOUND     → "Eilutės nerastos"         (pilka)
AMOUNT_MISMATCH     → "Sumos nesutampa"          (raudona) ← KRITINIS
LOW_CONFIDENCE      → "Žemas tikslumas"          (geltona)
DUPLICATE           → "Dublikatas"               (raudona)
VIES_UNAVAILABLE    → "VIES nepasiekiamas"       (pilka)
AZURE_LIMIT         → "Azure limitas viršytas"   (raudona)
```

### Kritiniai patikrinimai:
1. `CustomerName`/`CustomerTaxId` vs `company_settings.vat_code` → `WRONG_RECIPIENT`
2. `SUM(lines.amount_incl_vat)` vs `header.amount_incl_vat` → `AMOUNT_MISMATCH` jei > 0.01€
3. Dublikatas: `invoice_number + amount_incl_vat` → `CheckDuplicateAsync` (nepriklausomai nuo supplier)
4. VIES timeout: max 10s → `VIES_UNAVAILABLE`
5. Azure DI HTTP 429 → `AZURE_LIMIT`

### payment_method išlaidose:
```
'BANK'  → Banko pavedimas
'CASH'  → Grynais
'OTHER' → Kita
```

---

## 19. ARCHITEKTŪROS PRINCIPAI

```
UI (Blazor)   → tik rodymas ir vartotojo įvestis
Service       → visa verslo logika, validacija, audit log
DB (EF Core)  → tik CRUD
```

- **UI NIEKADA** nerašo tiesiogiai į DB
- **`await using var ctx`** — visada, ne `using var ctx`
- **Audit log** → kiekvienam statusų pakeitimui
- **`performed_by`** → visada iš `IAuthService`
- **Navigation properties** → `GetInvoiceLinesAsync` atskirai, ne `Include()`

---

## 20. DRAG & DROP FAILŲ ĮKĖLIMAS

Naudojamas `wwwroot/js/dropzone.js` su `setupDropZone` funkcija:

```javascript
// Dviejų žingsnių metodas:
// 1. JS → OnFileDropped (tik metadata: name, size, type)
// 2. Blazor → getDropFileBase64() → base64 turinys
window.setupDropZone = (dotNetHelper, elementId) => { ... }
window.getDropFileBase64 = async (elementId) => { ... }
window.cleanupDropZone = (elementId) => { ... }
```

Blazor pusėje:
- `OnAfterRenderAsync` → kviečia `setupDropZone` su retry logika (5x, 200ms tarpai)
- `[JSInvokable] OnFileDropped` → gauna metadata, tada kviečia `getDropFileBase64`
- `DroppedFile : IBrowserFile` → wrapper klasė nutemptiems failams
- `IAsyncDisposable` → `DisposeAsync` kviečia `cleanupDropZone`

⚠️ Drop zona turi būti `<div id="expense-drop-zone">` su `pointer-events:none` ant visų vaikų išskyrus MudFileUpload mygtuką.

---

## 21. GIT COMMIT KONVENCIJOS

```
feat: add expense category auto-assignment
fix: resolve duplicate detection with null supplier_id
refactor: move OCR logic to ExpenseOcrService
docs: update standards
```

---

## 22. DRAUDŽIAMA ❌

- Hardcoded VAT kodai, įmonių pavadinimai, nustatymai
- `Color.Success` pagrindiniams mygtukams
- Logika UI komponentuose
- Tiesioginiai DB kvietimai iš UI (išskyrus tiekėjų paiešką dialoguose)
- `using var ctx` (visada `await using var ctx`)
- Dialogai be `TitleContent` su ikona
- Lentelės be `PagerContent`
- Filtrai su "Filtruoti/Valyti" mygtukais
- `Console.WriteLine` produkciniame kode
- Keli dialogų langai vienam veiksmui
- Fizinis trynimas finansiniams dokumentams
- Pranešimai ne per Snackbar
- Trynimas be patvirtinimo dialogo
- `Include()` eilutėms — naudoti `GetInvoiceLinesAsync`
- `'bank_transfer'` išlaidų mokėjimuose — naudoti `'BANK'`
- `performed_by = "system"` hardcoded — visada iš `IAuthService`
- ULAK sąskaitų logikos keitimas be specialaus leidimo
- `ZERO_VAT` flag generavimas ULAK sąskaitoms (jų PVM = 6%, tai normalu)

---

## VERSIJŲ SISTEMA

Dabartinė versija: v0.9.3.14

fix (klaidos taisymas) → patch: v0.9.3.x
feat (naujas funkcionalumas) → minor: v0.9.x.0
refactor/chore → patch: v0.9.3.x

Kiekvienas commit PRIVALO tureti versijos zyme pabaigoje.

## SKAIČIŲ FORMATAVIMAS

Kiekiai: naudoti G29 formatą - nerodo nulių po kablelio kai jų nėra
Pvz: 2688.000 → rodo "2688", 2688.500 → rodo "2688.5"
Suma (EUR): visada 2 skaičiai po kablelio - N2 formatas
PVM %: naudoti G29 - nerodo 0.0% kai 0

Helper metodas (prideti i kiekviena komponenta arba i bendrą helper klase):
private string FormatQuantity(decimal qty) => qty == Math.Floor(qty) ? qty.ToString("N0") : qty.ToString("G29");
private string FormatVatRate(decimal rate) => rate == Math.Floor(rate) ? rate.ToString("N0") + "%" : rate.ToString("G29") + "%";

## EF CORE UPDATE PATTERN

VISADA naudoti ExecuteSqlRawAsync update operacijoms - NE FindAsync + modify + SaveChanges.
SaveChangesAsync naudoti TIK naujiems INSERT operacijoms.
Pvz: await context.Database.ExecuteSqlRawAsync("UPDATE ... WHERE id = {0}", ..., id);

## MIGRATION FAILO SINCHRONIZAVIMAS — PRIVALOMA ⚠️

**Vienintelis migration failas:** `Migrations/20260602150000_InitialCreate.cs` — jis yra source of truth visai DB schemai (naudoja `CREATE TABLE IF NOT EXISTS` + `SET FOREIGN_KEY_CHECKS=0/1`, lentelės sutvarkytos pagal FK dependencies tvarka).

**TAISYKLĖ:** Jei kuriamas naujas C# modelis su `[Table("...")]`, arba jei prie esamo modelio pridedama nauja `[Column("...")]` savybė — **TAME PAČIAME commit'e** privalu atnaujinti `Migrations/20260602150000_InitialCreate.cs`:
- Nauja lentelė → pridėti `CREATE TABLE IF NOT EXISTS` bloką prieš `SET FOREIGN_KEY_CHECKS=1;` eilutę (arba ankstesnėje vietoje, jei kitos lentelės turi FK į ją)
- Nauja kolona → pridėti `ALTER TABLE` arba įterpti koloną į esamą `CREATE TABLE IF NOT EXISTS` bloką

**DRAUDŽIAMA:**
- Kurti atskirus `.sql` failus `Migrations/Archive/` ar kitur ir manyti, kad jie bus pritaikyti automatiškai — `Program.cs` kviečia tik `db.Database.MigrateAsync()`, kuris vykdo TIK `Migrations/20260602150000_InitialCreate.cs`
- Palikti modelį/koloną kode be atitinkamo SQL migration faile — tai sukelia runtime klaidas (`Unknown column`, `Table doesn't exist`) tik per deployment, ne lokaliai

**Prieš commit'inant naują modelį/koloną** — patikrinti:
```bash
grep -c "CREATE TABLE IF NOT EXISTS" Migrations/20260602150000_InitialCreate.cs
```
Ir patvirtinti, kad naujos lentelės/kolonos tikrai yra ten, ne tik C# modelyje.
