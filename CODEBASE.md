# NordicBeesERP - Codebase Map
*Last updated: 2026-05-07*

## Project Overview
- **Stack:** .NET 10 / Blazor Server / MudBlazor 8.15 / EF Core / MariaDB
- **Company:** MB Lakštena (Nordic Bees), honey procurement & export, ~800t/year
- **Dev DB:** MariaDB @ 100.110.26.80:3306, database: `nordic_bees_erp`, user: `erp_user`
- **Prod server:** `lakstena-dev` (10.255.8.5), Docker + GitHub Actions CI/CD
- **Auth:** Custom cookie auth + BlazorAuthStateProvider (NOT ASP.NET Identity for main users)
- **OCR:** Azure Document Intelligence exclusively (no local LLM fallback)
- **PDF:** QuestPDF

## Current Development Priority
**Payment module (accounts receivable)** — active development.
Four phases:
1. DB migration + service layer
2. Invoice list + manual payment dialog
3. Cash flow dashboard
4. Bank import wizard

**Next after payment module:** n8n IMAP invoice import pipeline (blocked by `email_invoice_imports` idempotency TODO — table already exists in DB).

## Coding Conventions
- Services registered as `AddScoped<IService, Service>()`
- DbContextFactory pattern (`IDbContextFactory<NordicBeesERPContext>`) for thread safety
- All DB access via EF Core, `QueryTrackingBehavior.NoTracking` by default
- MudBlazor components throughout (MudDataGrid, MudDialog, MudSnackbar)
- Snackbar: BottomRight, 3000ms, max 3, no duplicates
- SignalR max message: 50MB (for file uploads)
- Pages use `@page` directive with route listed below
- `.clinerules` enforces project conventions for Cline AI

## Key NuGet Packages
- `MudBlazor` 8.15.0
- `Pomelo.EntityFrameworkCore.MySql` 8.0.0
- `Azure.AI.DocumentIntelligence` 1.0.0
- `QuestPDF` 2024.10.0
- `ClosedXML` 0.104.1
- `BCrypt.Net-Next` 4.1.0

---

## Database Schema (nordic_bees_erp)

### Core Business Tables

**invoices** — Sales invoices (PVM sąskaitos faktūros)
- `id`, `invoice_number` (UNIQUE), `invoice_date`, `customer_id` → business_partners
- `payment_due_date`, `payment_term_days` (default 7), `language` (default LT)
- `invoice_type` (default 'PVM SĄSKAITA FAKTŪRA'), `reverse_charge` bool
- `subtotal_excl_vat`, `total_vat`, `total_incl_vat`, `paid_amount` (default 0)
- `status` enum('draft','confirmed','paid','disputed')
- `payment_status` enum('unpaid','partial','paid','overdue')
- `last_payment_date`, `due_date`, `delivery_id`, `pdf_path`
- `issued_by`, `received_by`, `created_by` → erp_users, `notes`

**invoice_lines**
- `id`, `invoice_id`, `line_number`, `product_id`, `product_code`
- `description`, `quantity` decimal(10,3), `unit` (default 'vnt')
- `price_excl_vat` decimal(10,4), `vat_rate` decimal(5,2)
- `line_subtotal`, `vat_amount`, `line_total`
- `lot_number`, `warehouse_id`, `notes`

**payments** — Customer payments (accounts receivable)
- `id`, `payment_date`, `invoice_id`, `customer_id`, `amount`
- `payment_method` enum('bank_transfer','cash','card','other')
- `reference_number`, `notes`, `source` enum('manual','bank_import')
- `bank_import_row_id`, `bank_import_id`, `created_by`

**payment_allocations** — Many-to-many: payment ↔ invoice
- `id`, `payment_id`, `invoice_id`, `allocated_amount`, `allocated_at`

**payment_audit_log**
- `id`, `payment_id`, `invoice_id`, `action`, `old_amount`, `new_amount`
- `changed_by`, `changed_at`, `notes`

**business_partners** — Customers & suppliers unified table
- `id`, `partner_type` enum('customer','supplier','both','expense_supplier')
- `name`, `company_code`, `vat_code`, `address`, `city`, `postal_code`
- `country` (default Lithuania), `country_code` (default LT)
- `phone`, `contact_phone`, `email`, `invoice_email`, `bank_account`
- `payment_term_days` (default 7), `default_language` (default LT)
- `default_vat_rate` (default 21.00)
- `supplier_first_name`, `supplier_last_name`, `national_id_number`, `supplier_type`
- `vies_verified`, `vies_verified_at`, `vies_name`
- `is_active`, `notes`

**deliveries** — Honey procurement from beekeepers
- `id`, `delivery_number`, `delivery_date`, `supplier_id`, `warehouse_id`
- `status` enum('RECEIVED','PRICED','PARTIAL_PAID','PAID','ACCEPTED','CLOSED')
- `total_net_weight`, `total_amount`, `paid_amount`
- `barrels_owed`, `barrels_returned`, `need_return_barrels`
- `raw_material_type_id`, `invoice_id`, `invoice_number`, `notes`

**delivery_lines**
- `id`, `delivery_id`, `product_id`, `honey_type_id`
- `container_type` enum('BARREL','BUCKET_GROUP'), `container_count`
- `total_gross_weight`, `total_tare_weight`, `total_net_weight`
- `unit_price` decimal(10,4), `line_total`, `container_id`, `notes`

**containers** — Physical honey containers
- `id`, `container_code`, `container_type` enum('BARREL','BUCKET_GROUP')
- `supplier_id`, `delivery_line_id`, `warehouse_id`, `product_id`, `honey_type_id`
- `gross_weight`, `tare_weight`, `net_weight` decimal(10,3)
- `quantity`, `remaining_quantity`
- `status` enum('RECEIVED','IN_STOCK','RESERVED','IN_PRODUCTION','SOLD','RETURNED','WRITTEN_OFF')
- `reservation_customer_id`, `reservation_notes`, `reservation_date`
- `lot_id`, `quality_params` JSON, `notes`

**expense_invoices** — Purchase invoices (išlaidos)
- `id`, `supplier_id`, `invoice_number`, `invoice_date`, `due_date`
- `amount_excl_vat`, `vat_rate`, `vat_amount`, `amount_incl_vat`, `paid_amount`
- `status` varchar(30) default 'PENDING', `currency` default 'EUR'
- `ocr_status` enum('PENDING','PROCESSING','COMPLETED','FAILED','MANUAL')
- `ocr_confidence`, `ocr_raw_json` JSON, `ocr_pipeline`
- `pending_supplier_name`, `pending_supplier_vat`, `pending_supplier_address`
- `original_file_path`, `supplier_vat_verified`, `supplier_vat_verified_at`, `notes`

**expense_invoice_lines**
- `id`, `invoice_id`, `description`, `amount_excl_vat`, `vat_rate`, `amount_incl_vat`
- `quantity`, `unit_price`, `sort_order`

**expense_payments**
- `id`, `invoice_id`, `payment_date`, `amount`
- `payment_method` enum('BANK','CASH','OTHER'), `reference`, `notes`

**expense_line_allocations**
- `id`, `invoice_line_id`, `category_id`, `cost_center_id`
- `allocated_amount`, `allocated_percent`

**expense_ocr_queue**
- `id`, `invoice_id`, `file_content` longtext (base64), `file_name`
- `attempts`, `max_attempts` (3), `status` enum('WAITING','PROCESSING','COMPLETED','FAILED')
- `error_message`, `created_at`, `processed_at`

**email_invoice_imports** — n8n IMAP idempotency (TODO: connect to n8n workflow)
- `id`, `message_id` varchar(500) UNIQUE, `subject`, `sender`, `received_at`
- `attachment_name`, `status` enum('processing','imported','failed','skipped')
- `expense_invoice_id` FK → expense_invoices, `error_message`

### Bank Import
**bank_imports**
- `id`, `filename`/`file_name`, `bank_type` (default 'other')
- `row_count`/`total_rows`, `matched_count`/`matched_rows`, `unmatched_count`/`unmatched_rows`
- `processed_rows`, `total_amount`, `status` (default 'processing')
- `imported_by`/`created_by`, `file_hash`, `error_message`

**bank_import_rows**
- `id`, `import_id`, `row_date`, `payer_name`, `payer_account`
- `amount`, `currency` (default EUR), `reference`, `description`
- `match_status` (default 'unmatched'), `matched_invoice_id`, `payment_id`

### Warehouse & Production
**warehouses** — `id`, `code` UNIQUE, `name`, `warehouse_type_id`, `address`, `city`, `country`, `warehouse_type` enum('MAIN','PRODUCTION','SALES'), `is_active`

**warehouse_stocks** — Stock levels per warehouse/product/lot
- `id`, `warehouse_id`, `product_id`, `lot_number`
- `quantity`, `reserved_quantity`, `available_quantity`, `last_movement_date`

**warehouse_stock** — VIEW: warehouse_id, warehouse_name, warehouse_type, product_id, product_name, current_stock

**stock_movements** — `id`, `warehouse_id`, `product_id`, `quantity`, `movement_type` enum('IN','OUT','TRANSFER','ADJUSTMENT'), `reference_type`, `reference_id`, `container_id`, `from_warehouse_id`, `to_warehouse_id`, `lot_id`, `created_by`, `notes`

**lots** — `id`, `lot_number` UNIQUE, `lot_type` enum('PRODUCTION','DIRECT_SALE'), `created_date`, `customer_id`, `invoice_id`, `notes`

**production_batches** — `id`, `lot_number` UNIQUE, `batch_date`, `product_id`, `quantity_produced`, `warehouse_id`, `status` enum('planned','in_progress','completed','cancelled'), `total_cost`, `cost_per_unit`, `created_by`, `notes`

**production_batch_ingredients** — `id`, `batch_id`, `ingredient_type` enum('honey_delivery','product','other'), `honey_delivery_id`, `product_id`, `quantity_used`, `unit_cost`, `total_cost`

### Products & Reference Data
**products** — `id`, `code` UNIQUE, `name`, `ean_code`, `product_type`, `category_id`, `unit_id`, `unit` (default 'kg'), `cost_price`, `sale_price`, `purchase_price`, `warehouse_managed`, `track_lots`, `min_stock_level`, `is_active`

**honey_types** — `id`, `code` UNIQUE, `name`, `name_en`, `description`, `is_active`, `sort_order`

**raw_material_types** — `id`, `name`, `code` (5 char), `is_honey`, `is_active`, `sort_order`

**expense_categories** — `id`, `parent_id`, `name`, `code` UNIQUE, `is_active`, `sort_order`

**expense_cost_centers** — `id`, `name`, `code` UNIQUE, `is_active`

**expense_budgets** — `id`, `category_id`, `year`, `month`, `planned_amount`

**product_categories** — `id`, `code` UNIQUE, `name`, `parent_id`, `description`, `is_active`

**currencies** — `id`, `code` (3), `name`, `symbol`, `is_active`

**units_of_measure** — `id`, `code` UNIQUE, `name`, `name_en`, `unit_type` enum('weight','volume','piece','length','area'), `is_active`

**honey_deliveries** — Legacy/separate table (may overlap with deliveries): `id`, `delivery_date`, `delivery_number` UNIQUE, `supplier_id`, `product_id`, `honey_type_id`, `gross_weight`, `tare_weight`, `net_weight`, `container_quantity`, `warehouse_id`, `price_per_kg`, `total_cost`, `transport_cost`, `is_soured`, `quality_grade`, `beehive_location`, `notes`

**supplier_payments** — Payments to honey suppliers: `id`, `delivery_id`, `supplier_id`, `amount`, `payment_date`, `payment_method`, `notes`

### Auth & Settings
**erp_users** — `id`, `email` UNIQUE, `password_hash` (BCrypt), `full_name`, `role` (default 'User'), `is_active`

**company_settings** — Single row, company info: MB Lakštena, code 302905315, VAT LT100013406816, address P. Širvio g. 3, Juodupė, bank AB Artea Bankas, IBAN LT217189900060467854

**app_settings** — `id`, `setting_key` UNIQUE, `setting_value`

**credit_notes** — `id`, `credit_note_number` UNIQUE, `credit_date`, `original_invoice_id`, `applied_invoice_id`, `customer_id`, `currency_id`, `language`, `credit_note_type`, `reverse_charge`, totals, `status` enum('draft','issued','cancelled'), `pdf_path`, `issued_by`, `created_by`

**credit_note_lines** — mirrors invoice_lines structure

**orders** / **order_lines** — Sales orders (draft/confirmed/in_production/shipped/delivered/cancelled)

---

## Services

### Auth
- `AuthService` — `ValidateUserAsync`, `GetAuthenticatedUserAsync`, `GetCustomerIdAsync`, `GetUserIdAsync`, `SeedAdminAsync`
- `BlazorAuthStateProvider` — `LoginAsync(email, role, fullName)`, `AuthenticationState(_anonymous)`

### Sales (Accounts Receivable)
- `InvoiceService` — CRUD, status management, PDF generation, statistics, `CreateInvoiceFromDeliveryAsync`, `GenerateNextInvoiceNumberAsync`
- `PaymentService` — `RecalculateInvoiceStatusAsync`, `GetAgingReportAsync`, `GetPaymentWithDetailsAsync`, `DeletePaymentAsync`, `MatchBankImportRowAsync`, `CreatePaymentFromBankImportAsync`, `CreateBankImportAsync`, `GetBankImportWithRowsAsync`
- `CreditNoteService` — CRUD, confirm/dispute transitions, PDF generation
- `CreditNoteNumberGenerator` — `GenerateNextNumberAsync(DateTime, IDbContextTransaction?)`
- `CustomerService` — `GetBusinessPartnerByIdAsync`, CRUD for business partners (customer type)
- `ViesService` — `LookupAsync(vatCode)`

### Procurement (Accounts Payable)
- `ExpenseService` — Full CRUD: invoices, lines, allocations, payments, budgets, categories, cost centers
- `ExpenseOcrService` — Azure DI: `IsAzureHealthyAsync`, `ExtractInvoiceDataAsync`, `EnqueueAsync`, `FindSupplierIdAsync`
- `ExpenseExportService` — Export invoices/payments/allocations to CSV/Excel
- `OcrQueueWorker` — Background `IHostedService` processing OCR queue
- `SupplierService` — CRUD for business partners (supplier type)
- `SupplierPaymentService` — `GetTotalPaidForDeliveryAsync`, `CreatePaymentAsync`

### Warehouse
- `DeliveryService` — `GetByIdAsync`, `CreateDeliveryWithContainersAsync`, `UpdatePricesAsync`, `RecalculateTotalsAsync`, `GenerateDeliveryNumberAsync`, `UpdateDeliveryStatusAsync`
- `ContainerService` — CRUD, write-off, honey type update, counts/weights, last codes
- `TransferService` — `TransferContainersAsync`
- `StockMovementService` — `CreateMovementAsync`
- `WarehouseService` — `GetAsync(id)`

### Products & Production
- `ProductService` — CRUD by id/code
- `ProductionService` — `CreateBatchAsync(NewBatchViewModel)`
- `HoneyTypeService` — `GetByIdAsync`, `CreateAsync`

### Utility
- `PdfGeneratorService` — `GenerateInvoicePdfAsync(invoiceId)`, `GetPdfPath(creditNoteNumber)`
- `CompanySettingsService` — `GetSettingsAsync()`
- `RawMaterialTypeService` — (implicit from DI registration)

---

## Pages (Routes)

| Route | File |
|-------|------|
| `/` | Home.razor |
| `/login` | Login.razor |
| `/invoices` | Invoices.razor |
| `/invoices/sales` | Invoices.razor |
| `/invoices/purchases` | Invoices.razor |
| `/invoices/create` | InvoiceCreate.razor |
| `/invoices/create-purchase` | InvoiceCreate.razor |
| `/invoices/edit/{Id:int}` | InvoiceEdit.razor |
| `/invoices/{Id:int}` | InvoiceView.razor |
| `/invoices/pdf/{Id:int}` | PdfPage.razor |
| `/payments` | PaymentsDashboard.razor |
| `/payments/history` | PaymentHistory.razor |
| `/payments/forecast` | CashFlowForecast.razor |
| `/bank-import` | BankImport.razor |
| `/credit-notes` | CreditNotes.razor |
| `/credit-notes/new` | CreditNoteCreate.razor |
| `/credit-notes/{Id:int}` | CreditNoteView.razor |
| `/credit-notes/{Id:int}/edit` | CreditNoteEdit.razor |
| `/credit-notes/pdf/{Id:int}` | CreditNotePdfPage.razor |
| `/expenses` | Expenses.razor |
| `/customers` | Customers.razor |
| `/suppliers` | Suppliers.razor |
| `/products` | Products.razor |
| `/products/create` | ProductCreate.razor |
| `/products/edit/{Id:int}` | ProductEdit.razor |
| `/inventory` | Inventory.razor |
| `/warehouses` | Warehouses.razor |
| `/warehouses/create` | WarehouseCreate.razor |
| `/warehouses/edit/{Id:int}` | WarehouseEdit.razor |
| `/warehouse/stock` | StockOverview.razor |
| `/warehouse/deliveries` | DeliveryList.razor |
| `/warehouse/deliveries/new` | DeliveryCreate.razor |
| `/warehouse/deliveries/{Id:int}` | DeliveryView.razor |
| `/warehouse/deliveries/{Id:int}/pricing` | DeliveryPricingDetail.razor |
| `/warehouse/delivery-pricing` | DeliveryPricing.razor |
| `/warehouse/transfers` | TransferHistory.razor |
| `/warehouse/write-off` | WriteOff.razor |
| `/warehouse/write-off-history` | WriteOffHistory.razor |
| `/warehouse/supplier-debts` | SupplierDebts.razor |
| `/warehouse/supplier-debts/{SupplierId:int}` | SupplierDebtDetail.razor |
| `/production/newbatch` | NewBatch.razor |
| `/statistics` | StatisticsOverview.razor |
| `/statistics/sales` | SalesStatistics.razor |
| `/settings/company` | CompanySettingsPage.razor |
| `/settings/honey-types` | HoneyTypes.razor |
| `/settings/raw-materials` | RawMaterialSettings.razor |
| `/app-settings` | Settings.razor |
| `/not-found` | NotFound.razor |
| `/Error` | Error.razor |

---

## Pending TODOs
1. **n8n `email_invoice_imports` dashboard alert** — table exists, n8n flow needs idempotency check before OCR, dashboard alert if `status=failed`
2. **Payment module** — 4-phase implementation in progress
3. **HTR project** — deferred until payment module complete
