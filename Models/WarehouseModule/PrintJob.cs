using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

public enum PrintJobType
{
    RECEIPT_LABEL,
    QUARANTINE_LABEL,
    REPRINT
}

public enum PrintJobStatus
{
    PENDING,
    PROCESSING,
    DONE,
    FAILED,
    CANCELLED
}

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
    [Column("job_type")]
    public PrintJobType JobType { get; set; } = PrintJobType.RECEIPT_LABEL;

    [Required]
    [Column("zpl_content")]
    public string ZplContent { get; set; } = string.Empty;

    [Required]
    [Column("status")]
    public PrintJobStatus Status { get; set; } = PrintJobStatus.PENDING;

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
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("done_at")]
    public DateTime? DoneAt { get; set; }
}
