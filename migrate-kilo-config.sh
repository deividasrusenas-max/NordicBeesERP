#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="/Users/deividasru/Projects/NordicBeesERP"
cd "$PROJECT_ROOT"

TS=$(date +%Y%m%d_%H%M%S)

echo "== 1. Backup esamų failų =="
cp kilo.jsonc "kilo.jsonc.bak_${TS}"
cp .kilo/kilo.jsonc ".kilo/kilo.jsonc.bak_${TS}"
cp .env ".env.bak_${TS}"

echo "== 2. Įrašom OPENROUTER_API_KEY į .env (jei dar nėra) =="
if ! grep -q "^OPENROUTER_API_KEY=" .env; then
  echo "OPENROUTER_API_KEY=PASTE_YOUR_NEW_ROTATED_KEY_HERE" >> .env
  echo "  -> pridėta eilutė .env faile. UŽPILDYK TIKRĄ RAKTĄ RANKA."
else
  echo "  -> OPENROUTER_API_KEY jau yra .env, praleidžiu."
fi

echo "== 3. Rašom sujungtą .kilo/kilo.jsonc (šis failas turi prioritetą) =="
cat > .kilo/kilo.jsonc << 'EOF'
{
  "$schema": "https://app.kilo.ai/config.json",

  "instructions": [
    "AGENTS.md",
    ".clinerules/FROZEN.md",
    ".clinerules/UI_STANDARD.md",
    ".clinerules/DESIGN_SYSTEM.md",
    ".clinerules/PATTERNS.md",
    ".clinerules/nordicbees-standards.md",
    ".clinerules/rules.md"
  ],

  "provider": {
    "openrouter": {
      "name": "OpenRouter",
      "options": {
        "baseURL": "https://openrouter.ai/api/v1",
        "apiKey": "{env:OPENROUTER_API_KEY}"
      },
      "models": {
        "qwen/qwen3-235b-a22b-thinking-2507": {
          "name": "Qwen3 235B Thinking",
          "tool_call": true,
          "limit": { "context": 131072, "output": 16384 }
        }
      }
    },
    "qwen27b": {
      "name": "Qwen3.6-27B Q6K",
      "options": { "baseURL": "http://100.110.26.80:8086/v1", "apiKey": "local" },
      "models": {
        "qwen27b": {
          "name": "Qwen3.6-27B Q6K 98K",
          "tool_call": true,
          "limit": { "context": 98304, "output": 8192 }
        }
      }
    },
    "local-35b-debug": {
      "name": "Qwen3.6-35B-A3B (Debug)",
      "options": { "baseURL": "http://100.110.26.80:8087/v1", "apiKey": "local" },
      "models": {
        "qwen3-a3b-debug": {
          "name": "Qwen3.6-35B-A3B 65K",
          "tool_call": true,
          "limit": { "context": 65536, "output": 8192 }
        }
      }
    },
    "local-35b-review": {
      "name": "Qwen3.6-35B-A3B (Reviewer)",
      "options": { "baseURL": "http://100.110.26.80:8088/v1", "apiKey": "local" },
      "models": {
        "qwen3-a3b-review": {
          "name": "Qwen3.6-35B-A3B 65K",
          "tool_call": true,
          "limit": { "context": 65536, "output": 8192 }
        }
      }
    }
  },

  "model": "qwen27b/qwen27b",

  "agent": {
    "plan": {
      "model": "qwen/qwen3-235b-a22b-thinking-2507",
      "prompt": "{file:./.kilo/prompts/plan.md}",
      "steps": 500,
      "permission": {
        "edit": "allow",
        "bash": { "*": "allow" },
        "task": {
          "*": "deny",
          "code": "allow",
          "debug": "allow",
          "reviewer": "allow"
        }
      }
    },
    "code": {
      "mode": "all",
      "model": "qwen27b/qwen27b",
      "prompt": "{file:./.kilo/prompts/code.md}",
      "temperature": 0.1,
      "steps": 40
    },
    "debug": {
      "mode": "all",
      "model": "local-35b-debug/qwen3-a3b-debug",
      "prompt": "{file:./.kilo/prompts/debug.md}",
      "steps": 25
    }
  },

  "permission": {
    "bash": {
      "*": "ask",
      "dotnet build*": "allow",
      "dotnet ef*": "allow",
      "dotnet *": "allow",
      "./bump-version.sh*": "allow",
      "./build-errors.sh": "allow",
      "git *": "allow",
      "tail *": "allow",
      "grep *": "allow",
      "ls *": "allow",
      "echo *": "allow",
      "cat *": "allow",
      "head *": "allow",
      "mysql -u *": "allow",
      "cp *": "allow",
      "mysql --version *": "allow",
      "curl *": "allow",
      "lsof *": "allow",
      "sed *": "allow",
      "od *": "allow",
      "wc *": "allow",
      "sort *": "allow",
      "find *": "allow",
      "ilspycmd *": "allow",
      "rg *": "allow",
      "mkdir *": "allow",
      "read *": "allow",
      "cut *": "allow",
      "awk *": "allow",
      "tr *": "allow"
    },
    "external_directory": {
      "/Users/deividasru/Projects/NordicBeesERP/.kilo/plans/*": "allow",
      "/tmp/*": "allow",
      "/tmp/ApiCheck/*": "allow"
    },
    "glob":      "allow",
    "grep":      "allow",
    "list":      "allow",
    "task":      "allow",
    "skill":     "allow",
    "lsp":       "allow",
    "todoread":  "allow",
    "todowrite": "allow",
    "websearch": "allow",
    "webfetch":  "allow",
    "read":      "allow",
    "edit":      "allow"
  },

  "indexing": {
    "provider": "kilo",
    "model":    "mistralai/mistral-embed-2312",
    "enabled":  true
  },

  "experimental": {
    "codebase_search":       true,
    "native_notebook_tools": true
  }
}
EOF

echo "== 4. Pašalinam pasenusį šaknies kilo.jsonc, kad neliktų dviprasmybės =="
rm kilo.jsonc

echo "== 5. Pridedam kilo.jsonc.bak_* į .gitignore, kad backup'ai su senu raktu neitų į git =="
if ! grep -q "kilo.jsonc.bak_" .gitignore; then
  echo "kilo.jsonc.bak_*" >> .gitignore
  echo ".kilo/kilo.jsonc.bak_*" >> .gitignore
  echo ".env.bak_*" >> .gitignore
fi

echo ""
echo "GATA. Liko rankinių žingsnių:"
echo "  1. OpenRouter panelėje ROTUOK seną raktą (sk-or-v1-331b2f84...) - jis buvo plaintext ir tikriausiai jau commit'intas."
echo "  2. Įrašyk naują raktą į .env: OPENROUTER_API_KEY=<naujas raktas>"
echo "  3. Peržiūrėk .kilo/kilo.jsonc 'permission.bash' bloką - dabar '*':'ask' vietoj auto-allow, patikrink ar tinka darbo tempui."
echo "  4. Jei kilo.jsonc jau buvo commit'intas su raktu -> apsvarstyk 'git filter-repo' istorijos valymui, ne tik naują commit."
