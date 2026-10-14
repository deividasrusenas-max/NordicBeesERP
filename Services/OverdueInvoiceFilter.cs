using System.Linq.Expressions;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

/// <summary>
/// Single definition of "overdue LAK sales invoice" for the nav badge. The due date is COALESCE(due_date, payment_due_date)
/// (confirming an invoice writes only payment_due_date), the same rule UnpaidInvoicesService uses.
/// </summary>
public static class OverdueInvoiceFilter
{
    public static Expression<Func<Invoice, bool>> Predicate(DateTime today) =>
        i => i.InvoiceNumber.StartsWith("LAK")
            && !i.InvoiceNumber.StartsWith("ULAK")
            && (i.DueDate ?? i.PaymentDueDate) != null
            && (i.DueDate ?? i.PaymentDueDate) < today
            && i.PaymentStatus != "paid"
            && i.Status != InvoiceStatus.Disputed;
}
