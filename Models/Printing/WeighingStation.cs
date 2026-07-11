using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NordicBeesERP.Models.Honey;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Models.Printing;

/// <summary>
/// Physical weighing station: 1 Pi + 1 printer + 1 scale + 1 tablet.
/// BRC8 6.4 — calibration tracking included.
/// </summary>
[Table("weighing_stations")]
public class WeighingStation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("warehouse_id")]
    public int WarehouseId { get; set; }

    [Required]
    [Column("printer_id")]
    public int PrinterId { get; set; }

    [MaxLength(200)]
    [Column("pi_base_url")]
    public string? PiBaseUrl { get; set; }

    [MaxLength(20)]
    [Column("default_container_type")]
    public string? DefaultContainerType { get; set; }

    [Column("min_weight_kg", TypeName = "decimal(5,3)")]
    public decimal MinWeightKg { get; set; } = 0.500m;

    [MaxLength(20)]
    [Column("scale_protocol")]
    public string ScaleProtocol { get; set; } = "NONE";

    [MaxLength(200)]
    [Column("scale_regex")]
    public string? ScaleRegex { get; set; }

    // BRC8 6.4 — Calibration
    [Column("last_calibration_date", TypeName = "date")]
    public DateTime? LastCalibrationDate { get; set; }

    [Column("next_calibration_date", TypeName = "date")]
    public DateTime? NextCalibrationDate { get; set; }

    [MaxLength(100)]
    [Column("calibration_cert_number")]
    public string? CalibrationCertNumber { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties — not mapped
    [NotMapped]
    public Warehouse? Warehouse { get; set; }

    [NotMapped]
    public Printer? Printer { get; set; }
}
