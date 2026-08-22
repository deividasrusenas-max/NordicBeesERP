---
name: mudblazor
description: Guide for writing or fixing MudBlazor markup in NordicBeesERP (.NET 10 Blazor Server). Use this whenever creating or editing a .razor file that uses MudBlazor components (MudContainer, MudPaper, MudTable, MudStack, MudGrid, MudDialog, etc.), including layout structure, dialogs, snackbars, forms, and data grids. Also use when a build error mentions mismatched or unbalanced tags in a .razor file, or when troubleshooting popups/dropdowns rendering nothing.
---

# MudBlazor — NordicBeesERP Usage Guide

MudBlazor is a Material Design component library for Blazor written entirely in C#/Razor. This project uses it as the sole UI component library.

**This skill is a supplement, not a replacement, for project conventions.**
Before writing any markup, always check `Docs/UI_STANDARD.md` and
`Docs/DESIGN_SYSTEM.md` first — those are authoritative for this project
(layout shell, spacing conventions, color scheme).

Official docs: https://mudblazor.com/ · Component demos: https://mudblazor.com/components/

---

## Decision order when writing UI

1. Use an existing MudBlazor component and its parameters.
2. Compose MudBlazor layout primitives: `MudStack`, `MudGrid`, `MudItem`, `MudPaper`, `MudContainer`, `MudSpacer`.
3. Apply MudBlazor utility classes (`pa-4`, `mt-4`, `d-flex`, `justify-end`, `align-center`) via the `Class` attribute before writing custom CSS.
4. Use built-in parameters (`Color`, `Variant`, `Typo`, `Elevation`, `Dense`, `GutterSize`) before styling.
5. Only as a last resort, add an isolated `.razor.css` — and keep it minimal. Never add `<style>` blocks inline in a `.razor` file.

---

## PREVENTING TAG-NESTING ERRORS (read this before editing any existing .razor file)

The most common build-breaking mistake in this codebase has been mismatched/dangling closing tags (`</MudStack>`, `</MudGrid>`, `</MudPaper>`, `</MudTable>`) after editing a large existing file. Follow this checklist every time:

1. **Read the ENTIRE file first**, not just the section you're changing. Note the opening tag stack (which components are open, in what order) at the point where your edit starts and ends.
2. **Every MudBlazor container component needs an explicit closing tag** — `MudStack`, `MudGrid`, `MudItem`, `MudPaper`, `MudContainer`, `MudTable`, `MudCard`, `MudCardContent`, `MudCardActions`, `MudDialog`, `MudTabPanel`, `MudExpansionPanel` are NOT self-closing unless they have no children (e.g. `<MudSpacer />`, `<MudDivider />`).
3. **After any edit, mentally re-count the tag stack** from the start of the file to the end. If you added an opening tag, you must add exactly one matching closing tag; if you removed one, remove its pair too.
4. **When replacing a block, replace matched pairs together** — never replace just an opening tag or just a closing tag in isolation; always include the full block from open to close in your `edit` old/new strings so you can see both ends match.
5. **`MudTable`/`MudDataGrid` in particular have multi-part structure** (`<MudTable>` → `<ToolBarContent>`/`<HeaderContent>`/`<RowTemplate>`/`<PagerContent>` → `</MudTable>`) — don't drop a section's closing tag when adding a new column or row template.
6. If a build error reports "mismatched tag" or a cascading Razor compiler error with no clear single cause, the actual bug is very likely a tag count mismatch somewhere earlier in the file, not at the reported line — search upward from the error line for the nearest unclosed or extra tag.

---

## Setup (reference only — likely already done in this project)

`MainLayout.razor` must contain `<MudThemeProvider />`, `<MudPopoverProvider />` (required for dropdowns/autocomplete/select), `<MudDialogProvider />`, `<MudSnackbarProvider />`. Missing `MudPopoverProvider` is why a dropdown silently renders nothing; missing `MudDialogProvider`/`MudSnackbarProvider` is why `DialogService.ShowAsync`/`Snackbar.Add` silently do nothing.

---

## Common patterns — see real project files, don't guess syntax

Standard MudBlazor API syntax (dialogs, snackbars, tables, forms) is well-documented in the official docs linked above and is standard library usage — no need to look it up here. If you need a concrete in-project example of a specific pattern, read the actual file rather than relying on a generic snippet:

- **Dialog service pattern**: `Components/Dialogs/PackLineDialog.razor` (simple confirm/cancel dialog with typed result)
- **Snackbar usage**: any Service-calling handler in `Components/Pages/Orders/Detail.razor`
- **MudTable with filters/status chips**: `Components/Pages/Invoices.razor` (the project's reference implementation for filterable tables)
- **Loading state pattern**: any page with `_isLoading` — e.g. `Components/Pages/Orders/Index.razor`

---

## Do NOT use roslyn_get_diagnostics on .razor files

.razor files are compiled via the Razor source generator into a separate
generated .g.cs file — Roslyn/sharplens-mcp cannot resolve diagnostics for
the raw .razor file path directly, and this has been confirmed to fail
repeatedly in this environment (wastes multiple tool calls chasing a dead
end every time it's tried). Real compile verification for .razor changes
happens later via `dotnet build` (run by `fixer`) — don't attempt Roslyn
diagnostics on .razor files yourself; trust the build step instead.

## Known pitfalls

- **`MudPopoverProvider` missing** → autocomplete/select dropdown renders nothing. Fix: add it to `MainLayout.razor`.
- **`MudDialogProvider` missing** → `DialogService.ShowAsync` silently no-ops.
- **`MudSnackbarProvider` missing** → `Snackbar.Add` silently no-ops.
- **`MudColorPicker` binding** — use `@bind-Text` for a hex string, `@bind-Value` for a `MudColor` object. Don't mix them up.
- **Custom CSS introduced too early** — if you're about to add a `.razor.css` file or bespoke selector just for spacing/alignment, stop and use MudBlazor utility classes or layout primitives instead (see Decision order above).
- **Boolean parameters** — always bind booleans explicitly (e.g. `FullWidth="@true"`), don't rely on bare attribute presence (`FullWidth="true"` as a plain string is wrong).
- **`MudChip` always needs `T="string"`** (or whatever type) — `<MudChip Color="...">Text</MudChip>` without `T=` is a common build error. Same applies to `MudChip` inside a `MudChipSet T="string"` — the inner chip still needs its own `T=` too.
- **`MudSimpleTable` has no `Headers`/`Items` sub-tags** — it's a thin wrapper around a plain HTML `<table>`. Use real `<thead>`/`<tbody>`/`<tfoot>` with `<tr>`/`<th>`/`<td>` inside it, not MudBlazor sub-components.
- **`MudTable` has no `Column`/`RowCells`/`Footer` sub-tags** — the correct structure is `<HeaderContent><MudTh>...</MudTh></HeaderContent>`, `<RowTemplate Context="item"><MudTd>...</MudTd></RowTemplate>`, `<PagerContent><MudTablePager .../></PagerContent>`.
- **UI components must never write to the DB directly** — a `.razor` file should call a `Service` method, never inject `IDbContextFactory` and do `ctx.Add(...); await ctx.SaveChangesAsync()` itself (exceptions: read-only lookups in a couple of specific dialogs, e.g. supplier search — check the actual file before assuming a lookup is disallowed).
- **`[NotMapped]` navigation properties + `Include()` = silently empty** — if an entity has `[NotMapped]` on a navigation property (and a matching `entity.Ignore()` in the DbContext), `Include()` will NEVER load it, with no error. Load the related rows via a separate, dedicated Service method instead (e.g. `GetInvoiceLinesAsync(id)`), never `.Include(x => x.Lines)`.
- **Removing an interface method can silently break an unrelated caller** — before deleting or renaming any interface method, `grep -rn "MethodName" .` first to confirm nothing still calls it (a background worker or a different page is a common blind spot).
- **`@if`/`else` with a `@{ }` block right before a nested `@if` can cause RZ1010** — move the `@{ }` variable declaration before the `else`, or inline the expression directly into the `@if` condition, rather than putting `@{ }` as the first thing inside an `else` block.
- **`@keyframes` in a `<style>` block needs `@@keyframes`** in a `.razor` file (Razor treats a bare `@` as the start of a C# expression).
