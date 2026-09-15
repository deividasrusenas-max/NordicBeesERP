# NordicBeesERP — vieningas failų saugojimo standartas

Versija: 1 (2026-09-15) | Statusas: priimtas, diegiamas
Pilotas: išlaidų sąskaitos (`expense_invoices`)

---

## 1. Kodėl

Trys moduliai, trys skirtingi sprendimai:

| Modulis | Kelias | Volume | Serveriavimas |
|---|---|---|---|
| `artwork` | `IOptions<ArtworkStorageOptions>` | taip | HTTP endpoint'ai, `.AllowAnonymous()` |
| `delivery-receipts` | užkoduotas **dviejose** vietose | taip | komponente, be URL |
| sąskaitos | `wwwroot/uploads/invoices/` | **ne** | `UseStaticFiles()`, anonimiškai |

Pasekmė: 2026-07-13/14 įkelti 247 sąskaitų PDF dingo, kai konteineris perkurtas
2026-09-11. Neatkuriami.

Priežastis nėra „kažkas pamiršo volume". Priežastis architektūrinė: **kiekvienas
naujas modulis reikalauja prisiminti pridėti volume**, ir anksčiau ar vėliau kas
nors nepridės. Sąskaitos buvo trečias modulis.

Standarto tikslas — padaryti, kad pamiršti būtų neįmanoma.

## 2. Vienas mount'as visam laikui

```
Hostas                              Konteineris
/var/lib/nordicbees/prod/     →     /var/lib/nordicbees/data
/var/lib/nordicbees/staging/  →     /var/lib/nordicbees/data
```

Viena `-v` eilutė vienam environment'ui `deploy.yml`. Daugiau niekada.

Naujas modulis kuria savo pakatalogį **runtime metu**. `deploy.yml` neliečiamas.
Tai vienintelis pakeitimas, pašalinantis visą klaidų klasę, ne vieną jos atvejį.

Konteinerio kelias abiejuose environment'uose **vienodas** — aplikacijos
konfigūracijos keisti nereikia, skiriasi tik hosto pusė.

### Struktūra

```
data/
  .nordicbees-storage          sentinel (žr. §3)
  blobs/{sha[0:2]}/{sha[2:4]}/{sha}
  manifest/YYYY-MM-DD.jsonl
  tmp/                         atominiam rašymui
```

## 3. Sentinel — apsauga nuo tylaus praradimo

Vien mount'o neužtenka: paleidus konteinerį be `-v`, aplikacija linksmai rašytų į
konteinerio sluoksnį ir viskas pasikartotų.

Deploy'o metu į hosto katalogą įrašomas žymeklis `.nordicbees-storage`.
Aplikacija starto metu tikrina, ar jis matomas per mount'ą, ir jei ne —
**atsisako startuoti**. Ne įspėjimas logu, o kritimas.

```
Startup check:
  1. /var/lib/nordicbees/data/.nordicbees-storage egzistuoja?  ne -> FATAL
  2. jo turinys atitinka laukiamą environment?                 ne -> FATAL
  3. katalogas rašomas (rašom ir trinam tmp failą)?            ne -> FATAL
```

Su šia apsauga 2026 m. liepos scenarijus būtų baigęsis neveikiančiu deploy'u, o ne
247 prarastais dokumentais. Neveikiantis deploy'as pastebimas per minutę; tylus
duomenų praradimas nepastebėtas du mėnesius.

## 4. `IFileStore` — vienintelis kelias prie disko

```csharp
Task<StoredFile> SaveAsync(Stream content, FileMetadata meta, CancellationToken ct);
Task<Stream>     OpenAsync(long fileId, CancellationToken ct);
Task<bool>       ExistsAsync(long fileId, CancellationToken ct);
Task             SoftDeleteAsync(long fileId, string reason, CancellationToken ct);
```

Visi moduliai eina per jį. Semgrep taisyklė draudžia `File.WriteAllBytes`,
`File.ReadAllBytes` ir `Path.Combine` su saugyklos keliais **bet kur už
`Services/Storage/` ribų**. Be šios taisyklės ketvirtas modulis vėl darys savaip.

Rašymas atominis: į `tmp/`, `fsync`, tada `rename` į galutinę vietą. Nutrūkęs
rašymas nepalieka dalinio blob'o.

Trynimas visada minkštas (`deleted_at`). Apskaitos dokumentų netriname.

## 5. Lentelė `files`

```
id, sha256, byte_size, mime_type, original_filename,
module, entity_type, entity_id,
created_at, created_by, deleted_at, deleted_reason
```

Moduliai laiko `file_id`, ne kelią. `expense_invoices.original_file_path` tampa
nebereikalingas (pašalinamas su data, §8).

Indeksai: `sha256`, `(module, entity_type, entity_id)`.

## 6. Turiniu adresuojamas diskas

```
data/blobs/{sha[0:2]}/{sha[2:4]}/{sha256}
```

Kodėl turiniu, o ne pavadinimu:

- **Dublikatai gaudomi automatiškai.** 25 dublikatų grupės (27 pertekliniai
  įrašai, 28 679,74 €) nebūtų susikūrusios — tas pats failas duoda tą patį sha.
- Lietuviški pavadinimai nebekelia koduotės problemų.
- Susidūrimų nebūna, laiko žymės nereikia.
- **Atsarginę kopiją galima patikrinti**: `sha256sum` prieš `files` lentelę duoda
  vientisumo įrodymą, ne tik „failai nukopijuoti".

Originalus pavadinimas gyvena `files.original_filename` ir naudojamas
atsisiunčiant.

## 7. Manifestas — privaloma dalis, ne priedas

Turiniu adresuojamas diskas be DB yra beprasmis: pamatęs `a3f9c2…` nežinai, kad
tai Venipak sąskaita. Apskaitos dokumentams galioja saugojimo terminai, todėl
priklausomybė nuo vienos DB yra reali rizika.

Kartą per parą `files` lentelė eksportuojama į `data/manifest/YYYY-MM-DD.jsonl`.
Saugykla tampa savarankiška — praradus DB, iš manifesto atkuriami pavadinimai ir
priklausomybės.

**Be manifesto ši architektūra nerekomenduojama.**

## 8. Diegimo eiliškumas

Sąskaitos yra pilotas, nes neturi legacy duomenų — viskas prarasta, todėl švarus
startas be migracijos.

Naujas `data` root'as mount'inamas **šalia** esamų `artwork` ir
`delivery-receipts` mount'ų. Jie veikia toliau, niekas nelaužoma.

| # | Žingsnis | Vartai |
|---|---|---|
| 1 | Root'as, sentinel, `IFileStore`, `files` lentelė, sąskaitos ant jų, semgrep | Įkeltas PDF išlieka po `docker rm` + `run`; startas be mount'o krenta |
| 2 | Peržiūra ir atsisiuntimas per komponentą (`delivery-receipts` šablonas) | Senas `/uploads/...` URL nebegrąžina failo |
| 3 | Manifestas, atsarginės kopijos, ketvirtinis atkūrimo bandymas | Atkūrimas iš kopijos praeina `sha256sum` patikrą |
| 4 | `delivery-receipts` backfill į `files` | Senas ir naujas kelias sutampa visiems įrašams |
| 5 | `artwork` backfill | Tas pats |
| 6 | Senų stulpelių ir kelių pašalinimas | `Docs/infra/` užfiksuota data |

`artwork` migruoja **paskutinis**, nes ten gyvi HTTP endpoint'ai.

## 9. Atsarginės kopijos

Vienas `restic` arba `rsync` darbas vienam katalogui plius DB dump'as. Blobai
nekintami, todėl inkrementinė kopija pigi.

**Ketvirtinis atkūrimo bandymas privalomas**: atkurti į laikiną katalogą, paleisti
`sha256sum` prieš `files` lentelę, patvirtinti sutapimą. Nepatikrinta kopija nėra
kopija — tai prielaida.

## 10. Redteam

- **Ar per didelis sprendimas 35 sąskaitoms per mėnesį?** Iš dalies taip. Duomenų
  praradimą išsprendžia **vien §2 ir §3**. `files` lentelė ir turinio adresavimas
  sprendžia kitą problemą — nuoseklumą, dublikatus, vientisumo tikrinimą. Minimalus
  variantas (tik root + sentinel) yra sąžininga pozicija.
- **Didžiausia žūties rizika — pusiau migruota būsena**: sąskaitos ant naujo, du
  moduliai ant seno, ir taip metus. Apsauga: semgrep taisyklė užtikrina, kad bent
  **naujas** modulis negali nueiti savo keliu, net jei seni migruoja lėtai.
- **`artwork` migracija rizikingiausia** dėl gyvų endpoint'ų. Todėl paskutinė.
- **S3 / MinIO?** Ne. Šiam mastui vietinis diskas su kopijomis teisingas, MinIO
  prideda operacinę naštą. Bet `IFileStore` yra interfeisas, kad vėliau tai būtų
  pakeitimas, ne perrašymas.
- **Ko tai neišsprendžia:** anoniminės prieigos prie `artwork` endpoint'ų ir
  neveikiančios `UseAuthorization()`. Atskira problema, lieka atvira.
- **Sentinel gali tapti kliūtimi lokaliam dev'ui.** Sprendimas: Development
  aplinkoje sentinel tikrinamas, bet sukuriamas automatiškai, jei katalogas
  rašomas. Production ir Staging — niekada.
