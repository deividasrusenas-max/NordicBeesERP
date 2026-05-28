#!/bin/bash
# NordicBeesERP Project Audit Script
# Paleisk projekto šakniniame kataloge: bash erp_audit.sh > audit_report.txt

echo "========================================="
echo "NordicBeesERP AUDIT REPORT"
echo "Data: $(date)"
echo "========================================="

echo ""
echo "=== 1. PROJEKTO STRUKTŪRA (3 lygiai) ==="
find . -maxdepth 3 -type f \( -name "*.razor" -o -name "*.cs" -o -name "*.sql" \) | sort

echo ""
echo "=== 2. RAZOR PUSLAPIAI (GUI) ==="
echo "--- Pages/ katalogas ---"
find . -path "*/Pages/*.razor" -o -path "*/Components/*.razor" | sort
echo ""
echo "--- Kiekvieno puslapio @page routes ---"
grep -rn "@page" --include="*.razor" . 2>/dev/null | sort

echo ""
echo "=== 3. NAVMENU TURINYS ==="
find . -name "NavMenu.razor" -exec echo "--- {} ---" \; -exec cat {} \;

echo ""
echo "=== 4. DB MODELIAI (Entities/Models) ==="
find . -path "*/Models/*.cs" -o -path "*/Entities/*.cs" -o -path "*/Data/*.cs" | sort
echo ""
echo "--- DbContext klasė ---"
find . -name "*DbContext*.cs" -exec echo "--- {} ---" \; -exec cat {} \;

echo ""
echo "=== 5. SERVISAI (Services) ==="
find . -path "*/Services/*.cs" | sort
echo ""
echo "--- Service registracija Program.cs ---"
grep -n "builder.Services\|AddScoped\|AddTransient\|AddSingleton" $(find . -name "Program.cs" -not -path "*/obj/*" | head -1) 2>/dev/null

echo ""
echo "=== 6. MIGRACIJOS ==="
find . -path "*/Migrations/*.cs" -name "*.cs" ! -name "*.Designer.cs" | sort

echo ""
echo "=== 7. INVOICE SUSIJĘ FAILAI ==="
echo "--- Invoice razor puslapiai ---"
find . -name "*[Ii]nvoice*" -name "*.razor" | sort
echo "--- Invoice servisai ---"
find . -name "*[Ii]nvoice*" -name "*.cs" -not -path "*/obj/*" -not -path "*/Migrations/*" | sort

echo ""
echo "=== 8. LOT SUSIJĘ FAILAI ==="
find . -name "*[Ll]ot*" \( -name "*.razor" -o -name "*.cs" \) -not -path "*/obj/*" -not -path "*/Migrations/*" | sort

echo ""
echo "=== 9. PAYMENT/MOKĖJIMŲ FAILAI ==="
find . -name "*[Pp]ayment*" \( -name "*.razor" -o -name "*.cs" \) -not -path "*/obj/*" -not -path "*/Migrations/*" | sort

echo ""
echo "=== 10. WAREHOUSE/SANDĖLIO FAILAI ==="
find . -name "*[Ww]arehouse*" -o -name "*[Ss]tock*" | grep -E "\.(razor|cs)$" | grep -v "/obj/" | grep -v "/Migrations/" | sort

echo ""
echo "=== 11. PRODUCT/PREKIŲ FAILAI ==="
find . -name "*[Pp]roduct*" \( -name "*.razor" -o -name "*.cs" \) -not -path "*/obj/*" -not -path "*/Migrations/*" | sort

echo ""
echo "=== 12. PARTNER/KLIENTŲ FAILAI ==="
find . -name "*[Pp]artner*" -o -name "*[Cc]lient*" -o -name "*[Cc]ustomer*" | grep -E "\.(razor|cs)$" | grep -v "/obj/" | grep -v "/Migrations/" | sort

echo ""
echo "=== 13. PRODUCTION/GAMYBOS FAILAI ==="
find . -name "*[Pp]roduction*" -o -name "*[Bb]atch*" | grep -E "\.(razor|cs)$" | grep -v "/obj/" | grep -v "/Migrations/" | sort

echo ""
echo "=== 14. PDF GENERAVIMO FAILAI ==="
find . -name "*[Pp]df*" \( -name "*.razor" -o -name "*.cs" \) -not -path "*/obj/*" | sort
grep -rn "pdf\|PDF\|QuestPDF\|iText\|PdfSharp" --include="*.cs" --include="*.csproj" . 2>/dev/null | grep -v "/obj/" | head -20

echo ""
echo "=== 15. CSPROJ DEPENDENCIES ==="
find . -name "*.csproj" -not -path "*/obj/*" -exec echo "--- {} ---" \; -exec cat {} \;

echo ""
echo "=== 16. KOMPILIAVIMO KLAIDOS ==="
dotnet build --no-restore 2>&1 | tail -50

echo ""
echo "========================================="
echo "AUDIT BAIGTAS"
echo "========================================="
