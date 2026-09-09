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

**Leidimų numatytoji reikšmė yra `allow`.** Jei laukas neišvardintas agento
`permission` bloke — jis leidžiamas. Dėl to `reviewer` techniškai turi `write`
teisę, nors jo promptas sako „read-only auditor", o `glob`/`grep`/`list`
leidžiami ir jam, ir orchestratoriui. Uždrausti reikia eksplicitiškai.

**Orchestratorius neturi `write`** — jo realus mechanizmas yra scoped `edit`.
Patikrinta: `edit` su tuščiu `oldString` sukuria naują failą, tai jis gali
rašyti į `.opencode/reports/` nepaisant `write: deny`.

### Ataskaitų taisyklė

Ataskaitos failą rašo tas, kas turi `edit`/`write` teisę `.opencode/reports/` —
praktikoje orchestratorius. Agentas be tos teisės grąžina ataskaitą **tekstu**
savo atsakyme, ir tai yra atitikimas taisyklei, ne atsarginis variantas.

Ankstesnė `AGENTS.md` formuluotė reikalavo rašyti failą „be išimčių" ir
eksplicitiškai draudė tekstinį variantą — o `verifier`, `visual-qa` ir
`design-review` neturi jokio rašymo įrankio. 2026-09-09 tai baigėsi tuo, kad
`verifier` bandė POST'inti ataskaitą į penkis spėliotus HTTP endpoint'us per
`playwright_browser_run_code_unsafe`. Circuit-breaker'is jį sustabdė.

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

### FULL PATH (numatytasis)

1. Duodi orchestratoriui vieną didelį taską — nereikia pačiam skaidyti.
2. Orchestratorius daro `todowrite` dekompoziciją: vienas todo per failą, o jei
   viename faile daugiau nei 3 skirtingi pakeitimai — priverstinis skaidymas į
   kelis raundus.
3. `coder` redaguoja → `reviewer` duoda APPROVED/REJECTED → jei REJECTED,
   grįžta **per `coder`**, niekada tiesiai į `fixer`.
4. `fixer` bėga savo 13 žingsnių seką, commit'ina ir push'ina.
5. Orchestratorius parašo ataskaitą į `.opencode/reports/`.

Fixer'io seka po 2026-09-09 pakeitimų (13 žingsnių):
build → minimalus taisymas → git status → git add → grep BUCKET_GROUP staged
diff'e → grep FindAsync/SaveChangesAsync staged diff'e → **dotnet test** →
commit → git log patvirtinimas → **git push** → **bump-version (tik jei
eksplicitiškai paprašyta)** → agent-guardrails check.

`git push` ir bump'as yra **nepriklausomi** žingsniai. Push vyksta kiekvienam
commit'ui; bump — tik kai delegavimas jį įvardija. Push nesėkmė (rejected, no
upstream, network) — BLOCKED ir stop, jokio pull/rebase/merge/retry.

⚠️ Push į `main` trigerina staging deploy per `.github/workflows/deploy.yml`
(stebi `main` ir `production`). Tai sąmoningas sprendimas — staging'u naudojasi
tik Deividas kaip darbiniu stendu prieš keliant į `production`.

### FAST PATH

`orchestrator.md` turi „Task complexity triage" sekciją: coder → fixer,
praleidžiant reviewer. Leidžiama tik kai **visos** sąlygos tenkinamos:

- vienas failas, tikrai mažas pakeitimas (konstanta, akivaizdi rašybos klaida,
  null check nukopijuotas iš gretimo identiško šablono)
- jokio naujo ar pakeisto metodo, jokios verslo logikos
- neliečia DB rašymo kelio (`ExecuteSqlRawAsync`/`FindAsync`/`SaveChangesAsync`)
- **nėra naujo ar pakeisto vartotojui matomo teksto** — tik reviewer tikrina
  lietuvių/anglų teksto rišlumą, o 2026-08-24 į produkciją nuėjo hallucinuotas
  string'as
- neliečia `Docs/FROZEN.md` saugomų zonų
- bet kokia abejonė → FULL PATH

Pasirinktas kelias fiksuojamas prie kiekvieno todo. Jei fixer negali FAST PATH
tasko išspręsti minimaliu patch'u — grąžina orchestratoriui, kuris
permaršrutuoja per FULL PATH. Antro FAST PATH bandymo tam pačiam failui nebūna.

⚠️ FAST PATH yra Tier 3 gynyba — nėra mechanizmo, kuris priverstų orchestratorių
klasifikuoti. Ar realiai naudojamas, matosi iš `orchestrator-timing.jsonl`: du
`task` įrašai (coder, fixer) = FAST PATH, trys su reviewer = FULL PATH.

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
| `60fb231` | Pirmas taisymas verifier ataskaitų problemai (išimtis verifier'iui). |
| `bbdc882` | `orchestrator-timing.ts` dabar rašo `started` įrašą iškart `task` call'ams — kabantis delegavimas nebelieka nematomas. |
| `c07d4c8` | `60fb231` perrašytas kaip teigiama taisyklė vietoj išimčių sąrašo; `orchestrator.md` gavo atsakomybę išsaugoti subagento grąžintą ataskaitą. |
| `7506a70` | `fixer.md` gavo `git push` kaip 11-ą žingsnį — bump'as nebe vienintelis, kas push'ina. Seka perskaičiuota į 1–13. |

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
- **`playwright_browser_run_code_unsafe`** — leidžia subagentui vykdyti bet kokį
  JS, įskaitant `fetch()`. `--allowed-origins` šio vektoriaus nedengia (localhost
  lieka pasiekiamas). Paliktas sąmoningai, nes reikalingas „batch known flows"
  darbo eigai.
- **Harness lint** — neparašytas. Per vieną dieną rasti penki tos pačios šeimos
  prieštaravimai (nuoroda į tai, ko nėra): `.agent-reports/`, `.clinerules/`,
  `.kilo/prompts/`, `\bService\.cs\b`, fixer žingsnių numeracija vs DONE
  apibrėžimas. Mechaniškai tikrinamos trys klasės: mirusios failo nuorodos;
  prompt'as liepia veiksmą, kurio `opencode.json` tam agentui neleidžia;
  numeruotų žingsnių spragos ir vidiniai neatitikimai.
- **Kokybės trendas nematuotas** — nėra nieko, kas atsakytų, ar po rugpjūčio
  pakeitimų aplikacijos klaidų mažiau. `BUGLOG.md` datos ir `Category` leistų
  suskaičiuoti bugų per savaitę rugpjūtį prieš rugsėjį. **2026-09-09 yra
  nulinis taškas** — tą dieną pakeistas orchestratoriaus modelis, fixer'io
  žingsniai ir nužudyta fone kabėjusi sesija; ankstesni duomenys yra kitos
  konfigūracijos matavimas.
