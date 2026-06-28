// =====================================================
// NORDIC BEES ERP - DbContext
// Framework: .NET 10 + Entity Framework Core
// =====================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Models.Honey;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Data
{
    public class NordicBeesERPContext : DbContext
    {
        public NordicBeesERPContext(DbContextOptions<NordicBeesERPContext> options)
            : base(options)
        {
        }

        // =====================================================
        // DBSETS
        // =====================================================

        // Authentication & Users
        public DbSet<Company> Companies { get; set; }
        public DbSet<ErpUser> ErpUsers { get; set; }

        // Business Partners
        public DbSet<BusinessPartner> BusinessPartners { get; set; }

        // Currencies
        public DbSet<Currency> Currencies { get; set; }

        // Products
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<Product> Products { get; set; }

        // Warehouses
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<WarehouseStock> WarehouseStocks { get; set; }

        // Honey Procurement
        public DbSet<HoneyType> HoneyTypes { get; set; }
        public DbSet<HoneyDelivery> HoneyDeliveries { get; set; }

        // Production
        public DbSet<ProductionBatch> ProductionBatches { get; set; }
        public DbSet<ProductionBatchIngredient> ProductionBatchIngredients { get; set; }

        // Honey Batch (LOT tracking)
        public DbSet<HoneyBatch> HoneyBatches { get; set; }
        public DbSet<HoneyBatchIngredient> HoneyBatchIngredients { get; set; }

        // Warehouse Module
        public DbSet<Container> Containers { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<DeliveryLine> DeliveryLines { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<SupplierPayment> SupplierPayments { get; set; }
        public DbSet<Lot> Lots { get; set; }
        public DbSet<WarehouseType> WarehouseTypes { get; set; }
        public DbSet<QualityParamConfig> QualityParamConfigs { get; set; }
        public DbSet<RawMaterialType> RawMaterialTypes { get; set; }

        // Sales
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLine> InvoiceLines { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }

        // Credit Notes
        public DbSet<CreditNote> CreditNotes { get; set; }
        public DbSet<CreditNoteLine> CreditNoteLines { get; set; }

        // Finance
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; }
        public DbSet<BankImport> BankImports { get; set; }
        public DbSet<BankImportRow> BankImportRows { get; set; }
        public DbSet<PaymentAuditLog> PaymentAuditLogs { get; set; }
        
        public DbSet<NordicBeesERP.Models.Expenses.ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<Expense> Expenses { get; set; }

        // Expense Module
        public DbSet<NordicBeesERP.Models.Expenses.ExpenseCostCenter> ExpenseCostCenters { get; set; }
        public DbSet<NordicBeesERP.Models.Expenses.ExpenseInvoice> ExpenseInvoices { get; set; }
        public DbSet<NordicBeesERP.Models.Expenses.ExpenseInvoiceLine> ExpenseInvoiceLines { get; set; }
        public DbSet<NordicBeesERP.Models.Expenses.ExpenseLineAllocation> ExpenseLineAllocations { get; set; }
        public DbSet<NordicBeesERP.Models.Expenses.ExpensePayment> ExpensePayments { get; set; }
        public DbSet<NordicBeesERP.Models.Expenses.ExpenseBudget> ExpenseBudgets { get; set; }
        public DbSet<NordicBeesERP.Models.Expenses.ExpenseOcrQueue> ExpenseOcrQueue { get; set; }
         public DbSet<NordicBeesERP.Models.Expenses.AppSetting> AppSettings { get; set; }
         public DbSet<NordicBeesERP.Models.Expenses.ExpenseInvoiceAudit> ExpenseInvoiceAudits { get; set; }
         
         public DbSet<NordicBeesERP.Models.InvoiceAudit> InvoiceAudits { get; set; }

        // Units
        public DbSet<Unit> Units { get; set; }

        // Company Settings
        public DbSet<CompanySettings> CompanySettings { get; set; }

        // =====================================================
        // MODEL CONFIGURATION
        // =====================================================

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== BUSINESS PARTNERS =====
            modelBuilder.Entity<BusinessPartner>(entity =>
            {
                entity.HasIndex(e => e.PartnerType);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.VatCode);
                entity.HasIndex(e => new { e.IsActive, e.PartnerType });

                // Convert enum to snake_case string (DB expects lowercase like "customer", "supplier", "expense_supplier")
                entity.Property(e => e.PartnerType)
                    .HasConversion(
                        v => EnumToString(v),
                        v => StringToEnum(v)
                    )
                    .HasMaxLength(20);
            });

            // ===== CURRENCIES =====
            modelBuilder.Entity<Currency>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            // ===== PRODUCT CATEGORIES =====
            modelBuilder.Entity<ProductCategory>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== PRODUCTS =====
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.ProductType);
                // entity.HasIndex(e => e.CategoryId); // Column doesn't exist in MySQL
                entity.HasIndex(e => new { e.WarehouseManaged, e.IsActive });

                // Convert enum to string
                entity.Property(e => e.ProductType)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Decimal precision
                entity.Property(e => e.CostPrice).HasPrecision(10, 2);
                entity.Property(e => e.SalePrice).HasPrecision(10, 2);
                entity.Property(e => e.PurchasePrice).HasPrecision(10, 2);
                entity.Property(e => e.MinStockLevel).HasPrecision(10, 2);
            });

            // ===== WAREHOUSES =====
            modelBuilder.Entity<Warehouse>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
            });

            // ===== WAREHOUSE STOCKS =====
            modelBuilder.Entity<WarehouseStock>(entity =>
            {
                entity.HasIndex(e => new { e.WarehouseId, e.ProductId, e.LotNumber })
                    .IsUnique();
                entity.HasIndex(e => e.WarehouseId);
                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => e.LotNumber);

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);
                entity.Property(e => e.ReservedQuantity).HasPrecision(10, 3);

                // Computed column - read-only
                entity.Property(e => e.AvailableQuantity)
                    .HasComputedColumnSql("(quantity - reserved_quantity)", stored: true);
            });

            // ===== HONEY TYPES =====
            modelBuilder.Entity<HoneyType>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
            });

            // ===== HONEY DELIVERIES =====
            modelBuilder.Entity<HoneyDelivery>(entity =>
            {
                entity.HasIndex(e => e.DeliveryDate);
                entity.HasIndex(e => e.SupplierId);
                entity.HasIndex(e => e.WarehouseId);
                entity.HasIndex(e => e.HoneyTypeId);
                entity.HasIndex(e => new { e.SupplierId, e.DeliveryDate });

                // Decimal precision
                entity.Property(e => e.GrossWeight).HasPrecision(10, 3);
                entity.Property(e => e.TareWeight).HasPrecision(10, 3);
                entity.Property(e => e.NetWeight).HasPrecision(10, 3);
                entity.Property(e => e.PricePerKg).HasPrecision(10, 2);
                entity.Property(e => e.TotalCost).HasPrecision(10, 2);
                entity.Property(e => e.TransportCost).HasPrecision(10, 2);

                // Relationships
                entity.HasOne(e => e.Supplier)
                    .WithMany(e => e.HoneyDeliveries)
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== PRODUCTION BATCHES =====
            modelBuilder.Entity<ProductionBatch>(entity =>
            {
                entity.HasIndex(e => e.BatchNumber).IsUnique();
                entity.HasIndex(e => e.WarehouseId);
                entity.HasIndex(e => e.ProductionDate);
                entity.HasIndex(e => e.BatchStatus);
                entity.HasIndex(e => new { e.ProductCode, e.ProductionDate });

                // Convert enum to string
                entity.Property(e => e.BatchStatus)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);
            });

            // ===== PRODUCTION BATCH INGREDIENTS =====
            modelBuilder.Entity<ProductionBatchIngredient>(entity =>
            {
                entity.HasIndex(e => e.BatchId);
                entity.HasIndex(e => e.HoneyDeliveryId);
                entity.HasIndex(e => e.Quantity);

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);

                // Relationships
                entity.HasOne(e => e.Batch)
                    .WithMany(e => e.Ingredients)
                    .HasForeignKey(e => e.BatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.HoneyDelivery)
                    .WithMany(e => e.UsedInBatches)
                    .HasForeignKey(e => e.HoneyDeliveryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== INVOICES =====
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasIndex(e => e.InvoiceNumber).IsUnique();
                entity.HasIndex(e => e.InvoiceDate);
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => new { e.InvoiceDate, e.CustomerId });
                entity.HasIndex(e => new { e.Status, e.InvoiceDate });

                // Convert enum to string
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Decimal precision
                entity.Property(e => e.SubtotalExclVat).HasPrecision(10, 2);
                entity.Property(e => e.TotalVat).HasPrecision(10, 2);
                entity.Property(e => e.TotalInclVat).HasPrecision(10, 2);

                // Relationships - navigation properties that are NOT mapped
                entity.Property(e => e.DueDate).HasColumnName("due_date");
                entity.Ignore(e => e.Lines);
                entity.Ignore(e => e.Payments);

                // Currency relationship
                entity.HasOne(e => e.Currency)
                    .WithMany(e => e.Invoices)
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Customer relationship (BusinessPartner)
                entity.HasOne(e => e.Customer)
                    .WithMany(e => e.Invoices)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== INVOICE LINES =====
            modelBuilder.Entity<InvoiceLine>(entity =>
            {
                entity.HasIndex(e => e.InvoiceId);
                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => e.LotNumber);

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);
                entity.Property(e => e.PriceExclVat).HasPrecision(10, 4);
                entity.Property(e => e.VatRate).HasPrecision(5, 2);
                entity.Property(e => e.LineSubtotal).HasPrecision(10, 2);
                entity.Property(e => e.VatAmount).HasPrecision(10, 2);
                entity.Property(e => e.LineTotal).HasPrecision(10, 2);

                // Relationships
                entity.HasOne(e => e.Invoice)
                    .WithMany(e => e.Lines)
                    .HasForeignKey(e => e.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== ORDERS =====
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasIndex(e => e.OrderNumber).IsUnique();
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Status);

                // Convert enum to string
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);
            });

            // ===== ORDER LINES =====
            modelBuilder.Entity<OrderLine>(entity =>
            {
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.ProductId);

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);
                entity.Property(e => e.Price).HasPrecision(10, 4);

                // Relationships
                entity.HasOne(e => e.Order)
                    .WithMany(e => e.Lines)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== CREDIT NOTES =====
            modelBuilder.Entity<CreditNote>(entity =>
            {
                entity.ToTable("credit_notes");

                entity.HasIndex(e => e.CreditNoteNumber).IsUnique();
                entity.HasIndex(e => e.CreditDate);
                entity.HasIndex(e => e.OriginalInvoiceId);
                entity.HasIndex(e => e.AppliedInvoiceId);
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Status);

                // Convert enum to lowercase string for case-insensitive DB matching
                // Saves "draft", reads back regardless of original case
                entity.Property(e => e.Status)
                    .HasConversion(
                        v => v.ToString().ToLower(),
                        v => ParseCreditNoteStatus(v))
                    .HasMaxLength(20);

                // Decimal precision
                entity.Property(e => e.SubtotalExclVat).HasPrecision(10, 2);
                entity.Property(e => e.TotalVat).HasPrecision(10, 2);
                entity.Property(e => e.TotalInclVat).HasPrecision(10, 2);

                // Relationships
                entity.HasOne(e => e.OriginalInvoice)
                    .WithMany()
                    .HasForeignKey(e => e.OriginalInvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.AppliedInvoice)
                    .WithMany()
                    .HasForeignKey(e => e.AppliedInvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== CREDIT NOTE LINES =====
            modelBuilder.Entity<CreditNoteLine>(entity =>
            {
                entity.ToTable("credit_note_lines");

                entity.HasIndex(e => e.CreditNoteId);
                entity.HasIndex(e => e.InvoiceLineId);
                entity.HasIndex(e => e.ProductCode);

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);
                entity.Property(e => e.PriceExclVat).HasPrecision(10, 4);
                entity.Property(e => e.VatRate).HasPrecision(5, 2);
                entity.Property(e => e.LineSubtotal).HasPrecision(10, 2);
                entity.Property(e => e.VatAmount).HasPrecision(10, 2);
                entity.Property(e => e.LineTotal).HasPrecision(10, 2);

                // Relationships
                entity.HasOne(e => e.CreditNote)
                    .WithMany(e => e.Lines)
                    .HasForeignKey(e => e.CreditNoteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.InvoiceLine)
                    .WithMany()
                    .HasForeignKey(e => e.InvoiceLineId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

             // ===== PAYMENTS =====
             modelBuilder.Entity<Payment>(entity =>
             {
                 entity.HasIndex(e => e.PaymentDate);
                 entity.HasIndex(e => e.InvoiceId);
                 entity.HasIndex(e => e.CustomerId);

                  // Convert enum to snake_case string (DB expects: bank_transfer, cash, card, other)
                  entity.Property(e => e.PaymentMethod)
                      .HasConversion(
                          v => v == PaymentMethod.BankTransfer ? "bank_transfer" :
                                 v == PaymentMethod.Cash ? "cash" :
                                 v == PaymentMethod.Card ? "card" : "other",
                          v => v == "bank_transfer" ? PaymentMethod.BankTransfer :
                                 v == "cash" ? PaymentMethod.Cash :
                                 v == "card" ? PaymentMethod.Card : PaymentMethod.Other)
                      .HasMaxLength(20);

                // Decimal precision
                entity.Property(e => e.Amount).HasPrecision(10, 2);

                // Relationships
                entity.HasOne(e => e.Invoice)
                    .WithMany(e => e.Payments)
                    .HasForeignKey(e => e.InvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== EXPENSE INVOICES =====
            modelBuilder.Entity<NordicBeesERP.Models.Expenses.ExpenseInvoice>(entity =>
            {
                entity.HasIndex(e => new { e.SupplierId, e.InvoiceNumber })
                      .HasDatabaseName("IX_expense_invoices_supplier_invoice");
                entity.HasIndex(e => e.InvoiceDate);
                entity.HasIndex(e => e.Status);
                entity.Ignore(e => e.DuplicateOfId);
                entity.Ignore(e => e.SupplierName);
                entity.Ignore(e => e.Supplier);
                entity.Ignore(e => e.ExpenseInvoiceLines);
                entity.Ignore(e => e.ExpensePayments);
                entity.Ignore(e => e.DuplicateOf);
            });

            // ===== EXPENSE CATEGORIES =====
            modelBuilder.Entity<NordicBeesERP.Models.Expenses.ExpenseCategory>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
            });

            // ===== EXPENSES =====
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasIndex(e => e.ExpenseDate);
                entity.HasIndex(e => e.CategoryId);
                entity.HasIndex(e => e.SupplierId);

                // Decimal precision
                entity.Property(e => e.Amount).HasPrecision(10, 2);
                entity.Property(e => e.VatAmount).HasPrecision(10, 2);
            });

            // ===== HONEY BATCHES (LOT TRACKING) =====
            modelBuilder.Entity<HoneyBatch>(entity =>
            {
                entity.HasIndex(e => e.LotNumber).IsUnique();
                entity.HasIndex(e => e.ProcessingDate);
                entity.HasIndex(e => e.WarehouseId);
                entity.HasIndex(e => new { e.ProcessingDate, e.LotNumber });

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);

                // Relationships
                entity.HasOne(e => e.Warehouse)
                    .WithMany(e => e.HoneyBatches)
                    .HasForeignKey(e => e.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== HONEY BATCH INGREDIENTS =====
            modelBuilder.Entity<HoneyBatchIngredient>(entity =>
            {
                entity.HasIndex(e => e.BatchId);
                entity.HasIndex(e => e.HoneyDeliveryId);
                entity.HasIndex(e => e.Quantity);

                // Decimal precision
                entity.Property(e => e.Quantity).HasPrecision(10, 3);

                // Relationships
                entity.HasOne(e => e.Batch)
                    .WithMany(e => e.HoneyBatchIngredients)
                    .HasForeignKey(e => e.BatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.HoneyDelivery)
                    .WithMany(e => e.UsedInHoneyBatches)
                    .HasForeignKey(e => e.HoneyDeliveryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== RAW MATERIAL TYPES =====
            modelBuilder.Entity<RawMaterialType>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.SortOrder);

                // Convert enum to string
                entity.Property(e => e.IsHoney)
                    .HasConversion<bool>();
            });

            // Seed data for RawMaterialTypes
            modelBuilder.Entity<RawMaterialType>().HasData(
                new RawMaterialType { Id = 1, Name = "Medus", IsHoney = true, SortOrder = 1 },
                new RawMaterialType { Id = 2, Name = "Bičių duona", IsHoney = false, SortOrder = 2 },
                new RawMaterialType { Id = 3, Name = "Pikis", IsHoney = false, SortOrder = 3 },
                new RawMaterialType { Id = 4, Name = "Propolis", IsHoney = false, SortOrder = 4 },
                new RawMaterialType { Id = 5, Name = "Vaškas", IsHoney = false, SortOrder = 5 }
            );

             // ===== PAYMENT ALLOCATIONS =====
             modelBuilder.Entity<PaymentAllocation>(entity =>
             {
                 entity.HasIndex(e => e.PaymentId);
                 entity.HasIndex(e => e.InvoiceId);
                 entity.HasIndex(e => new { e.PaymentId, e.InvoiceId });

                 // Decimal precision
                 entity.Property(e => e.AllocatedAmount).HasPrecision(15, 2);

                 // Ignore CreatedAt/UpdatedAt - table doesn't have these columns
                 entity.Ignore(e => e.CreatedAt);
                 entity.Ignore(e => e.UpdatedAt);

                 // Relationships
                 entity.HasOne(e => e.Payment)
                     .WithMany(e => e.Allocations)
                     .HasForeignKey(e => e.PaymentId)
                     .OnDelete(DeleteBehavior.Cascade);

                 entity.HasOne(e => e.Invoice)
                     .WithMany(e => e.PaymentAllocations)
                     .HasForeignKey(e => e.InvoiceId)
                     .OnDelete(DeleteBehavior.Restrict);
             });

            // ===== BANK IMPORTS =====
            modelBuilder.Entity<BankImport>(entity =>
            {
                entity.HasIndex(e => e.ImportDate);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedBy);

                // Convert status to string
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);
            });

              // ===== BANK IMPORT ROWS =====
              modelBuilder.Entity<BankImportRow>(entity =>
              {
                  entity.HasIndex(e => e.ImportId);
                  entity.HasIndex(e => e.MatchStatus);
                  entity.HasIndex(e => e.RowDate);
                  entity.HasIndex(e => e.Amount);

                 // Decimal precision
                 entity.Property(e => e.Amount).HasPrecision(15, 2);

                 // Convert match_status to string
                 entity.Property(e => e.MatchStatus)
                     .HasConversion<string>()
                     .HasMaxLength(20);

                 // Relationships
                 entity.HasOne(e => e.BankImport)
                     .WithMany(e => e.Rows)
                     .HasForeignKey(e => e.ImportId)
                     .OnDelete(DeleteBehavior.Cascade);
             });

             // ===== PAYMENT AUDIT LOG =====
             modelBuilder.Entity<PaymentAuditLog>(entity =>
             {
                 entity.HasIndex(e => e.PaymentId);
                 entity.HasIndex(e => e.Action);
                 entity.HasIndex(e => e.ChangedBy);
                 entity.HasIndex(e => e.ChangedAt);

                 // No FK constraints - audit log is standalone
             });
        }

        // =====================================================
        // HELPER METHODS
        // =====================================================

        // Parse CreditNoteStatus from string (case-insensitive)
        private static CreditNoteStatus ParseCreditNoteStatus(string value)
        {
            if (string.IsNullOrEmpty(value)) return CreditNoteStatus.Draft;
            if (Enum.TryParse<CreditNoteStatus>(value, ignoreCase: true, out var status)) return status;
            return CreditNoteStatus.Draft;
        }

        // Convert enum to snake_case string (e.g., "ExpenseSupplier" -> "expense_supplier")
        private string EnumToString(PartnerType value)
        {
            var name = value.ToString();
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    result.Append('_');
                }
                result.Append(char.ToLower(name[i]));
            }
            return result.ToString();
        }

        // Parse snake_case string to enum (e.g., "expense_supplier" -> PartnerType.ExpenseSupplier)
        private PartnerType StringToEnum(string value) => value switch {
            "customer" => PartnerType.Customer,
            "supplier" => PartnerType.Supplier,
            "both" => PartnerType.Both,
            "expense_supplier" => PartnerType.ExpenseSupplier,
            _ => PartnerType.Customer
        };

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                // Only update UpdatedAt if the property exists, is mapped (not ignored), and has a column
                var updatedAtProperty = entry.Metadata.FindProperty("UpdatedAt");
                if (updatedAtProperty != null && !updatedAtProperty.IsShadowProperty())
                {
                    // GetColumnType() throws with InMemoryDatabase - check if it works
                    try
                    {
                        if (updatedAtProperty.GetColumnType() != null)
                        {
                            entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // InMemoryDatabase doesn't support GetColumnType - skip
                    }
                }

                // Only update CreatedAt if the property exists, is mapped (not ignored), and entity is being added
                if (entry.State == EntityState.Added)
                {
                    var createdAtProperty = entry.Metadata.FindProperty("CreatedAt");
                    if (createdAtProperty != null && !createdAtProperty.IsShadowProperty())
                    {
                        // GetColumnType() throws with InMemoryDatabase - check if it works
                        try
                        {
                            if (createdAtProperty.GetColumnType() != null)
                            {
                                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                            }
                        }
                        catch (InvalidCastException)
                        {
                            // InMemoryDatabase doesn't support GetColumnType - skip
                        }
                    }
                }
            }
        }
    }
}