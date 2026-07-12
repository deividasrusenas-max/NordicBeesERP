using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Models.Printing;

/// <summary>
/// Print job queued for label printing. Processed by LabelPrintWorker.
/// </summary>
[Table("print_jobs")]
public class PrintJob
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("printer_id")]
    public int PrinterId { get; set; }

    [Column("station_id")]
    public int? StationId { get; set; }

    [Required]
    [Column("container_id")]
    public int ContainerId { get; set; }

    [Required]
    [MaxLength(30)]
    [Column("job_type")]
    public string JobType { get; set; } = "RECEIPT_LABEL";

    [Required]
    [Column("zpl_content")]
    public string ZplContent { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("retry_count")]
    public int RetryCount { get; set; } = 0;

    [Column("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [Column("last_error")]
    public string? LastError { get; set; }

    [Required]
    [Column("created_by_user_id")]
    public int CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("done_at")]
    public DateTime? DoneAt { get; set; }

    // Navigation properties — not mapped
    [NotMapped]
    public Printer? Printer { get; set; }

    [NotMapped]
    public WeighingStation? Station { get; set; }

    [NotMapped]
    public Container? Container { get; set; }
}
