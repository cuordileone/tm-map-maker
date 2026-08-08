# Shape Catalog — Design v1

## Obiettivo

Dare al Path Compiler e al Validator (componenti futuri, vedi design principale)
un modo affidabile di sapere, per un blocco dato, **quante celle occupa** e
**dove si può collegare** (con quali blocchi vicini, a quale rotazione, con
quale variazione di quota). Senza questo, non è possibile né tradurre un
percorso disegnato in blocchi reali, né validare che una pista costruita a
mano sia effettivamente percorribile.

Questo spec descrive solo il **Shape Catalog**: il livello di conoscenza
geometrica. Non descrive ancora il Path Compiler o il Validator stessi
(restano piani futuri, si appoggeranno su questo).

## Intuizione chiave (verificata sui dati reali)

I nomi dei blocchi seguono lo schema `<Famiglia><Forma>` — es.
`RoadTechStraight`, `RoadDirtStraight`, `PlatformIceStraight`: famiglia
diversa, stessa `Forma` ("Straight"). La **geometria** (celle occupate,
punti di collegamento, rotazioni valide) è la stessa a parità di Forma,
indipendentemente dalla famiglia — cambia solo l'aspetto/superficie.

Conseguenza pratica: invece di dover determinare la geometria per centinaia
di combinazioni famiglia×forma, basta un catalogo di **forme** (poche decine)
riusato su tutte le famiglie già classificate dal Block Catalog Reader.

## Come NON farlo (lezione dal vecchio progetto)

Non si inferisce la geometria "a statistica" analizzando automaticamente
quali blocchi appaiono vicini nelle mappe reali e assumendo che siano
collegati — è fragile: due blocchi possono essere semplicemente vicini nello
spazio senza essere collegati sul percorso di guida (decorazioni, strutture
di supporto, sezioni parallele). Questo tipo di inferenza indiretta è
esattamente ciò che ha prodotto un tracciante inaffidabile in `analizzatore/`
(vedi design principale, sezione Rischi noti).

## Come farlo

1. **Definizione dichiarata**: per ogni Forma in scope, si dichiara a mano
   la geometria (celle occupate, porte di ingresso/uscita con direzione
   locale e delta di quota), basandosi sulle convenzioni note e fisse di
   Trackmania (griglia a celle di 32 unità, altezza 8 unità per livello).
2. **Verifica empirica obbligatoria**: ogni Forma dichiarata viene verificata
   contro sequenze reali estratte con `GbxMapReader` (già costruito e
   testato) dalle mappe di riferimento. Se la geometria dichiarata non trova
   riscontro in almeno un caso reale verificabile, la Forma resta "non
   verificata" e il sistema non la usa (stesso principio fail-loud del
   Block Catalog Reader — mai fidarsi di una definizione non controllata
   contro dati reali).
3. La verifica **non richiede un tracciante generale del percorso** (il
   problema che ha rotto `analizzatore/` su mappe con posa libera massiccia).
   Basta un controllo di adiacenza mirato: dato un blocco a griglia con
   coordinate/rotazione note, esiste un vicino nella cella e rotazione
   attesa dalla Forma dichiarata? Un controllo locale, non una ricostruzione
   globale del tracciato.

## Scope v1 — vocabolario di Forme

Set minimo per rendere percorribile una pista base (dritto, curva, salita,
partenza/arrivo/checkpoint), applicabile a tutte le famiglie di superficie
già coperte dal Block Catalog Reader:

- `Start` (una porta di uscita)
- `Finish` (una porta di ingresso)
- `Checkpoint` (pass-through, come Straight ma con marker di progresso)
- `Straight`
- `Curve1` (curva 90°, 1 cella)
- `Slope2Straight`, `Slope2Up`, `Slope2Down` (transizione di quota — il
  delta esatto di quota per cella va confermato dalla verifica empirica,
  non assunto a priori)

Tutte le altre Forme (curve a raggio più ampio, chicane, loop, diagonali,
blocchi multi-cella) restano fuori scope v1 — si aggiungono dopo, con lo
stesso procedimento (dichiarazione + verifica), quando servono per stili di
mappa più avanzati.

## Modello dati (bozza, da confermare in fase di piano)

```json
{
  "shape": "Curve1",
  "footprintCells": [{"x": 0, "y": 0, "z": 0}],
  "ports": [
    {"id": "in",  "localDirection": "South", "yOffset": 0},
    {"id": "out", "localDirection": "East",  "yOffset": 0}
  ],
  "verifiedAgainst": ["R_g Avatar.Map.Gbx"]
}
```

I valori esatti (yOffset per le Slope2, eventuali celle aggiuntive) vanno
determinati e scritti nel piano di implementazione **dopo** aver interrogato
i dati reali con GBX.NET — stesso metodo già usato per il piano precedente
(mai numeri stimati a occhio nel piano finale).

## Fuori scope v1

- Path Compiler (percorso disegnato → blocchi) — piano futuro, consuma
  questo catalogo.
- Validator (connettività/overlap su geometria reale) — piano futuro,
  consuma questo catalogo.
- Forme multi-cella, curve a raggio largo, chicane, loop, diagonali.
- Blocchi in posa libera come "sorgente" di verifica in questa fase v1: la
  verifica si appoggia su blocchi a griglia (coordinate intere, confronto
  diretto). I connettori per la posa libera arriveranno quando il Path
  Compiler dovrà effettivamente gestirla in scrittura — qui serve solo
  conoscere la geometria dei blocchi, che è la stessa a prescindere da come
  vengono poi piazzati.
