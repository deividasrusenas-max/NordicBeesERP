using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// containers/delivery_lines.container_type: the code writes 'BUCKET_GROUP' (InitialCreate schema) but the
    /// dev/test DB enum was changed by hand to ('BARREL','BUCKET'), so every bucket delivery failed with
    /// "Data truncated for column 'container_type'". Widen both enums to the superset so the same migration is
    /// correct for either schema (dev: BUCKET, InitialCreate/prod: BUCKET_GROUP). Existing rows are untouched.
    /// Plain MODIFY (no IF [NOT] EXISTS) runs on MySQL 8.0 and MariaDB 11.8 and is idempotent.
    /// </summary>
    public partial class ContainerTypeEnumAcceptsBucketGroup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE containers MODIFY container_type ENUM('BARREL','BUCKET','BUCKET_GROUP') NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE delivery_lines MODIFY container_type ENUM('BARREL','BUCKET','BUCKET_GROUP') NOT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op: narrowing the enum would truncate/fail on rows holding either value.
        }
    }
}
