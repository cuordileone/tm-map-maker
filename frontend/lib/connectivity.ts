export type ShapeType = "straight" | "curve" | "slopeUp" | "slopeDown" | "checkpoint" | "start" | "finish";

export type PlacedBlock = {
  id: string;
  cellX: number;
  cellZ: number;
  shape: ShapeType;
  rotationSteps: number; // 0-3, each step = 90 degrees clockwise
  level: number;
};

type Socket = { dx: number; dz: number; dLevel: number } | null;

// Local sockets before rotation is applied, "forward" = local +Z (matches the
// GridRotation convention used elsewhere in the project). null = no socket on
// that side (Start has no entry, Finish has no exit).
const ENTRY_SOCKET: Record<ShapeType, Socket> = {
  straight: { dx: 0, dz: -1, dLevel: 0 },
  curve: { dx: 0, dz: -1, dLevel: 0 },
  slopeUp: { dx: 0, dz: -1, dLevel: 0 },
  slopeDown: { dx: 0, dz: -1, dLevel: 0 },
  checkpoint: { dx: 0, dz: -1, dLevel: 0 },
  start: null,
  finish: { dx: 0, dz: -1, dLevel: 0 },
};

const EXIT_SOCKET: Record<ShapeType, Socket> = {
  straight: { dx: 0, dz: 1, dLevel: 0 },
  curve: { dx: 1, dz: 0, dLevel: 0 },
  slopeUp: { dx: 0, dz: 1, dLevel: 1 },
  slopeDown: { dx: 0, dz: 1, dLevel: -1 },
  checkpoint: { dx: 0, dz: 1, dLevel: 0 },
  start: { dx: 0, dz: 1, dLevel: 0 },
  finish: null,
};

function rotateOffset(dx: number, dz: number, steps: number): { dx: number; dz: number } {
  let x = dx;
  let z = dz;
  for (let i = 0; i < ((steps % 4) + 4) % 4; i++) {
    const nx = z;
    const nz = -x;
    x = nx;
    z = nz;
  }
  return { dx: x, dz: z };
}

function worldExit(block: PlacedBlock): { cellX: number; cellZ: number; level: number } | null {
  const socket = EXIT_SOCKET[block.shape];
  if (!socket) return null;
  const { dx, dz } = rotateOffset(socket.dx, socket.dz, block.rotationSteps);
  return { cellX: block.cellX + dx, cellZ: block.cellZ + dz, level: block.level + socket.dLevel };
}

function worldEntry(block: PlacedBlock): { cellX: number; cellZ: number; level: number } | null {
  const socket = ENTRY_SOCKET[block.shape];
  if (!socket) return null;
  // ENTRY_SOCKET's local direction already points "backward" (toward where the
  // previous block should sit), the same way EXIT_SOCKET points "forward" - so
  // this must add the rotated offset, exactly like worldExit does. Subtracting
  // here was the bug: it silently doubled the offset's sign, so even two plain
  // Straight blocks placed in a row (rotation 0) never registered as connected.
  const { dx, dz } = rotateOffset(socket.dx, socket.dz, block.rotationSteps);
  return { cellX: block.cellX + dx, cellZ: block.cellZ + dz, level: block.level + socket.dLevel };
}

export type ConnectivityResult = {
  status: "no-start" | "multiple-start" | "broken" | "no-finish" | "connected";
  connectedIds: string[];
  breakAfterId: string | null;
  message: string;
};

export function checkConnectivity(blocks: PlacedBlock[]): ConnectivityResult {
  const starts = blocks.filter((b) => b.shape === "start");
  if (starts.length === 0) {
    return { status: "no-start", connectedIds: [], breakAfterId: null, message: "Manca il blocco Partenza." };
  }
  if (starts.length > 1) {
    return {
      status: "multiple-start",
      connectedIds: [],
      breakAfterId: null,
      message: "C'è più di un blocco Partenza — ce ne vuole uno solo.",
    };
  }

  const byPosition = new Map<string, PlacedBlock>();
  for (const b of blocks) byPosition.set(`${b.cellX},${b.cellZ},${b.level}`, b);

  const connectedIds: string[] = [];
  const visited = new Set<string>();
  let current: PlacedBlock | undefined = starts[0];

  while (current) {
    connectedIds.push(current.id);
    visited.add(current.id);

    if (current.shape === "finish") {
      return {
        status: "connected",
        connectedIds,
        breakAfterId: null,
        message: "Pista collegata da Partenza ad Arrivo!",
      };
    }

    const exit = worldExit(current);
    if (!exit) {
      return {
        status: "broken",
        connectedIds,
        breakAfterId: current.id,
        message: "Il percorso si interrompe qui — questo blocco non ha un'uscita.",
      };
    }

    const next = byPosition.get(`${exit.cellX},${exit.cellZ},${exit.level}`);
    if (!next) {
      return {
        status: "broken",
        connectedIds,
        breakAfterId: current.id,
        message: "Il percorso si interrompe qui — manca il blocco successivo.",
      };
    }

    const nextEntry = worldEntry(next);
    const facesBack =
      nextEntry && nextEntry.cellX === current.cellX && nextEntry.cellZ === current.cellZ && nextEntry.level === current.level;
    if (!facesBack) {
      return {
        status: "broken",
        connectedIds,
        breakAfterId: current.id,
        message: "Il blocco successivo c'è ma è girato dalla parte sbagliata — ruotalo.",
      };
    }

    if (visited.has(next.id)) {
      return {
        status: "broken",
        connectedIds,
        breakAfterId: current.id,
        message: "Il percorso torna su se stesso (loop) senza arrivare al traguardo.",
      };
    }

    current = next;
  }

  return { status: "no-finish", connectedIds, breakAfterId: null, message: "Manca il blocco Arrivo." };
}
