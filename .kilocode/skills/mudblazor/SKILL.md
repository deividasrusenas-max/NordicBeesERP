---
name: mudblazor
description: Guide for writing or fixing MudBlazor markup in NordicBeesERP (.NET 10 Blazor Server). Use this whenever creating or editing a .razor file that uses MudBlazor components (MudContainer, MudPaper, MudTable, MudStack, MudGrid, MudDialog, etc.), including layout structure, dialogs, snackbars, forms, and data grids. Also use when a build error mentions mismatched or unbalanced tags in a .razor file, or when troubleshooting popups/dropdowns rendering nothing.
---

# MudBlazor — NordicBeesERP Usage Guide

MudBlazor is a Material Design component library for Blazor written entirely in C#/Razor. This project uses it as the sole UI component library.

**This skill is a supplement, not a replacement, for project conventions.**
Before writing any markup, always check `.clinerules/UI_STANDARD.md`,
`.clinerules/PATTERNS.md`, and `.clinerules/DESIGN_SYSTEM.md` first — those
are authoritative for this project (layout shell, spacing conventions,
color scheme). This skill fills in general MudBlazor API/syntax knowledge
those files don't repeat.

Official docs: https://mudblazor.com/ · Component demos: https://mudblazor.com/components/

---

## Decision order when writing UI

1. Use an existing MudBlazor component and its parameters.
2. Compose MudBlazor layout primitives: `MudStack`, `MudGrid`, `MudItem`, `MudPaper`, `MudContainer`, `MudSpacer`.
3. Apply MudBlazor utility classes (`pa-4`, `mt-4`, `d-flex`, `justify-end`, `align-center`) via the `Class` attribute before writing custom CSS.
4. Use built-in parameters (`Color`, `Variant`, `Typo`, `Elevation`, `Dense`, `GutterSize`) before styling.
5. Only as a last resort, add an isolated `.razor.css` — and keep it minimal. Never add `<style>` blocks inline in a `.razor` file.

Prefer `MudStack`/`MudGrid` over a raw `<div>` used only for layout. Prefer `MudText`, `MudAlert`, `MudChip`, `MudPaper`, `MudDivider` over styled HTML elements.

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

```csharp
// Program.cs
builder.Services.AddMudServices();
```
```razor
@* _Imports.razor *@
@using MudBlazor
```
`MainLayout.razor` must contain `<MudThemeProvider />`, `<MudPopoverProvider />` (required for dropdowns/autocomplete/select), `<MudDialogProvider />`, `<MudSnackbarProvider />`. Missing `MudPopoverProvider` is why a dropdown silently renders nothing; missing `MudDialogProvider`/`MudSnackbarProvider` is why `DialogService.ShowAsync`/`Snackbar.Add` silently do nothing.

---

## Common patterns

### Dialog service
```csharp
[Inject] private IDialogService DialogService { get; set; } = default!;

var parameters = new DialogParameters<MyDialog> { { x => x.SomeParam, value } };
var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
var dialog = await DialogService.ShowAsync<MyDialog>("Title", parameters, options);
var result = await dialog.Result;
if (!result.Canceled) { /* handle confirmed result */ }
```
```razor
@* MyDialog.razor *@
<MudDialog>
    <TitleContent>Title</TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_name" Label="Name" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" OnClick="@Submit">Save</MudButton>
    </DialogActions>
</MudDialog>
@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    private void Cancel() => MudDialog.Cancel();
    private void Submit() => MudDialog.Close(DialogResult.Ok(_name));
}
```

### Snackbar
```csharp
[Inject] private ISnackbar Snackbar { get; set; } = default!;
Snackbar.Add("Saved successfully.", Severity.Success);
Snackbar.Add("Failed to save.", Severity.Error);
```

### Loading state
```razor
@if (_loading)
{
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
}
else
{
    @* content *@
}
```

### Table (follow PATTERNS.md MudTable convention already established in this project — Suppliers.razor/Invoices.razor are the reference)
```razor
<MudPaper Class="pa-4">
    <MudTable Items="@_items" Hover="true" Dense="true">
        <HeaderContent>
            <MudTh>Name</MudTh>
            <MudTh>Status</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd>@context.Name</MudTd>
            <MudTd>@context.Status</MudTd>
        </RowTemplate>
    </MudTable>
</MudPaper>
```

---

## Known pitfalls

- **`MudPopoverProvider` missing** → autocomplete/select dropdown renders nothing. Fix: add it to `MainLayout.razor`.
- **`MudDialogProvider` missing** → `DialogService.ShowAsync` silently no-ops.
- **`MudSnackbarProvider` missing** → `Snackbar.Add` silently no-ops.
- **`MudColorPicker` binding** — use `@bind-Text` for a hex string, `@bind-Value` for a `MudColor` object. Don't mix them up.
- **Custom CSS introduced too early** — if you're about to add a `.razor.css` file or bespoke selector just for spacing/alignment, stop and use MudBlazor utility classes or layout primitives instead (see Decision order above).
- **Boolean parameters** — per this project's PATTERNS.md, always bind booleans explicitly (e.g. `FullWidth="@true"`), don't rely on bare attribute presence.
