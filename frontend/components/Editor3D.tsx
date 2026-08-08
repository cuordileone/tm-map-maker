"use client";

import { useState } from "react";
import { Canvas, type ThreeEvent } from "@react-three/fiber";
import { OrbitControls, Grid } from "@react-three/drei";

const CELL_SIZE = 2;

type PlacedBlock = {
  id: string;
  cellX: number;
  cellZ: number;
};

function StraightBlock({ cellX, cellZ }: { cellX: number; cellZ: number }) {
  return (
    <group position={[cellX * CELL_SIZE, 0, cellZ * CELL_SIZE]}>
      {/* road surface */}
      <mesh position={[0, 0.05, 0]}>
        <boxGeometry args={[CELL_SIZE * 0.9, 0.1, CELL_SIZE * 0.9]} />
        <meshStandardMaterial color="#6b7280" />
      </mesh>
      {/* side borders, so it reads as a track piece and not a plain slab */}
      <mesh position={[-CELL_SIZE * 0.42, 0.15, 0]}>
        <boxGeometry args={[CELL_SIZE * 0.06, 0.3, CELL_SIZE * 0.9]} />
        <meshStandardMaterial color="#f59e0b" />
      </mesh>
      <mesh position={[CELL_SIZE * 0.42, 0.15, 0]}>
        <boxGeometry args={[CELL_SIZE * 0.06, 0.3, CELL_SIZE * 0.9]} />
        <meshStandardMaterial color="#f59e0b" />
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
    { id: "start", cellX: 0, cellZ: 0 },
  ]);

  function placeBlockAt(cellX: number, cellZ: number) {
    setBlocks((prev) => {
      const alreadyThere = prev.some((b) => b.cellX === cellX && b.cellZ === cellZ);
      if (alreadyThere) return prev;
      return [...prev, { id: `${cellX}-${cellZ}-${prev.length}`, cellX, cellZ }];
    });
  }

  function reset() {
    setBlocks([{ id: "start", cellX: 0, cellZ: 0 }]);
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
          <StraightBlock key={b.id} cellX={b.cellX} cellZ={b.cellZ} />
        ))}
        <OrbitControls makeDefault />
      </Canvas>

      <div className="pointer-events-none absolute left-4 top-4 flex flex-col gap-1 rounded-lg bg-black/60 px-4 py-3 text-sm text-white">
        <p className="font-semibold">TM Track Studio — editor 3D (prototipo)</p>
        <p className="text-slate-300">Clicca sulla griglia per piazzare un blocco dritto.</p>
        <p className="text-slate-300">Trascina per ruotare la vista, rotellina per zoom.</p>
      </div>

      <button
        onClick={reset}
        className="pointer-events-auto absolute right-4 top-4 rounded-lg bg-slate-700 px-4 py-2 text-sm font-medium text-white hover:bg-slate-600"
      >
        Reset pista
      </button>
    </div>
  );
}
