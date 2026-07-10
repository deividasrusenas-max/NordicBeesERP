# P0a — Warehouse Labeling System

Detailed specifications: Docs/LABELING_PLAN_2.md
Read the relevant section before starting each task.

## Rules
- DB: `nordic_bees_erp_STAGING` — never production
- Read existing files before writing — never guess namespaces or signatures
- ONE file at a time → build → bump → commit → next file
- After any enum changes: `grep -r "BUCKET_GROUP" --include="*.cs" --include="*.razor" .` must return 0

## After Every File — Mandatory
```bash
dotnet build                    # zero errors
# bump patch in NordicBeesERP.csproj
git add -A
git commit -m "P0a: <FileName> — <what was done>"
```

## Delegation
- Implementation → `code` agent (one file at a time)
- After each file: build + bump + commit → `debug` agent
- After ALL tasks complete → `@reviewer`

---

## Tasks

### Task 0 — Version bump
`NordicBeesERP.csproj`: bump minor version before starting P0a.

### Task 1 — DB migration
Read Docs/LABELING_PLAN_2.md "DB schema" section.
New tables: `printers`, `weighing_stations`, `print_jobs`, `container_label_events`,
`container_weight_corrections`, `label_templates`, `supplier_approvals`, `non_conformances`, `document_files`
ALTER: `containers`, `deliveries`, `delivery_lines`, `business_partners`
Idempotent migration (IF NOT EXISTS).

### Task 2 — EF Core models + DbContext
Read Docs/LABELING_PLAN_2.md "Nauji failai" section.
New models in `Models/Printing/` and `Models/Warehouse/`.
Update: `ContainerEnums.cs`, `Container.cs`, `Delivery.cs`, `BusinessPartner.cs`
DbContext: 11 new DbSet<> + ContainerLabelEvent immutability override.
Run: `dotnet ef migrations add LabelingP0a`

### Task 3 — BUCKET_GROUP → BUCKET
Global replace all .cs and .razor files.
Verify with grep — must return 0.

### Task 4 — ContainerService cleanup
Delete: `GetLastContainerCodeAsync()`, `GetLastBucketCodeAsync()`
Build must pass.

### Task 5 — DeliveryService
`CreateDeliveryWithContainersAsync(delivery, lines, containers, operatorId)`
- delivery_number in transaction + UNIQUE constraint + retry
- Container codes: `$"{delivery.DeliveryNumber}/{seq:D3}"` — only in transaction
- `StockMovement.CreatedBy = operatorId` (bug fix)
- `delivery.CreatedByUserId = operatorId`, `container.ReceivedByUserId = operatorId`
- Enqueue RECEIPT_LABEL print_jobs per container after creation

### Task 6 — IPrinterGateway
- `IPrinterGateway`: `Task<PrintResult> PrintAsync(string zpl, Printer printer)`
- `StubPrinterGateway`: ZPL → `/tmp/stub_labels/{job_id}.zpl`, PNG via Labelary API, always Success=true
- `HttpPrinterGateway`: POST `{printer.EndpointUrl}/print`, timeout 5s

### Task 7 — ILabelTemplateService
- `RenderZpl(LabelTemplateType, ContainerLabelData) → string`
- `PreviewPngAsync(string zpl) → Task<byte[]>`
- ZplLabelTemplateService: hardcoded ZPL 108mm (850 dots @ 200dpi)
- ContainerLabelData: ContainerCode, SupplierName, RawMaterialName, OriginCountry,
  NetWeightKg, TareWeightKg, GrossWeightKg, DeliveryDate (NOT DateTime.Now), WarehouseName, NonConformanceId?
- RECEIPT: ContainerCode + QR, supplier, material, origin, net weight, date, warehouse
- QUARANTINE: same + large KARANTINAS mark

### Task 8 — ILabelPrintService
- `PrintReceiptLabelAsync(containerId, stationId, operatorId) → int`
- `PrintQuarantineLabelAsync(containerId, stationId, operatorId) → int`
- `ReprintLabelAsync(containerId, reasonCode, reasonText?, operatorId) → int`
- Returns print_job.id
- Flow: get container → RenderZpl → INSERT print_jobs(PENDING) → INSERT container_label_events

### Task 9 — LabelPrintWorker
BackgroundService loop every 1s:
- PENDING jobs grouped by printer_id → SemaphoreSlim(1) per printer
- PENDING → PROCESSING → DONE or FAILED
- Retry: max 3, exponential backoff (2^retry seconds)
- Final FAILED: INSERT container_label_events(PRINT_FAILED, operator_id = print_job.created_by_user_id)

### Task 10 — Program.cs
Register all new services.
`appsettings.json`: `"Printing": { "ConnectionType": "STUB" }`
IPrinterGateway: StubPrinterGateway if != "HTTP", else HttpPrinterGateway.

### Task 11 — warehouse.css
Create `wwwroot/css/warehouse.css` per Docs/LABELING_PLAN_2.md "UI stilius".
Add `<link>` in `App.razor`.

### Task 12 — DeliveryCreate.razor
Read Docs/LABELING_PLAN_2.md "Wizard" section.
- Step 1 (NEW): workstation selection → ProtectedSessionStorage["station_id"], _warehouseId readonly
- Step 2: OriginCountry auto-fill, supplier approval check (Warning/Error)
- Step 5: inputmode="decimal", container codes show "Will be generated", remove _startId/_bucketStartId
- Step 6: InspectionResult; NOK → QUARANTINE + non_conformances + QUARANTINE_LABEL jobs

### Task 13 — DeliveryView.razor
- Print status polling every 2s (max 10x) → MudAlert DONE/FAILED
- Reprint button (only when label_print_count > 0) → ReprintReasonDialog → ReprintLabelAsync
- Auto-save intercept: label_print_count > 0 → WeightCorrectionDialog → container_weight_corrections INSERT → save

### Task 14 — Dialogs
- `ReprintReasonDialog.razor`: reason_code dropdown + reason_text optional
- `WeightCorrectionDialog.razor`: old/new weights, reason required
- `NonConformanceDialog.razor`: description required, severity (MINOR/MAJOR/CRITICAL)

---

After ALL tasks: `@reviewer`
