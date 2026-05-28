using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("delivery_lines")]
public class DeliveryLine
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("delivery_id")]
    public int DeliveryId { get; set; }

    [Column("product_id")]
    public int? ProductId { get; set; }

    [Column("honey_type_id")]
    public int? HoneyTypeId { get; set; }

    [Column("container_type")]
    public string ContainerType { get; set; } = string.Empty;

    [Column("container_count")]
    public int ContainerCount { get; set; }

    [Column("total_net_weight")]
    public decimal? TotalNetWeight { get; set; }

    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }

    [Column("line_total")]
    public decimal? LineTotal { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}