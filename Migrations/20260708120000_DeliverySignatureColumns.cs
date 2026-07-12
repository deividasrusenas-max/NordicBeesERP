using Microsoft.EntityFrameworkCore.Migrations;

namespace NordicBeesERP.Migrations;

public partial class DeliverySignatureColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE deliveries
                ADD COLUMN IF NOT EXISTS supplier_signature_svg MEDIUMTEXT NULL,
                ADD COLUMN IF NOT EXISTS supplier_signed_at DATETIME NULL,
                ADD COLUMN IF NOT EXISTS supplier_signer_name VARCHAR(200) NULL;
        ");

        // Remove redundant inspection_by (varchar) — canonical field is inspection_by_user_id (int, FK to erp_users)
        migrationBuilder.Sql(@"
            ALTER TABLE deliveries DROP COLUMN inspection_by;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE deliveries
                DROP COLUMN supplier_signature_svg,
                DROP COLUMN supplier_signed_at,
                DROP COLUMN supplier_signer_name;
        ");

        // Reverse: re-add inspection_by column
        migrationBuilder.Sql(@"
            ALTER TABLE deliveries ADD COLUMN IF NOT EXISTS inspection_by VARCHAR(200) NULL;
        ");
    }
}
