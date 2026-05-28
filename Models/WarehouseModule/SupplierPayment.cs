using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("supplier_payments")]
public class SupplierPayment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("delivery_id")]
    public int DeliveryId { get; set; }

    [Column("supplier_id")]
    public int SupplierId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("payment_date")]
    public DateTime PaymentDate { get; set; } = DateTime.Now;

    [MaxLength(50)]
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "bank_transfer";

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}