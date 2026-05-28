# NordicBeesERP — Privalomi standartai

## ⚠️ PRIEŠ KIEKVIENĄ VEIKSMĄ perskaityk:
1. `.clinerules/FROZEN.md` — UŽŠALDYTI BLOKAI (nekeisti!)
2. `.clinerules/nordicbees-standards.md` — PILNI standartai
3. `.clinerules/PATTERNS.md` — DAŽNIAUSIŲ KLAIDŲ ŠABLONAI

## Greita santrauka:

### NEKEISTI (užšaldyta):
- `wwwroot/js/dropzone.js` — drag & drop veikia
- `Components/App.razor` global script — drag & drop veikia
- `ExpenseUploadDialog.razor` OnAfterRenderAsync, OnFileDropped, DisposeAsync, DroppedFile
- `OcrQueueWorker.cs` — veikia, naudoja ExtractInvoiceDataAsync
- `ViesService.cs` — veikia
- ULAK modulis — veikia, PVM=6%

### UI stilius:
- Etalonas: `PaymentsDashboard.razor`
- Kortelės: mėlyna `#dbeafe`, geltona `#fef3c7`, violetinė `#f5f3ff`, raudona `#fef2f2`
- Filtrai: instant search, be "Filtruoti" mygtuko
- Lentelės: `Dense + Hover + Striped + PagerContent` — VISADA
- Dialogai: `TitleContent` su ikona, Atšaukti kairėje, Primary dešinėje
- `MudChip` VISADA su `T="string"`

### Pranešimai:
- VISADA Snackbar lietuviškai
- žalia=sėkmė, raudona=klaida, geltona=įspėjimas

### DB:
- `await using var ctx` — visada
- `GetInvoiceLinesAsync` — eilutėms, ne `Include()`
- Dublikatai: invoice_number + amount_incl_vat (nepriklausomai nuo supplier)

### Išlaidų workflow:
```
OCR → validacija → statusas → patvirtinimas → apmokėjimas
```
