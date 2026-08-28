#!/bin/bash
# Paleidžia OpenCode TUI su jau įrašytu naujausiu Claude paruoštu promptu.
# Prieš paleidimą automatiškai archyvuoja esamą latest.md turinį su timestamp'u —
# tai daroma shell script'e, ne per Claude, kad nereikėtų tam eikvoti tokenų.
#
# Naudojimas:
#   ./start-task.sh                      -> naudoja .opencode/tasks/latest.md
#   ./start-task.sh path/to/other.md     -> naudoja kitą failą (be auto-archyvavimo)
set -e

FILE="${1:-.opencode/tasks/latest.md}"

if [ ! -f "$FILE" ]; then
  echo "Task failas nerastas: $FILE"
  exit 1
fi

if [ "$FILE" = ".opencode/tasks/latest.md" ]; then
  TIMESTAMP=$(date +%Y%m%d-%H%M%S)
  mkdir -p .opencode/tasks/archive
  cp "$FILE" ".opencode/tasks/archive/task-${TIMESTAMP}.md"
  echo "Archyvuota: .opencode/tasks/archive/task-${TIMESTAMP}.md"
fi

opencode --agent orchestrator --prompt "$(cat "$FILE")"
