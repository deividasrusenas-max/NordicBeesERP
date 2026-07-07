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
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE deliveries
                DROP COLUMN IF EXISTS supplier_signature_svg,
                DROP COLUMN IF EXISTS supplier_signed_at,
                DROP COLUMN IF EXISTS supplier_signer_name;
        ");
    }
}
