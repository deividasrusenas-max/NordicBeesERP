# Offer Generator Module — NordicBeesERP

## Tikslas

Laisvo teksto įvestis → LLM ištraukia parametrus → C# skaičiuoja savikainos → vartotojas nustato galutinę kainą → LLM generuoja pasiūlymo tekstą → PDF išsaugomas ir susietas su klientu ERP'e.

---

## Flow

```
[1] Vartotojas įveda laisvą tekstą (sąnaudos, produktas, kiekis)
        ↓
[2] LLM (Qwen3, local port 8080) → JSON parametrai
        ↓
[3] C# OfferCalculationService → savikaina, marža, galutinė kaina
        ↓
[4] Vartotojas peržiūri skaičiavimus, gali koreguoti galutinę kainą / maržą
        ↓
[5] Vartotojas pasirenka klientą iš business_partners
        ↓
[6] LLM generuoja pasiūlymo tekstą (EN arba pagal kliento default_language)
        ↓
[7] Vartotojas gali redaguoti sugeneruotą tekstą
        ↓
[8] ERP generuoja unikalų offer_number (OFFER-2026-0001)
        ↓
[9] PDF generuojamas (QuestPDF), išsaugomas /storage/offers/
        ↓
[10] Įrašas į DB → susietas su klientu
```

---

## DB migracija

### Nauja lentelė: `offers`

```sql
CREATE TABLE offers (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    offer_number        VARCHAR(30) NOT NULL UNIQUE,        -- OFFER-2026-0001
    business_partner_id INT NOT NULL,
    product_description VARCHAR(500),                       -- trumpas produkto aprašas
    quantity_kg         DECIMAL(10,2),
    input_prompt        TEXT NOT NULL,                      -- originalus vartotojo tekstas
    parsed_json         JSON,                               -- LLM ištraukti parametrai
    calculation_json    JSON,                               -- C# skaičiavimų rezultatas
    cost_price_eur      DECIMAL(10,4),                      -- savikaina €/kg
    margin_pct          DECIMAL(5,2),                       -- marža %
    final_price_eur     DECIMAL(10,4),                      -- galutinė kaina €/kg
    incoterm            VARCHAR(20),                        -- DAP, EXW, FOB...
    delivery_location   VARCHAR(255),
    validity_days       INT DEFAULT 14,
    currency            VARCHAR(5) DEFAULT 'EUR',
    offer_text          TEXT,                               -- LLM sugeneruotas tekstas
    pdf_path            VARCHAR(500),                       -- /storage/offers/OFFER-2026-0001.pdf
    status              ENUM('draft','sent','accepted','rejected','expired') DEFAULT 'draft',
    notes               TEXT,
    created_by_user_id  INT,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (business_partner_id) REFERENCES business_partners(id)
);
```

---

## Failų struktūra

```
NordicBeesERP/
├── Features/
│   └── Offers/
│       ├── Models/
│       │   ├── Offer.cs                    -- EF entitija
│       │   ├── OfferStatus.cs              -- enum
│       │   ├── ParsedOfferParams.cs        -- LLM JSON → C# record
│       │   └── OfferCalculationResult.cs   -- skaičiavimų rezultatas
│       ├── Services/
│       │   ├── IOfferService.cs
│       │   ├── OfferService.cs             -- CRUD, numeravimas
│       │   ├── IOfferCalculationService.cs
│       │   ├── OfferCalculationService.cs  -- gryna C# matematika
│       │   ├── IOfferLlmService.cs
│       │   ├── OfferLlmService.cs          -- Qwen3 HTTP calls
│       │   └── OfferPdfService.cs          -- QuestPDF generavimas
│       ├── Pages/
│       │   ├── OfferList.razor             -- visų pasiūlymų sąrašas
│       │   ├── OfferList.razor.cs
│       │   ├── OfferCreate.razor           -- kūrimo wizard
│       │   ├── OfferCreate.razor.cs
│       │   ├── OfferDetail.razor           -- peržiūra + PDF atsisiuntimas
│       │   └── OfferDetail.razor.cs
│       └── Templates/
│           └── OfferPdfTemplate.cs         -- QuestPDF layout
├── Migrations/
│   └── XXXXXX_AddOffersTable.cs
```

---

## C# modeliai

### `ParsedOfferParams.cs`
```csharp
public record ParsedOfferParams
{
    public string ProductName { get; init; } = "";
    public string? ProductOrigin { get; init; }
    public decimal QuantityKg { get; init; }
    public decimal ExwPricePerKg { get; init; }
    public List<CostItem> CostItems { get; init; } = new();
    public string? PackagingDescription { get; init; }
    public decimal? PackagingCostPerKg { get; init; }
    public string? Incoterm { get; init; }
    public string? DeliveryLocation { get; init; }
    public int? ValidityDays { get; init; }
}

public record CostItem
{
    public string Name { get; init; } = "";       // "Pervežimas RO→LT", "Tyrimai", ...
    public decimal Amount { get; init; }           // bendra suma arba €/kg
    public string AmountType { get; init; } = "total"; // "total" | "per_kg"
    public decimal? SurchargePct { get; init; }    // kuro mokestis ir pan.
}
```

### `OfferCalculationResult.cs`
```csharp
public record OfferCalculationResult
{
    public decimal ExwTotal { get; init; }
    public List<CostLineResult> CostLines { get; init; } = new();
    public decimal TotalCostEur { get; init; }
    public decimal CostPerKg { get; init; }
    public decimal MarginPct { get; init; }
    public decimal FinalPricePerKg { get; init; }
    public decimal FinalPriceTotal { get; init; }
}

public record CostLineResult
{
    public string Name { get; init; } = "";
    public decimal BaseAmount { get; init; }
    public decimal SurchargeAmount { get; init; }
    public decimal Total { get; init; }
    public decimal PerKg { get; init; }
}
```

---

## OfferCalculationService logika

```csharp
public class OfferCalculationService : IOfferCalculationService
{
    public OfferCalculationResult Calculate(ParsedOfferParams p, decimal marginPct)
    {
        var exwTotal = p.ExwPricePerKg * p.QuantityKg;
        var costLines = new List<CostLineResult>();

        foreach (var item in p.CostItems)
        {
            decimal baseAmount = item.AmountType == "per_kg"
                ? item.Amount * p.QuantityKg
                : item.Amount;

            decimal surcharge = item.SurchargePct.HasValue
                ? baseAmount * (item.SurchargePct.Value / 100m)
                : 0m;

            decimal total = baseAmount + surcharge;

            costLines.Add(new CostLineResult
            {
                Name = item.Name,
                BaseAmount = baseAmount,
                SurchargeAmount = surcharge,
                Total = total,
                PerKg = total / p.QuantityKg
            });
        }

        decimal packagingTotal = (p.PackagingCostPerKg ?? 0) * p.QuantityKg;
        if (packagingTotal > 0)
            costLines.Add(new CostLineResult
            {
                Name = "Pakavimas",
                BaseAmount = packagingTotal,
                Total = packagingTotal,
                PerKg = p.PackagingCostPerKg ?? 0
            });

        decimal totalCost = exwTotal + costLines.Sum(c => c.Total);
        decimal costPerKg = totalCost / p.QuantityKg;
        decimal finalPricePerKg = costPerKg * (1 + marginPct / 100m);

        return new OfferCalculationResult
        {
            ExwTotal = exwTotal,
            CostLines = costLines,
            TotalCostEur = totalCost,
            CostPerKg = costPerKg,
            MarginPct = marginPct,
            FinalPricePerKg = Math.Round(finalPricePerKg, 4),
            FinalPriceTotal = Math.Round(finalPricePerKg * p.QuantityKg, 2)
        };
    }
}
```

---

## LLM Integration (OfferLlmService)

### Qwen3 endpoint
```
POST http://localhost:8080/v1/chat/completions
Model: qwen3-coder (arba koks užkrautas)
```

### Parsing sistemos promptas

```
You are a cost calculation assistant for a honey trading company.
Extract cost parameters from the user's free-form text and return ONLY valid JSON.
No explanations, no markdown, no code blocks — raw JSON only.

Return this exact structure:
{
  "product_name": "string",
  "product_origin": "string or null",
  "quantity_kg": number,
  "exw_price_per_kg": number,
  "packaging_description": "string or null",
  "packaging_cost_per_kg": number or null,
  "incoterm": "string or null",
  "delivery_location": "string or null",
  "validity_days": number or null,
  "cost_items": [
    {
      "name": "string",
      "amount": number,
      "amount_type": "total" | "per_kg",
      "surcharge_pct": number or null
    }
  ]
}

Rules:
- If a surcharge is mentioned (fuel, kuro mokestis, etc.), set surcharge_pct on the parent item
- Transport costs are always "total" unless explicitly stated per kg
- Packaging costs are always "per_kg"
- If quantity not mentioned, use null (will ask user)
- Currency is always EUR
```

### Text generation sistemos promptas

```
You are a professional B2B sales assistant for MB Lakštena, a Lithuanian honey trading company.
Generate a concise, professional offer email body in {language}.
Use formal but friendly tone. Do not include greetings or signatures — only the offer content.
Structure: product description → packaging → price → incoterm & delivery → validity → brief call to action.
Return plain text only, no markdown.
```

---

## Blazor UI — OfferCreate.razor žingsniai

### Žingsnis 1 — Įvestis
- `MudTextField` multiline — vartotojas rašo laisvą tekstą
- Mygtukas "Analizuoti" → `OfferLlmService.ParseAsync(text)`
- Loading spinner kol LLM analizuoja

### Žingsnis 2 — Skaičiavimų peržiūra
- Rodoma lentelė su ištrauktais parametrais (galima koreguoti)
- Marža % slider arba input
- Skaičiavimai atnaujinami realiu laiku (C# pusėje)
- Rodomas: savikaina/kg, galutinė kaina/kg, galutinė kaina viso

### Žingsnis 3 — Kliento pasirinkimas
- `MudAutocomplete` iš `business_partners` (tik `partner_type IN ('customer','both')`)
- Rodoma kliento informacija (šalis, mokėjimo terminas)
- Kalbos pasirinkimas pasiūlymui (default iš `business_partners.default_language`)

### Žingsnis 4 — Teksto generavimas
- Mygtukas "Generuoti pasiūlymą" → `OfferLlmService.GenerateTextAsync(...)`
- `MudTextField` multiline su sugeneruotu tekstu — redaguojamas
- Pasiūlymo galiojimo dienos (default 14)

### Žingsnis 5 — PDF ir išsaugojimas
- Preview (HTML render arba tiesiog tekstas)
- Mygtukas "Išsaugoti ir generuoti PDF"
- PDF kelias: `/storage/offers/OFFER-{year}-{seq}.pdf`
- Redirect į OfferDetail puslapį

---

## Offer numeravimas

```csharp
// OfferService.cs
public async Task<string> GenerateOfferNumberAsync()
{
    var year = DateTime.UtcNow.Year;
    var lastOffer = await _db.Offers
        .Where(o => o.OfferNumber.StartsWith($"OFFER-{year}-"))
        .OrderByDescending(o => o.Id)
        .FirstOrDefaultAsync();

    int seq = 1;
    if (lastOffer != null)
    {
        var lastSeq = int.Parse(lastOffer.OfferNumber.Split('-').Last());
        seq = lastSeq + 1;
    }

    return $"OFFER-{year}-{seq:D4}";
}
```

---

## PDF (QuestPDF — jau naudojamas projekte)

### `OfferPdfTemplate.cs` struktūra
```
[Lakštena logo + kontaktai]        [Pasiūlymo numeris + data]
─────────────────────────────────────────────────────────────
Kam: [kliento pavadinimas, adresas, VAT]

OFFER / PASIŪLYMAS Nr. OFFER-2026-0001

[offer_text — sugeneruotas LLM arba redaguotas]

─────────────────────────────────────────────────────────────
Sąnaudų suvestinė (vidaus informacija — NESPAUSDINAMA):
[CostLines lentelė]
Savikaina: X.XX €/kg | Marža: X% | Galutinė kaina: X.XX €/kg
─────────────────────────────────────────────────────────────

Galioja iki: [data]
```

**Pastaba:** Sąnaudų suvestinė yra tik vidaus peržiūrai — į PDF klientui NESPAUSDINAMA.

---

## Navigacija

```csharp
// NavMenu.razor — pridėti
<MudNavLink Href="/offers" Icon="@Icons.Material.Filled.Description">
    Pasiūlymai
</MudNavLink>
```

Maršrutai:
- `/offers` — sąrašas
- `/offers/create` — naujas
- `/offers/{id}` — detalės + PDF atsisiuntimas

---

## EF Core Offer.cs entitija

```csharp
[Table("offers")]
public class Offer
{
    [Key] public int Id { get; set; }
    [Column("offer_number")] public string OfferNumber { get; set; } = "";
    [Column("business_partner_id")] public int BusinessPartnerId { get; set; }
    [Column("product_description")] public string? ProductDescription { get; set; }
    [Column("quantity_kg")] public decimal? QuantityKg { get; set; }
    [Column("input_prompt")] public string InputPrompt { get; set; } = "";
    [Column("parsed_json", TypeName = "json")] public string? ParsedJson { get; set; }
    [Column("calculation_json", TypeName = "json")] public string? CalculationJson { get; set; }
    [Column("cost_price_eur")] public decimal? CostPriceEur { get; set; }
    [Column("margin_pct")] public decimal? MarginPct { get; set; }
    [Column("final_price_eur")] public decimal? FinalPriceEur { get; set; }
    [Column("incoterm")] public string? Incoterm { get; set; }
    [Column("delivery_location")] public string? DeliveryLocation { get; set; }
    [Column("validity_days")] public int ValidityDays { get; set; } = 14;
    [Column("currency")] public string Currency { get; set; } = "EUR";
    [Column("offer_text")] public string? OfferText { get; set; }
    [Column("pdf_path")] public string? PdfPath { get; set; }
    [Column("status")] public OfferStatus Status { get; set; } = OfferStatus.Draft;
    [Column("notes")] public string? Notes { get; set; }
    [Column("created_by_user_id")] public int? CreatedByUserId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }

    public BusinessPartner? BusinessPartner { get; set; }
}
```

---

## Implementacijos eilės tvarka (Cline darbų sąrašas)

1. **DB migracija** — `AddOffersTable` migration, `Offer.cs` entitija, `DbContext` registracija
2. **OfferCalculationService** — gryna matematika, unit testai
3. **OfferLlmService** — HTTP client Qwen3, parsing + text generation metodai
4. **OfferService** — CRUD + offer_number generavimas
5. **OfferCreate.razor** — 5 žingsnių wizard
6. **OfferPdfTemplate.cs** — QuestPDF layout
7. **OfferPdfService** — PDF generavimas + išsaugojimas
8. **OfferList.razor** — sąrašas su statusais
9. **OfferDetail.razor** — peržiūra, PDF atsisiuntimas, statuso keitimas
10. **NavMenu** — navigacijos nuoroda

---

## Pastabos / ateičiai

- Pasiūlymas galės būti konvertuotas į `order` (orders lentelė jau egzistuoja)
- `status` keitimas: draft → sent (pažymima rankiniu būdu) → accepted/rejected
- Ateityje: el. pašto siuntimas tiesiai iš ERP (SMTP jau konfigūruotas)
- Kalbos palaikymas: EN (default), LT, PL, DE — pagal `business_partners.default_language`
- Jei Qwen3 nepasiekiamas — graceful fallback: parsing žingsnis prašo vartotojo pildyti formą rankiniu būdu
