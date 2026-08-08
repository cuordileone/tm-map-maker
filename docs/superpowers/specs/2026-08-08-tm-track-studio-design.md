# TM Track Studio — Design v1

## Obiettivo

Sito web pubblico che rende la creazione di piste per Trackmania 2020 molto più
semplice e intuitiva dell'editor in-game ufficiale (percepito dall'utente come
"terribile, non intuitivo"). L'utente costruisce una pista in un editor 3D nel
browser e scarica un file `.Map.Gbx` giocabile direttamente in TM2020.

Progetto rivolto sia all'uso personale sia alla community Trackmania (sito
pubblico, con account e galleria).

## Relazione con progetti esistenti

Questo progetto è un **sostituto completo**, non un frontend sopra la pipeline
esistente in `mappa trackmania/tm2020-claude-tracks` (Python + gbx-py). Quel
pipeline aveva bug sistemici nel piazzamento blocchi (rotazioni errate, blocchi
sovrapposti, blocchi sbagliati) e non viene riusato architetturalmente.

Vengono riusate solo le mappe di riferimento in `mappa trackmania/riferimenti/`
come ground truth per validare il nuovo Block Catalog (vedi sotto).

## Scope v1

- Solo ambiente **Stadium**, ma con copertura di **tutte le famiglie di
  superficie principali**, non solo quella "veloce": `RoadTech`, `RoadDirt`,
  `RoadBump`, `RoadIce`, `RoadTechIce`, `RoadGrass`, più gli equivalenti
  `Platform*` (Tech, Dirt, Ice, Grass, Plastic) e i muri (`TrackWall`,
  `DecoWall`). Per ciascuna famiglia: dritto, curva base, salita/rampa,
  checkpoint, start/finish. Il set completo di varianti (chicane, diagonali,
  loop, ecc.) resta fuori scope v1, ma **nessuno stile di mappa deve essere
  strutturalmente escluso** — tech, dirt, ice, stunt, fullspeed devono essere
  tutti costruibili fin dal v1, anche se con un catalogo blocchi ridotto
  all'interno di ciascuna famiglia.
- Due modalità di editing disponibili fin dal v1: **disegno del percorso** e
  **posizionamento manuale a blocchi**, sullo stesso modello dati.
- **Posa libera (FREE placement) inclusa fin dal v1**, non solo blocchi a
  griglia. Verificato empiricamente (vedi sotto) che le mappe reali di
  qualità usano posa libera in modo massiccio — un motore solo-griglia
  escluderebbe di fatto gli stili tech/stunt/precision curati.
- Account utente + galleria pubblica fin dal v1 (salvataggio progetti,
  pubblicazione, elenco/categoria).
- Editor 3D reale (three.js), non 2D.

**Perché questo punto è critico**: il vecchio progetto (`tm2020-claude-tracks`)
aveva verificato solo un vocabolario di blocchi `RoadTech` (velocità/tecnico
puro) — di fatto escludeva strutturalmente dirt, ice, stunt, grass fin dalla
progettazione. Questo tool deve permettere a chiunque di costruire qualsiasi
tipo di mappa, quindi il Block Catalog Builder va puntato su mappe di
riferimento che coprano più famiglie fin dal primo giro, non solo velocità.

## Fuori scope v1 (esplicitamente rimandato)

- Altri ambienti (Canyon, Lagoon, ecc.)
- Catalogo blocchi completo (solo set base Stadium)
- Item/oggetti custom (solo blocchi standard)
- Integrazione diretta con l'editor live di TM2020 (approccio Openplanet/MCP
  valutato e scartato per v1: richiederebbe che ogni utente abbia il gioco
  aperto con un plugin installato — incompatibile con "sito pubblico, scarichi
  il file")

## Architettura

```
Frontend (Next.js + three.js/react-three-fiber) — hosted su Netlify
   editor 3D: modalità Disegno + modalità Blocchi manuali,
   stesso modello dati sotto (lista blocchi: id, x,y,z, rotazione)
        │
        │  REST API
        ▼
Backend (ASP.NET Core Web API, C#) — hosted su Railway
   - Auth utenti
   - CRUD progetti + galleria pubblica
   - Export: modello dati → .Map.Gbx via GBX.NET (BigBang1112/gbx-net)
        │
        ▼
Database (Postgres, su Railway): utenti, progetti, metadati galleria
```

Motivazione stack:
- **GBX.NET** (C#/.NET) è la libreria più matura per leggere/scrivere il
  formato Gbx (400+ classi supportate, TM2020 aggiornato), molto più
  affidabile di `gbx-py` usata nel vecchio progetto Python.
- Backend in C# permette di usare GBX.NET direttamente, senza porting.
- **Next.js** invece di semplice React+Vite: la galleria pubblica (pagine
  progetto/pista) beneficia di SSR per essere indicizzata da Google — una SPA
  pura non lo farebbe bene. Per l'editor 3D in sé non cambia nulla rispetto a
  Vite. Scelto al posto di Vercel perché tecnicamente equivalente per questo
  progetto (nessuno dei due fa girare il backend .NET) e perché Netlify è
  infrastruttura che l'utente già conosce e usa (stesso account team usato per
  altri progetti) — nessun vantaggio a introdurre un secondo provider.
- Frontend su Netlify, backend su Railway: separati perché nessuna piattaforma
  di hosting frontend (Netlify, Vercel, ecc.) fa girare nativamente un backend
  .NET persistente.

## Componenti

### Block Catalog Builder (tool offline, C# + GBX.NET)
Analizza le mappe di riferimento e produce un catalogo verificato dei blocchi
Stadium: id, dimensioni/footprint, punti di connessione, rotazioni valide.
Output: JSON versionato, non generato a runtime.

Il catalogo deve coprire almeno un dritto/curva/rampa verificati per **ogni**
famiglia di superficie in scope (Tech, Dirt, Bump, Ice, TechIce, Grass, e i
`Platform*` corrispondenti) — non solo la famiglia velocità. Le mappe attuali
in `riferimenti/` (Alpha Valley, Aram, Jeskai, Mile Zero, R_g Avatar,
[FS] Cliffhanger, [MiniFS] First, weekly5/FLOAT, weekly5/spin) non sono
garantite coprire tutte le famiglie — vanno ispezionate all'inizio
dell'implementazione, e se mancano superfici (dirt/ice/grass in particolare)
vanno aggiunte mappe di riferimento mirate prima di considerare il catalogo
completo per il v1.

**Regola fail-loud** (lezione appresa dal vecchio progetto): se un blocco non
è verificato contro una mappa di riferimento reale, il sistema si rifiuta di
usarlo — mai sostituzione silenziosa con un blocco "simile".

### Editor 3D (frontend)
Due modalità sullo stesso modello dati (lista di blocchi piazzati), non due
sistemi paralleli:
- **Disegno percorso**: l'utente disegna una linea/curva, il Path Compiler la
  traduce in blocchi collegati.
- **Blocchi manuali**: drag & drop di blocchi singoli su griglia 3D.

Rendering: modelli 3D **ricostruiti da zero** (non mesh originali Nadeo, che
sono asset proprietari non redistribuibili su un sito pubblico) — stesse
proporzioni reali (estratte dal Block Catalog), stile visivo simile
(asfalto grigio, bordi colorati) ma geometria/texture originali.

### Path Compiler
Converte il percorso disegnato in blocchi reali usando il Block Catalog,
validando ogni passo contro connessioni verificate.

### Validator (client-side, prima di salvare/esportare)
1. Connettività: ogni blocco ha un vicino compatibile — verificata su
   **geometria reale** (bounding box/connettori nello spazio), non solo
   adiacenza a cella griglia, per supportare correttamente i blocchi FREE
   (vedi nota sotto)
2. Nessuna sovrapposizione di footprint (griglia + FREE)
3. Percorso Start → Checkpoint(s) → Finish raggiungibile
4. Ogni blocco esiste nel Block Catalog verificato

**Nota tecnica critica**: un tracciante basato su adiacenza a cella griglia
(BFS su coordinate intere) **non funziona** su mappe con blocchi in posa
libera — verificato analizzando `Alpha Valley 1.Map.Gbx` (partenza FREE,
tracciante non trova nemmeno il punto di inizio) e `[FS] Cliffhanger.Map.Gbx`
(206 blocchi guidabili su ~210 sono FREE, il tracciante a griglia ne segue
solo 4). Il Validator e il Path Compiler devono lavorare su **posizione
world-space reale + connettori dei blocchi**, non su indici di cella.

### Export Service (backend)
Modello dati validato → `.Map.Gbx` reale via GBX.NET.

### Account + Galleria
Login, salvataggio progetti, galleria pubblica con categorie (tassonomia
riusata da `tm2020-claude-tracks`: speedtech, fullspeed, tech, dirt, rally,
stunts, ice, fun, lol, beginner — solo lista di categorie, non codice).

## Modello dati pista

Schema nuovo, non eredita `track_schema.json` del vecchio progetto:

```json
{
  "meta": {"name": "...", "author": "...", "category": "speedtech", "difficulty": 3},
  "environment": "Stadium",
  "blocks": [
    {"id": "RoadTechStraight", "x": 10, "y": 1, "z": 5, "rot": 0},
    {"id": "RoadTechCurve90", "x": 11, "y": 1, "z": 5, "rot": 1}
  ]
}
```

## Testing

- Block Catalog Builder testato contro le mappe note in `riferimenti/` (Alpha
  Valley, Aram, Jeskai, Mile Zero, ecc.) — se il catalogo estratto non
  riproduce esattamente blocchi/rotazioni delle mappe note, il builder fallisce.
- Round-trip test: genera pista semplice → esporta `.Map.Gbx` → rileggi con
  GBX.NET → verifica che i blocchi tornino identici.
- Test manuale in TM2020: le prime piste generate vanno aperte nel gioco vero
  per conferma visiva prima di considerare il pipeline affidabile. Questo
  passo non è automatizzabile e resta un checkpoint umano esplicito prima di
  fidarsi della pipeline in produzione.

## Note UI (non bloccanti per l'architettura core)

Attenzioni standard da applicare in fase di UI: supporto dark/light mode,
layout responsive, compatibilità cross-browser per utenti con browser/setup
meno comuni.

## Rischi noti / incertezze aperte

- L'accuratezza del Block Catalog dipende dalla qualità e copertura delle
  mappe di riferimento disponibili. Le mappe già presenti in `riferimenti/`
  non sono verificate per coprire tutte le famiglie di superficie (dirt/ice/
  grass in particolare rischiano di essere sotto-rappresentate) — primo passo
  dell'implementazione: verificarlo e raccogliere mappe mirate se mancano.
  **Strategia di reperimento decisa**: mappe mandate direttamente dall'utente
  + mappe scaricate da trackmania.exchange (mapsearch), filtrate per premi/
  numero giocatori alti (per evitare mappe troll/di bassa qualità), una
  ricerca per ciascuno stile/famiglia di superficie mancante. L'analisi delle
  mappe scaricate/ricevute usa la skill `analizza-mappe-tm` già disponibile.
- I modelli 3D ricostruiti "somiglianti ma non identici" sono una scelta di
  compromesso: leggibilità per il giocatore vs. rischio IP. Non sostituisce un
  parere legale formale se il progetto crescesse in visibilità/monetizzazione.
- Nessun test automatizzato può sostituire l'apertura reale in TM2020: il
  primo batch di piste generate va validato a mano.
- Il tool `analizzatore/` (skill `analizza-mappe-tm`) ha un tracciante basato
  su BFS a griglia che è stato verificato **inaffidabile su mappe reali con
  posa libera** (vedi nota nel Validator). È stato corretto un bug di
  classificazione (muri trattati come guidabili), ma il limite architetturale
  di fondo resta: serve un tracciante geometrico, non a griglia. Finché non
  viene riscritto, va usato solo per ispezionare dati grezzi (blocchi/
  waypoint/coordinate), non per fidarsi della sua narrazione "tracciato in
  ordine" su mappe complesse.
