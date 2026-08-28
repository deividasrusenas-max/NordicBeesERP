#!/bin/bash
# Paleidžia Claude paruoštą task promptą per OpenCode, be copy-paste.
# Naudojimas:
#   ./run-task.sh                        -> paleidžia .opencode/tasks/latest.md
#   ./run-task.sh .opencode/tasks/archive/kazkas.md   -> paleidžia konkretų failą
set -e

FILE="${1:-.opencode/tasks/latest.md}"

if [ ! -f "$FILE" ]; then
  echo "Task failas nerastas: $FILE"
  exit 1
fi

echo "=== Vykdomas task failas: $FILE ==="
echo "--- Pirmos eilutės ---"
head -5 "$FILE"
echo "----------------------"
echo ""

opencode run "$(cat "$FILE")"
