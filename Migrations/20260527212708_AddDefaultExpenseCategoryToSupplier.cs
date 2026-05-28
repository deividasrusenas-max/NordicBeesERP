using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultExpenseCategoryToSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "source",
                table: "payments",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "due_date",
                table: "invoices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_payment_date",
                table: "invoices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "paid_amount",
                table: "invoices",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "invoices",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "ocr_confidence",
                table: "expense_invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "expense_invoices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "expense_invoices",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "expense_invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_type",
                table: "expense_invoices",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ocr_flags",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ocr_pipeline",
                table: "expense_invoices",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "original_file_path",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "original_filename",
                table: "expense_invoices",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "pending_supplier_bank_account",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "pending_supplier_city",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "pending_supplier_company_code",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "pending_supplier_country_code",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "pending_supplier_postal_code",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "rejected_reason",
                table: "expense_invoices",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "expense_invoices",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "supplier_vat_verified",
                table: "expense_invoices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "supplier_vat_verified_name",
                table: "expense_invoices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "expense_invoice_lines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit_of_measure",
                table: "expense_invoice_lines",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "old_status",
                table: "expense_invoice_audit",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "new_status",
                table: "expense_invoice_audit",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "default_expense_category_id",
                table: "business_partners",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bank_imports",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    import_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_rows = table.Column<int>(type: "int", nullable: false),
                    matched_rows = table.Column<int>(type: "int", nullable: false),
                    unmatched_rows = table.Column<int>(type: "int", nullable: false),
                    processed_rows = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_message = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_imports", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_imports_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    credit_note_number = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    credit_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    original_invoice_id = table.Column<int>(type: "int", nullable: true),
                    applied_invoice_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    currency_id = table.Column<int>(type: "int", nullable: false),
                    language = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reverse_charge = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    subtotal_excl_vat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    total_vat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    total_incl_vat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    pdf_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issued_by = table.Column<int>(type: "int", nullable: true),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_notes_business_partners_customer_id",
                        column: x => x.customer_id,
                        principalTable: "business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_notes_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_notes_invoices_applied_invoice_id",
                        column: x => x.applied_invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_notes_invoices_original_invoice_id",
                        column: x => x.original_invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_id = table.Column<int>(type: "int", nullable: false),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    allocated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_allocations_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "bank_import_rows",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    import_id = table.Column<int>(type: "int", nullable: false),
                    row_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payer_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payer_account = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    match_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    matched_invoice_id = table.Column<int>(type: "int", nullable: true),
                    payment_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_import_rows", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_import_rows_bank_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "bank_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "credit_note_lines",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    credit_note_id = table.Column<int>(type: "int", nullable: false),
                    invoice_line_id = table.Column<int>(type: "int", nullable: true),
                    line_number = table.Column<int>(type: "int", nullable: false),
                    product_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    price_excl_vat = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    vat_rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    line_subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    lot_number = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_note_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_note_lines_credit_notes_credit_note_id",
                        column: x => x.credit_note_id,
                        principalTable: "credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_credit_note_lines_invoice_lines_invoice_line_id",
                        column: x => x.invoice_line_id,
                        principalTable: "invoice_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5000), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5070) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5080), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100) });

            migrationBuilder.CreateIndex(
                name: "IX_payments_bank_import_row_id",
                table: "payments",
                column: "bank_import_row_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_import_rows_amount",
                table: "bank_import_rows",
                column: "amount");

            migrationBuilder.CreateIndex(
                name: "IX_bank_import_rows_import_id",
                table: "bank_import_rows",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_import_rows_match_status",
                table: "bank_import_rows",
                column: "match_status");

            migrationBuilder.CreateIndex(
                name: "IX_bank_import_rows_row_date",
                table: "bank_import_rows",
                column: "row_date");

            migrationBuilder.CreateIndex(
                name: "IX_bank_imports_created_by",
                table: "bank_imports",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_bank_imports_import_date",
                table: "bank_imports",
                column: "import_date");

            migrationBuilder.CreateIndex(
                name: "IX_bank_imports_status",
                table: "bank_imports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_credit_note_lines_credit_note_id",
                table: "credit_note_lines",
                column: "credit_note_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_note_lines_invoice_line_id",
                table: "credit_note_lines",
                column: "invoice_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_note_lines_product_code",
                table: "credit_note_lines",
                column: "product_code");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_applied_invoice_id",
                table: "credit_notes",
                column: "applied_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_credit_date",
                table: "credit_notes",
                column: "credit_date");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_credit_note_number",
                table: "credit_notes",
                column: "credit_note_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_currency_id",
                table: "credit_notes",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_customer_id",
                table: "credit_notes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_original_invoice_id",
                table: "credit_notes",
                column: "original_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_status",
                table: "credit_notes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_invoice_id",
                table: "payment_allocations",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_payment_id",
                table: "payment_allocations",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_payment_id_invoice_id",
                table: "payment_allocations",
                columns: new[] { "payment_id", "invoice_id" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_payments_bank_import_rows_bank_import_row_id",
                table: "payments",
                column: "bank_import_row_id",
                principalTable: "bank_import_rows",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_bank_import_rows_bank_import_row_id",
                table: "payments");

            migrationBuilder.DropTable(
                name: "bank_import_rows");

            migrationBuilder.DropTable(
                name: "credit_note_lines");

            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "payment_audit_log");

            migrationBuilder.DropTable(
                name: "bank_imports");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropIndex(
                name: "IX_payments_bank_import_row_id",
                table: "payments");

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
                name: "source",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "last_payment_date",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "invoice_type",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "ocr_flags",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "ocr_pipeline",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "original_file_path",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "original_filename",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "pending_supplier_bank_account",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "pending_supplier_city",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "pending_supplier_company_code",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "pending_supplier_country_code",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "pending_supplier_postal_code",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "rejected_reason",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "source",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "supplier_vat_verified",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "supplier_vat_verified_name",
                table: "expense_invoices");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "expense_invoice_lines");

            migrationBuilder.DropColumn(
                name: "unit_of_measure",
                table: "expense_invoice_lines");

            migrationBuilder.DropColumn(
                name: "default_expense_category_id",
                table: "business_partners");

            migrationBuilder.AlterColumn<decimal>(
                name: "ocr_confidence",
                table: "expense_invoices",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "old_status",
                table: "expense_invoice_audit",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "new_status",
                table: "expense_invoice_audit",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2640), new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2680) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2680), new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2680) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2680), new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2690) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2690), new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2690) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2690), new DateTime(2026, 3, 28, 11, 47, 3, 187, DateTimeKind.Local).AddTicks(2690) });
        }
    }
}
