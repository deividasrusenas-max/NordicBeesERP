# NordicBeesERP — UŽŠALDYTI BLOKAI

## ⛔ ŠIŲ FAILŲ / BLOKŲ NEKEISTI be specialaus leidimo

---

## 1. DRAG & DROP — `wwwroot/js/dropzone.js`

**Statusas:** ✅ VEIKIA (Chrome + Firefox)  
**Nekeisti:** viso failo

Veikiantis mechanizmas:
- `setupDropZone(dotNetHelper, elementId)` — priskiria event listeners
- `getDropFileBase64(elementId)` — grąžina failo base64
- `cleanupDropZone(elementId)` — išvalo listeners

---

## 2. DRAG & DROP — `Components/App.razor` (global script blokas)

**Statusas:** ✅ VEIKIA  
**Nekeisti:** `<script>` bloko su `dragover`/`drop` global preventDefault

---

## 3. DRAG & DROP — `Components/Dialogs/ExpenseUploadDialog.razor` (@code blokas)

**Statusas:** ✅ VEIKIA  
**Nekeisti:** šių metodų:
- `OnAfterRenderAsync` — setupDropZone su retry logika
- `OnFileDropped` — [JSInvokable], gauna metadata + kviečia getDropFileBase64
- `DisposeAsync` — cleanupDropZone
- `DroppedFile` — IBrowserFile wrapper klasė

**Galima keisti:** tik HTML upload fazę ir kitus @code metodus

---

## 4. ULAK MODULIS

**Statusas:** ✅ VEIKIA  
**Nekeisti:** viso ULAK sąskaitų generavimo ir apdorojimo kodo

ULAK taisyklės (žr. nordicbees-standards.md 18 skyrius):
- PVM = 6% (ne 0%)
- Tiekėjai = fiziniai asmenys
- `ZERO_VAT` flag NEREIKIA ULAK sąskaitoms

---

## 5. `OcrQueueWorker.cs`

**Statusas:** ✅ VEIKIA  
**Nekeisti:** viso failo  
**Svarbu:** naudoja `IExpenseOcrService.ExtractInvoiceDataAsync` — šio metodo nešalinti iš interface'o

---

## 6. `ViesService.cs`

**Statusas:** ✅ VEIKIA  
**Nekeisti:** viso failo (timeout jau sukonfigūruotas — 15s)

---

## 7. Banko importo modulis (`BankImport.razor`, `BankImportService.cs`)

**Statusas:** ✅ VEIKIA  
**Nekeisti:** be specialaus leidimo

---

## Kaip elgtis su šiais failais

Jei reikia keisti šalia esančią funkciją:
1. Perskaityk šį failą pirma
2. Jei reikia keisti užšaldytą bloką — paklausk vartotojo leidimo
3. Jei klaida yra užšaldytame bloke — pranešk, nekeisk

## Kaip atpažinti veikiančią versiją

Jei abejoji ar pakeitimas sugadins — žiūrėk git log:
```bash
git log --oneline Components/Dialogs/ExpenseUploadDialog.razor | head -5
```
Ieškoti commit: `feat: restore working drag and drop using setupDropZone JS interop`
