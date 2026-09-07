# TASK: Fix inert (non-clickable) VISI/ŪKININKAI/ĮMONĖS tabs on /suppliers — legacy MudChip parameters

## Root cause (confirmed by investigation report `.opencode/reports/suppliers-tabs-rendering-investigation-20260907-0948.md`)

`Components/Pages/Suppliers.razor:61-98` — the three tabs use **legacy pre-v8 MudChip parameters** that no longer exist in the installed MudBlazor 8.15.0: `Checked`, `OnCheckedChange`, `SelectionGroup`. Because `MudComponentBase` has `[Parameter(CaptureUnmatchedValues = true)] UserAttributes`, these unknown parameter names do NOT cause a build error — they're silently captured and dumped as inert HTML attributes. Since no real `OnClick` or `MudChipSet` wiring exists, MudChip's internal `IsButton` check evaluates false, so the chip renders as a plain non-clickable `<div>` instead of a clickable button — hence it visually looks like static text and does nothing when clicked.

The underlying C# logic (`_tabIndex`, `OnTabChanged`, `FilteredSuppliers`) is already correct and does NOT need to change — this is purely a markup/parameter-binding fix.

## Required fix

In `Components/Pages/Suppliers.razor:61-98`, for each of the three chips (VISI / ŪKININKAI / ĮMONĖS), remove `Checked`, `OnCheckedChange`, `SelectionGroup` and replace with an `OnClick` handler, following the exact same working pattern already used by the PVM chips (lines ~102-113) and Aktyvus/Neaktyvus chips (lines ~114-135) on the SAME page, and matching the reference pattern in `Invoices.razor:108-139` (quick-filter chips) per `Docs/FILTER_STANDARDIZATION_PLAN.md` §3.

Target result for each chip (example for tab 0 — adapt index for 1 and 2):

```razor
<MudChip T="string"
         Color="@(_tabIndex == 0 ? Color.Primary : Color.Default)"
         Variant="@(_tabIndex == 0 ? Variant.Filled : Variant.Outlined)"
         Size="Size.Small"
         Style="@(_tabIndex == 0 ? "" : "opacity:0.7")"
         OnClick="@(() => { if (_tabIndex != 0) OnTabChanged(0); })">
    VISI
</MudChip>
```

Keep the existing `Color`/`Variant`/`Size`/`Style` bindings exactly as they are — only `Checked`/`OnCheckedChange`/`SelectionGroup` need to be removed and replaced with `OnClick`. Keep the existing `@if (!_isWarehouse)` wrapping conditions around the VISI and ĮMONĖS chips unchanged. `OnTabChanged` already handles `?tab=` URL navigation — do not change its internals.

**Do NOT** attempt the alternative "just rename `Checked`→`Selected`, `OnCheckedChange`→`SelectedChanged`" — per the investigation report, that alone still leaves the chip non-clickable, since `IsButton` requires `OnClick.HasDelegate` OR a `MudChipSet` wrapper. Use the `OnClick` approach above (the MudChipSet wrapper alternative is not required — keep this fix minimal and consistent with the other working chips on this same page).

## Scope

Only touch `Components/Pages/Suppliers.razor` lines ~61-98 (the three tab chips). Do not touch the PVM chips, Aktyvus/Neaktyvus chips, `OnTabChanged`, `FilteredSuppliers`, or any other file — they are already confirmed working.

## Verification gates

- `dotnet build` — 0 errors.
- Reviewer verdict.
- Grep the edited block to confirm `Checked`, `OnCheckedChange`, `SelectionGroup` no longer appear anywhere in `Suppliers.razor`.

## Report

Write a full work report (diff summary, build output, verification) to `.opencode/reports/suppliers-tabs-fix-<timestamp>.md`. Use a real timestamp.

## Final step

Run `./bump-version.sh patch` as the last step — required, not optional.

Call `task_complete` as a real structured tool call when done, not as plain text.

## Before starting

Verify `git branch --show-current == main` before making any changes.
