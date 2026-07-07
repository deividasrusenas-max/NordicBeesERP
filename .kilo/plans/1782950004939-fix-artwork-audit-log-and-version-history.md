# Fix ArtworkService Audit Log & Version History

## Issues

1. **Audit log writes to wrong table** — 3 INSERT statements target `artwork_version_audits` (old table with columns: version_id, action_details, old_status, new_status, performed_by, performed_at) instead of `artwork_audit_log` (new table with columns: entity_type, entity_id, action, user_id, details, created_at). Audit rows are written to a dead table.

2. **ArtworkVersionAudit model dead** — `Models/Artwork/ArtworkVersionAudit.cs` maps to `artwork_version_audits` table which is no longer created by InitialCreate migration. DbContext has a DbSet for it.

3. **Version history missing uploaded versions** — `GetAssetDetailAsync` History query filters out `v.Status != "pending"`, so newly uploaded versions (which are always "pending") don't appear in the history table. They're only shown in the separate pending card. Also filters out the current actual version (`v.Id != actualVersion?.Id`), which is unnecessary — the approved version should be visible in history as the current entry.

---

## Changes

### 1. ArtworkService.cs — Fix 3 INSERT statements (lines 83-112)

**ApproveVersionAsync** — supersede audit (line 83-85):
```csharp
// Before:
await _context.Database.ExecuteSqlRawAsync(
    "INSERT INTO artwork_version_audits (version_id, action, action_details, old_status, new_status, performed_by, performed_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
    oldVersion.Id, "STATUS_CHANGED", $"Superseded by version {versionToApprove.VersionNumber}", "approved", "superseded", performedBy, currentTimestamp);

// After:
await _context.Database.ExecuteSqlRawAsync(
    "INSERT INTO artwork_audit_log (entity_type, entity_id, action, user_id, details, created_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
    "version", oldVersion.Id, "STATUS_CHANGED", userId.Value, $"Superseded by version {versionToApprove.VersionNumber} (approved→superseded)", currentTimestamp);
```

**ApproveVersionAsync** — approval audit (line 89-91):
```csharp
// Before:
await _context.Database.ExecuteSqlRawAsync(
    "INSERT INTO artwork_version_audits (version_id, action, action_details, old_status, new_status, performed_by, performed_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
    versionId, "APPROVED", $"Approved by reviewer ID {reviewerId}", "pending", "approved", performedBy, currentTimestamp);

// After:
await _context.Database.ExecuteSqlRawAsync(
    "INSERT INTO artwork_audit_log (entity_type, entity_id, action, user_id, details, created_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
    "version", versionId, "APPROVED", userId.Value, $"Approved by reviewer ID {reviewerId} (pending→approved)", currentTimestamp);
```

**RejectVersionAsync** (line 110-112):
```csharp
// Before:
await _context.Database.ExecuteSqlRawAsync(
    "INSERT INTO artwork_version_audits (version_id, action, action_details, old_status, new_status, performed_by, performed_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
    versionId, "REJECTED", $"Rejected by reviewer ID {reviewerId}: {comment}", oldStatus, "rejected", performedBy, DateTime.UtcNow);

// After:
await _context.Database.ExecuteSqlRawAsync(
    "INSERT INTO artwork_audit_log (entity_type, entity_id, action, user_id, details, created_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
    "version", versionId, "REJECTED", userId.Value, $"Rejected by reviewer ID {reviewerId} ({oldStatus}→rejected): {comment}", DateTime.UtcNow);
```

Also update both `ApproveVersionAsync` and `RejectVersionAsync` to capture `userId` before the audit writes:
- In `ApproveVersionAsync`, add after line 61: `var userId = await _authService.GetUserIdAsync();`
- In `RejectVersionAsync`, add after line 108: `var userId = await _authService.GetUserIdAsync();`

### 2. ArtworkService.cs — Fix version history (line 38-40)

```csharp
// Before:
var history = allVersions
    .Where(v => v.Status != "pending" && v.Id != (actualVersion?.Id ?? 0))
    .ToList();

// After:
var history = allVersions
    .Where(v => v.Status != "pending")
    .ToList();
```

This shows all non-pending versions (approved, superseded, rejected, archived) in history. Pending versions are already displayed in the separate "Laukianti patvirtinimo" card. The current actual version now appears in history (showing the full version lifecycle) instead of being hidden.

### 3. Remove ArtworkVersionAudit model

Delete file: `Models/Artwork/ArtworkVersionAudit.cs`

### 4. Remove ArtworkVersionAudit DbSet

In `Data/NordicBeesERPContext.cs`, remove line 112:
```csharp
// Remove this line:
public DbSet<ArtworkVersionAudit> ArtworkVersionAudits { get; set; }
```

### 5. Run version bump

```bash
./bump-version.sh patch
```

---

## Validation

- Verify no remaining references to `artwork_version_audits` in code (grep for it)
- Verify no remaining references to `ArtworkVersionAudit` class
- The `artwork_version_audits` table still exists in DB (from migration 20260701192157) but is no longer used — that's fine
- After approval: new audit row appears in `artwork_audit_log` with correct `entity_type='version'`, `user_id`, `action`, `details`
- After reject: same — audit row in `artwork_audit_log`
- After upload: pending version appears in pending card, all non-pending versions appear in History table
- After approve: the newly approved version appears in History table (previously hidden by `v.Id != actualVersion?.Id` filter)
