# NordicBeesERP — Artwork Management Module (MVP)

## 1. Purpose

Internal Label & Artwork Management (LAM) module. Single source of truth for all print-ready design files (labels, brochures, boxes) across brands. Solves the "which version is actual?" problem via an explicit approval workflow:

> **Actual version = the latest APPROVED version, not the latest uploaded one.**

Users: Deividas (Admin — reviews/approves), external designer (Designer role — uploads, sees only this module). Designer accesses the ERP over WireGuard (internal network, no public exposure).

## 2. Scope (MVP)

IN: brands, assets, immutable versions, mandatory change description, PDF preview/thumbnail generation, approval workflow (pending → approved/rejected, auto-supersede), version history with comments, audit log, Designer role with restricted access, Telegram notification on upload/decision.

OUT (later phases): source files (AI/PSD) storage — schema supports it via `file_type`, UI does not; side-by-side version compare; share links for print shops with expiry; metadata fields (bleed, color profile, die-line dims).

## 3. Domain rules (invariants)

1. A version file is **immutable**. Never overwritten, never deleted. New upload = new `version_number` (auto-increment per asset, starting at 1).
2. `change_description` is **required** on every upload (commit-message analogy).
3. Version statuses: `pending` → `approved` | `rejected`. When a version is approved, the previously approved version of the same asset transitions to `superseded` (in the same DB transaction).
4. Per asset, at most **one** version has status `approved`. That version is "actual" and is what the brand/asset page features with the Download button.
5. Only Admin role can approve/reject. Designer can upload and comment.
6. If the physical format / die-line changes (e.g., new print shop requirements), do **not** version the old asset — create a **new asset** with `predecessor_asset_id` pointing to the old one. The old asset is archived manually.
7. Reject identical re-uploads: compute SHA-256 on upload; if it equals the hash of the latest version of the same asset, refuse with a clear message.

## 4. Data model (MariaDB, EF Core)

All tables prefixed `artwork_`. Use existing `erp_users` for user FKs (NOT `users`/`AspNetUsers` — those are dead tables).

```sql
CREATE TABLE artwork_brands (
  id INT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(100) NOT NULL UNIQUE,        -- e.g. 'Nordic Bees', 'Honeymark', 'MEDŽIO'
  slug VARCHAR(100) NOT NULL UNIQUE,        -- filesystem-safe
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE artwork_assets (
  id INT AUTO_INCREMENT PRIMARY KEY,
  brand_id INT NOT NULL,
  name VARCHAR(200) NOT NULL,               -- e.g. 'Etiketė Medus 500g stiklainis'
  asset_type ENUM('label','brochure','box','sticker','other') NOT NULL DEFAULT 'label',
  description TEXT NULL,
  predecessor_asset_id INT NULL,            -- lineage when format/die-line changed
  status ENUM('active','archived') NOT NULL DEFAULT 'active',
  created_by INT NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_asset_brand FOREIGN KEY (brand_id) REFERENCES artwork_brands(id),
  CONSTRAINT fk_asset_predecessor FOREIGN KEY (predecessor_asset_id) REFERENCES artwork_assets(id),
  UNIQUE KEY uq_brand_asset_name (brand_id, name)
);

CREATE TABLE artwork_versions (
  id INT AUTO_INCREMENT PRIMARY KEY,
  asset_id INT NOT NULL,
  version_number INT NOT NULL,              -- 1, 2, 3... per asset
  file_type ENUM('print_ready','source') NOT NULL DEFAULT 'print_ready',
  file_path VARCHAR(500) NOT NULL,          -- relative to storage root
  original_filename VARCHAR(300) NOT NULL,
  file_size_bytes BIGINT NOT NULL,
  file_sha256 CHAR(64) NOT NULL,
  preview_path VARCHAR(500) NULL,           -- full-size PNG of page 1 (filled by background job)
  thumbnail_path VARCHAR(500) NULL,         -- ~400px wide PNG
  page_count INT NULL,
  change_description TEXT NOT NULL,         -- mandatory, the "commit message"
  status ENUM('pending','approved','rejected','superseded') NOT NULL DEFAULT 'pending',
  uploaded_by INT NOT NULL,
  uploaded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reviewed_by INT NULL,
  reviewed_at DATETIME NULL,
  review_comment TEXT NULL,                 -- mandatory when status = rejected
  CONSTRAINT fk_ver_asset FOREIGN KEY (asset_id) REFERENCES artwork_assets(id),
  UNIQUE KEY uq_asset_version (asset_id, version_number),
  KEY idx_asset_status (asset_id, status)
);

CREATE TABLE artwork_comments (
  id INT AUTO_INCREMENT PRIMARY KEY,
  version_id INT NOT NULL,
  user_id INT NOT NULL,
  body TEXT NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_comment_version FOREIGN KEY (version_id) REFERENCES artwork_versions(id)
);

CREATE TABLE artwork_audit_log (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  entity_type VARCHAR(50) NOT NULL,         -- 'brand' | 'asset' | 'version' | 'comment'
  entity_id INT NOT NULL,
  action VARCHAR(50) NOT NULL,              -- 'created' | 'uploaded' | 'approved' | 'rejected' | 'archived' | 'downloaded'
  user_id INT NOT NULL,
  details JSON NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_entity (entity_type, entity_id)
);
```

Note: include `downloaded` action in audit log — knowing which version was downloaded (and presumably sent to the print shop) is valuable history.

## 5. File storage

- Storage root on the VM: `/var/lib/nordicbees/artwork/` mounted into the container as a Docker volume (add to both prod and staging compose files; staging gets its own root).
- Path convention: `{brand_slug}/{asset_id}/v{version_number}/{original_filename}` plus `preview.png` and `thumb.png` alongside.
- Files written once, never modified. Deletion not exposed in UI.
- Upload limits: print-ready PDFs can reach 500 MB. Configure Kestrel `MaxRequestBodySize` = 1 GB for the upload endpoint; use **streaming** upload to disk (do not buffer in memory; MudBlazor `IBrowserFile.OpenReadStream(maxAllowedSize: ...)` streamed to FileStream). Compute SHA-256 while streaming.
- Include this directory in the existing VM backup routine (flag for Saulius).

## 6. Preview generation

- Background processing via a `BackgroundService` + in-memory channel queue (no external queue needed at this scale).
- Tool: **Ghostscript** (`apt-get install ghostscript` in Dockerfile). Commands:
  - Thumbnail: `gs -dNOPAUSE -dBATCH -sDEVICE=png16m -r72 -dFirstPage=1 -dLastPage=1 -o thumb_raw.png file.pdf` then downscale to 400px width (ImageSharp or `-r` tuning).
  - Full preview: same at `-r150`, first page (or all pages later).
- Ghostscript handles CMYK PDFs; colors on screen are approximate — acceptable, the PDF itself is the deliverable.
- UI shows a "preview generating…" placeholder until `preview_path` is filled; page polls or uses Blazor Server state refresh.
- On Ghostscript failure: log, leave preview NULL, show generic PDF icon. Never block the upload because of preview failure.

## 7. Authorization

- New role `Designer` in the existing `erp_users` role mechanism.
- Authorization policy `ArtworkAccess`: Admin + Designer. All `/artwork/*` pages require it.
- Designer role: ERP navigation shows ONLY the Artwork section; direct URL access to other modules returns 403. Implement as a policy check on existing modules' base layout or per-page `[Authorize(Policy=...)]` — verify how current pages are protected and follow the same pattern.
- Designer permissions: create assets, upload versions, comment, view everything in the module. Cannot approve/reject, cannot archive, cannot create brands (Admin creates brands).

## 8. UI (Blazor Server + MudBlazor)

### `/artwork` — Brand dashboard
Grid of brand cards: brand name, count of assets, count of pending versions (badge — this is the Admin's "inbox" signal).

### `/artwork/brand/{id}` — Asset list
Cards or table: thumbnail of the actual (approved) version, asset name, type, actual version number, chip if a pending version exists, archived assets hidden behind a toggle.

### `/artwork/asset/{id}` — Asset detail (the core page)
- Header: asset name, brand, type, lineage link if `predecessor_asset_id` set.
- **Actual version block**: large preview, `v{n}`, approved date, approver, prominent **Download print-ready** button (streams original file, logs `downloaded` to audit).
- If a pending version exists and user is Admin: review block with preview, change description, **Approve** / **Reject** buttons (Reject opens a dialog requiring a comment).
- **History timeline** (descending): every version with version number, status chip (color-coded: pending=orange, approved=green, rejected=red, superseded=grey), uploader, date, change_description, review_comment if any, expandable comments thread with add-comment field, per-version download link.

### `/artwork/upload` — Upload flow (designer's main page)
1. Select brand → select existing asset OR "New asset" (name + type + optional description; show hint: "If the format/die-line changed, create a NEW asset and link the old one as predecessor").
2. Drag & drop PDF (accept `.pdf` only in MVP).
3. `change_description` — required multiline field, validation blocks submit if empty.
4. Submit → streaming upload with progress bar → version created as `pending` → redirect to asset page.

## 9. Notifications (MVP-simple)

Plain HTTP call to Telegram Bot API (reuse existing bot token pattern; config in appsettings/env):
- New version uploaded → message to Deividas: brand / asset / v{n} / change description + direct link.
- Approved/rejected → message to designer's chat (store designer chat_id in config for MVP, not DB).
Failure to notify must never fail the main operation — fire-and-forget with logging.

## 10. Implementation phases (Cline work units)

Each phase = one self-contained task, build must pass after each.

1. **Entities + migration**: EF Core entities, DbContext registration, migration creating the 5 tables. Seed initial brands (Nordic Bees, Honeymark, MEDŽIO).
2. **Storage service**: `IArtworkStorageService` — streamed save with SHA-256, path convention, duplicate-hash check, file streaming for download. Unit-testable, storage root from config.
3. **Preview background service**: channel queue, Ghostscript wrapper, thumbnail + preview generation, DB update. Dockerfile: add ghostscript.
4. **Pages — read side**: brand dashboard, brand page, asset detail with history timeline (no upload yet; seed test data manually).
5. **Upload flow**: upload page, streaming, validation, version numbering (transaction-safe: `SELECT MAX(version_number) ... FOR UPDATE` or equivalent EF approach), pending status.
6. **Approval workflow**: approve/reject actions, supersede transition in one transaction, review dialogs, audit log writes for all actions.
7. **Designer role + authorization**: role, policy, nav restrictions, designer account creation.
8. **Telegram notifications + polish**: notifications, download audit logging, empty states, error states.

Deploy: staging (8081) first, designer tests upload over WireGuard, then prod.

## 11. Infrastructure checklist (Saulius / manual)

- [ ] WireGuard peer for the designer, routing to 10.255.8.5
- [ ] Docker volume `/var/lib/nordicbees/artwork` created, owned by container user, added to compose (prod + staging, separate roots)
- [ ] Backup routine includes the artwork directory
- [ ] Disk space check: assume ~200 MB avg per version; plan for tens of GB over time

## 12. Explicitly deferred (do not build now)

- Side-by-side version compare view
- Expiring share links for print shops (no-login download)
- Source file (AI/PSD) upload UI
- Metadata fields: dimensions, bleed, color profile, die-line reference
- Multi-page preview gallery
- Email notifications
