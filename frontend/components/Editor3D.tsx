"use client";

import { useState } from "react";
import { Canvas, type ThreeEvent } from "@react-three/fiber";
import { OrbitControls, Grid } from "@react-three/drei";

const CELL_SIZE = 2;

type ShapeType = "straight" | "curve" | "diagonal";

type PlacedBlock = {
  id: string;
  cellX: number;
  cellZ: number;
  shape: ShapeType;
  rotationSteps: number; // 0-3, each step = 90 degrees
};

const SHAPE_LABELS: Record<ShapeType, string> = {
  straight: "Dritto",
  curve: "Curva",
  diagonal: "Diagonale",
};

const SHAPE_COLORS: Record<ShapeType, string> = {
  straight: "#6b7280",
  curve: "#4b7bec",
  diagonal: "#a55eea",
};

function BlockMesh({ block, selected }: { block: PlacedBlock; selected: boolean }) {
  const rotationY = -(block.rotationSteps * Math.PI) / 2;
  const surfaceColor = SHAPE_COLORS[block.shape];
  const borderColor = selected ? "#22d3ee" : "#f59e0b";

  return (
    <group position={[block.cellX * CELL_SIZE, 0, block.cellZ * CELL_SIZE]} rotation={[0, rotationY, 0]}>
      {block.shape === "straight" && (
        <>
          <mesh position={[0, 0.05, 0]}>
            <boxGeometry args={[CELL_SIZE * 0.9, 0.1, CELL_SIZE * 0.9]} />
            <meshStandardMaterial color={surfaceColor} />
          </mesh>
          <mesh position={[-CELL_SIZE * 0.42, 0.15, 0]}>
            <boxGeometry args={[CELL_SIZE * 0.06, 0.3, CELL_SIZE * 0.9]} />
            <meshStandardMaterial color={borderColor} />
          </mesh>
          <mesh position={[CELL_SIZE * 0.42, 0.15, 0]}>
            <boxGeometry args={[CELL_SIZE * 0.06, 0.3, CELL_SIZE * 0.9]} />
            <meshStandardMaterial color={borderColor} />
          </mesh>
        </>
      )}

      {block.shape === "curve" && (
        <>
          {/* L-shaped road surface: a leg pointing "in" (south) and a leg pointing "right" (east) before rotation */}
          <mesh position={[0, 0.05, CELL_SIZE * 0.2]}>
            <boxGeometry args={[CELL_SIZE * 0.5, 0.1, CELL_SIZE * 0.9]} />
            <meshStandardMaterial color={surfaceColor} />
          </mesh>
          <mesh position={[CELL_SIZE * 0.2, 0.05, -CELL_SIZE * 0.2]}>
            <boxGeometry args={[CELL_SIZE * 0.9, 0.1, CELL_SIZE * 0.5]} />
            <meshStandardMaterial color={surfaceColor} />
          </mesh>
          <mesh position={[-CELL_SIZE * 0.22, 0.15, CELL_SIZE * 0.42]}>
            <boxGeometry args={[CELL_SIZE * 0.06, 0.3, CELL_SIZE * 0.5]} />
            <meshStandardMaterial color={borderColor} />
          </mesh>
          <mesh position={[CELL_SIZE * 0.42, 0.15, -CELL_SIZE * 0.22]}>
            <boxGeometry args={[CELL_SIZE * 0.5, 0.3, CELL_SIZE * 0.06]} />
            <meshStandardMaterial color={borderColor} />
          </mesh>
        </>
      )}

      {block.shape === "diagonal" && (
        <mesh position={[0, 0.05, 0]} rotation={[0, Math.PI / 4, 0]}>
          <boxGeometry args={[CELL_SIZE * 0.65, 0.1, CELL_SIZE * 1.15]} />
          <meshStandardMaterial color={surfaceColor} />
        </mesh>
      )}

      {/* facing indicator: small arrow-like wedge pointing the block's "forward" direction (+Z before rotation) */}
      <mesh position={[0, 0.25, CELL_SIZE * 0.3]}>
        <coneGeometry args={[0.12, 0.25, 3]} />
        <meshStandardMaterial color={selected ? "#22d3ee" : "#e5e7eb"} />
      </mesh>
    </group>
  );
}

function GroundPlane({ onCellClick }: { onCellClick: (cellX: number, cellZ: number) => void }) {
  const handleClick = (event: ThreeEvent<MouseEvent>) => {
    event.stopPropagation();
    const { x, z } = event.point;
    const cellX = Math.round(x / CELL_SIZE);
    const cellZ = Math.round(z / CELL_SIZE);
    onCellClick(cellX, cellZ);
  };

  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} onClick={handleClick}>
      <planeGeometry args={[200, 200]} />
      <meshBasicMaterial visible={false} />
    </mesh>
  );
}

export default function Editor3D() {
  const [blocks, setBlocks] = useState<PlacedBlock[]>([
    { id: "start", cellX: 0, cellZ: 0, shape: "straight", rotationSteps: 0 },
  ]);
  const [activeShape, setActiveShape] = useState<ShapeType>("straight");
  const [nextRotation, setNextRotation] = useState(0);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  function placeBlockAt(cellX: number, cellZ: number) {
    const existing = blocks.find((b) => b.cellX === cellX && b.cellZ === cellZ);
    if (existing) {
      // clicking an existing block rotates it in place - the whole point is
      // "click a block to spin it", no separate select+rotate step to learn
      setBlocks((prev) =>
        prev.map((b) => (b.id === existing.id ? { ...b, rotationSteps: (b.rotationSteps + 1) % 4 } : b))
      );
      setSelectedId(existing.id);
      return;
    }
    setBlocks((prev) => [
      ...prev,
      { id: `${cellX}-${cellZ}-${prev.length}`, cellX, cellZ, shape: activeShape, rotationSteps: nextRotation },
    ]);
  }

  function rotateNextPlacement() {
    setNextRotation((prev) => (prev + 1) % 4);
  }

  function reset() {
    setBlocks([{ id: "start", cellX: 0, cellZ: 0, shape: "straight", rotationSteps: 0 }]);
    setSelectedId(null);
    setNextRotation(0);
  }

  return (
    <div className="relative h-full w-full">
      <Canvas camera={{ position: [8, 8, 8], fov: 50 }}>
        <color attach="background" args={["#0f172a"]} />
        <ambientLight intensity={0.6} />
        <directionalLight position={[10, 15, 5]} intensity={1} />
        <Grid
          args={[200, 200]}
          cellSize={CELL_SIZE}
          cellColor="#334155"
          sectionColor="#475569"
          fadeDistance={60}
          infiniteGrid
        />
        <GroundPlane onCellClick={placeBlockAt} />
        {blocks.map((b) => (
          <BlockMesh key={b.id} block={b} selected={b.id === selectedId} />
        ))}
        <OrbitControls makeDefault />
      </Canvas>

      <div className="pointer-events-none absolute left-4 top-4 flex flex-col gap-1 rounded-lg bg-black/60 px-4 py-3 text-sm text-white">
        <p className="font-semibold">TM Track Studio — editor 3D (prototipo)</p>
        <p className="text-slate-300">Scegli una forma sotto, clicca sulla griglia per piazzarla.</p>
        <p className="text-slate-300">Clicca un blocco già piazzato per farlo ruotare.</p>
      </div>

      <button
        onClick={reset}
        className="pointer-events-auto absolute right-4 top-4 rounded-lg bg-slate-700 px-4 py-2 text-sm font-medium text-white hover:bg-slate-600"
      >
        Reset pista
      </button>

      <div className="pointer-events-auto absolute bottom-4 left-1/2 flex -translate-x-1/2 items-center gap-2 rounded-xl bg-black/70 p-3">
        {(Object.keys(SHAPE_LABELS) as ShapeType[]).map((shape) => (
          <button
            key={shape}
            onClick={() => setActiveShape(shape)}
            className={`rounded-lg px-4 py-2 text-sm font-medium text-white transition ${
              activeShape === shape ? "ring-2 ring-cyan-400" : "opacity-80 hover:opacity-100"
            }`}
            style={{ backgroundColor: SHAPE_COLORS[shape] }}
          >
            {SHAPE_LABELS[shape]}
          </button>
        ))}
        <div className="mx-1 h-8 w-px bg-white/20" />
        <button
          onClick={rotateNextPlacement}
          className="flex items-center gap-2 rounded-lg bg-slate-600 px-4 py-2 text-sm font-medium text-white hover:bg-slate-500"
          title="Ruota l'orientamento del prossimo blocco da piazzare"
        >
          ⟳ Ruota prossimo ({nextRotation * 90}°)
        </button>
      </div>
    </div>
  );
}
