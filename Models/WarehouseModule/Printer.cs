using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

public enum PrinterConnectionType { HTTP, STUB }

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

    [Column("label_width_mm")]
    public decimal LabelWidthMm { get; set; } = 108.0m;

    [Column("label_height_mm")]
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
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
