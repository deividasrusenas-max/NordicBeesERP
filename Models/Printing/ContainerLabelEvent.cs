using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Models.Printing;

/// <summary>
/// BRC8 Clause 3.3 — Immutable audit trail for container label events.
/// INSERT ONLY — never updated or deleted after creation.
/// Enforced by NordicBeesErpContext.SaveChanges override.
/// </summary>
[Table("container_label_events")]
public class ContainerLabelEvent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("container_id")]
    public int ContainerId { get; set; }

    [Required]
    [MaxLength(30)]
    [Column("event_type")]
    public string EventType { get; set; } = "PRINTED";

    [Column("print_job_id")]
    public int? PrintJobId { get; set; }

    [MaxLength(20)]
    [Column("reason_code")]
    public string? ReasonCode { get; set; }

    [MaxLength(200)]
    [Column("reason_text")]
    public string? ReasonText { get; set; }

    [Column("operator_id")]
    public int? OperatorId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties — not mapped
    [NotMapped]
    public Container? Container { get; set; }

    [NotMapped]
    public PrintJob? PrintJob { get; set; }
}
