# NordicBeesERP — Codebase Documentation

> Generated from live MySQL database, source files, and project configuration.  
> Version: 0.9.3.3 | Last updated: 2026-05-30

---

## 1. PROJECT OVERVIEW

**Name:** NordicBeesERP  
**Type:** Blazor Server ERP System  
**Framework:** .NET 10, ASP.NET Core  
**Database:** MariaDB (database: `nordic_bees_erp`)  
**UI Framework:** MudBlazor  
**Authentication:** Custom cookie-based (IAuthService + BlazorAuthStateProvider)  
**GitHub:** https://github.com/deividasrusenas-max/NordicBeesERP.git

### Purpose
MB Lakštena — bitininkystės produktų gamyba ir pardavimas (Beekeeping products manufacturing and sales)

### Key Configuration (from appsettings.json)
- **Default Connection:** `server=localhost;database=nordic_bees_erp;userid=root;password=`
- **Azure OpenAI:** Endpoint + key configured via app_settings table
- **VIES Lookup Timeout:** 15 seconds
- **Blazor RenderMode:** InteractiveServer (Server prerendered)
- **Reconnection:** Max 3 attempts, 30s timeout, 5s delay

### .csproj Key Settings
- Target: `net10.0`
- Nullable: enable
- LangVersion: latest
- NoWarn: NU1902,NU1903
- FrameworkReference: Microsoft.AspNetCore.App
- Runtime: win-x64, Linux, self-contained false

---

## 2. DATABASE SCHEMA

### 2.1 Tables (from MySQL `SHOW TABLES`)

| Table | Description |
|-------|-------------|
| `__EFMigrationsHistory` | EF Core migration tracking |
| `app_settings` | System-wide settings (key-value pairs) |
| `bank_import_rows` | Bank statement import rows |
| `bank_imports` | Bank import batches |
| `business_partners` | Unified clients and suppliers |
| `companies` | Nordic Bees company info |
| `company_settings` | Company settings (VAT code, name, etc.) |
| `containers` | Container management |
| `credit_note_lines` | Credit note line items |
| `credit_notes` | Sales credit notes |
| `currencies` | Currency lookup |
| `deliveries` | Deliveries (honey, raw materials) |
| `delivery_lines` | Delivery line items |
| `email_invoice_imports` | Email-based invoice imports |
| `erp_users` | ERP users for cookie auth |
| `expense_budgets` | Expense budgets |
| `expense_categories` | Expense categories |
| `expense_cost_centers` | Expense cost centers |
| `expense_invoice_audit` | Expense invoice audit log |
| `expense_invoice_lines` | Expense invoice line items |
| `expense_invoices` | Sales expense invoices |
| `expense_line_allocations` | Expense line allocations |
| `expense_ocr_queue` | OCR processing queue |
| `expense_payments` | Expense payments |
| `honey_deliveries` | Honey purchases from beekeepers |
| `honey_types` | Honey types (liepa, rapsas, etc.) |
| `invoices` | Sales invoices (LAK/ULAK prefix) |
| `lots` | Lot/batch tracking |
| `order_lines` | Order line items |
| `orders` | Orders (future integration) |
| `payment_allocations` | Payment-to-invoice allocations |
| `payment_audit_log` | Payment audit trail |
| `payments` | Sales payments |
| `product_categories` | Product category hierarchy |
| `products` | Product catalog (raw materials, packaging, finished goods) |
| `raw_material_types` | Raw material types |
| `stock_movements` | Stock movement records |
| `supplier_payments` | Supplier payments |
| `units_of_measure` | Units of measure |
| `users` | System users (authentication) |
| `warehouse_stock` | View: warehouse stock by product/lot |
| `warehouse_stocks` | Warehouse stock by product and lot |
| `warehouse_types` | Warehouse types |
| `warehouses` | Warehouses and locations |

### 2.2 Foreign Key Constraints (from MySQL)

```
bank_import_rows.import_id     → bank_imports.id
credit_note_lines.credit_note_id → credit_notes.id
deliveries.supplier_id         → business_partners.id
delivery_lines.delivery_id     → deliveries.id
email_invoice_imports.user_id  → erp_users.id
expense_budgets.company_id     → companies.id
expense_budgets.user_id        → users.id
expense_invoice_lines.invoice_id → expense_invoices.id
expense_invoices.supplier_id   → business_partners.id
expense_invoices.category_id   → expense_categories.id
expense_payments.invoice_id    → expense_invoices.id
honey_deliveries.supplier_id   → business_partners.id
lots.expense_invoice_id        → expense_invoices.id
lots.product_id                → products.id
lots.supplier_id               → business_partners.id
order_lines.delivery_id        → deliveries.id
payment_allocations.invoice_id → invoices.id
payment_allocations.payment_id → payments.id
payment_audit_log.payment_id   → payments.id
payment_audit_log.changed_by   → users.id
payments.invoice_id            → invoices.id
payments.customer_id           → business_partners.id
production_batch_ingredients.batch_id → production_batches.id
production_batch_ingredients.raw_material_id → products.id
production_batches.product_id  → products.id
products.category_id           → product_categories.id
products.default_supplier_id   → business_partners.id
products.packaging_material_id → products.id
supplier_payments.supplier_id  → business_partners.id
```

### 2.3 Indexes (selected)

```
bank_imports            idx_status            (status)
business_partners       idx_vat_code           (vat_code) UNIQUE
business_partners       idx_partner_type       (partner_type)
credit_notes            idx_customer           (customer_id)
credit_notes            idx_number             (invoice_number) UNIQUE
credit_notes            idx_status             (status)
expense_invoices        idx_invoice_number     (invoice_number)
expense_invoices        idx_supplier           (supplier_id)
expense_invoices        idx_status             (status)
expense_invoices        idx_invoice_number_amount (invoice_number, amount_incl_vat) -- duplicate detection
expense_invoice_lines   idx_invoice            (invoice_id)
expense_payments        idx_invoice            (invoice_id)
expense_payments        idx_payment_date       (payment_date)
invoices                idx_customer           (customer_id)
invoices                idx_number             (invoice_number) UNIQUE
invoices                idx_payment_status     (payment_status)
lots                    idx_lot_number         (lot_number) UNIQUE
lots                    idx_invoice          (expense_invoice_id)
lots                    idx_product_lot        (product_id, lot_number) UNIQUE
payment_allocations     idx_invoice            (invoice_id)
payment_allocations     idx_payment            (payment_id)
payments                idx_customer           (customer_id)
payments                idx_payment_date       (payment_date)
production_batches      idx_product            (product_id)
products                idx_category           (category_id)
products                idx_sku                (sku) UNIQUE
users                   idx_username           (username) UNIQUE
users                   idx_email              (email) UNIQUE
warehouse_stocks        idx_product            (product_id)
warehouses              idx_company            (company_id)
```

---

## 3. MODELS

### 3.1 Core Models

#### `BusinessPartner` (Table: `business_partners`)
- `Id`, `PartnerType` (Customer/Supplier/Both enum), `Name`, `CompanyCode`, `VatCode`, `Address`, `City`, `PostalCode`, `Country`, `CountryCode`
- `Phone`, `ContactPhone`, `Email`, `InvoiceEmail`, `BankAccount`
- `PaymentTermDays`, `DefaultLanguage`, `DefaultVatRate`, `Notes`
- `IsActive`, `CreatedAt`, `UpdatedAt`
- Navigation: `CreatedInvoices` (FK), `Invoices` (FK), `Payments` (FK), `Deliveries` (FK), `HoneyDeliveries` (FK), `LotsSupplier` (FK)
- FKs: `CreatedById` → `users`, `DefaultSupplierId` → `products`

#### `Invoice` (Table: `invoices`)
- `Id`, `InvoiceNumber` (UNIQUE), `InvoiceDate`, `DueDate`, `Status` (enum)
- `CustomerId` → `business_partners`
- `SubtotalExclVat`, `TotalVat`, `TotalInclVat`
- `PaidAmount`, `PaymentStatus` (unpaid/partial/paid)
- `LastPaymentDate`, `Source` (MANUAL/EMAIL/BANK_IMPORT)
- `CreatedAt`, `UpdatedAt`
- Navigation: `Customer`, `CreatedById` → `users`, `PaymentAllocations`, `Payments`

#### `Payment` (Table: `payments`)
- `Id`, `PaymentDate`, `Amount`, `PaymentMethod` (BankTransfer/Cash/Card/Other enum)
- `InvoiceId` → `invoices`, `CustomerId` → `business_partners`
- `ReferenceNumber`, `Notes`, `Source` (MANUAL/BANK_IMPORT/ULAK)
- `BankImportRowId` → `bank_import_rows`
- `CreatedBy` → `users`, `CreatedAt`, `UpdatedAt`
- Navigation: `Allocations`, `AuditLogs`, `Invoice`, `Customer`, `CreatedByUser`

#### `PaymentAllocation` (Table: `payment_allocations`)
- `Id`, `PaymentId` → `payments`, `InvoiceId` → `invoices`
- `AllocatedAmount`, `AllocatedAt`

#### `PaymentAuditLog` (Table: `payment_audit_log`)
- `Id`, `PaymentId` → `payments`, `InvoiceId` → `invoices`
- `Action` (create/update/delete), `OldAmount`, `NewAmount`
- `ChangedBy` → `users`, `ChangedAt`, `Notes`

#### `Customer` (ViewModel — NOT a table)
Maps to `BusinessPartner` filtered by `PartnerType.Customer` or `PartnerType.Both`
- `Id`, `Name`, `City`, `VatCode`, `PartnerType` (as string), `CompanyCode`, `PaymentTermDays`, `DefaultLanguage`, `DefaultVatRate`, `Phone`, `Email`, `BankAccount`, `Notes`, `IsActive`, `CreatedAt`, `UpdatedAt`

#### `Delivery` (Table: `deliveries`)
- `Id`, `DeliveryDate`, `SupplierId` → `business_partners`
- `DeliveryType`, `TotalWeight`, `Notes`, `CreatedAt`
- Navigation: `Lines`, `Supplier`

#### `DeliveryLine` (Table: `delivery_lines`)
- `Id`, `DeliveryId` → `deliveries`
- `HoneyType` (name string), `HoneyTypeId` → `honey_types`
- `GrossWeight`, `TareWeight`, `NetWeight`, `QualityNotes`, `Temperature`
- `CreatedAt`

#### `HoneyDelivery` (Table: `honey_deliveries`)
- `Id`, `SupplierId` → `business_partners`, `DeliveryDate`
- `HoneyType` (name string), `Quantity`, `QualityNotes`
- `CreatedAt`

#### `HoneyType` (Table: `honey_types`)
- `Id`, `Name`, `Code`, `Color`, `IsActive`

### 3.2 Expense Models (Models/Expenses/)

#### `ExpenseInvoice` (Table: `expense_invoices`)
- `Id`, `SupplierId` → `business_partners` (nullable)
- `InvoiceType` (STANDARD/ULAK), `PendingSupplier*` fields
- `InvoiceNumber`, `InvoiceDate`, `DueDate`, `Amount*`, `Currency`
- `Status` (PENDING/PENDING_SUPPLIER/NEEDS_REVIEW/DUPLICATE_PENDING/REJECTED/PARTIAL/PAID/APPROVED/APPROVED_PAID)
- `OcrStatus`, `OcrConfidence` (int), `OcrFlags` (JSON), `OcrPipeline`
- `SupplierVatVerified`, `SupplierVatVerifiedName`
- `CategoryId` → `expense_categories`, `Notes`, `Source`, `OriginalFilename`, `OriginalFilePath`
- `ApprovedBy`, `ApprovedAt`, `RejectedReason`
- `[NotMapped]` navigation: `Category`, `ExpenseInvoiceLines` (use GetInvoiceLinesAsync separately)

#### `ExpenseInvoiceLine` (Table: `expense_invoice_lines`)
- `Id`, `InvoiceId` → `expense_invoices`
- `CategoryId` → `expense_categories` (nullable)
- `Description`, `Quantity`, `UnitPrice`, `UnitOfMeasure`, `Amount*`, `VatRate`, `SortOrder`

#### `ExpenseInvoiceAudit` (Table: `expense_invoice_audit`)
- `Id`, `InvoiceId`, `InvoiceNumber`
- `Action` (CREATED/STATUS_CHANGED/SUPPLIER_ASSIGNED/PAYMENT_ADDED/APPROVED/REJECTED/EDITED)
- `ActionDetails`, `OldStatus`, `NewStatus`
- `PerformedBy`, `PerformedAt`

#### `ExpensePayment` (Table: `expense_payments`)
- `Id`, `InvoiceId` → `expense_invoices`, `Amount`, `PaymentDate`
- `PaymentMethod` (BANK/CASH/OTHER)
- `Reference`, `Notes`

#### `ExpenseCategory` (Table: `expense_categories`)
- `Id`, `Name`, `Code`, `IsActive`, `SortOrder`

#### `ExpenseBudget` (Table: `expense_budgets`)
- `Id`, `CompanyId` → `companies`, `UserId` → `users`
- `Year`, `Month`, `Amount`, `Category` (nullable), `Notes`

### 3.3 Credit Note Models (Models/CreditNoteModels.cs)

#### `CreditNote` (Table: `credit_notes`)
- `Id`, `InvoiceId` → `invoices` (nullable)
- `InvoiceNumber` (UNIQUE), `CreditType` (SALES), `IssueDate`, `Reason`
- `SubtotalExclVat`, `TotalVat`, `TotalInclVat`, `Status` (DRAFT/SENT/PAID/CANCELLED)
- `Notes`, `CreatedAt`, `UpdatedAt`
- Navigation: `Lines`, `Invoice`, `CreatedBy` → `users`

#### `CreditNoteLine` (Table: `credit_note_lines`)
- `Id`, `CreditNoteId` → `credit_notes`
- `Description`, `Quantity`, `UnitPrice`, `VatRate`, `TotalAmount`

### 3.4 Warehouse/Production Models (Models/WarehouseModule/)

#### `Product` (Table: `products`)
- `Id`, `Name`, `Sku` (UNIQUE), `CategoryId` → `product_categories`
- `ProductId` (self-ref for packaging → raw material mapping)
- `ProductId` → `products` (packaging_material_id)
- `DefaultSupplierId` → `business_partners`
- `UnitOfMeasure`, `PurchasePrice`, `SellingPrice`, `MinStock`, `MaxStock`
- `IsActive`
- Navigation: `Category`, `InLots1`, `InLots2`, `InProductionBatches`, `OrderLines`, `UsedAsPackaging`, `WarehouseStocks`

#### `Warehouse` (Table: `warehouses`)
- `Id`, `CompanyId` → `companies`, `Name`, `WarehouseTypeId` → `warehouse_types`
- `Address`, `IsActive`, `CreatedAt`
- Navigation: `Company`, `Type`, `Stocks`, `StockMovements`

#### `WarehouseStock` (Table: `warehouse_stocks`)
- `Id`, `WarehouseId` → `warehouses`, `ProductId` → `products`
- `LotId` → `lots`, `Quantity`, `LastUpdated`
- UNIQUE: `(product_id, lot_id, warehouse_id)`

#### `Lot` (Table: `lots`)
- `Id`, `LotNumber` (UNIQUE), `ProductId` → `products`
- `ExpenseInvoiceId` → `expense_invoices`
- `SupplierId` → `business_partners`
- `Quantity`, `ExpiryDate`, `CreatedAt`
- UNIQUE: `(product_id, lot_number)`
- Navigation: `ExpenseInvoice`, `Product`, `Supplier`, `WarehouseStocks`

#### `ProductionBatch` (Table: `production_batches`)
- `Id`, `BatchNumber`, `ProductId` → `products`, `QuantityProduced`
- `StartDate`, `EndDate`, `CreatedBy`, `CreatedAt`, `Status` (PLANNED/IN_PROGRESS/COMPLETED)
- Navigation: `Product`, `Ingredients`

#### `ProductionBatchIngredient` (Table: `production_batch_ingredients`)
- `Id`, `BatchId` → `production_batches`, `RawMaterialId` → `products`
- `QuantityUsed`, `LotId` → `lots`

#### `StockMovement` (Table: `stock_movements`)
- `Id`, `WarehouseId` → `warehouses`, `ProductId` → `products`
- `MovementType` (IN/OUT/TRANSFER/ADJUSTMENT)
- `Quantity`, `ReferenceType`, `ReferenceId`, `Notes`, `CreatedAt`
- Navigation: `Warehouse`, `Product`

#### `Container` (Table: `containers`)
- `Id`, `ContainerNumber`, `Capacity`, `CurrentWeight`, `Status`
- `WarehouseId` → `warehouses`, `Location`, `LastUsedAt`, `CreatedAt`

#### `RawMaterialType` (Table: `raw_material_types`)
- `Id`, `Name`, `Code`, `Description`, `IsActive`

#### `ProductCategory` (Table: `product_categories`)
- `Id`, `Name`, `ParentId` (self-ref), `Description`, `SortOrder`, `IsActive`

### 3.5 Other Models

#### `User` (Table: `users`)
- `Id`, `Username` (UNIQUE), `Email` (UNIQUE), `FullName`
- `Role` (Admin/Manager/Warehouse/Viewer), `IsActive`
- `PasswordHash`, `CreatedAt`, `UpdatedAt`

#### `ErpUser` (Table: `erp_users`)
- `Id`, `Username`, `PasswordHash`, `Email`, `FullName`, `Role`, `IsActive`, `LastLoginAt`, `CreatedAt`, `UpdatedAt`

#### `Company` (Table: `companies`)
- `Id`, `Name`, `Code`, `VatCode`, `Address`, `City`, `PostalCode`, `CountryCode`, `IsActive`

#### `CompanySetting` (single row per company)
- `Id`, `CompanyId` → `companies`
- `CompanyName`, `CompanyCode`, `VatCode`, `Address`, `BankName`, `BankIban`
- `DefaultCurrency`, `DefaultPaymentTermDays`, `DefaultLanguage`, `EmailSettings` (JSON)
- `PdfLogoPath`, `PdfFooterText`, `EmailFrom`, `IsActive`

#### `AppSetting` (Table: `app_settings`)
- Key-value pairs for system settings (Azure credentials, etc.)

#### `Currency` (Table: `currencies`)
- `Code` (PK), `Name`, `Symbol`, `IsActive`

#### `UnitOfMeasure` (Table: `units_of_measure`)
- `Id`, `Name`, `Code`, `IsActive`

#### `WarehouseType` (Table: `warehouse_types`)
- `Id`, `Name`, `Description`, `IsActive`

---

## 4. SERVICES

### 4.1 Core Services

| Service | Key Methods |
|---------|-----------|
| **AuthService** | `GetAuthenticatedUserAsync()`, `ValidateUserAsync(username, password)`, `ValidateTokenAsync(token)` |
| **BlazorAuthStateProvider** | `NotifyChanged()`, `HasChanges()` — notifies UI of auth changes |
| **CompanySettingsService** | `GetSettingsAsync()`, `UpdateSettingsAsync(settings)`, `GetPdfSettingsAsync()` |
| **CustomerService** | `GetCustomersAsync()`, `GetSuppliersAsync()`, `CreateBusinessPartnerAsync()`, `UpdateBusinessPartnerAsync()`, `SaveCustomerAsync()`, `DeleteBusinessPartnerAsync()` |
| **InvoiceService** | `GetAllInvoicesAsync()`, `GetInvoiceByNumberAsync(number)`, `CreateInvoiceAsync(invoice, lines)`, `UpdateInvoiceAsync(invoice, lines)`, `DeleteInvoiceAsync(id)`, `GetUnpaidInvoicesAsync()`, `GenerateInvoiceNumberAsync()` |
| **PaymentService** | `RegisterPaymentAsync(invoices, amount, date, method)`, `RecalculateInvoiceStatusAsync(invoiceId)`, `GetUnpaidInvoicesAsync()`, `GetCashFlowForecastAsync(weeks)`, `GetAgingReportAsync()`, `GetPaymentHistoryAsync(...)`, `GetPaymentDetailAsync(id)`, `DeletePaymentAsync(id, userId)`, `UpdatePaymentAsync(...)`, `GetSalesInvoicesAsync(...)`, `GetTotalIncomeThisYearAsync()`, `GetMonthlyIncomeAsync(year)` |
| **DeliveryService** | `GetDeliveriesAsync()`, `GetDeliveryByIdAsync(id)`, `CreateDeliveryAsync(delivery, lines)`, `UpdateDeliveryAsync(delivery, lines)`, `DeleteDeliveryAsync(id)`, `GetDeliveriesByYearAsync(year)`, `GetTotalDeliveryWeightByYearAsync(year)` |
| **PdfGeneratorService** | `GenerateInvoicePdfAsync(invoice, customer, lines)`, `GeneratePaymentReceiptPdfAsync(payment, invoice)`, `GenerateCreditNotePdfAsync(creditNote, lines, customer)` |

### 4.2 Expense Module Services

| Service | Key Methods |
|---------|-----------|
| **IExpenseService** | `GetInvoiceWithDetailsAsync(id)`, `GetInvoiceLinesAsync(id)`, `GetAllInvoicesAsync(yearFilter)`, `UpdateInvoiceAsync(invoice)`, `GetInvoiceStatisticsAsync(yearFilter)`, `GetInvoiceCategoriesAsync()`, `GetExpenseDashboardAsync(yearFilter)`, `DeleteExpenseInvoiceAsync(id)` |
| **IExpenseOcrService** | `ProcessAsync(base64, fileName)`, `ExtractInvoiceDataAsync(base64, fileName)` (alias), `FindSupplierIdAsync(name, vat)`, `IsAzureHealthyAsync()` |
| **ExpenseOcrService** | Azure DI invoice extraction, VIES lookup, supplier matching, flag generation |
| **ExpenseService** | `CreateFromOcrAsync(ocrResult, source)`, `CheckDuplicateAsync(supplierId, invoiceNumber, amount)`, `AssignSupplierAsync(invoiceId, supplierId, performedBy)`, `ApproveAsync(invoiceId, performedBy)`, `RejectAsync(invoiceId, reason, performedBy)` |
| **IExpenseCategoryService** | `GetCategoriesAsync()`, `CreateCategoryAsync()`, `UpdateCategoryAsync()`, `DeleteCategoryAsync(id)` |

### 4.3 Warehouse/Production Services

| Service | Key Methods |
|---------|-----------|
| **WarehouseService** | `GetProductsAsync()`, `GetWarehousesAsync()`, `GetStockAsync(productId)`, `GetAllStockAsync()`, `GetLowStockAsync()`, `GetStockValueAsync()`, `UpdateStockAsync()`, `AddStockAsync()`, `RemoveStockAsync()`, `TransferStockAsync()`, `AdjustStockAsync()`, `GetStockMovementsAsync()`, `GetProductsWithSupplierInfoAsync()` |
| **ProductionService** | `GetProductionBatchesAsync()`, `GetBatchByIdAsync(id)`, `CreateBatchAsync(batch, rawMaterials)`, `UpdateBatchAsync(id, quantity, rawMaterials)`, `DeleteBatchAsync(id)`, `GetProductionReportsAsync()` |
| **ProductService** | `GetProductsAsync()`, `GetProductByIdAsync(id)`, `CreateProductAsync(product)`, `UpdateProductAsync(product)`, `DeleteProductAsync(id)`, `GetLowStockProductsAsync()`, `SearchProductsAsync(searchTerm)` |
| **IContainerService** | `GetContainersAsync()`, `GetAvailableContainersAsync()`, `CreateContainerAsync()`, `UpdateContainerAsync()`, `DeleteContainerAsync(id)`, `AssignContainerToDeliveryAsync()` |
| **RawMaterialTypeService** | `GetRawMaterialTypesAsync()`, `CreateRawMaterialTypeAsync()`, `UpdateRawMaterialTypeAsync()`, `DeleteRawMaterialTypeAsync(id)` |
| **HoneyTypeService** | `GetHoneyTypesAsync()`, `CreateHoneyTypeAsync()`, `UpdateHoneyTypeAsync()`, `DeleteHoneyTypeAsync(id)` |
| **StockMovementService** | `GetStockMovementsAsync()`, `CreateStockMovementAsync()` |

### 4.4 Credit Note Services

| Service | Key Methods |
|---------|-----------|
| **CreditNoteService** | `GetAllCreditNotesAsync()`, `GetCreditNoteByIdAsync(id)`, `CreateCreditNoteAsync(creditNote, lines)`, `UpdateCreditNoteAsync(creditNote, lines)`, `DeleteCreditNoteAsync(id)`, `GenerateCreditNoteNumberAsync()`, `GetCreditNoteStatisticsAsync()` |
| **ICreditNoteNumberGenerator** | `GenerateAsync()` — produces unique credit note numbers |

### 4.5 Other Services

| Service | Key Methods |
|---------|-----------|
| **ErpUserService** | `GetAllUsersAsync()`, `GetUserByIdAsync(id)`, `CreateUserAsync(user)`, `UpdateUserAsync(user)`, `DeleteUserAsync(id)`, `UpdateLastLoginAsync(id)`, `SearchUsersAsync(searchTerm)`, `GetUsersByRoleAsync(role)` |
| **SupplierService** | `GetAllSuppliersAsync()`, `SearchSuppliersAsync(searchTerm)`, `GetSuppliersByTypeAsync(type)`, `GetSupplierWithInvoicesAsync(id)`, `GetSupplierWithPaymentsAsync(id)`, `GetSupplierWithDeliveriesAsync(id)` |
| **ViesService** | `LookupAsync(vatCode)` — EU VIES VAT validation (15s timeout) |
| **OcrQueueWorker** | Background worker processing `expense_ocr_queue` table entries |
| **ExpenseExportService** | Export expense data to CSV/PDF |
| **ContainerService** | Container lifecycle management |

---

## 5. COMPONENTS

### 5.1 Layout

| Component | Route/Purpose |
|-----------|--------------|
| `MainLayout.razor` | Main app layout with NavMenu |
| `EmptyLayout.razor` | Layout without navigation |
| `ReconnectModal.razor` | Blazor Server reconnect UI |

### 5.2 Pages (Components/Pages/)

| Component | Route | Purpose |
|-----------|-------|---------|
| `BankImport.razor` | `/bank-import` | Bank statement import and matching |
| `CashFlowForecast.razor` | `/cash-flow-forecast` | Cash flow projection (8 weeks) |
| `CompanySettingsPage.razor` | `/company-settings` | Company settings management |
| `Counter.razor` | `/counter` | Blazor template counter page |
| `CreditNoteCreate.razor` | `/credit-notes/create` | Create new credit note |
| `CreditNoteEdit.razor` | `/credit-notes/edit/{id}` | Edit existing credit note |
| `CreditNotePdfPage.razor` | `/credit-notes/pdf/{id}` | PDF preview page |
| `CreditNotes.razor` | `/credit-notes` | Credit note list |
| `CreditNoteView.razor` | `/credit-notes/view/{id}` | Credit note detail view |
| `Customers.razor` | `/customers` | Customer/supplier management |
| `Error.razor` | `/error` | Error page |
| `ExpenseCategorySettings.razor` | `/expense-categories` | Expense category management |
| `ExpenseForecast.razor` | `/expense-forecast` | Expense forecasting |

### 5.3 Dialogs (Components/Dialogs/)

| Component | Purpose |
|-----------|---------|
| `ExpenseUploadDialog.razor` | Upload and OCR invoice PDF |
| `InvoiceDetailDialog.razor` | View/edit expense invoice detail |
| `AssignSupplierDialog.razor` | Assign/create supplier for invoice |
| `SupplierCreateDialog.razor` | Create new business partner |
| `SupplierEditDialog.razor` | Edit existing business partner |
| `SupplierSelectDialog.razor` | Select from existing suppliers |
| `PaymentDetailDialog.razor` | Payment detail view |
| `PaymentDialog.razor` | Register new payment |
| `EditPaymentDialog.razor` | Edit existing payment |
| `PaymentRegisterDialog.razor` | Bulk payment registration |
| `CustomerSelectDialog.razor` | Customer selection dialog |
| `ClientCreateDialog.razor` | Create new client |
| `ProductSelectDialog.razor` | Product selection |
| `RawMaterialSelectDialog.razor` | Raw material selection |
| `BatchSelectDialog.razor` | Production batch selection |
| `ResolveSupplierDialog.razor` | Supplier resolution for OCR |
| `DuplicateInvoiceDialog.razor` | Duplicate invoice handling |
| `RejectReasonDialog.razor` | Enter rejection reason |
| `ExpenseAllocationEditor.razor` | Expense line allocation editor |
| `ExpenseBudgetDialog.razor` | Budget management |
| `ExpenseCashFlow.razor` | Expense cash flow |
| `ExpenseCategoryEditDialog.razor` | Category CRUD |
| `ExpenseCostCenterEditDialog.razor` | Cost center CRUD |
| `ExpensePaymentDialog.razor` | Expense payment dialog |
| `ExpenseSupplierHistory.razor` | Supplier history viewer |
| `InvoiceDetailDialog.razor` | Invoice detail dialog |
| `ConfirmDialog.razor` | Generic confirmation dialog |
| `CustomerCreateDialog.razor` | Customer creation |
| `CustomerSelectDialog.razor` | Customer selection |
| `ErpUserDialog.razor` | ERP user management |
| `ResetPasswordDialog.razor` | Password reset |
| `CreditNoteCreateDialog.razor` | Credit note creation |
| `PaymentDetailDialog.razor` | Payment detail |

### 5.4 Navigation Menu Routes

From `NavMenu.razor`:
- **Dashboard** → `/`
- **Sąskaitos** (Invoices) → `/invoices`
- **Mokėjimai** (Payments) → `/payments`
- **Klientai** (Customers) → `/customers`
- **Tiekėjai** (Suppliers) → `/suppliers`
- **Sandėlis** (Warehouse) → `/warehouse`
- **Gamyba** (Production) → `/production`
- **Pristatymai** (Deliveries) → `/deliveries`
- **Medus** (Honey) → `/honey`
- **Išlaidos** (Expenses) → `/expenses`
- **Kreditai** (Credit Notes) → `/credit-notes`
- **Bankas** (Bank) → `/bank-import`
- **Ataskaitos** (Reports) → various
- **Naudotojai** (Users) → `/users`
- **Nustatymai** (Settings) → `/company-settings`

---

## 6. KNOWN ISSUES & DB ↔ MODEL DISCREPANCIES

### 6.1 `[NotMapped]` Navigation Properties

**ExpenseInvoice** has `[NotMapped]` on all navigation properties AND `entity.Ignore()` in DbContext:
- `Category` → must use `GetInvoiceWithDetailsAsync()`
- `ExpenseInvoiceLines` → must use `GetInvoiceLinesAsync()` — `Include()` does NOT work
- `ExpenseInvoiceAudit` → separate query needed

**Confirmed in:** `ExpenseService.cs`, `nordicbees-standards.md` (PATTERN 7)

### 6.2 `PaymentMethod` Format Mismatch

**Expense payments** use: `'BANK'`, `'CASH'`, `'OTHER'` (ENUM in SQL)  
**Sales payments** use: `PaymentMethod.BankTransfer`, `PaymentMethod.Cash`, etc. (C# enum) + string mapping:
```csharp
"bank_transfer" => PaymentMethod.BankTransfer,
"cash" => PaymentMethod.Cash,
```

⚠️ DRAUDŽIAMA use `'bank_transfer'` in expense module — must use `'BANK'`

### 6.3 `Customer` Model is Not a Table

`Customer` class exists in `Models/` but maps entirely to `BusinessPartner`. The `CustomerService` filters by `PartnerType`. No separate `customers` table exists.

### 6.4 `OcrConfidence` Type Change

DB column `ocr_confidence` is `int?` (was `decimal?` in older migrations). Models correctly use `int?`.

### 6.5 `expense_invoices.invoice_type` ENUM

Added via migration `20260513_AddInvoiceTypeToExpenseInvoices.sql`. Values: `STANDARD`, `ULAK`.  
ULAK invoices have 6% VAT — NOT 0%. `ZERO_VAT` flag should NOT be used for ULAK.

### 6.6 `expense_invoices.source` ENUM

Values: `'MANUAL'`, `'EMAIL'`, `'N8N'` (VARCHAR(10)). Default `'MANUAL'`.

### 6.7 `expense_invoices.approved_by` MaxLength

`approved_by` is `VARCHAR(100)` — `ExpenseInvoice.ApprovedBy` has `[MaxLength(100)]`.

### 6.8 Duplicate Detection Index

`expense_invoices` has composite index on `(invoice_number, amount_incl_vat)` for duplicate detection.  
Logic: same `invoice_number` + same `amount_incl_vat` (within 0.01€) = duplicate, regardless of supplier.

### 6.9 `warehouse_stocks` UNIQUE Constraint

`(product_id, lot_id, warehouse_id)` must be unique. No two stocks for same product+lot+warehouse.

### 6.10 `lot_number` UNIQUE + Per-Product Unique

`lots.lot_number` is UNIQUE globally. Also `(product_id, lot_number)` is UNIQUE.

### 6.11 `users` vs `erp_users`

Two separate user tables:
- `users` — system users with `Role` enum (Admin/Manager/Warehouse/Viewer)
- `erp_users` — legacy/auth users with `Username`, `PasswordHash`
- `IAuthService` works with `erp_users` for cookie auth
- Audit logs reference `users.Id` via `CreatedBy`/`ChangedBy`

⚠️ Potential mismatch: audit entries may reference user IDs that don't exist in `erp_users`.

### 6.12 `business_partners.default_expense_category_id`

Added via migration `20260527212708`. Business partners can have a default expense category.

### 6.13 `HoneyType` and `RawMaterialType` in `Deliveries`

`DeliveryLine` has both `HoneyType` (string name) AND `HoneyTypeId` (FK to `honey_types`).  
This is a potential data consistency issue — the name could diverge from the typed reference.

### 6.14 `Product` Self-Reference for Packaging

`Product.ProductId` creates a self-referencing relationship: packaging materials reference themselves as raw materials and vice versa. The FK is `ProductId` (self-referencing) and `PackagingMaterialId` → `products`.

### 6.15 `Payment.PaymentMethod` Enum Storage

The `payments` table stores `payment_method` as ENUM('BANK_TRANSFER','CASH','CARD','OTHER') — matching C# enum names directly, NOT the string format used in UI.

### 6.16 `ExpenseInvoiceAudit.old_status` / `new_status` MaxLength

Changed from `VARCHAR(20)` to `VARCHAR(30)` via migration. Status values like `"APPROVED_PAID"` need 14 chars.

---

## APPENDIX: File Paths Reference

```
Project Root:
  NordicBeesERP.csproj          — Project configuration
  Program.cs                    — App startup, DI, middleware
  appsettings.json              — Default configuration
  appsettings.Development.json  — Dev overrides

Components/
  App.razor                     — Root Blazor app
  Routes.razor                  — Route definitions
  Layout/                       — MainLayout, NavMenu, ReconnectModal
  Pages/                        — All page components
  Dialogs/                      — All dialog components

Models/
  Models_Part1.cs               — Core entities (Invoice, Payment, etc.)
  Models_Part2.cs               — More entities
  Models/Expenses/              — Expense module models
  Models/CreditNoteModels.cs    — Credit note entities
  Models/InvoiceModels.cs       — Invoice view models
  Models/PaymentModels.cs       — Payment view models
  Models/WarehouseModule/       — Warehouse/production models

Data/
  NordicBeesErpContext.cs       — EF Core DbContext

Services/
  *.cs                          — All service implementations
  Dtos/                         — DTO classes

Helpers/
  ExpenseStatusHelper.cs        — Status labels/colors
  CompanyNameHelper.cs          — Company name normalization

Migrations/                     — EF Core migrations (SQL + C#)

wwwroot/
  js/                           — JavaScript interop files
  css/app.css                   — Global styles
```

---

*This document is auto-generated from live database and source files. Always verify critical changes against the actual codebase and database.*