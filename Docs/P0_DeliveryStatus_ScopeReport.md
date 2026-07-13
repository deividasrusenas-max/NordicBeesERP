# P0: Delivery.Status string → DeliveryStatus enum — Scope Report

## Decision
**NOT small enough for immediate inline refactor** (9 files, ~20 code points).
**BUT mechanically safe** — all changes are find-and-replace, zero logic changes.

## Current State
- `Delivery.Status` is `string`, default `"RECEIVED"`
- `DeliveryStatus` enum exists with 4 values: `RECEIVED, PRICED, PARTIAL_PAID, PAID`
- DB column has 6 enum values: adds `ACCEPTED`, `CLOSED` (missing from C#)

## Files to Change (9 total)

### 1. Models/WarehouseModule/DeliveryEnums.cs
- Add `ACCEPTED` and `CLOSED` to `DeliveryStatus` enum

### 2. Models/WarehouseModule/Delivery.cs
- Change `public string Status { get; set; } = "RECEIVED";` → `public DeliveryStatus Status { get; set; } = DeliveryStatus.RECEIVED;`

### 3. Services/IDeliveryService.cs
- Change `GetFilteredAsync(string? status, ...)` → `GetFilteredAsync(DeliveryStatus? status, ...)`

### 4. Services/DeliveryService.cs
- Filter: `d.Status == status` → compare enum (or convert in SQL)
- 6 hardcoded string assignments → enum values

### 5. Components/Pages/Warehouse/DeliveryList.razor
- `MudSelect T="string"` → `T="DeliveryStatus?"`
- Filter var `string?` → `DeliveryStatus?`
- `GetStatusColor`/`GetStatusText` helpers: switch on enum

### 6. Components/Pages/Warehouse/DeliveryView.razor
- `GetStatusColor`/`GetStatusText` helpers: switch on enum

### 7. Components/Pages/Warehouse/DeliveryPricingDetail.razor
- `GetStatusText(_delivery?.Status ?? "RECEIVED")` → `?? DeliveryStatus.RECEIVED`
- `GetStatusColor`/`GetStatusText` helpers: switch on enum

### 8. Components/Pages/Warehouse/SupplierDebtDetail.razor
- `GetStatusColor`/`GetStatusText` helpers: switch on enum

### 9. Components/Pages/Warehouse/DeliveryCreate.razor
- `Status = "RECEIVED"` → `Status = DeliveryStatus.RECEIVED`

## No Changes Needed
- `DeliveryPricing.razor` — derives status from amounts, doesn't read `.Status`
- `DeliverySignature.razor` — no `.Status` references
- `SupplierPaymentService.cs` — delegates to `UpdateDeliveryStatusAsync`, no direct status strings
- DB migration — column stays ENUM, EF Core handles string↔enum mapping

## Recommendation
Proceed file-by-file. Order: Enums → Model → Interface → Service → Razor pages.
