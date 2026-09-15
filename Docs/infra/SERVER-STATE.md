# Serverio ir konteinerių būsena — kontekstas atskirai sesijai

Sudaryta: 2026-09-15 | Serveris: `lakstena-dev` | Vartotojas: `deirsn`

Šis failas NĖRA OCR modulio dalis. Jis atsirado kaip šalutinis rezultatas, tiriant,
kodėl dingo 247 sąskaitų PDF. Skirtas atskirai sesijai apie infrastruktūrą.

**Pirmas žingsnis toje sesijoje — read-only tyrimas, ne komandos.** Šioje sesijoje
buvo daroma priešingai (žr. §4), ir tai buvo klaida.

---

## 1. Patikrinti faktai

Kiekvienas punktas patvirtintas komanda, ne prielaida.

### 1.1 Konteinerius kuria GitHub Actions, ne compose

`docker inspect nordicbees_staging --format '{{json .Config.Labels}}'` grąžino
tik `org.opencontainers.image.version`. **Jokių `com.docker.compose.*` žymų.**

`.github/workflows/deploy.yml` kuria konteinerius per `docker run -d` su `-v`
vėliavėlėmis (eilutės ~32 ir ~45).

**Išvada:** `~/nordicbees/docker-compose.yml` nieko nevaldo. Tai negyvas failas,
kuris atrodo kaip infrastruktūros konfigūracija. Spąstai — žr. §4.

### 1.2 `deploy.yml` jau teisingai atskiria staging nuo prod

```
prod:     -v /var/lib/nordicbees/artwork:/var/lib/nordicbees/artwork
          -v /var/lib/nordicbees/delivery-receipts:/var/lib/nordicbees/delivery-receipts
staging:  -v /var/lib/nordicbees/artwork-staging:/var/lib/nordicbees/artwork
          -v /var/lib/nordicbees/delivery-receipts-staging:/var/lib/nordicbees/delivery-receipts
```

Konteinerio vidinis kelias vienodas, hosto kelias skiriasi. Šablonas teisingas.

Negyvame `docker-compose.yml` staging rodė į prod kelius — tai klaidino ir
paskatino §4 klaidą.

### 1.3 Sąskaitoms volume nėra nei viename servise

`docker inspect nordicbees_prod --format '{{json .Mounts}}'` rodo tik `artwork` ir
`delivery-receipts`.

`docker exec nordicbees_prod find /app/wwwroot/uploads -name '*.pdf'` →
`No such file or directory`.

`/var/lib/nordicbees/invoices` iki 2026-09-15 **neegzistavo**.

**Pasekmė:** 247 sąskaitų PDF, įkelti 2026-07-13/14, gyveno tuometinio konteinerio
rašomajame sluoksnyje. Prod konteineris perkurtas 2026-09-11. Failai dingo su senuoju
konteineriu. `docker ps -a` rodo tik tris konteinerius, nė vieno iš liepos — atkūrimas
per `docker cp` neįmanomas.

DB stulpelis `original_file_path` užpildytas visose 247 eilutėse ir rodo į
`uploads/invoices/2026/07/...` — kelius, kurių nebėra.

### 1.4 Duomenų bazė

Prod `ConnectionStrings__DefaultConnection`:
`Server=host.docker.internal;Port=3306;Database=nordic_bees_erp;Uid=erp_user`

Staging: ta pati forma, `Database=nordic_bees_erp_staging`.

Aplikacija jungiasi prie **hosto MariaDB 11.8**, ne prie `nordicbees_mysql`
konteinerio. DB atskirtos teisingai.

### 1.5 Konteineriai sukasi kaip root

`docker exec nordicbees_prod id` → `uid=0(root) gid=0(root)`.
`/var/lib/nordicbees/artwork` ir `delivery-receipts` yra `0:0`.

### 1.6 `nordicbees_mysql` yra orphan

`docker compose up -d` įspėjo: `Found orphan containers ([nordicbees_mysql])`.
Reiškia, kad konteineris kažkada buvo aprašytas tame compose faile, o dabar nebėra.

Image `mysql:8.0`, sukurtas 2026-03-20, sukasi 3 mėnesius.
Duomenys `~/nordicbees/mysql_data`, paskutinį kartą liesti 2026-06-03.

Aplikacija prie jo nesijungia (žr. 1.4).

### 1.7 Slaptažodis plikame tekste

DB slaptažodis matomas per `docker inspect ... .Config.Env` ir guli negyvame
`docker-compose.yml`. Nė vienas iš šių failų nėra versijų kontrolėje.

`deploy.yml` (repozitorijoje) slaptažodį greičiausiai paduoda per GitHub Secrets —
**nepatikrinta**, žr. §2.

---

## 2. Nepatikrinti dalykai

Sąmoningai nepatikrinta. Netraktuoti kaip fakto.

- Ar `nordicbees_mysql` kam nors reikalingas. Kas jame yra. Ar jį galima sustabdyti.
- Ar `/var/lib/nordicbees/` patenka į atsarginių kopijų planą. Kas apskritai
  kopijuojama ir kaip dažnai.
- Kaip `delivery-receipts` modulis serveriuoja failus — ar per autentikuotą
  endpoint'ą, ar kitaip. **Tai svarbiausias nepatikrintas punktas**, nes sąskaitoms
  reikia pakartoti tą patį sprendimą.
- Kaip `deploy.yml` gauna slaptažodį (Secrets ar įrašytas failе).
- Ar `~/nordicbees/docker-compose.yml` dar kam nors naudojamas (pvz. rankiniam
  paleidimui po restart'o).
- Ar yra kitų rašymo į `wwwroot` vietų, be sąskaitų modulio.

---

## 3. Sukurta / pakeista šioje sesijoje

- `sudo mkdir -p /var/lib/nordicbees/invoices /var/lib/nordicbees/invoices-staging`
  plius `chmod 750`. **Katalogai palikti** — bus reikalingi. Šiuo metu tušti ir
  niekur neprijungti.
- `~/nordicbees/docker-compose.yml` buvo perrašytas ir **grąžintas į pradinę būseną**
  iš `.bak`. `.bak` ištrintas. Pakeitimas nieko neveikė (žr. 1.1).

Kodas ir `deploy.yml` **nepakeisti**.

---

## 4. Ką čia padariau blogai

Užrašyta, kad nepasikartotų.

Dešimt žinučių iš eilės daviau komandas po vieną, reaguodamas į kiekvieną išvestį,
be plano ir be vartų. Daviau `cat` komandą, perrašančią infrastruktūros failą,
prieš tai nepatikrinęs, kas tą failą valdo — nevaldė niekas. Tada dar pasiūliau jį
trinti. Rėmiausi negyvo compose failo turiniu ir iš jo padariau neteisingą išvadą,
kad staging rašo į prod katalogus.

**Serveriui galioja tos pačios taisyklės kaip kodui:** pirma read-only tyrimas,
tada planas, tada vykdymas su patikrinamais vartais. Šioje sesijoje to nebuvo.

---

## 5. Darbai — be sprendimų, tik sąrašas

Sprendimai priimami atskiroje sesijoje, po tyrimo.

| # | Darbas | Adresatas | Pastaba |
|---|---|---|---|
| I-1 | Sąskaitų volume į `deploy.yml` (prod + staging) | Deividas | Eina kartu su kelio pakeitimu kode; priklauso OCR projektui (F1/F2) |
| I-2 | Failų kelias kode iš `wwwroot/uploads` į `/var/lib/nordicbees/invoices` | OCR projektas | Pagal `delivery-receipts` šabloną — pirma jį ištirti (§2) |
| I-3 | Negyvas `~/nordicbees/docker-compose.yml` — trinti ar atgaivinti | Deividas | Prieš trinant patikrinti I-4 |
| I-4 | `nordicbees_mysql` orphan — reikalingas ar likutis | Deividas | `mysql_data` neliestas nuo birželio |
| I-5 | Slaptažodis iš plikų failų į `.env` / GitHub Secrets | Saulius | |
| I-6 | Konteineriai sukasi kaip root | Saulius | Per bind mount pasiekia hosto katalogus |
| I-7 | `/var/lib/nordicbees/` atsarginės kopijos | Saulius | Sąskaitos yra apskaitos dokumentai su saugojimo terminais |
| I-8 | `deploy.yml` peržiūra apskritai | Saulius / Deividas | Vienintelis realus infrastruktūros šaltinis |

---

## 6. Kaip pradėti tą sesiją

```
Perskaityk Docs/infra/SERVER-STATE.md.
Pradedam nuo read-only tyrimo: §2 nepatikrinti dalykai.
Jokių komandų, keičiančių serverio būseną, kol nėra plano.
```
