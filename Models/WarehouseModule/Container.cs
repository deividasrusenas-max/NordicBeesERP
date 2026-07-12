using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("containers")]
public class Container
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("container_code")]
    public string ContainerCode { get; set; } = string.Empty;

    [Required]
    [Column("container_type")]
    public string ContainerType { get; set; } = "BARREL";

    [Column("supplier_id")]
    public int SupplierId { get; set; }

    [Column("delivery_line_id")]
    public int? DeliveryLineId { get; set; }

    [Column("warehouse_id")]
    public int WarehouseId { get; set; }

    [Column("product_id")]
    public int? ProductId { get; set; }

    [Column("honey_type_id")]
    public int? HoneyTypeId { get; set; }

    [Column("gross_weight")]
    public decimal GrossWeight { get; set; }

    [Column("tare_weight")]
    public decimal TareWeight { get; set; }

    [Column("net_weight")]
    public decimal NetWeight { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    [Column("remaining_quantity")]
    public int RemainingQuantity { get; set; } = 1;

    [Column("status")]
    public string Status { get; set; } = "IN_STOCK";

    [Column("reservation_customer_id")]
    public int? ReservationCustomerId { get; set; }

    [Column("reservation_notes")]
    public string? ReservationNotes { get; set; }

    [Column("reservation_date")]
    public DateTime? ReservationDate { get; set; }

    [Column("lot_id")]
    public int? LotId { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("quality_params")]
    public string? QualityParams { get; set; }

    [Column("quarantine_reason")]
    public string? QuarantineReason { get; set; }

    // Labeling module fields
    [MaxLength(10)]
    [Column("weighing_mode")]
    public string WeighingMode { get; set; } = "MANUAL";

    [Column("received_by_user_id")]
    public int? ReceivedByUserId { get; set; }

    [Column("last_label_printed_at")]
    public DateTime? LastLabelPrintedAt { get; set; }

    [Column("label_print_count")]
    public int LabelPrintCount { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}