# Bitininkystės/medaus interneto monitoringo sistema — MVP specifikacija

**Data:** 2026-06-23
**Tikslas:** uždaryti "praleidau svarbų reglamentą/naujieną" skylę, su realistiniu, ribotos apimties pirmu žingsniu.

---

## 0. Apimties ribos (sąmoningai NETraukiame į MVP)

- Auto-discovery / Common Crawl plėtros — atidėta į V2
- Realaus laiko monitoringas — 2x/savaitę pakanka
- Pilnas horizontaliųjų ES reglamentų aptikimas — stebime tik žinomus punktus
- Concept-level daugiakalbis žodyno tinklas — paprastas vienas-terminas-vienas-vertimas modelis

---

## 1. Šaltinių sąrašai (40-60, kuratoriuojami rankiniu būdu)

### 1a. Reglamentai/teisė (~15-20 šaltinių)
- EUR-Lex paieška pagal CN kodą 0409 (medus) + raktažodžiai "honey", "apiculture"
- LT, PL, LV, EE žemės ūkio ministerijų naujienų skiltys
- 2-3 maisto teisės firmų biuleteniai (jau filtruoja sudėtingus horizontalius dokumentus)
- VMVT ir analogiškų institucijų PL/LV/EE naujienos

### 1b. Rinka/naujienos (~25-40 šaltinių)
- Esami 6 RSS: honeybeesuite.com, beekeepinginsider.com, scientificbeekeeping.com, americanbeejournal.com, beeaware.org.au, ebaeurope.eu
- Feedspot "Top Beekeeping Blogs" sąrašo papildomi 20-30 šaltinių
- Žinomi kainų portalai iš Hermes POC: swiatmiodu.pl, ss.lv, mesinikeliit.ee, ir t.t.
- 2-3 pramonės asociacijų svetainės (Apimondia, European Honey Bee Network)

**Formatas:** vienas `source_registry.csv`/lentelė su laukais: URL, šalis, kalba, tipas (RSS/scrape), kategorija (reglamentas/rinka), pridėjimo data.

---

## 2. Šaltinių registras (MariaDB, lakstena-dev)

```sql
CREATE TABLE source_registry (
  id INT AUTO_INCREMENT PRIMARY KEY,
  url VARCHAR(500) NOT NULL UNIQUE,
  source_type ENUM('rss','scrape') NOT NULL,
  category ENUM('reglamentas','rinka') NOT NULL,
  country VARCHAR(5),
  language_code VARCHAR(5),
  added_at DATETIME DEFAULT NOW(),
  last_status ENUM('ok','blocked','404','timeout','unknown') DEFAULT 'unknown',
  last_checked_at DATETIME NULL,
  consecutive_failures INT DEFAULT 0
);
```

---

## 3. n8n crawler workflow (lakstena-dev)

- **Trigeris:** cron 2x/savaitę (pirmadienis + ketvirtadienis, 08:00)
- **Žingsniai kiekvienam šaltiniui registre:**
  1. RSS parse arba Firecrawl scrape (priklausomai nuo `source_type`)
  2. Klaidos atveju → žr. p. 9 (klaidų valdymas), nestabdo viso workflow
  3. Sėkmės atveju → atnaujina `source_registry.last_status='ok'`, `consecutive_failures=0`
- **Talpinimas:** lakstena-dev, tas pats n8n instance, kuriame jau (arba bus) bitininkystės RSS workflow

---

## 4. Dedup logika

```sql
CREATE TABLE seen_content (
  id INT AUTO_INCREMENT PRIMARY KEY,
  source_id INT NOT NULL,
  content_hash VARCHAR(64) NOT NULL,  -- SHA256 first 2000 chars
  url VARCHAR(500),
  seen_at DATETIME DEFAULT NOW(),
  UNIQUE KEY (source_id, content_hash)
);
```

Prieš LLM filtravimą: apskaičiuoti SHA256 iš straipsnio pirmų 2000 simbolių + patikrinti, ar jau egzistuoja `seen_content`. Jei taip — praleisti, nesiųsti į LLM (taupo LLM PC resursus ir Firecrawl kreditus).

---

## 5. Šaltinių sveikatos stebėjimas

- Po kiekvieno crawl ciklo: jei šaltinis grąžina klaidą **3 kartus iš eilės** (`consecutive_failures >= 3`), `last_status` keičiamas į atitinkamą kodą (`blocked`/`404`/`timeout`)
- Dashboard rodo šaltinių sąrašą su statusu — leidžia per 2-3 savaites pamatyti, kurie iš 40-60 šaltinių realiai veikia
- **Be automatinio pašalinimo iš sąrašo** MVP etape — tu rankiniu būdu sprendi, ką daryti su mirusiu šaltiniu (pakeisti URL, pašalinti, ignoruoti)

---

## 6. LLM filtras — prompt/schema specifikacija

**Modelis:** Qwen3.6-35B-A3B Q4 (LLM PC, esamas endpoint 8080/8082)

**System prompt apima:**
- Esamą patvirtintą žodyno kontekstą (žr. p. 7) — terminai su `status='confirmed'`
- Instrukciją: "tu NEGALI atsakyti remdamasis žiniomis, vien tik iš pateikto teksto"

**Grąžinamas JSON formatas (vienam straipsniui):**
```json
{
  "relevant": true,
  "category": "reglamentas|kaina|liga|technika|rinka|kita",
  "summary_lt": "viena eilutė lietuviškai",
  "source_lang": "pl",
  "new_terms": [
    {"term": "miód wielokwiatowy", "context": "sakinys, kur rastas", "guessed_translation": "daugiažiedis medus"}
  ],
  "confidence": "high|medium|low"
}
```

**Validacija:** n8n tikrina, ar JSON parse'inasi; jei ne — 1 retry su paprastesniu prompt'u, jei vėl ne — žymima kaip `llm_parse_failed`, praleidžiama (žr. p. 9).

---

## 7. Žodyno lentelė (paprastas modelis)

```sql
CREATE TABLE glossary_terms (
  id INT AUTO_INCREMENT PRIMARY KEY,
  term_original VARCHAR(255) NOT NULL,
  language_code VARCHAR(5) NOT NULL,
  term_lt VARCHAR(255),
  category VARCHAR(50),
  status ENUM('confirmed','needs_review') DEFAULT 'needs_review',
  source_context TEXT,
  first_seen_at DATETIME DEFAULT NOW(),
  confirmed_at DATETIME NULL,
  UNIQUE KEY (term_original, language_code)
);
```

**Kalbos nuo pradžių:** LT, PL, DE, EN (pirminės), LV, ET, RO, BG (antrinės, pagal esamus kainų šaltinius).

**Ciklas:** LLM aptinka naują/neaiškų terminą → įrašo su `needs_review` → dashboard rodo eilę → tu patvirtini/pataisai → `status='confirmed'` → terminas patenka į kitą LLM užklausos kontekstą.

---

## 8. Dashboard (lakstena-dev, Nginx, statinis HTML + JSON)

**Trys tab'ai vienoje HTML faile:**
1. **Kainos** — Chart.js grafikai (jau planuota anksčiau)
2. **Naujienos** — sąrašas, filtruojamas pagal kategoriją (reglamentas/rinka), rūšiuojamas pagal datą
3. **Žodynas** — `needs_review` eilė su patvirtinimo mygtukais + šaltinių sveikatos lentelė (p. 5)

**Duomenų failai:** `prices.json`, `news.json`, `glossary_review.json` — visi `/var/www/honeymark-dashboard/`

---

## 9. Klaidų valdymas

| Klaidos tipas | Veiksmas |
|---|---|
| Šaltinis neatsako/timeout | Žymima `source_registry`, praleidžiama, workflow tęsiasi |
| Firecrawl API klaida | Retry 1x po 30s, jei vėl klaida — praleidžiama |
| LLM PC nepasiekiamas | Visas ciklas sustabdomas, n8n siunčia **vieną** pranešimą (pvz. el. paštu ar n8n built-in error workflow) — tai vienintelis "alert" MVP'e, nes tai reiškia visa sistema sustojo, ne tik vienas šaltinis |
| LLM grąžina blogą JSON | 1 retry, tada žymima `llm_parse_failed`, praleidžiama |

**Principas:** vieno šaltinio nesėkmė niekada nesustabdo viso ciklo. Sustoja tik jei pati LLM infrastruktūra nepasiekiama.

---

## 10. Sėkmės kriterijus (kaip žinosi, kad veikia)

Po **2 savaičių** (4 crawl ciklai) patikrinti:
- [ ] Bent **1 reglamentinė/rinkos naujiena**, kurios anksčiau nebūtum pastebėjęs organiškai
- [ ] Klaidingai kategorizuotų straipsnių dalis **< 20%** (rankinis patikrinimas 10-15 atsitiktinių įrašų)
- [ ] Bent **70% šaltinių** turi `last_status='ok'` (jei mažiau — šaltinių sąrašo kokybė prasta, reikia peržiūrėti)
- [ ] Žodyno `needs_review` eilė **nepasiekia >50 įrašų per savaitę** (jei pasiekia — LLM per dažnai neatpažįsta terminų, reikia papildyti pradinį žodyną)

Jei šie 4 kriterijai įvykdyti — MVP pagrįstas, galima plėsti (V2: auto-discovery, daugiau šaltinių, dažnesnis ciklas).

---

## Įgyvendinimo eilės tvarka (savaitės planas)

1. **1-2 diena:** šaltinių sąrašo sudarymas (p. 1) + `source_registry` lentelė (p. 2)
2. **3 diena:** n8n crawler (p. 3) + dedup (p. 4) + sveikatos stebėjimas (p. 5)
3. **4 diena:** LLM prompt/schema (p. 6) + žodyno lentelė (p. 7)
4. **5-6 diena:** dashboard (p. 8) + klaidų valdymas (p. 9)
5. **7 diena + 2 savaitės:** stebėjimas pagal sėkmės kriterijus (p. 10)
