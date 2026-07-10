# P0a — Warehouse Labeling System

Detailed specifications: Docs/LABELING_PLAN_2.md
Read the relevant section from that file before starting each task.

## Rules
- DB: `nordic_bees_erp_STAGING` — never production
- Read existing files before writing new ones — never guess at namespaces or signatures
- After any model/enum changes: `grep -r "BUCKET_GROUP" --include="*.cs" --include="*.razor" .` must return 0

## After EVERY File Change — Mandatory

```bash
# 1. Build
dotnet build
# Must be zero errors before proceeding

# 2. Version bump
# NordicBeesERP.csproj: increment patch (e.g. 1.2.3 → 1.2.4)

# 3. Commit
git add -A
git commit -m "P0a: <FileName> — <what was done>"
# Example: "P0a: ContainerLabelEvent.cs — INSERT ONLY model + SaveChanges override"
```

Do NOT create multiple files then build. One file → build → bump → commit → next file.

## Delegation
- Implementation → delegate to `code` agent
- After each file: build + bump + commit → delegate to `debug` agent
- Final BRC8 compliance check (after all tasks) → delegate to `reviewer` agent

## Tasks (in order)

### Task 0 — Version bump
`NordicBeesERP.csproj`: bump minor version before starting P0a.

### Task 1 — DB migration
Create EF Core migration. Read Docs/LABELING_PLAN_2.md "DB schema" section.
New tables: `printers`, `weighing_stations`, `print_jobs`, `container_label_events`,
`container_weight_corrections`, `label_templates`, `supplier_approvals`,
`non_conformances`, `document_files`
ALTER: `containers`, `deliveries`, `delivery_lines`, `business_partners`
Migration must be idempotent (IF NOT EXISTS checks).

### Task 2 — EF Core models + DbContext
Read Docs/LABELING_PLAN_2.md "Nauji failai" section.
New models in `Models/Printing/` and `Models/Warehouse/`.
Update: `ContainerEnums.cs`, `Container.cs`, `Delivery.cs`, `BusinessPartner.cs`
DbContext: 11 new DbSet<>, ContainerLabelEvent immutability override in SaveChanges + SaveChangesAsync.
Run: `dotnet ef migrations add LabelingP0a`

### Task 3 — BUCKET_GROUP → BUCKET
Global replace in all .cs and .razor files.
Verify with grep — must return 0 results.

### Task 4 — ContainerService cleanup
Delete: `GetLastContainerCodeAsync()`, `GetLastBucketCodeAsync()`
Verify nothing calls them (build must pass).

### Task 5 — DeliveryService
Method: `CreateDeliveryWithContainersAsync(delivery, lines, containers, operatorId)`
- delivery_number generated inside transaction with UNIQUE constraint + retry
- Container codes: `$"{delivery.DeliveryNumber}/{seq:D3}"` — only inside transaction
- `StockMovement.CreatedBy = operatorId` (bug fix — was null)
- `delivery.CreatedByUserId = operatorId`, `container.ReceivedByUserId = operatorId`
- After creation: enqueue RECEIPT_LABEL print_jobs for each container

### Task 6 — IPrinterGateway
- `IPrinterGateway`: `Task<PrintResult> PrintAsync(string zpl, Printer printer)`
- `StubPrinterGateway`: ZPL → `/tmp/stub_labels/{job_id}.zpl`, PNG via Labelary API → `/tmp/stub_labels/{job_id}.png`, always returns Success=true
- `HttpPrinterGateway`: POST to `{printer.EndpointUrl}/print`, timeout 5s

### Task 7 — ILabelTemplateService
- `ILabelTemplateService`: `string RenderZpl(LabelTemplateType, ContainerLabelData)` + `Task<byte[]> PreviewPngAsync(string zpl)`
- `ZplLabelTemplateService`: hardcoded ZPL, 108mm (850 dots @ 200dpi)
- `ContainerLabelData`: ContainerCode, SupplierName, RawMaterialName, OriginCountry, NetWeightKg, TareWeightKg, GrossWeightKg, DeliveryDate (NOT DateTime.Now), WarehouseName, NonConformanceId?
- RECEIPT template: ContainerCode + QR, supplier, material, origin, net weight, date, warehouse
- QUARANTINE template: same as RECEIPT + large "KARANTINAS" mark

### Task 8 — ILabelPrintService
- `PrintReceiptLabelAsync(containerId, stationId, operatorId) → int`
- `PrintQuarantineLabelAsync(containerId, stationId, operatorId) → int`
- `ReprintLabelAsync(containerId, reasonCode, reasonText?, operatorId) → int`
- Returns print_job.id
- Flow: get container → RenderZpl → INSERT print_jobs (PENDING) → INSERT container_label_events

### Task 9 — LabelPrintWorker
BackgroundService, loop every 1s:
- PENDING jobs grouped by printer_id → SemaphoreSlim(1) per printer
- PENDING → PROCESSING → DONE or FAILED
- Retry: max 3, exponential backoff (2^retry seconds)
- On final FAILED: INSERT container_label_events(PRINT_FAILED, operator_id = print_job.created_by_user_id)

### Task 10 — Program.cs registration
Register all new services. Add `appsettings.json`: `"Printing": { "ConnectionType": "STUB" }`
IPrinterGateway: STUB if ConnectionType != "HTTP", else HttpPrinterGateway.

### Task 11 — warehouse.css
Create `wwwroot/css/warehouse.css` per Docs/LABELING_PLAN_2.md "UI — planšetės stilius".
Add `<link>` in `App.razor`.

### Task 12 — DeliveryCreate.razor
6-step wizard. Read Docs/LABELING_PLAN_2.md "Wizard" section.
- Step 1 (NEW): workstation selection → ProtectedSessionStorage["station_id"], _warehouseId readonly
- Step 2: OriginCountry auto-fill, supplier approval check (Warning/Error)
- Step 5: inputmode="decimal", container codes show "Will be generated", remove _startId/_bucketStartId
- Step 6: InspectionResult; NOK → QUARANTINE + non_conformances + QUARANTINE_LABEL jobs

### Task 13 — DeliveryView.razor
- Print status polling (every 2s, 10x) → MudAlert DONE/FAILED
- Reprint button (only when label_print_count > 0) → ReprintReasonDialog → ReprintLabelAsync
- Auto-save intercept: if label_print_count > 0 → WeightCorrectionDialog → container_weight_corrections INSERT → save

### Task 14 — Dialogs
- `ReprintReasonDialog`: reason_code dropdown + reason_text optional
- `WeightCorrectionDialog`: old/new weights display, reason required
- `NonConformanceDialog`: description required, severity dropdown (MINOR/MAJOR/CRITICAL)

---

After ALL tasks: delegate final BRC8 compliance check to `@reviewer`
