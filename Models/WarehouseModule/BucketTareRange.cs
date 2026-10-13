using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

/// <summary>
/// One brutto interval with its bucket tare. Intervals are contiguous: the lower bound is
/// exclusive except for the first one, so brutto 16.005 falls into the interval ending after 16.00.
/// </summary>
[Table("bucket_tare_ranges")]
public class BucketTareRange
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("min_brutto_kg", TypeName = "decimal(6,2)")]
    public decimal MinBruttoKg { get; set; }

    [Column("max_brutto_kg", TypeName = "decimal(6,2)")]
    public decimal MaxBruttoKg { get; set; }

    [Column("tare_kg", TypeName = "decimal(6,2)")]
    public decimal TareKg { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at", TypeName = "datetime(6)")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at", TypeName = "datetime(6)")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
