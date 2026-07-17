using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

public enum TemplateType { ZPL, EPL, PLAIN_TEXT }

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

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("template_type")]
    public TemplateType TemplateType { get; set; } = TemplateType.ZPL;

    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("default_printer_id")]
    public int? DefaultPrinterId { get; set; }

    [Column("width_mm", TypeName = "decimal(5,1)")]
    public decimal WidthMm { get; set; } = 108.0m;

    [Column("height_mm", TypeName = "decimal(5,1)")]
    public decimal HeightMm { get; set; } = 75.0m;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public virtual Printer? DefaultPrinter { get; set; }
}
