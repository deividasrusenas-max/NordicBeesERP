---
name: crud-completeness
description: Mandatory field-parity checklist for any Create/Edit page, dialog, or Service method that reads or writes a model with multiple properties. Use this whenever implementing or reviewing a form, dialog, page, or Service method that persists or loads data — this catches the single most common class of bugs in this codebase: a model gains a field, but the UI/Service that creates, edits, loads, or saves it doesn't handle every field.
---

# CRUD Field-Completeness Checklist — NordicBeesERP

The single most common recurring bug class in this codebase is **partial field handling**: a model/table has N properties, but the Create page, Edit dialog, Load method, or Save method only handles M < N of them. This produces working-looking code that silently drops data. This skill exists to make that impossible to miss during initial scaffolding, instead of being discovered and patched later.

## The rule

**For every model/entity you create or touch UI/Service code for, build an explicit field inventory first, then verify every single field against every stage below — not just the ones that seem obviously relevant to the current task.**

## Stage-by-stage checklist

For each property on the model (from the `[Column]`-mapped C# class, not just what a spec document happens to mention):

1. **Create/New form** — is there an input for it, OR is it deliberately server-set (e.g. `Id`, `CreatedAt`, audit fields)? If deliberately omitted, that's fine — but it must be a conscious decision, not an oversight. If unsure whether a field should be user-editable, say so explicitly rather than silently skipping it.
2. **Edit/Load** — when opening an existing record for editing, does the load code populate ALL editable fields from the database, or only some? A classic bug: 5 fields show correctly, a 6th silently resets to default because the load method forgot to map it.
3. **Save/Update** — does the save method write ALL fields the user could have changed, or does it drop some (leaving stale old values in the DB even though the UI looked like it changed)? Cross-check against Stage 1/2 — every field that's editable must appear in both load AND save.
4. **Validation parity** — if a DB column is `NOT NULL`, is there a corresponding required-field validation in the UI? If a DB column has a `CHECK`/enum constraint, does the UI restrict input to the same valid set (see Stage 5)?
5. **Enum/dropdown coverage** — if a field is an enum or has a fixed set of DB values (check the actual migration, not just the C# enum, since they can drift — see `dotnet-efcore-nordicbees` skill), does the UI dropdown/select offer ALL of them, or only some? A dropdown missing one enum value silently makes that state unreachable from the UI.
6. **Display/read views** — if there's a separate read-only view/table showing this entity (e.g. a list page, a details panel), does it show all the fields a user would reasonably expect, or only the ones from the original scaffold?

## How to actually do this without missing anything

1. Read the full model class first — list every `[Column]`-mapped property explicitly (write it out, don't just skim).
2. Read the full migration `CREATE TABLE`/`ALTER TABLE` for the same entity — cross-check the C# model isn't missing or has extra fields vs. the real DB (see `dotnet-efcore-nordicbees` skill Rule 2).
3. Go through the checklist above for every property on that explicit list — not from memory, from the list.
4. If a field is intentionally excluded from a stage (e.g. `CreatedAt` isn't user-editable), note it as intentional in your own report rather than silently omitting it — this makes the decision auditable later instead of looking like an oversight.

## Common places this bug hides in this codebase

- Edit dialogs that were copy-pasted from an earlier, simpler version of the entity before new columns were added (e.g. weight correction fields added after the dialog was first scaffolded).
- Service `UpdateAsync` methods using `ExecuteSqlRawAsync` (per project convention) with a hand-written column list — easy to forget a column when adding a new one, since there's no compiler check tying the SQL string to the model's properties.
- MudTable `HeaderContent`/`RowTemplate` pairs where a column was added to `HeaderContent` but the matching cell was never added to `RowTemplate`, or vice versa.
- Multi-step wizards (e.g. `DeliveryCreate.razor`) where a field captured in an early step is never actually read when constructing the final saved entity in the last step.
