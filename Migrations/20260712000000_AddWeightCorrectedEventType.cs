using Microsoft.EntityFrameworkCore.Migrations;

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// BRC8 3.7 — Add WEIGHT_CORRECTED to container_label_events.event_type ENUM.
    /// The C# enum (ContainerLabelEventType) already contains WEIGHT_CORRECTED.
    /// Pure addition — no data migration needed.
    /// </summary>
    public partial class AddWeightCorrectedEventType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE container_label_events
                    MODIFY event_type ENUM('PRINTED','REPRINTED','QUARANTINE_PRINTED','CANCELLED','PRINT_FAILED','WEIGHT_CORRECTED') NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE container_label_events
                    MODIFY event_type ENUM('PRINTED','REPRINTED','QUARANTINE_PRINTED','CANCELLED','PRINT_FAILED') NOT NULL;
            ");
        }
    }
}
