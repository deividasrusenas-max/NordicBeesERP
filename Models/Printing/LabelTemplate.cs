using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Printing;

/// <summary>
/// ZPL label template stored in database.
/// P0: hardcoded in ZplLabelTemplateService. P1: loaded from this table.
/// </summary>
[Table("label_templates")]
public class LabelTemplate
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [Column("template_type")]
    public string TemplateType { get; set; } = string.Empty;

    [Required]
    [Column("scriban_content")]
    public string ScribanContent { get; set; } = string.Empty;

    [Column("label_width_mm", TypeName = "decimal(5,1)")]
    public decimal LabelWidthMm { get; set; } = 108.0m;

    [Column("label_height_mm", TypeName = "decimal(5,1)")]
    public decimal LabelHeightMm { get; set; } = 75.0m;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_default")]
    public bool IsDefault { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
