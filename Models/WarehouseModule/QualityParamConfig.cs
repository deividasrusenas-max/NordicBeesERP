using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("quality_param_configs")]
public class QualityParamConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("param_key")]
    public string ParamKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("param_name")]
    public string ParamName { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("unit")]
    public string? Unit { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;
}