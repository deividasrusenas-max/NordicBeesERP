#!/bin/bash
set -e
cd /Users/deividasru/Projects/NordicBeesERP

echo "=== FAZĖ 6 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --read Models/Artwork/ArtworkBrand.cs \
  --read Models/Artwork/ArtworkAsset.cs \
  --read Models/Artwork/ArtworkVersion.cs \
  --read Services/Artwork/ArtworkService.cs \
  --file Data/NordicBeesErpContext.cs \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 6 from ARTWORK_MODULE_PLAN.md. Create Components/Pages/Artwork/ArtworkAssetDetail.razor with: actual version block, approve/reject buttons for Admin, reject dialog requiring comment, version history timeline. Approve/reject actions use ExecuteSqlRawAsync - supersede old approved version and set new status in ONE transaction. Write to artwork_audit_log on every action. DO NOT use tool_call functions. Run ./bump-version.sh patch when done."

echo "=== FAZĖ 7 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --file Components/Layout/NavMenu.razor \
  --file Program.cs \
  --file Services/AuthService.cs \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 7 from ARTWORK_MODULE_PLAN.md. Add Designer to ErpUser role options. Add _isDesigner bool in NavMenu.razor following exact same pattern as _isAdmin. Designer sees ONLY Artwork nav items. Add ArtworkAccess policy in Program.cs requiring Admin OR Designer role. DO NOT use tool_call functions. Run ./bump-version.sh patch when done."

echo "=== FAZĖ 8 ==="
aider \
  --read Docs/ARTWORK_MODULE_PLAN.md \
  --read .clinerules/nordicbees-standards.md \
  --read Services/Artwork/ArtworkService.cs \
  --file appsettings.json \
  --test-cmd "dotnet build 2>&1 | tail -20" \
  --auto-test \
  --message "Implement Phase 8 from ARTWORK_MODULE_PLAN.md. Create Services/Artwork/ArtworkNotificationService.cs with fire-and-forget Telegram HTTP calls for upload and approve/reject events. Config keys: Telegram:BotToken, Telegram:AdminChatId, Telegram:DesignerChatId in appsettings.json. Failure must never throw - wrap in try/catch with logging only. Register as AddScoped in Program.cs. DO NOT use tool_call functions. Run ./bump-version.sh patch when done."

echo "=== VISKAS BAIGTA ==="
