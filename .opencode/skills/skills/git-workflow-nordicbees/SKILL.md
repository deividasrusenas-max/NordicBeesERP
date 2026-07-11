---
name: git-workflow-nordicbees
description: Mandatory commit/version workflow and CI-enforced coding restrictions for NordicBeesERP. Use this whenever about to commit any change, whenever writing a commit message, or whenever writing code that touches VAT rates, company name/address, console output, or file paths — this project has a CI check that blocks builds containing certain hardcoded values.
---

# NordicBeesERP — Git Workflow & Hardcode Restrictions

## Every commit must follow this exact sequence

1. Make the change (one logical unit — one file or one tightly related group).
2. `dotnet build` — must be zero errors before committing.
3. Bump the patch version: run `./bump-version.sh patch` (this modifies `AssemblyInfo.cs`/`.csproj`, commits, tags, and pushes automatically — do not bump the version manually by editing the file yourself).
4. If `bump-version.sh` doesn't itself create the content commit, commit with the message format below.

## Commit message format

```
P0a: <FileName or Area> — <what changed, in a few words>
```

Examples from this project's history:
- `P0a: Migrations/20260602150000_InitialCreate.cs — added 9 labeling tables and 4 ALTER statements for BRC8 compliance`
- `fix: BUCKET_GROUP -> BUCKET safe conversion (widen/update/narrow) — preserves existing data`
- `feat: ContainerWeightCorrection model — BRC8 3.7 weight correction audit trail with 6 weight columns`

Use `P0a:` prefix for labeling-module/task-tracked work, `fix:`/`feat:`/`chore:` for general changes. Keep the message on one line, descriptive but concise. Never write a generic message like "update file" or "fixes".

## CI hardcode-check — build-blocking, not just style

This project has a CI workflow (`ci: add hardcode-check workflow`) that **blocks the build** if it finds:

- **Hardcoded VAT rates** — never write `0.21m`, `21m`, `1.21m` etc. as a literal VAT/tax rate anywhere. Always read the rate from `CompanySettings.DefaultVatRate` or from the actual invoice's stored `SubtotalExclVat`/`TotalVat` fields — never recompute or assume a fixed percentage.
- **Hardcoded company name/address** — never write the company's literal name/address as a string literal in code (e.g. in a PDF template, email, or UI label). Always pull it from the `CompanySettings` service/DB record.
- **`Console.WriteLine`** — never use it, anywhere, for any reason (including debug logging). Use the injected `ILogger`/`ILogger<T>` instead.
- **`Directory.GetCurrentDirectory()`** — never use it for resolving file paths (uploads, logos, generated PDFs, etc.). Use `IWebHostEnvironment.WebRootPath` (injected) instead — `GetCurrentDirectory()` resolves incorrectly depending on how/where the app is launched (IDE vs Docker vs systemd), which has caused real bugs in this project before.

Before finishing any task, mentally check your new/changed code against this list — the CI check will fail the build otherwise, which is more costly to fix later than to avoid now.

## Staging DB only for automated task work

Automated task work (via `code`/`debug`/`fixer` agents) should target the **dev/staging environment only**. Never run destructive SQL or apply migrations against the production database (`10.255.8.5`) as part of an automated task — production changes are a separate, manual, human-supervised step.
