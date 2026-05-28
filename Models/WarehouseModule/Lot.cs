using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("lots")]
public class Lot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("lot_number")]
    public string LotNumber { get; set; } = string.Empty;

    [Required]
    [Column("lot_type")]
    public string LotType { get; set; } = "PRODUCTION";

    [Column("created_date")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Column("customer_id")]
    public int? CustomerId { get; set; }

    [Column("invoice_id")]
    public int? InvoiceId { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}