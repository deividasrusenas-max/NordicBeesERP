---
name: url-filter-persistence-nordicbees
description: Mandatory pattern for ANY list/search page with filters (search box, date range, status chips, tabs) in NordicBeesERP. Use this whenever adding filters to a new page, or modifying existing filters, or fixing a "filters reset when navigating back" bug.
---

# URL Filter Persistence — NordicBeesERP

**Core rule: filter state must live in the URL query string, never only in component fields.** If filters are only in `private` fields, clicking into a detail page and coming back (via browser Back or an in-page "Atgal" button) loses them — this was a real bug in Invoices.razor, fixed in commit 372c4ec, then found to also affect the in-page "Atgal" button in InvoiceView.razor (fixed separately, commit 41d170c). Both halves are required.

## Reference implementation

`Components/Pages/Invoices.razor` is the canonical example — read it before implementing filters elsewhere. Key pieces:

1. **Query-bound parameters** in `@code`, one per filter, using `[SupplyParameterFromQuery(Name = "...")]` with short query keys (`q`, `from`, `to`, `status`, `tab`).
2. **`OnParametersSetAsync`** (not `OnInitializedAsync`) reads the query parameters into the component's working-state fields, then loads data.
3. Every filter-changing handler (search box, date picker, status chip, tab) calls `Navigation.NavigateTo(newUrl, replace: ...)` with a URL built from current filter state, instead of just mutating fields and calling `StateHasChanged()`.
4. Use `replace: true` only for rapid-fire input (e.g. every keystroke in a search box) to avoid spamming browser history. Use `replace: false` (push a real history entry) for discrete actions (date picked, status toggled, tab changed) so Back actually steps through filter states.
5. "Clear filters" navigates to the bare path with no query string.
6. A `BuildFilterUrl(...)` helper builds the query string from current filter values, omitting empty/default ones (don't write `?status=` for an empty filter).

## The other half — "Atgal"/back buttons

A detail page (e.g. `InvoiceView.razor`, `Orders/Detail.razor`) that's reached from a filtered list must NOT hardcode `Navigation.NavigateTo("/list-path")` in its back button. That silently discards the referring filters even though the URL-based approach above is otherwise working. Instead:

- The list page's "view detail" navigation passes the current full URL (path + query string) as a `returnUrl` query parameter to the detail route.
- The detail page has `[SupplyParameterFromQuery(Name = "returnUrl")] public string? ReturnUrl { get; set; }` and its back handler navigates to `ReturnUrl` if present, falling back to the bare list path otherwise.

See `Components/Pages/InvoiceView.razor` (`HandleBack`) and `Components/Pages/Invoices.razor` (`ViewInvoice`) for the exact pattern.

## When this applies

Any page matched by: `grep -l "_filterSearch\|_dateRange\|_selectedStatuses" Components/Pages/**/*.razor` — as of July 2026 this includes ExpensePayments, PaymentHistory, Products, Suppliers, Customers, InvoicePaymentList, Orders/Index, Warehouse/DeliveryList, Warehouse/WriteOff, Warehouse/StockOverview, CreditNotes, ExpenseInvoices. Each of these was built BEFORE this pattern existed and needs retrofitting — do not assume they already do this correctly just because they compile.

## CRITICAL: never bind bool? directly via [SupplyParameterFromQuery]

`FilterUrlBuilder.ToQueryValue(bool?)` outputs "1"/"0" for compactness.
Blazor's OWN built-in `[SupplyParameterFromQuery]` type binder for `bool?`
only accepts literal "true"/"false" and throws
`InvalidOperationException: Cannot parse the value '0' as type
'Nullable<Boolean>'` on anything else — a real crash, confirmed twice
(ExpenseInvoices.razor, Customers.razor, both 2026-07-17).

ALWAYS declare boolean filter query properties as `string?`, never `bool?`:

    // WRONG — crashes on ?flag=0 or ?flag=1
    [SupplyParameterFromQuery(Name = "flag")]
    public bool? MyFilter { get; set; }

    // CORRECT
    [SupplyParameterFromQuery(Name = "flag")]
    public string? MyFilter { get; set; }
    ...
    _myFilter = FilterUrlBuilder.ParseBool(MyFilter);

This applies to every boolean filter field on every page using this
pattern — always grep for `public bool?.*SupplyParameterFromQuery` (or
the reverse order) across the whole Components/Pages tree after any new
filter rollout, not just the file just touched.

## Scope discipline

Apply this pattern only to the filters that already exist on the page — do not invent new filters, do not change what data is queried, only change how filter state is captured/restored via the URL. If the page has no "view detail then come back" flow at all (a flat list with no drill-down), the `returnUrl` half doesn't apply — only add it if the page actually navigates elsewhere.
