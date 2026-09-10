#!/usr/bin/env bash
# Dump the dev DB schema to .opencode/db-schema.md for agents to read.
#
# Why this exists: agents repeatedly burned 20-40s of model reasoning per
# SHOW COLUMNS / DESCRIBE round trip to answer questions this file answers
# in a single read. See dotnet-efcore-nordicbees SKILL.md Rule 2 — the C#
# model and the real DB are known to disagree, so the real schema has to
# come from the DB, not from the model classes.
#
# Output is deliberately NOT tracked in git: a file that rewrites itself in
# the background would leave the tree dirty and make bump-version.sh refuse
# to run (that exact failure happened 2026-09-10 with opencode.json).
#
# Idempotent: only rewrites when the schema actually changed, so the file's
# mtime is meaningful.
#
# Usage:
#   scripts/dump-db-schema.sh          # refresh if changed
#   scripts/dump-db-schema.sh --check  # exit 1 if stale, write nothing
#   scripts/dump-db-schema.sh --force  # always rewrite

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$REPO_ROOT/.opencode/db-schema.md"
DB="${SCHEMA_DB:-nordic_bees_erp}"
DB_HOST="${SCHEMA_DB_HOST:-100.110.26.80}"
DB_PORT="${SCHEMA_DB_PORT:-3306}"
DB_USER="${SCHEMA_DB_USER:-erp_user}"

MODE="${1:-refresh}"

# --- credentials -----------------------------------------------------------
# Never hardcoded here (AGENTS.md "Secrets"). Order: env var, then .env.
DB_PASS="${SCHEMA_DB_PASS:-}"
if [ -z "$DB_PASS" ] && [ -f "$REPO_ROOT/.env" ]; then
  DB_PASS="$(sed -n 's/.*Pwd=\([^;]*\).*/\1/p' "$REPO_ROOT/.env" | head -1)"
fi
if [ -z "$DB_PASS" ]; then
  echo "ERROR: no DB password. Set SCHEMA_DB_PASS, or provide .env with Pwd=..." >&2
  exit 2
fi

command -v mariadb >/dev/null 2>&1 || { echo "ERROR: mariadb client not found" >&2; exit 2; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

q() {
  mariadb -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" \
    --skip-ssl --batch --raw --skip-column-names "$DB" -e "$1"
}

# --batch already emits tab-separated rows, so no CONCAT_WS needed.
q "SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE,
          IFNULL(COLUMN_KEY,''), IFNULL(COLUMN_DEFAULT,''), IFNULL(EXTRA,'')
   FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA='$DB'
   ORDER BY TABLE_NAME, ORDINAL_POSITION;" > "$TMP/cols.tsv" || {
  echo "ERROR: schema query failed — DB unreachable or credentials wrong." >&2
  exit 2
}

q "SELECT TABLE_NAME, INDEX_NAME,
          GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ','),
          IF(NON_UNIQUE=0,'UNIQUE','')
   FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA='$DB'
   GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE
   ORDER BY TABLE_NAME, INDEX_NAME;" > "$TMP/idx.tsv"

q "SELECT TABLE_NAME, COLUMN_NAME,
          CONCAT(REFERENCED_TABLE_NAME,'.',REFERENCED_COLUMN_NAME)
   FROM information_schema.KEY_COLUMN_USAGE
   WHERE TABLE_SCHEMA='$DB' AND REFERENCED_TABLE_NAME IS NOT NULL
   ORDER BY TABLE_NAME, COLUMN_NAME;" > "$TMP/fks.tsv"

# --- render ----------------------------------------------------------------
awk -F'\t' -v IDXF="$TMP/idx.tsv" -v FKSF="$TMP/fks.tsv" '
function emit_extras(t,   j, printed) {
  printed = 0
  for (j = 1; j <= ni; j++) {
    if (it[j] == t && iname[j] != "PRIMARY") {
      if (!printed) { printf "\nIndexes: "; printed = 1 } else printf " * "
      printf "%s(%s)%s", iname[j], icols[j], (iuniq[j] == "UNIQUE" ? " UNIQUE" : "")
    }
  }
  if (printed) printf "\n"
  printed = 0
  for (j = 1; j <= nf; j++) {
    if (ft[j] == t) {
      if (!printed) { printf "\nForeign keys: "; printed = 1 } else printf " * "
      printf "%s -> %s", fcol[j], fref[j]
    }
  }
  if (printed) printf "\n"
}
FILENAME == IDXF { ni++; it[ni]=$1; iname[ni]=$2; icols[ni]=$3; iuniq[ni]=$4; next }
FILENAME == FKSF { nf++; ft[nf]=$1; fcol[nf]=$2; fref[nf]=$3; next }
{
  if ($1 != prev) {
    if (prev != "") emit_extras(prev)
    printf "\n## %s\n\n", $1
    printf "| column | type | null | key | default | extra |\n"
    printf "|---|---|---|---|---|---|\n"
    prev = $1
  }
  printf "| %s | %s | %s | %s | %s | %s |\n", $2, $3, $4, $5, $6, $7
}
END { if (prev != "") emit_extras(prev) }
' "$TMP/idx.tsv" "$TMP/fks.tsv" "$TMP/cols.tsv" > "$TMP/body.md"

HASH="$(shasum -a 256 < "$TMP/body.md" | cut -c1-16)"
TABLES="$(cut -f1 "$TMP/cols.tsv" | sort -u | grep -c . || true)"

# --- staleness -------------------------------------------------------------
OLD_HASH=""
[ -f "$OUT" ] && OLD_HASH="$(sed -n 's/^schema-hash: //p' "$OUT" | head -1)"

if [ "$MODE" = "--check" ]; then
  if [ "$HASH" = "$OLD_HASH" ]; then echo "current ($HASH)"; exit 0; fi
  echo "STALE: file=${OLD_HASH:-none} db=$HASH"; exit 1
fi

if [ "$HASH" = "$OLD_HASH" ] && [ "$MODE" != "--force" ]; then
  echo "unchanged ($HASH) — not rewritten"
  exit 0
fi

mkdir -p "$(dirname "$OUT")"
{
  echo "# $DB — live schema"
  echo
  echo "generated: $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
  echo "schema-hash: $HASH"
  echo "tables: $TABLES"
  echo
  echo "> Generated from information_schema by scripts/dump-db-schema.sh."
  echo "> This is the REAL schema — the C# model classes are known to disagree"
  echo "> with it (see dotnet-efcore-nordicbees SKILL.md Rule 2). Read this"
  echo "> instead of running SHOW COLUMNS / DESCRIBE."
  echo ">"
  echo "> Not tracked in git, never edited by hand — rerun the script."
  cat "$TMP/body.md"
} > "$OUT"

echo "written: $OUT ($TABLES tables, $HASH)"
