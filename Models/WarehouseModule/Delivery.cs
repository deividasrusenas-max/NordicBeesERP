using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("deliveries")]
public class Delivery
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [MaxLength(50)]
    [Column("delivery_number")]
    public string? DeliveryNumber { get; set; }

    [Required]
    [Column("delivery_date")]
    public DateTime DeliveryDate { get; set; } = DateTime.Now;

    [Column("supplier_id")]
    public int SupplierId { get; set; }

    [Column("warehouse_id")]
    public int WarehouseId { get; set; }

    [Column("raw_material_type_id")]
    public int? RawMaterialTypeId { get; set; }

    [ForeignKey("RawMaterialTypeId")]
    public RawMaterialType? RawMaterialType { get; set; }

    [Column("status")]
    public string Status { get; set; } = "RECEIVED";

    [Column("total_net_weight")]
    public decimal TotalNetWeight { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("paid_amount")]
    public decimal PaidAmount { get; set; }

    [Column("barrels_owed")]
    public int BarrelsOwed { get; set; }

    [Column("barrels_returned")]
    public int BarrelsReturned { get; set; }

    [Column("need_return_barrels")]
    public bool NeedReturnBarrels { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("invoice_id")]
    public int? InvoiceId { get; set; }

    [Column("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [Column("transport_cost_deduction")]
    [Precision(10, 2)]
    public decimal? TransportCostDeduction { get; set; }

    [Column("barrel_cost_deduction")]
    [Precision(10, 2)]
    public decimal? BarrelCostDeduction { get; set; }

    [Column("other_cost_deduction")]
    [Precision(10, 2)]
    public decimal? OtherCostDeduction { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [Column("supplier_signature_svg")]
    public string? SupplierSignatureSvg { get; set; }

    [Column("supplier_signed_at")]
    public DateTime? SupplierSignedAt { get; set; }

    [Column("supplier_signer_name")]
    [MaxLength(200)]
    public string? SupplierSignerName { get; set; }

    [Column("inspection_result")]
    public string? InspectionResult { get; set; }

    [Column("inspection_notes")]
    public string? InspectionNotes { get; set; }

    [Column("inspection_by_user_id")]
    public int? InspectionByUserId { get; set; }

    [Column("inspection_at")]
    public DateTime? InspectionAt { get; set; }

    [Column("receipt_pdf_path")]
    [MaxLength(500)]
    public string? ReceiptPdfPath { get; set; }

    [Column("signed_by_type")]
    [MaxLength(20)]
    public string SignedByType { get; set; } = "SUPPLIER";

    [Column("receiver_name")]
    [MaxLength(200)]
    public string? ReceiverName { get; set; }

    [Column("origin_country")]
    [MaxLength(100)]
    public string? OriginCountry { get; set; }
}