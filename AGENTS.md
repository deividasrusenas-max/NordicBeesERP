# NordicBeesERP

.NET 10 / Blazor Server / MudBlazor / EF Core / MariaDB.
Honey purchasing, packaging and export ERP (MB Lakštena).

## Project Layout

```
Models/          — EF Core entities
Services/        — Interfaces + implementations
Components/
  Pages/         — Blazor pages
  Dialogs/       — MudBlazor dialogs
wwwroot/css/     — Stylesheets
Docs/            — Plans and specifications
```

**Always use staging DB:** `nordic_bees_erp_STAGING` — never production.

## After Every Single File Change — Mandatory

```bash
dotnet build                          # zero errors required
# bump patch in NordicBeesERP.csproj  e.g. 1.2.3 → 1.2.4
git add -A
git commit -m "P0a: <FileName> — <what was done>"
```

One file → build → bump → commit. Never batch multiple files.

## Coding Standards

- Async/await everywhere — never `.Result` or `.Wait()`
- EF Core: `IDbContextFactory<>` in Blazor components
- MudBlazor: `MaxWidth.Small`, `inputmode="decimal"` on weight inputs
- All action buttons: `class="tablet-action-btn"`
- Constructor injection — no service locator

## BRC8 Rules — Never Violate

- `ContainerLabelEvent` INSERT ONLY — override `SaveChanges` + `SaveChangesAsync` to throw on Modified/Deleted
- ZPL label date: `delivery.DeliveryDate` — NEVER `DateTime.Now`
- `StockMovement.CreatedBy` = operatorId — never null
- Container codes only inside DB transaction: `$"{delivery.DeliveryNumber}/{seq:D3}"`
- Never delete: `container_label_events`, `supplier_approvals`, `container_weight_corrections`

## DB Technical Debt — Do Not Touch

- `invoices.PaymentTermId1` — EF Core duplicate, touch only before payment module
- `warehouse_stock` vs `warehouse_stocks` — do not touch
- `users` + `AspNetUsers` — empty, real table is `erp_users`

## Detailed Specifications

`Docs/LABELING_PLAN_2.md` — read the relevant section before each task.
