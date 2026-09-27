using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NordicBeesERP.Models;

/// <summary>
/// "This normalised OCR supplier name means that partner" (PLAN-ETAPAS2 §2, D-017, D-044 Q4). Learned only from explicit human
/// actions, promoted after N confirmations from distinct invoices, frozen on a conflict, visible and revocable. Name only — never a
/// VAT code, company code or IBAN. No foreign keys (D-044 Q11).
/// </summary>
[Table("supplier_aliases")]
[Index(nameof(AliasKey), nameof(PartnerId), IsUnique = true, Name = "uq_alias_partner")]
[Index(nameof(AliasKey), Name = "idx_alias_key")]
public class SupplierAlias
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("partner_id")]
    public int PartnerId { get; set; }

    /// <summary>The normalised OCR supplier name (SupplierIdentityNormalizer.NameNormalized).</summary>
    [Required]
    [MaxLength(255)]
    [Column("alias_key")]
    public string AliasKey { get; set; } = string.Empty;

    /// <summary>The string as read, for humans.</summary>
    [Required]
    [MaxLength(255)]
    [Column("raw_example")]
    public string RawExample { get; set; } = string.Empty;

    /// <summary>CANDIDATE | ACTIVE | FROZEN | REVOKED</summary>
    [Required]
    [MaxLength(12)]
    [Column("state")]
    public string State { get; set; } = "CANDIDATE";

    [Column("confirmations")]
    public int Confirmations { get; set; }

    [MaxLength(255)]
    [Column("frozen_reason")]
    public string? FrozenReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>One thing that happened to an alias: CONFIRMED | PROMOTED | CONFLICT_FROZEN | REVOKED | UNFROZEN | APPLIED.</summary>
[Table("supplier_alias_events")]
[Index(nameof(AliasId), Name = "idx_alias_events_alias")]
[Index(nameof(InvoiceId), Name = "idx_alias_events_invoice")]
public class SupplierAliasEvent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("alias_id")]
    public int AliasId { get; set; }

    [Column("invoice_id")]
    public int? InvoiceId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("event")]
    public string Event { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("actor")]
    public string? Actor { get; set; }

    [Column("details")]
    public string? Details { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
