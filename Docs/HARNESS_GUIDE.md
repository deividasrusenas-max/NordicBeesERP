# NordicBeesERP harneso vadovas

Sukurta 2026-09-09. Paskirtis — vienoje vietoje aprašyti, **kas yra harnesas,
kaip jis veikia ir kaip juo dirbti**.

Kaip šis failas santykiauja su kitais `Docs/` failais:

- `HARNESS_STATUS.md` — chronologinis incidentų ir sprendimų žurnalas (§0–§15a).
  Ten yra „kodėl", su datomis. Šis failas — „kas ir kaip", be istorijos.
- `BUGLOG.md` — kiekvieno bug'o postmortem su `Error class` ir `Status`.
- `QUALITY_PLAN.md` — eval-driven kokybės plano metodologija.
- `AGENTS.md` — taisyklės, kurias skaito pats orchestratorius (ne subagentai).

Jei šis failas prieštarauja `opencode.json` arba prompt failams — teisingi jie,
o šis failas pasenęs. Tikrink realų failą, ne šitą aprašą.

---

## 1. Kas tai per sistema

Daugiaagentis AI kodavimo harnesas ant OpenCode (v1.18.30), veikiantis
MacBook Air, su lokaliais modeliais namų GPU serveryje `local-llm`
(4× RTX 3090) per Tailscale.

Pagrindinė idėja: orchestratorius pats nerašo kodo. Jis suskaido užduotį,
deleguoja specializuotiems subagentams ir tikrina rezultatą. Visa ceremonija
egzistuoja tam, kad kompensuotų silpnesnius lokalius modelius — su stipriu
modeliu jos didelės dalies nereikėtų.

---

## 2. Agentai

Apibrėžti `opencode.json` `agent` sekcijoje. Kiekvieno promptas —
`.opencode/prompts/<vardas>.md`.

| Agentas | Modelis | Ką gali | Ko negali |
|---|---|---|---|
| `orchestrator` | `opencode/big-pickle` (cloud) | deleguoti, bash, skaityti, planuoti | rašyti kodą; `edit` leidžiamas tik `.opencode/planning/`, `.opencode/reports/`, `Docs/BUGLOG.md`, `.agent-guardrails/evidence/` |
| `coder` | `llama-swap/coder` | `edit` | bash, grep, glob, list — nieko iš to neturi |
| `fixer` | `llama-swap/fixer` | bash, edit, DB | `mariadb`/`mysql` komandos uždraustos |
| `reviewer` | `llama-swap/reviewer` | read-only git (`diff`, `show`, `status`, `log`), grep, find | edit, write |
| `verifier` | `llama-swap/coder` | Playwright, read | beveik visas bash |
| `visual-qa` | `llama-swap/vl-ocr` | read | viskas kita |
| `design-review` | `llama-swap/vl-ocr` | read | viskas kita |

Svarbu suprasti: `coder` **neturi bash**. Tai sąmoninga — buvo incidentas, kai
jam duotas taskas, reikalaujantis `dotnet test`, ir jis 45 min sukosi ieškodamas
apėjimų. Jei taskas reikalauja komandų vykdymo, jis eina fixer'iui.

Temperatūros: coder 0.25, fixer 0.1, reviewer 0.1, verifier 0.1.

---

## 3. Modeliai ir serveris

llama-swap proxy: `http://100.110.26.80:9292/v1`

| Modelis | Kas | Konteksto limitas `opencode.json` |
|---|---|---|
| `coder` | Qwen3.8-27B dense | 262144 |
| `fixer` | Qwen3.6-35B-A3B MoE | 65536 |
| `reviewer` | Qwen3.6-35B-A3B | 61440 |
| `vl-ocr` | Qwen2.5-VL-7B | 16384 |

Papildomi provideriai: `openrouter`, `qwen27b` (:8086), `local-35b-debug`
(:8087), `local-35b-review` (:8088), `qwen-vl` (:8089).

**Kritinė taisyklė:** `opencode.json` deklaruojamas `limit.context` turi
sutapti su realiu `--ctx-size` serveryje. Jei nesutampa, OpenCode kompaktuoja
prieš neteisingą lubą, o serverio pusėje padidintas kontekstas neturi jokio
efekto. Tai realiai įvyko 2026-08-23/24 ir kainavo parą.

GPU config: `/home/asus/AI/llama-swap-config.yaml` serveryje `local-llm`.
**Šis failas nėra git'e** — žinoma spraga, backup dar nepadarytas.

---

## 4. Pluginai

Visi `.opencode/plugin/`, TypeScript.

- **`nordicbees-skill-inject.ts`** — deterministiškai įkiša skill'ų turinį į
  delegavimo tekstą pagal 9 regex taisykles. Egzistuoja todėl, kad modelis pats
  nepatikimai kviečiasi `skill` tool'ą. Turi idempotency guard'ą (2026-09-06).
  `ALWAYS_FOR_AGENT`: fixer → `git-workflow-nordicbees`; reviewer →
  `git-workflow-nordicbees` + `llm-code-quality-gate`; coder → nieko.
- **`nordicbees-circuit-breaker.ts`** — abortina subagent sesiją, kai matomas
  same-tool-streak 8 arba identical-args-streak 3. Veikia tik subagentams
  (`parentID` yra), orchestratoriaus sesija niekada neabortinama.
- **`nordicbees-quality-monitor.ts`** — rašo `task-stats.jsonl`: agentas,
  modelis, `duration_sec`, `prompt_chars`, `n_toolcalls`, reviewer verdiktas,
  fixer `guardrail_score`. Turi disko pagrindu veikiantį interruption
  detection'ą (10 min be „completed" → retroaktyvus `interrupted`).
- **`nordicbees-orchestrator-timing.ts`** — NAUJAS (2026-09-09). Rašo
  `orchestrator-timing.jsonl`: kiekvieno orchestratoriaus tool call'o
  `duration_ms` ir `gap_before_ms`, o `task` call'ams dar `prompt_chars` ir
  `skills_injected`. Skirtas išsiaiškinti, kur dingsta laikas tarp delegavimų.
- **`nordicbees-verify.ts`** — build-check hook plius fixer claim-vs-git-reality
  patikros (ar cituojamas commit hash realus, ar deklaruotas mechanizmas tikrai
  yra diff'e). Anotuoja, neblokuoja.
- **`nordicbees-reminder.ts`** — kompensuoja silpną modelių dėmesį prompto
  viduriui. Ne dublikatas, nenaikinti.
- **`nordicbees-mempalace-sync.ts`** — mempalace indeksavimas.

Išorinis plugin'as: `opencode-auto-resume@1.1.3`, pinned. **Bet kuris naujas
plugin'as PRIVALO turėti eksplicitinį `@version`** — nepinintas `@latest` jau
kartą sukėlė realų incidentą (2026-08-24).

⚠️ Žinoma problema: log'e matoma `failed to load plugin opencode-auto-resume@1.1.3
— todos.filter is not a function`. Neištirta.

---

## 5. MCP serveriai

Įjungti: `semgrep`, `roslyn` (sharplens-mcp), `mudblazor`, `microsoft-docs`,
`mempalace`, `nordicbees-db`, `playwright`.
Išjungtas: `agent-guardrails` (`enabled: false`).

Playwright: `--headless`, `--allowed-origins http://localhost:5081`. Pagal
politiką paleidžiamas **tik** kai eksplicitiškai prašai naršyklės verifikacijos.

---

## 6. Kaip veikia vienas taskas

1. Duodi orchestratoriui vieną didelį taską — nereikia pačiam skaidyti.
2. Orchestratorius daro `todowrite` dekompoziciją: vienas todo per failą, o jei
   viename faile daugiau nei 3 skirtingi pakeitimai — priverstinis skaidymas į
   kelis raundus.
3. `coder` redaguoja → `reviewer` duoda APPROVED/REJECTED → jei REJECTED,
   grįžta **per `coder`**, niekada tiesiai į `fixer`.
4. `fixer` bėga savo 12 žingsnių seką ir commit'ina.
5. Orchestratorius parašo ataskaitą į `.opencode/reports/`.

Fixer'io seka po 2026-09-09 pakeitimų:
build → minimalus taisymas → git status → git add → grep BUCKET_GROUP staged
diff'e → grep FindAsync/SaveChangesAsync staged diff'e → **dotnet test** →
commit → git log patvirtinimas → **bump-version (tik jei eksplicitiškai
paprašyta)** → agent-guardrails check.

---

## 7. Versijos bump'as — pasikeitusi taisyklė

**Bump'as nebėra automatinis kiekvieno commit'o žingsnis.**

`./bump-version.sh` bėga tik tada, kai delegavimo instrukcija jį eksplicitiškai
įvardija kaip finalinį žingsnį. Jei neįvardijai — fixer commit'ina ir sustoja.

Priežastis: `premature-version-bump-mid-task` klaidų klasė, du atvejai per
vieną dieną (v0.17.58, v0.17.65) — versija skelbė feature'ą baigtu, kai dalis
jo dar nebuvo sukommitinta.

Blokuojantis mechaninis vartas buvo apsvarstytas ir **atmestas**: jis būtų
uždėjęs rankinį žingsnį kiekvienam teisėtam release'ui dėl problemos, kurios
reali žala buvo kosmetinė.

`bump-version.sh` savo GATE'us (build, testai, anti-pattern grep) išlaiko kaip
release-time backstop. Dubliavimas su fixer'io žingsniais yra sąmoningas —
**nededuplikuoti**.

---

## 8. Kaip dirbti — praktika

Paleidimas:

```bash
eval "$(centurio use nordicbeeserp --eval)"
cd /Users/deividasru/Projects/NordicBeesERP
opencode
```

Su log'ais (kai kažką tikrini):

```bash
opencode run --agent orchestrator --print-logs --log-level DEBUG "tavo taskas"
```

**Prieš pradedant harneso darbą — visada:**

```bash
pgrep -a opencode
```

Fone kabanti sesija tame pačiame kataloge yra reali rizika: 2026-09-09 rasta
sesija, kabėjusi 5 paras, ir yra dokumentuotų atvejų, kai `Docs/` turinys
tyliai dingo dėl lygiagretaus proceso.

**Niekada neredaguok harneso failų, kol prieš juos bėga realus taskas.**

Diagnostika:

```bash
tail -20 .opencode/reports/task-stats.jsonl
tail -20 .opencode/reports/orchestrator-timing.jsonl
tail -20 .opencode/reports/circuit-breaker.jsonl
```

Po bet kokio commit'o, liečiančio `.opencode/`:

```bash
git show --stat <hash>
```

`.opencode/plugin/` ir `.opencode/prompts/` jau buvo tyliai praleisti git'o —
visada patvirtink, kad failas realiai pateko į commit'ą.

---

## 9. Gynybos lygiai

Trijų lygių modelis, pagal patikimumą:

- **Tier 1** — deterministinis: semgrep, pluginas, mechaninis patikrinimas.
  Patikimiausia.
- **Tier 2** — reviewer LLM checklist.
- **Tier 3** — prompt tekstas. Silpniausia.

Praktinė taisyklė, išvesta iš realios patirties:

- Naujas incidentas, pirmas kartas → Tier 3 užtenka.
- Ta pati klasė pasikartojo → tekstas neveikia. Kelk į Tier 1/2 **ir tekstą
  išimk**, ne palik šalia.
- Prompt tekstas ypač blogai veikia **draudimams**. Orchestratorius du kartus
  bandė editinti, nors buvo aiškiai uždrausta; bump'as bėgo, nors buvo liepta
  laikyti. Draudimams reikia mechanizmo, ne sakinio.

Būtent dėl to, kad senas tekstas lieka po mechanizmo pridėjimo, `AGENTS.md` +
7 prompt failai išaugo iki ~105 KB ir dedublikacijos pravažiavimas jų
praktiškai nesumažino.

---

## 10. Kas padaryta 2026-09-09

| Commit | Kas |
|---|---|
| `3686bb1` | Testai ir FindAsync/SaveChangesAsync grep'as atkabinti nuo bump'o, dabar bėga kiekvienam commit'ui prieš commit'inant. Plius `bump-version.sh` GATE 2 komentaras nukreiptas į gyvą kelią. |
| `fa0f024` | Bump'as padarytas sąlyginiu (`fixer.md` + `orchestrator.md`). |
| `73d98d1` | Abu `premature-version-bump-mid-task` BUGLOG įrašai atnaujinti. Status paliktas `escalated`. |
| `1022431` | Pašalintos mirusios `.clinerules/` ir `.kilo/` nuorodos. |
| `ae17237` | Naujas `nordicbees-orchestrator-timing.ts`. |
| `4bec053` | `orchestrator` gavo eksplicitinį `model: opencode/big-pickle` — iki tol lauko nebuvo visai ir jis krito į default'ą. |

Anksčiau tą pačią dieną: `nordicbees-skill-inject.ts` regex susiaurintas
(`questpdf` nebe nuo bet kokio „PDF", `verify-before-done` nebe nuo `form`/
`button`, `crud-completeness` nebe nuo `new field`, `efcore-performance` nebe
nuo `slow`/`optimize`), plius ištaisytas `\bService\.cs\b` under-match bug'as —
jis niekada nesutapdavo su realiais `XxxService.cs` failais, todėl
`dotnet-efcore-nordicbees` skill'as beveik niekada nesuveikdavo.

Taip pat nužudyta 5 paras kabėjusi OpenCode sesija (pid 85573).

---

## 11. Kas atvira

- **Reviewer'io besąlygiškos injekcijos.** Pamatuota: trivialus „suskaičiuok
  eilutes" taskas gavo 16 368 simbolių promptą ir užtruko 56s. `ALWAYS_FOR_AGENT`
  neturi jokios sąlygos. Reikia duomenų iš kelių dienų prieš sprendžiant.
- **Cloud orchestratoriaus kaina.** Pamatuota: ~20s vienam žingsniui prieš
  ~1,7s lokaliai. Ar geresnis delegavimas tai atperka — parodys duomenys.
- **`opencode-auto-resume` load error** — neištirta.
- **`llama-swap-config.yaml` backup** į repo — nepadaryta.
- **`.githooks/pre-commit`** ištaisytas, bet išjungtas (`core.hooksPath`
  nenustatytas). Prieš įjungiant reikia sutriažuoti realius
  `nordicbees-notracking-savechanges` hit'us `Tests/*.cs`.
- **mempalace room config** — įtariami stale `bin/`/`obj/` įrašai, netikrinta.
- **`AGENTS.md` role-relevance trim** — laukia sprendimo dėl granuliarumo.
- **Performance puslapis** — vizualizacija virš trijų JSONL failų. Po duomenų
  kaupimo.
- **`Docs/PROJECT_STATE.md`** — 7 savaičių senumo šablonas su placeholder
  tekstu, o orchestratorius jį skaito kaip būsenos šaltinį.
- **§8 coder+fixer merge** — sąmoningoje pauzėje, laukia baseline statistikos.
