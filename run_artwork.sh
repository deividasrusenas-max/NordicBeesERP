#!/bin/bash
set -e
cd /Users/deividasru/Projects/NordicBeesERP

echo "=== FAZĖ 4 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --read UI_STANDARD.md \
  --read Models/Artwork/ArtworkBrand.cs \
  --read Models/Artwork/ArtworkAsset.cs \
  --read Models/Artwork/ArtworkVersion.cs \
  --file Data/NordicBeesErpContext.cs \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 4 ONLY: create Components/Pages/Artwork/ArtworkDashboard.razor and Components/Pages/Artwork/ArtworkBrandPage.razor. Read-only pages, no upload/approval. Follow UI_STANDARD.md. Run ./bump-version.sh patch when done."

echo "=== FAZĖ 5 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --read UI_STANDARD.md \
  --read Models/Artwork/ArtworkBrand.cs \
  --read Models/Artwork/ArtworkAsset.cs \
  --read Models/Artwork/ArtworkVersion.cs \
  --file Data/NordicBeesErpContext.cs \
  --file Services/Artwork/ArtworkStorageService.cs \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 5 ONLY: create Components/Pages/Artwork/ArtworkUpload.razor with streaming upload, progress bar, change_description validation, SHA-256 duplicate check. Run ./bump-version.sh patch when done."

echo "=== FAZĖ 6 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --read Models/Artwork/ArtworkBrand.cs \
  --read Models/Artwork/ArtworkAsset.cs \
  --read Models/Artwork/ArtworkVersion.cs \
  --file Data/NordicBeesErpContext.cs \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 6 ONLY: approve/reject actions with supersede in one DB transaction via ExecuteSqlRawAsync, reject dialog requiring comment, audit log writes. Run ./bump-version.sh patch when done."

echo "=== FAZĖ 7 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --file Components/Layout/NavMenu.razor \
  --file Program.cs \
  --file Services/AuthService.cs \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 7 ONLY: add Designer role check in NavMenu.razor, ArtworkAccess authorization policy in Program.cs, Designer sees only Artwork section. Run ./bump-version.sh patch when done."

echo "=== FAZĖ 8 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --file appsettings.json \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 8 ONLY: Telegram fire-and-forget HTTP notifications on upload and approve/reject, config from appsettings, failure never fails main operation. Download audit logging. Run ./bump-version.sh patch when done."

echo "=== VISKAS BAIGTA ==="
