using NordicBeesERP.Models;

namespace NordicBeesERP.Helpers;

public static class PartnerRoleFlagsHelper
{
    /// <summary>
    /// Derives the legacy <c>partner_type</c> column value from the role flags
    /// during the Phase 3 transition. All-flags-false defaults to
    /// <see cref="PartnerType.Supplier"/> because the suppliers list is the
    /// primary editing surface and a never-edited legacy partner is most likely
    /// supplier-facing.
    /// </summary>
    public static PartnerType DeriveFromFlags(bool isCustomer, bool isSupplier, bool isExpenseSupplier)
    {
        if (isCustomer && isSupplier)
            return PartnerType.Both;

        if (isExpenseSupplier && !isSupplier)
            return PartnerType.ExpenseSupplier;

        if (isSupplier)
            return PartnerType.Supplier;

        if (isCustomer)
            return PartnerType.Customer;

        return PartnerType.Supplier;
    }
}
