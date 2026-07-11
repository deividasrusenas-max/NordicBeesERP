using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

/// <summary>
/// BRC8 Clause 3.5 — Supplier approval tracking.
/// When a new approval is added, the previous one's IsCurrent is set to false (application logic).
/// Never deleted — immutable audit record.
/// </summary>
[Table("supplier_approvals")]
public class SupplierApproval
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("supplier_id")]
    public int SupplierId { get; set; }

    [Required]
    [Column("approved_by")]
    public int ApprovedBy { get; set; }

    [Required]
    [Column("approval_date", TypeName = "date")]
    public DateTime ApprovalDate { get; set; } = DateTime.UtcNow.Date;

    [Column("expires_at", TypeName = "date")]
    public DateTime? ExpiresAt { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("risk_level")]
    public string RiskLevel { get; set; } = "MEDIUM";

    [Required]
    [MaxLength(20)]
    [Column("approval_method")]
    public string ApprovalMethod { get; set; } = "OTHER";

    [MaxLength(100)]
    [Column("cert_number")]
    public string? CertNumber { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("is_current")]
    public bool IsCurrent { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties — not mapped
    [NotMapped]
    public BusinessPartner? Supplier { get; set; }

    [NotMapped]
    public ErpUser? ApprovedByUser { get; set; }
}
