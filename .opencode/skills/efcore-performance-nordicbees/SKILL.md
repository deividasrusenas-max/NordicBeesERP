---
name: efcore-performance-nordicbees
description: EF Core query performance patterns for NordicBeesERP — N+1 detection, avoiding shallow performance "fixes," and choosing the right read pattern given this project's raw-SQL-heavy write convention. Use when writing or reviewing any Service method that queries data, especially list/report pages, or when asked to "make this faster."
---

# EF Core Performance — NordicBeesERP

**The shallow-fix trap:** when asked to speed up a slow query, the reflexive
answers — "switch to raw SQL" or "add `.AsNoTracking()` everywhere" — are
usually wrong or already irrelevant here, since this project's `DbContext`
already applies `QueryTrackingBehavior.NoTracking` globally (see
`dotnet-efcore-nordicbees` skill). Before touching anything, actually find
the real cause: is it N+1, a missing index, over-fetching, or something else.

## Step 1: is it actually N+1?

The classic pattern: a loop that queries once per item instead of once
total.

```csharp
// WRONG — N+1: one query per delivery to fetch its lines
foreach (var delivery in deliveries)
{
    var lines = await context.DeliveryLines
        .Where(l => l.DeliveryId == delivery.Id).ToListAsync();
}
```
```csharp
// CORRECT — one query, grouped in memory (or a single join query)
var allLines = await context.DeliveryLines
    .Where(l => deliveryIds.Contains(l.DeliveryId))
    .ToListAsync();
var linesByDelivery = allLines.ToLookup(l => l.DeliveryId);
```

Also watch for N+1 hiding inside a Razor component — a `@foreach` in a
`.razor` file that calls an injected service per row is the same bug,
just less visible than a C# loop.

**Note on `Include()`** — per `dotnet-efcore-nordicbees` skill, this
project uses `[NotMapped]`/`entity.Ignore()` for several navigation
properties, meaning EF Core's `Include()` silently returns empty
collections for those. Don't reach for `Include()` as the fix here — it
won't work for the ignored ones. Use dedicated `GetXLinesAsync(id)` methods
(the project's existing pattern) or the batched-lookup pattern above.

## Step 2: if it's not N+1, check these before reaching for raw SQL

- **Missing index** — is the query filtering/sorting/joining on an
  unindexed column? Check the migration file for existing `KEY`/`INDEX`
  definitions on the table before assuming code is the problem.
- **Over-fetching** — is the query pulling entire entities (with large
  text/blob columns like `zpl_content`, `content`, `document_path`) when
  only 2-3 fields are actually displayed? Project to a DTO/anonymous type
  instead of loading the full entity.
- **Missing pagination** — a list/report page that loads an entire table
  into memory will get slower as the table grows regardless of query
  shape. Check whether the page should page/limit results.

## Step 3: only reach for raw SQL / ExecuteSqlRawAsync when

- It's a WRITE operation (per this project's mandatory convention — see
  `dotnet-efcore-nordicbees` skill Rule 1), or
- The query genuinely can't be expressed efficiently in LINQ (rare —
  verify this is actually true before assuming it).

Raw SQL for a slow READ is usually treating a symptom, not the cause —
diagnose with steps 1-2 first.

## Reporting

When you "optimize" a query, say explicitly what the actual cause was
(N+1 / missing index / over-fetching / other) — a report that just says
"made it faster" without naming the cause is not useful and can't be
verified.
