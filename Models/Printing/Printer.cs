using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Models.Printing;

/// <summary>
/// Physical printer device (Zebra ZM400 or similar).
/// Each printer has an endpoint URL for HTTP printing via Pi gateway.
/// </summary>
[Table("printers")]
public class Printer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("location")]
    public string Location { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("endpoint_url")]
    public string EndpointUrl { get; set; } = string.Empty;

    [Required]
    [Column("connection_type")]
    public PrinterConnectionType ConnectionType { get; set; } = PrinterConnectionType.STUB;

    [Column("label_width_mm", TypeName = "decimal(5,1)")]
    public decimal LabelWidthMm { get; set; } = 108.0m;

    [Column("label_height_mm", TypeName = "decimal(5,1)")]
    public decimal LabelHeightMm { get; set; } = 75.0m;

    [Column("darkness")]
    public int Darkness { get; set; } = 25;

    [Column("dpi")]
    public int Dpi { get; set; } = 200;

    [Column("last_test_print_at")]
    public DateTime? LastTestPrintAt { get; set; }

    [MaxLength(50)]
    [Column("last_test_result")]
    public string? LastTestResult { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
