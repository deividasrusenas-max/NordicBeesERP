# NordicBees ERP — UI Standartas: Sąrašo puslapiai

> Referensas: `ExpenseInvoices.razor` + `Suppliers.razor` + **`/invoices/sales` (Sales Invoices)**
> Visi sąrašo puslapiai TURI atitikti šį standartą.
>
> **PRIVALOMA VISIEMS NAUJIEMS PUSLAPIAMS** (2026-08-18): Kuriant BET KOKĮ naują sąrašo/index tipo puslapį, filtrų juosta TURI atitikti `/invoices/sales` puslapio pattern'ą iš karto — search laukas, data range (jei aktualu), status chip filtrai (jei 3+ statusai), quick-filter tabs (jei loginiai pogrupiai), "Išvalyti filtrus" veiksmas. Neišradinėti naujo filtrų UI stiliaus. Žr. `Docs/FILTER_STANDARDIZATION_PLAN.md` — esamų puslapių audito rezultatai pagal šį standartą.

---

## 1. Puslapio struktūra (tvarka iš viršaus į apačią)

```
1. PageTitle
2. MudContainer (MaxWidth.ExtraLarge, Class="mt-4")
   ├── Header row
   ├── MudProgressLinear (loading)
   ├── Alert (jei reikia)
   ├── Filtrų blokas (MudPaper)
   ├── Chip filtrai (jei yra)
   ├── MudTabs (jei yra)
   └── MudPaper > MudTable
```

---

## 2. Header row

```razor
<div class="d-flex justify-space-between align-center mb-3">
    <MudText Typo="Typo.h5">Puslapio pavadinimas</MudText>
    <MudButton OnClick="OpenAction"
               StartIcon="@Icons.Material.Filled.Add"
               Variant="Variant.Filled"
               Color="Color.Primary">
        Veiksmas
    </MudButton>
</div>
```

**Taisyklės:**
- Pavadinimas — kairėje, `Typo.h5`, be emoji, be `font-weight:600`
- Pagrindinis mygtukas — dešinėje, `Variant.Filled`, `Color.Primary`
- Jei keli mygtukai — naudoti `<div class="d-flex gap-2">`

---

## 3. Loading indikatorius

```razor
@if (_isLoading)
{
    <MudProgressLinear Color="Color.Primary" Indeterminate="true" Class="mb-2" />
}
```

Visada po header row, prieš filtrus.

---

## 4. Filtrų blokas

```razor
<MudPaper Class="pa-3 mb-4" Elevation="0" Style="background:#f8fafc; border-radius:8px">
    <MudGrid>
        <MudItem xs="12" sm="6" md="4">
            <MudTextField Value="@_filterSearch"
                          ValueChanged="@(async (string val) => { _filterSearch = val; await LoadDataAsync(); })"
                          Label="Paieška pagal Nr. arba pavadinimą"
                          Variant="Variant.Outlined"
                          Immediate="true"
                          FullWidth="true"
                          Clearable="true"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          Adornment="Adornment.Start" />
        </MudItem>
        <MudItem xs="12" sm="6" md="4">
            <MudDateRangePicker DateRange="@_dateRange"
                                DateRangeChanged="@(async (DateRange dr) => { _dateRange = dr; await LoadDataAsync(); })"
                                Label="Data (nuo / iki)"
                                Variant="Variant.Outlined"
                                FullWidth="true"
                                Clearable="true" />
        </MudItem>
        @if (!string.IsNullOrEmpty(_filterSearch) || _dateRange?.Start != null)
        {
            <MudItem xs="12">
                <MudButton StartIcon="@Icons.Material.Filled.FilterAltOff"
                           Variant="Variant.Text"
                           Color="Color.Secondary"
                           Size="Size.Small"
                           OnClick="@(async () => { _filterSearch = null; _dateRange = null; await LoadDataAsync(); StateHasChanged(); })">
                    Išvalyti filtrus
                </MudButton>
            </MudItem>
        }
    </MudGrid>
</MudPaper>
```

**Taisyklės:**
- Visada `Variant.Outlined`
- Search — `Immediate=true`, `Clearable=true`, search icon adornment
- Datos — `MudDateRangePicker` (NE du atskiri `MudDatePicker`)
- "Valyti filtrus" — rodomas TIK kai aktyvus bent vienas filtras
- Filtrai keičia duomenis iš karto (`ValueChanged` → `await LoadDataAsync()`)

---

## 5. Status chip filtrai

```razor
<div class="d-flex flex-wrap gap-2 mb-3 align-center">
    <MudText Typo="Typo.body2" Style="color:#6b7280;margin-right:4px;">Statusas:</MudText>
    @if (_selectedStatuses.Any())
    {
        <MudButton Size="Size.Small" Variant="Variant.Text" Color="Color.Secondary"
                   StartIcon="@Icons.Material.Filled.FilterAltOff"
                   OnClick="@(() => { _selectedStatuses.Clear(); StateHasChanged(); })">
            Rodyti visas
        </MudButton>
    }
    @foreach (var status in _availableStatuses)
    {
        var isSelected = _selectedStatuses.Contains(status);
        <MudChip T="string"
                 Color="@(isSelected ? GetStatusColor(status) : Color.Default)"
                 Variant="@(isSelected ? Variant.Filled : Variant.Outlined)"
                 OnClick="@(() => ToggleStatus(status))"
                 Size="Size.Small"
                 Style="@(isSelected ? "" : "opacity:0.7")">
            @GetStatusLabel(status)
        </MudChip>
    }
</div>
```

**Taisyklės:**
- Chip filtrai naudojami kai statusų yra 3+
- Jei statusų ≤ 2 — galima `MudSelect`
- `ToggleStatus` metodas: jei jau selected → pašalina, jei ne → prideda

---

## 6. Tabs

```razor
<MudTabs ActivePanelIndex="_selectedTab"
         ActivePanelIndexChanged="OnTabChanged"
         Elevation="0" Rounded="true" Square="false" Class="mt-2">
    <MudTabPanel Text="Visos" />
    <MudTabPanel Text="Nesumokėtos" />
    <MudTabPanel Text="Vėluojančios" />
    <MudTabPanel Text="Šis mėnuo" />
</MudTabs>
```

**Taisyklės:**
- Tabs naudojami kai turinys filtruojamas pagal loginius pogrupius
- Tabs filtracija vyksta `FilteredItems` computed property — NE `LoadDataAsync()`
- Tabs nekeičia duomenų iš DB — tik filtruoja jau gautus

---

## 7. Lentelė

```razor
<MudPaper Elevation="0" Rounded="true" Square="false" Class="mt-2 pa-3">
    <MudTable Items="@FilteredItems"
              Dense="true"
              Hover="true"
              Striped="true"
              RowsPerPage="25"
              Elevation="0"
              RowStyleFunc="@GetRowStyle"
              OnRowClick="@(async (TableRowClickEventArgs<T> args) => await OnRowClick(args.Item))"
              Style="cursor:pointer">
        <HeaderContent>
            ...
        </HeaderContent>
        <RowTemplate>
            ...
            @* Veiksmai su stopPropagation *@
            <MudTd>
                <div @onclick:stopPropagation="true">
                    <MudIconButton ... />
                </div>
            </MudTd>
        </RowTemplate>
        <PagerContent>
            <MudTablePager />
        </PagerContent>
        <NoRecordsContent>
            <MudText>Įrašų nėra</MudText>
        </NoRecordsContent>
    </MudTable>
</MudPaper>
```

**Taisyklės:**
- Visada `Dense=true`, `Hover=true`, `Striped=true`, `Elevation=0`
- `RowsPerPage="25"` pagal nutylėjimą
- `OnRowClick` — navigacija arba dialog atidarymas
- `Style="cursor:pointer"` ant lentelės
- Veiksmai (`Edit`, `Delete`) su `@onclick:stopPropagation="true"` wrapper div
- `NoRecordsContent` — visada užpildytas
- `RowStyleFunc` — spalvina eilutes pagal būseną:
  - Kritinė/vėluojanti: `background:#fff5f5`
  - Įspėjimas: `background:#fffbeb`
  - Normali: `""`

---

## 8. @code bloko struktūra

```csharp
// State
private List<T> _items = new();
private bool _isLoading = false;
private int _selectedTab = 0;

// Filtrai
private string? _filterSearch = null;
private DateRange? _dateRange = null;
private HashSet<string> _selectedStatuses = new();

// Computed
private List<T> FilteredItems => GetFilteredItems();

// Lifecycle
protected override async Task OnInitializedAsync() { ... }

// Data loading
private async Task LoadDataAsync() { ... }

// Filter logic
private List<T> GetFilteredItems() { ... }
private void ToggleStatus(string status) { ... }
private void OnTabChanged(int index) { _selectedTab = index; StateHasChanged(); }

// Navigation / Actions
private void OnRowClick(T item) { ... }

// Display helpers
private string GetStatusLabel(string status) => status switch { ... };
private Color GetStatusColor(string status) => status switch { ... };
private string GetRowStyle(T item, int index) { ... }
```

---

## 9. Direktyvos (privalomas orderis)

```razor
@page "/route"
@rendermode InteractiveServer
@using ...
@inject ...

<PageTitle>...</PageTitle>
```

`@rendermode InteractiveServer` — visada ant VISŲ sąrašo puslapių.

---

## 10. Ko NEGALIMA daryti

| ❌ Draudžiama | ✅ Vietoje to |
|---|---|
| Du atskiri `MudDatePicker` | `MudDateRangePicker` |
| `LoadDataAsync().Wait()` | `await LoadDataAsync()` |
| Emoji pavadinime (`🛒 Klientai`) | Tik tekstas (`Klientai`) |
| `font-weight:600` ant h5 | Naudoti `Typo.h5` be stiliaus |
| `MudContainer Class="mt-8"` | `Class="mt-4"` |
| Mygtukas žemiau filtrų | Mygtukas header row dešinėje |
| `MudSelect` statusams (3+) | Status chip filtrai |

---

## 11. Puslapiai kurie neatitinka standarto (reikia taisyti)

| Puslapis | Problemos |
|---|---|
| `Customers.razor` | Nėra header layout, nėra search, nėra row click, emoji pavadinime, `mt-8` |
| `Invoices.razor` | Du date picker, nėra chips, nėra tabs, nėra row click, mygtukas ne header |
| `CreditNotes.razor` | Nėra header layout, nėra chips, nėra tabs, nėra row click, mygtukas ne header |
