# NordicBeesERP — Design System 2024

## Spalvų sistema

### Pagrindinės
- Primary: #2563eb
- Primary hover: #1d4ed8
- Success: #16a34a
- Warning: #d97706
- Error: #dc2626
- Info: #0891b2

### Fonas
- Page bg: #f1f5f9
- Card bg: #ffffff
- Section bg: #f8fafc
- Input bg: #ffffff

### Tekstas
- Primary text: #1e293b
- Secondary text: #64748b
- Muted text: #94a3b8

### Akcentų kortelės
- Mėlyna: background:#eff6ff; border-left:4px solid #2563eb
- Violetinė: background:#f5f3ff; border-left:4px solid #7c3aed
- Žalia: background:#f0fdf4; border-left:4px solid #16a34a
- Geltona: background:#fffbeb; border-left:4px solid #d97706
- Raudona: background:#fff5f5; border-left:4px solid #dc2626

---

## Tipografija
- h5 (puslapio pavadinimas): Typo.h5, BE font-weight inline stiliaus
- overline (sekcijos žymė): Typo.overline, Color.Secondary, Class="mb-1"
- body2 (laukų reikšmės): Typo.body2
- caption (laukų pavadinimai): Typo.caption, Color.Secondary

---

## Mygtukai
- Pagrindinis (1 puslapyje): Variant.Filled, Color.Primary, Style="text-transform:none"
- Antrinis: Variant.Outlined, Color.Primary, Style="text-transform:none"
- Pavojingas: Variant.Outlined, Color.Error, Style="text-transform:none"
- Neutralus: Variant.Outlined, Color.Default, Style="text-transform:none"
- Tvarka view puslapyje: Atgal | Redaguoti | Spausdinti | Kopijuoti | [Statusas] | Pagrindinis
- DRAUDŽIAMA: caps lock, >1 Filled mygtukas, emoji mygtukuose

---

## Kortelės
- Informacinė: Elevation="0" Style="background:#f8fafc; border-radius:8px; padding:16px"
- Akcentuota: Elevation="0" Style="background:#eff6ff; border-radius:8px; border-left:4px solid #2563eb; padding:16px"
- Sekcijos headeris: Typo.overline, Color.Secondary, Class="mb-1"
- DRAUDŽIAMA: spalvoti solid headeriai, Elevation > 2, border-radius > 12px

---

## Formos
- Input laukai: Variant.Outlined, visada su Label
- Boolean parametrai: VISADA @true/@false — NIEKADA "true" kaip string
- Išdėstymas: MudGrid + MudItem

---

## Dialogai
- TitleContent: MudIcon (Color.Primary) + MudText Typo.h6
- DialogActions: Atšaukti (Outlined, Default) KAIRĖJE | Pagrindinis (Filled, Primary) DEŠINĖJE
- Dydžiai: Small/Medium/Large/ExtraLarge su FullWidth="@true"

---

## View puslapiai

### Header
```razor
<div class="d-flex justify-space-between align-center mb-2">
    <div>
        <div class="d-flex align-center gap-2">
            <MudText Typo="Typo.h5" Style="font-weight:600">NUMERIS</MudText>
            <MudChip T="string" Color="statusColor" Size="Size.Small">Statusas</MudChip>
        </div>
        <MudText Typo="Typo.body1" Color="Color.Secondary">Partnerio vardas</MudText>
    </div>
    <div class="d-flex gap-2 flex-wrap"><!-- mygtukai --></div>
</div>
```

### Informacijos sekcijos
- 2 stulpeliai: kairė (partneris, border:#2563eb) + dešinė (dok. info, border:#7c3aed)
- Laukų layout: caption viršuje + body2 žemiau

### Eilučių lentelė
- Wrapper: background:#f8fafc, border-radius:8px
- Dense + Hover + Striped + Elevation=0
- Sumos blokas: right-aligned, background:#eff6ff, border-radius:8px, pa-3

---

## NEGALIMA niekada
| Draudžiama | Vietoje to |
|---|---|
| Emoji pavadinime | Tik tekstas |
| Žali/mėlyni solid headeriai | Typo.overline + Color.Secondary |
| Caps lock mygtukuose | Style="text-transform:none" |
| FullWidth="true" | FullWidth="@true" |
| Clearable="true" | Clearable="@true" |
| Immediate="true" | Immediate="@true" |
| mt-8 | mt-4 |
| >1 Filled mygtukas puslapyje | Tik 1 pagrindinis |
| Console.WriteLine klaidos | Snackbar vartotojui |
## Patvirtinimo dialogas (ConfirmDialog)
- TitleContent: MudIcon (spalva pagal veiksmo tipą) + MudText Typo.h6
- DialogContent: MudText Typo.body1, Color.Secondary
- DialogActions: Atšaukti (Outlined, Default) KAIRĖJE | Pagrindinis veiksmas (Filled, spalva pagal tipą) DEŠINĖJE
- Ištrinimo veiksmas: Color.Error
- Patvirtinimo veiksmas: Color.Primary
- Dydis: MaxWidth.ExtraSmall
- DRAUDŽIAMA: caps lock mygtukuose
