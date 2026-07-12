using Microsoft.EntityFrameworkCore.Migrations;

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// BRC8 3.9 — Add NON_CONFORMITY to container_label_events.event_type ENUM.
    /// The C# enum (ContainerLabelEventType) already contains NON_CONFORMITY.
    /// Pure addition — no data migration needed.
    /// </summary>
    public partial class AddNonConformityEventType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE container_label_events
                    MODIFY event_type ENUM('PRINTED','REPRINTED','QUARANTINE_PRINTED','CANCELLED','PRINT_FAILED','WEIGHT_CORRECTED','NON_CONFORMITY') NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE container_label_events
                    MODIFY event_type ENUM('PRINTED','REPRINTED','QUARANTINE_PRINTED','CANCELLED','PRINT_FAILED','WEIGHT_CORRECTED') NOT NULL;
            ");
        }
    }
}
