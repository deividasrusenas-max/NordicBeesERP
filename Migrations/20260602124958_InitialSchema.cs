using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_payments");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_stocks_honey_type_id",
                table: "warehouse_stocks");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_stocks_raw_material_type_id",
                table: "warehouse_stocks");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_honey_type_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_movement_date",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_product_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_raw_material_type_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_warehouse_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_invoices_applied_credit_note_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_honey_types_name",
                table: "honey_types");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoices_category_id",
                table: "expense_invoices");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoices_created_at",
                table: "expense_invoices");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoices_invoice_number",
                table: "expense_invoices");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoices_supplier_id",
                table: "expense_invoices");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoice_lines_invoice_id",
                table: "expense_invoice_lines");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoice_audit_invoice_id",
                table: "expense_invoice_audit");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoice_audit_invoice_number",
                table: "expense_invoice_audit");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoice_audit_performed_at",
                table: "expense_invoice_audit");

            migrationBuilder.DropColumn(
                name: "type",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "honey_type_id",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "lot",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "raw_material_type_id",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "movement_date",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "reference",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "price_per_kg",
                table: "raw_material_types");

            migrationBuilder.DropColumn(
                name: "unit",
                table: "raw_material_types");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "product_categories");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "product_categories");

            migrationBuilder.DropColumn(
                name: "description",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "amount_excl_vat",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "amount_excl_vat",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "unit_price_excl_vat",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "price_per_kg",
                table: "honey_types");

            migrationBuilder.DropColumn(
                name: "amount",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "fat",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "file_hash",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "file_name",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "impurities",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "moisture",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "honey_deliveries");

            migrationBuilder.RenameColumn(
                name: "location",
                table: "warehouses",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "warehouse_id",
                table: "stock_movements",
                newName: "container_id");

            migrationBuilder.RenameColumn(
                name: "raw_material_type_id",
                table: "stock_movements",
                newName: "to_warehouse_id");

            migrationBuilder.RenameColumn(
                name: "honey_type_id",
                table: "stock_movements",
                newName: "reference_id");

            migrationBuilder.RenameColumn(
                name: "price_excl_vat",
                table: "products",
                newName: "sale_price");

            migrationBuilder.RenameColumn(
                name: "reference",
                table: "payments",
                newName: "notes");

            migrationBuilder.RenameColumn(
                name: "applied_credit_note_id",
                table: "invoices",
                newName: "payment_term_days");

            migrationBuilder.RenameColumn(
                name: "provider_id",
                table: "honey_deliveries",
                newName: "warehouse_id");

            migrationBuilder.RenameColumn(
                name: "created_by_id",
                table: "honey_deliveries",
                newName: "supplier_id");

            migrationBuilder.RenameIndex(
                name: "IX_honey_deliveries_provider_id",
                table: "honey_deliveries",
                newName: "IX_honey_deliveries_warehouse_id");

            migrationBuilder.RenameIndex(
                name: "IX_honey_deliveries_created_by_id",
                table: "honey_deliveries",
                newName: "IX_honey_deliveries_supplier_id");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "warehouses",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "warehouses",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "warehouses",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "warehouses",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "warehouses",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "warehouse_type_id",
                table: "warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "product_id",
                table: "warehouse_stocks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expiration_date",
                table: "warehouse_stocks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "honey_batch_id",
                table: "warehouse_stocks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_movement_date",
                table: "warehouse_stocks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lot_number",
                table: "warehouse_stocks",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "reserved_quantity",
                table: "warehouse_stocks",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_movements",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,3)");

            migrationBuilder.AlterColumn<string>(
                name: "movement_type",
                table: "stock_movements",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "created_by",
                table: "stock_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "from_warehouse_id",
                table: "stock_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lot_id",
                table: "stock_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_type",
                table: "stock_movements",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "raw_material_types",
                type: "varchar(5)",
                maxLength: 5,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_honey",
                table: "raw_material_types",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "raw_material_types",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "category_id",
                table: "products",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "cost_price",
                table: "products",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ean_code",
                table: "products",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "min_stock_level",
                table: "products",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "products",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "products",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "product_type",
                table: "products",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "purchase_price",
                table: "products",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "track_lots",
                table: "products",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "unit_id",
                table: "products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "warehouse_managed",
                table: "products",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "product_categories",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "product_categories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_categories",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "payments",
                keyColumn: "payment_method",
                keyValue: null,
                column: "payment_method",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "payment_method",
                table: "payments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "invoice_id",
                table: "payments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "payments",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "bank_import_id",
                table: "payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "bank_import_row_id",
                table: "payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "created_by",
                table: "payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_number",
                table: "payments",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "payments",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_vat",
                table: "invoices",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "total_incl_vat",
                table: "invoices",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "subtotal_excl_vat",
                table: "invoices",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.UpdateData(
                table: "invoices",
                keyColumn: "status",
                keyValue: null,
                column: "status",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "invoices",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "invoices",
                keyColumn: "payment_status",
                keyValue: null,
                column: "payment_status",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "invoices",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "paid_amount",
                table: "invoices",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AlterColumn<DateTime>(
                name: "last_payment_date",
                table: "invoices",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "invoice_number",
                table: "invoices",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "due_date",
                table: "invoices",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<int>(
                name: "currency_id",
                table: "invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "delivery_id",
                table: "invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_type",
                table: "invoices",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "invoices",
                type: "varchar(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_due_date",
                table: "invoices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "reverse_charge",
                table: "invoices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "vat_amount",
                table: "invoice_lines",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "invoice_lines",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "invoice_lines",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "line_number",
                table: "invoice_lines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "line_subtotal",
                table: "invoice_lines",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "line_total",
                table: "invoice_lines",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "lot_number",
                table: "invoice_lines",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "invoice_lines",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "price_excl_vat",
                table: "invoice_lines",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "product_code",
                table: "invoice_lines",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "product_id",
                table: "invoice_lines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                table: "invoice_lines",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "invoice_lines",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "honey_types",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "honey_types",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "honey_types",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "honey_types",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                table: "honey_types",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "honey_types",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<decimal>(
                name: "price_per_kg",
                table: "honey_deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.AlterColumn<int>(
                name: "honey_type_id",
                table: "honey_deliveries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "beehive_location",
                table: "honey_deliveries",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "container_quantity",
                table: "honey_deliveries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "delivery_date",
                table: "honey_deliveries",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "delivery_number",
                table: "honey_deliveries",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "gross_weight",
                table: "honey_deliveries",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_soured",
                table: "honey_deliveries",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "net_weight",
                table: "honey_deliveries",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "product_id",
                table: "honey_deliveries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quality_grade",
                table: "honey_deliveries",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "tare_weight",
                table: "honey_deliveries",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "total_cost",
                table: "honey_deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "transport_cost",
                table: "honey_deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "honey_deliveries",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<decimal>(
                name: "vat_rate",
                table: "expense_invoices",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)");

            migrationBuilder.AlterColumn<string>(
                name: "supplier_vat_verified_name",
                table: "expense_invoices",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "supplier_vat_verified",
                table: "expense_invoices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "expense_invoices",
                keyColumn: "status",
                keyValue: null,
                column: "status",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "expense_invoices",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "expense_invoices",
                keyColumn: "source",
                keyValue: null,
                column: "source",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                table: "expense_invoices",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "expense_invoices",
                keyColumn: "ocr_status",
                keyValue: null,
                column: "ocr_status",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ocr_status",
                table: "expense_invoices",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ocr_raw_json",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "vat_rate",
                table: "expense_invoice_lines",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "expense_invoice_lines",
                type: "decimal(12,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "expense_invoice_lines",
                type: "decimal(10,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,3)");

            migrationBuilder.UpdateData(
                table: "expense_invoice_lines",
                keyColumn: "description",
                keyValue: null,
                column: "description",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "expense_invoice_lines",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "invoice_number",
                table: "expense_invoice_audit",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "expense_invoice_audit",
                keyColumn: "action",
                keyValue: null,
                column: "action",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "expense_invoice_audit",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "available_quantity",
                table: "warehouse_stocks",
                type: "decimal(65,30)",
                nullable: false,
                computedColumnSql: "(quantity - reserved_quantity)",
                stored: true);

            migrationBuilder.CreateTable(
                name: "containers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    container_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    container_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    supplier_id = table.Column<int>(type: "int", nullable: false),
                    delivery_line_id = table.Column<int>(type: "int", nullable: true),
                    warehouse_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: true),
                    honey_type_id = table.Column<int>(type: "int", nullable: true),
                    gross_weight = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    tare_weight = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    net_weight = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    remaining_quantity = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reservation_customer_id = table.Column<int>(type: "int", nullable: true),
                    reservation_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reservation_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    lot_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quality_params = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_containers", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    delivery_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    delivery_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    supplier_id = table.Column<int>(type: "int", nullable: false),
                    warehouse_id = table.Column<int>(type: "int", nullable: false),
                    raw_material_type_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_net_weight = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    barrels_owed = table.Column<int>(type: "int", nullable: false),
                    barrels_returned = table.Column<int>(type: "int", nullable: false),
                    need_return_barrels = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    invoice_id = table.Column<int>(type: "int", nullable: true),
                    invoice_number = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_deliveries_raw_material_types_raw_material_type_id",
                        column: x => x.raw_material_type_id,
                        principalTable: "raw_material_types",
                        principalColumn: "id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "delivery_lines",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    delivery_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: true),
                    honey_type_id = table.Column<int>(type: "int", nullable: true),
                    container_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    container_count = table.Column<int>(type: "int", nullable: false),
                    total_net_weight = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    unit_price = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    line_total = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_lines", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expense_line_allocations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    invoice_line_id = table.Column<int>(type: "int", nullable: false),
                    category_id = table.Column<int>(type: "int", nullable: false),
                    cost_center_id = table.Column<int>(type: "int", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    allocated_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_line_allocations", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expense_ocr_queue",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    invoice_id = table.Column<int>(type: "int", nullable: true),
                    file_content = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    max_attempts = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_message = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_ocr_queue", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expense_payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    payment_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    payment_method = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_payments", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "honey_batches",
                columns: table => new
                {
                    batch_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    processing_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    warehouse_id = table.Column<int>(type: "int", nullable: false),
                    lot_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_honey_batches", x => x.batch_id);
                    table.ForeignKey(
                        name: "FK_honey_batches_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lots",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    lot_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    lot_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    invoice_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lots", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    order_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    order_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_audit_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_id = table.Column<int>(type: "int", nullable: true),
                    invoice_id = table.Column<int>(type: "int", nullable: true),
                    action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    old_amount = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    new_amount = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    changed_by = table.Column<int>(type: "int", nullable: true),
                    changed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_audit_log_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "production_batches",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    batch_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    production_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    warehouse_id = table.Column<int>(type: "int", nullable: true),
                    quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    product_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    product_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    batch_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_batches", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quality_param_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    param_key = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    param_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    unit = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quality_param_configs", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supplier_payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    delivery_id = table.Column<int>(type: "int", nullable: false),
                    supplier_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    payment_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payment_method = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payments", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "warehouse_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_types", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "honey_batch_ingredients",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    batch_id = table.Column<int>(type: "int", nullable: false),
                    honey_delivery_id = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_honey_batch_ingredients", x => x.id);
                    table.ForeignKey(
                        name: "FK_honey_batch_ingredients_honey_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "honey_batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_honey_batch_ingredients_honey_deliveries_honey_delivery_id",
                        column: x => x.honey_delivery_id,
                        principalTable: "honey_deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "order_lines",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    order_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: true),
                    product_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    price = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    total_price = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    fulfilled_quantity = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_lines_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "production_batch_ingredients",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    batch_id = table.Column<int>(type: "int", nullable: false),
                    honey_delivery_id = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_batch_ingredients", x => x.id);
                    table.ForeignKey(
                        name: "FK_production_batch_ingredients_honey_deliveries_honey_delivery~",
                        column: x => x.honey_delivery_id,
                        principalTable: "honey_deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_batch_ingredients_production_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "production_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "raw_material_types",
                columns: new[] { "id", "code", "created_at", "is_active", "is_honey", "name", "sort_order", "updated_at" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7170), true, true, "Medus", 1, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7220) },
                    { 2, null, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7220), true, false, "Bičių duona", 2, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7220) },
                    { 3, null, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230), true, false, "Pikis", 3, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230) },
                    { 4, null, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230), true, false, "Propolis", 4, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230) },
                    { 5, null, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230), true, false, "Vaškas", 5, new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_code",
                table: "warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_lot_number",
                table: "warehouse_stocks",
                column: "lot_number");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_warehouse_id_product_id_lot_number",
                table: "warehouse_stocks",
                columns: new[] { "warehouse_id", "product_id", "lot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_raw_material_types_is_active",
                table: "raw_material_types",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_raw_material_types_sort_order",
                table: "raw_material_types",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "IX_products_product_type",
                table: "products",
                column: "product_type");

            migrationBuilder.CreateIndex(
                name: "IX_products_warehouse_managed_is_active",
                table: "products",
                columns: new[] { "warehouse_managed", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_code",
                table: "product_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_bank_import_row_id",
                table: "payments",
                column: "bank_import_row_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_payment_id_invoice_id",
                table: "payment_allocations",
                columns: new[] { "payment_id", "invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_currency_id",
                table: "invoices",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_delivery_id",
                table: "invoices",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_date_customer_id",
                table: "invoices",
                columns: new[] { "invoice_date", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_status_invoice_date",
                table: "invoices",
                columns: new[] { "status", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_lot_number",
                table: "invoice_lines",
                column: "lot_number");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_product_id",
                table: "invoice_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_honey_types_code",
                table: "honey_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_honey_deliveries_delivery_date",
                table: "honey_deliveries",
                column: "delivery_date");

            migrationBuilder.CreateIndex(
                name: "IX_honey_deliveries_supplier_id_delivery_date",
                table: "honey_deliveries",
                columns: new[] { "supplier_id", "delivery_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoices_invoice_date",
                table: "expense_invoices",
                column: "invoice_date");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoices_supplier_invoice",
                table: "expense_invoices",
                columns: new[] { "supplier_id", "invoice_number" });

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_raw_material_type_id",
                table: "deliveries",
                column: "raw_material_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_honey_batch_ingredients_batch_id",
                table: "honey_batch_ingredients",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_honey_batch_ingredients_honey_delivery_id",
                table: "honey_batch_ingredients",
                column: "honey_delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_honey_batch_ingredients_quantity",
                table: "honey_batch_ingredients",
                column: "quantity");

            migrationBuilder.CreateIndex(
                name: "IX_honey_batches_lot_number",
                table: "honey_batches",
                column: "lot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_honey_batches_processing_date",
                table: "honey_batches",
                column: "processing_date");

            migrationBuilder.CreateIndex(
                name: "IX_honey_batches_processing_date_lot_number",
                table: "honey_batches",
                columns: new[] { "processing_date", "lot_number" });

            migrationBuilder.CreateIndex(
                name: "IX_honey_batches_warehouse_id",
                table: "honey_batches",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_order_id",
                table: "order_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_product_id",
                table: "order_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_number",
                table: "orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_payment_audit_log_action",
                table: "payment_audit_log",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_payment_audit_log_changed_at",
                table: "payment_audit_log",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_audit_log_changed_by",
                table: "payment_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_payment_audit_log_payment_id",
                table: "payment_audit_log",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_batch_ingredients_batch_id",
                table: "production_batch_ingredients",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_batch_ingredients_honey_delivery_id",
                table: "production_batch_ingredients",
                column: "honey_delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_batch_ingredients_quantity",
                table: "production_batch_ingredients",
                column: "quantity");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_batch_number",
                table: "production_batches",
                column: "batch_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_batch_status",
                table: "production_batches",
                column: "batch_status");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_product_code_production_date",
                table: "production_batches",
                columns: new[] { "product_code", "production_date" });

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_production_date",
                table: "production_batches",
                column: "production_date");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_warehouse_id",
                table: "production_batches",
                column: "warehouse_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bank_import_rows_bank_imports_import_id",
                table: "bank_import_rows",
                column: "import_id",
                principalTable: "bank_imports",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_note_lines_credit_notes_credit_note_id",
                table: "credit_note_lines",
                column: "credit_note_id",
                principalTable: "credit_notes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_note_lines_invoice_lines_invoice_line_id",
                table: "credit_note_lines",
                column: "invoice_line_id",
                principalTable: "invoice_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_notes_business_partners_customer_id",
                table: "credit_notes",
                column: "customer_id",
                principalTable: "business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_notes_currencies_currency_id",
                table: "credit_notes",
                column: "currency_id",
                principalTable: "currencies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_notes_invoices_applied_invoice_id",
                table: "credit_notes",
                column: "applied_invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_notes_invoices_original_invoice_id",
                table: "credit_notes",
                column: "original_invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_honey_deliveries_business_partners_supplier_id",
                table: "honey_deliveries",
                column: "supplier_id",
                principalTable: "business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_lines_invoices_invoice_id",
                table: "invoice_lines",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_business_partners_customer_id",
                table: "invoices",
                column: "customer_id",
                principalTable: "business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_currencies_currency_id",
                table: "invoices",
                column: "currency_id",
                principalTable: "currencies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_deliveries_delivery_id",
                table: "invoices",
                column: "delivery_id",
                principalTable: "deliveries",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_allocations_invoices_invoice_id",
                table: "payment_allocations",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_allocations_payments_payment_id",
                table: "payment_allocations",
                column: "payment_id",
                principalTable: "payments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_bank_import_rows_bank_import_row_id",
                table: "payments",
                column: "bank_import_row_id",
                principalTable: "bank_import_rows",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_payments_invoices_invoice_id",
                table: "payments",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_product_categories_product_categories_parent_id",
                table: "product_categories",
                column: "parent_id",
                principalTable: "product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bank_import_rows_bank_imports_import_id",
                table: "bank_import_rows");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_note_lines_credit_notes_credit_note_id",
                table: "credit_note_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_note_lines_invoice_lines_invoice_line_id",
                table: "credit_note_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_notes_business_partners_customer_id",
                table: "credit_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_notes_currencies_currency_id",
                table: "credit_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_notes_invoices_applied_invoice_id",
                table: "credit_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_notes_invoices_original_invoice_id",
                table: "credit_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_honey_deliveries_business_partners_supplier_id",
                table: "honey_deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_lines_invoices_invoice_id",
                table: "invoice_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_business_partners_customer_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_currencies_currency_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_deliveries_delivery_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_allocations_invoices_invoice_id",
                table: "payment_allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_allocations_payments_payment_id",
                table: "payment_allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_bank_import_rows_bank_import_row_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_invoices_invoice_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_product_categories_product_categories_parent_id",
                table: "product_categories");

            migrationBuilder.DropTable(
                name: "containers");

            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "delivery_lines");

            migrationBuilder.DropTable(
                name: "expense_line_allocations");

            migrationBuilder.DropTable(
                name: "expense_ocr_queue");

            migrationBuilder.DropTable(
                name: "expense_payments");

            migrationBuilder.DropTable(
                name: "honey_batch_ingredients");

            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropTable(
                name: "order_lines");

            migrationBuilder.DropTable(
                name: "payment_audit_log");

            migrationBuilder.DropTable(
                name: "production_batch_ingredients");

            migrationBuilder.DropTable(
                name: "quality_param_configs");

            migrationBuilder.DropTable(
                name: "supplier_payments");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "warehouse_types");

            migrationBuilder.DropTable(
                name: "honey_batches");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "production_batches");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_code",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_stocks_lot_number",
                table: "warehouse_stocks");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_stocks_warehouse_id_product_id_lot_number",
                table: "warehouse_stocks");

            migrationBuilder.DropIndex(
                name: "IX_raw_material_types_is_active",
                table: "raw_material_types");

            migrationBuilder.DropIndex(
                name: "IX_raw_material_types_sort_order",
                table: "raw_material_types");

            migrationBuilder.DropIndex(
                name: "IX_products_product_type",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_warehouse_managed_is_active",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_product_categories_code",
                table: "product_categories");

            migrationBuilder.DropIndex(
                name: "IX_payments_bank_import_row_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payment_allocations_payment_id_invoice_id",
                table: "payment_allocations");

            migrationBuilder.DropIndex(
                name: "IX_invoices_currency_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_delivery_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_invoice_date_customer_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_status_invoice_date",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoice_lines_lot_number",
                table: "invoice_lines");

            migrationBuilder.DropIndex(
                name: "IX_invoice_lines_product_id",
                table: "invoice_lines");

            migrationBuilder.DropIndex(
                name: "IX_honey_types_code",
                table: "honey_types");

            migrationBuilder.DropIndex(
                name: "IX_honey_deliveries_delivery_date",
                table: "honey_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_honey_deliveries_supplier_id_delivery_date",
                table: "honey_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoices_invoice_date",
                table: "expense_invoices");

            migrationBuilder.DropIndex(
                name: "IX_expense_invoices_supplier_invoice",
                table: "expense_invoices");

            migrationBuilder.DeleteData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "available_quantity",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "address",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "city",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "code",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "country",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "email",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "warehouse_type_id",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "expiration_date",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "honey_batch_id",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "last_movement_date",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "lot_number",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "reserved_quantity",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "from_warehouse_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "lot_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "reference_type",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "code",
                table: "raw_material_types");

            migrationBuilder.DropColumn(
                name: "is_honey",
                table: "raw_material_types");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "raw_material_types");

            migrationBuilder.DropColumn(
                name: "cost_price",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ean_code",
                table: "products");

            migrationBuilder.DropColumn(
                name: "min_stock_level",
                table: "products");

            migrationBuilder.DropColumn(
                name: "name",
                table: "products");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "products");

            migrationBuilder.DropColumn(
                name: "product_type",
                table: "products");

            migrationBuilder.DropColumn(
                name: "purchase_price",
                table: "products");

            migrationBuilder.DropColumn(
                name: "track_lots",
                table: "products");

            migrationBuilder.DropColumn(
                name: "unit_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "warehouse_managed",
                table: "products");

            migrationBuilder.DropColumn(
                name: "code",
                table: "product_categories");

            migrationBuilder.DropColumn(
                name: "description",
                table: "product_categories");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_categories");

            migrationBuilder.DropColumn(
                name: "bank_import_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "bank_import_row_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "reference_number",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "source",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "currency_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "delivery_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "invoice_type",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "language",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "payment_due_date",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "reverse_charge",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "line_number",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "line_subtotal",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "line_total",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "lot_number",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "price_excl_vat",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "product_code",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "unit",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "code",
                table: "honey_types");

            migrationBuilder.DropColumn(
                name: "color",
                table: "honey_types");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "honey_types");

            migrationBuilder.DropColumn(
                name: "description",
                table: "honey_types");

            migrationBuilder.DropColumn(
                name: "name_en",
                table: "honey_types");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "honey_types");

            migrationBuilder.DropColumn(
                name: "beehive_location",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "container_quantity",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "delivery_date",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "delivery_number",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "gross_weight",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "is_soured",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "net_weight",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "quality_grade",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "tare_weight",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "total_cost",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "transport_cost",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "honey_deliveries");

            migrationBuilder.DropColumn(
                name: "ocr_raw_json",
                table: "expense_invoices");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "warehouses",
                newName: "location");

            migrationBuilder.RenameColumn(
                name: "to_warehouse_id",
                table: "stock_movements",
                newName: "raw_material_type_id");

            migrationBuilder.RenameColumn(
                name: "reference_id",
                table: "stock_movements",
                newName: "honey_type_id");

            migrationBuilder.RenameColumn(
                name: "container_id",
                table: "stock_movements",
                newName: "warehouse_id");

            migrationBuilder.RenameColumn(
                name: "sale_price",
                table: "products",
                newName: "price_excl_vat");

            migrationBuilder.RenameColumn(
                name: "notes",
                table: "payments",
                newName: "reference");

            migrationBuilder.RenameColumn(
                name: "payment_term_days",
                table: "invoices",
                newName: "applied_credit_note_id");

            migrationBuilder.RenameColumn(
                name: "warehouse_id",
                table: "honey_deliveries",
                newName: "provider_id");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "honey_deliveries",
                newName: "created_by_id");

            migrationBuilder.RenameIndex(
                name: "IX_honey_deliveries_warehouse_id",
                table: "honey_deliveries",
                newName: "IX_honey_deliveries_provider_id");

            migrationBuilder.RenameIndex(
                name: "IX_honey_deliveries_supplier_id",
                table: "honey_deliveries",
                newName: "IX_honey_deliveries_created_by_id");

            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "warehouses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "product_id",
                table: "warehouse_stocks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "warehouse_stocks",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "honey_type_id",
                table: "warehouse_stocks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lot",
                table: "warehouse_stocks",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "raw_material_type_id",
                table: "warehouse_stocks",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_movements",
                type: "decimal(10,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<string>(
                name: "movement_type",
                table: "stock_movements",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "movement_date",
                table: "stock_movements",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "product_id",
                table: "stock_movements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "stock_movements",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "stock_movements",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_kg",
                table: "raw_material_types",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                table: "raw_material_types",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "category_id",
                table: "products",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "product_categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "product_categories",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "payment_method",
                table: "payments",
                type: "varchar(20)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "invoice_id",
                table: "payments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "payments",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "payments",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "payments",
                type: "varchar(20)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_vat",
                table: "invoices",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "total_incl_vat",
                table: "invoices",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "subtotal_excl_vat",
                table: "invoices",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "invoices",
                type: "varchar(20)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "invoices",
                type: "varchar(20)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "paid_amount",
                table: "invoices",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "last_payment_date",
                table: "invoices",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "invoice_number",
                table: "invoices",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "due_date",
                table: "invoices",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "amount_excl_vat",
                table: "invoices",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "vat_amount",
                table: "invoice_lines",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.UpdateData(
                table: "invoice_lines",
                keyColumn: "description",
                keyValue: null,
                column: "description",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "invoice_lines",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "amount_excl_vat",
                table: "invoice_lines",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price_excl_vat",
                table: "invoice_lines",
                type: "decimal(15,4)",
                precision: 15,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_kg",
                table: "honey_types",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_per_kg",
                table: "honey_deliveries",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "honey_type_id",
                table: "honey_deliveries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "honey_deliveries",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "fat",
                table: "honey_deliveries",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "file_hash",
                table: "honey_deliveries",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "file_name",
                table: "honey_deliveries",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "impurities",
                table: "honey_deliveries",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "moisture",
                table: "honey_deliveries",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "honey_deliveries",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "vat_rate",
                table: "expense_invoices",
                type: "decimal(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AlterColumn<string>(
                name: "supplier_vat_verified_name",
                table: "expense_invoices",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "supplier_vat_verified",
                table: "expense_invoices",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "expense_invoices",
                type: "varchar(30)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                table: "expense_invoices",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ocr_status",
                table: "expense_invoices",
                type: "varchar(20)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "vat_rate",
                table: "expense_invoice_lines",
                type: "decimal(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "expense_invoice_lines",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "expense_invoice_lines",
                type: "decimal(10,3)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "expense_invoice_lines",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "invoice_number",
                table: "expense_invoice_audit",
                type: "varchar(50)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "expense_invoice_audit",
                type: "varchar(50)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "invoice_payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    is_applied = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    payment_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payment_method = table.Column<string>(type: "varchar(20)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_payments", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_honey_type_id",
                table: "warehouse_stocks",
                column: "honey_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_raw_material_type_id",
                table: "warehouse_stocks",
                column: "raw_material_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_honey_type_id",
                table: "stock_movements",
                column: "honey_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_movement_date",
                table: "stock_movements",
                column: "movement_date");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_product_id",
                table: "stock_movements",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_raw_material_type_id",
                table: "stock_movements",
                column: "raw_material_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_warehouse_id",
                table: "stock_movements",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_applied_credit_note_id",
                table: "invoices",
                column: "applied_credit_note_id");

            migrationBuilder.CreateIndex(
                name: "IX_honey_types_name",
                table: "honey_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoices_category_id",
                table: "expense_invoices",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoices_created_at",
                table: "expense_invoices",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoices_invoice_number",
                table: "expense_invoices",
                column: "invoice_number");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoices_supplier_id",
                table: "expense_invoices",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoice_lines_invoice_id",
                table: "expense_invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoice_audit_invoice_id",
                table: "expense_invoice_audit",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoice_audit_invoice_number",
                table: "expense_invoice_audit",
                column: "invoice_number");

            migrationBuilder.CreateIndex(
                name: "IX_expense_invoice_audit_performed_at",
                table: "expense_invoice_audit",
                column: "performed_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_invoice_id",
                table: "invoice_payments",
                column: "invoice_id");
        }
    }
}
