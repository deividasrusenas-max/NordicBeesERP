using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NordicBeesERP.Models;

/// <summary>
/// A bank account known for a partner (PLAN-ETAPAS2 §1.4, D-044 Q2/Q5): the matcher checks a document IBAN against these and
/// raises SUPPLIER_NEW_IBAN when the partner has at least one and the document's is not among them. No foreign keys (D-044 Q11);
/// the same IBAN may be known for two partners on purpose — the matcher must see that as ambiguity, not a constraint violation.
/// </summary>
[Table("supplier_bank_accounts")]
[Index(nameof(PartnerId), nameof(Iban), IsUnique = true, Name = "uq_partner_iban")]
[Index(nameof(Iban), Name = "idx_iban")]
public class SupplierBankAccount
{
    public const string SourceMigrated = "MIGRATED";
    public const string SourceManual = "MANUAL";
    public const string SourceInvoiceConfirmed = "INVOICE_CONFIRMED";

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("partner_id")]
    public int PartnerId { get; set; }

    /// <summary>Normalised: upper-case, no spaces, IbanValidator-valid.</summary>
    [Required]
    [MaxLength(34)]
    [Column("iban")]
    public string Iban { get; set; } = string.Empty;

    /// <summary>MIGRATED | MANUAL | INVOICE_CONFIRMED</summary>
    [Required]
    [MaxLength(20)]
    [Column("source")]
    public string Source { get; set; } = SourceManual;

    [Column("source_invoice_id")]
    public int? SourceInvoiceId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }
}
