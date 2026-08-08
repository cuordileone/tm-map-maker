"use client";

import { useMemo, useRef, useState } from "react";
import { Canvas, type ThreeEvent } from "@react-three/fiber";
import { OrbitControls, Grid, Text } from "@react-three/drei";
import { MOUSE } from "three";
import { checkConnectivity, type ShapeType, type PlacedBlock } from "@/lib/connectivity";

const CELL_SIZE = 2;
const LEVEL_HEIGHT = 0.6;
const ROAD_WIDTH = CELL_SIZE * 0.5;

const SHAPE_LABELS: Record<ShapeType, string> = {
  straight: "Dritto",
  curve: "Curva",
  slopeUp: "Salita",
  slopeDown: "Discesa",
  checkpoint: "Checkpoint",
  start: "Partenza",
  finish: "Arrivo",
};

const SHAPE_COLORS: Record<ShapeType, string> = {
  straight: "#6b7280",
  curve: "#4b7bec",
  slopeUp: "#26de81",
  slopeDown: "#fd9644",
  checkpoint: "#45aaf2",
  start: "#20bf6b",
  finish: "#eb3b5a",
};

// Every shape's own footprint is a single CELL_SIZE x CELL_SIZE cell, oriented so
// "forward" (the direction traffic exits) is local +Z before rotation is applied -
// same convention as GridRotation (North = identity, 90 deg steps clockwise).
type TestHighlight = "connected" | "break" | null;

function BlockMesh({
  block,
  selected,
  testHighlight,
}: {
  block: PlacedBlock;
  selected: boolean;
  testHighlight: TestHighlight;
}) {
  // Must match connectivity.ts's rotateOffset convention exactly (positive steps
  // rotate local +Z "forward" toward world +X) - a mismatched sign here means a
  // block can look visually connected while the connectivity checker disagrees.
  const rotationY = (block.rotationSteps * Math.PI) / 2;
  const surfaceColor = SHAPE_COLORS[block.shape];
  const borderColor =
    testHighlight === "connected" ? "#22c55e" : testHighlight === "break" ? "#ef4444" : selected ? "#22d3ee" : "#f59e0b";
  const y = block.level * LEVEL_HEIGHT;

  return (
    <group position={[block.cellX * CELL_SIZE, y, block.cellZ * CELL_SIZE]}>
      {/* DEBUG label, not rotated with the block, so it stays readable - shows
          exactly what state.ts sees: shape, rotationSteps, cell. Temporary,
          for diagnosing the connectivity mismatch reported after two failed
          blind fixes - remove once confirmed correct. */}
      <Text position={[0, 1.6, 0]} fontSize={0.16} color="#fde047" anchorX="center" anchorY="middle">
        {`${block.shape} r${block.rotationSteps} (${block.cellX},${block.cellZ})`}
      </Text>
      <group rotation={[0, rotationY, 0]}>
      {block.shape === "straight" && <StraightGeometry color={surfaceColor} borderColor={borderColor} />}
      {block.shape === "curve" && <CurveGeometry color={surfaceColor} borderColor={borderColor} />}
      {block.shape === "slopeUp" && <SlopeGeometry color={surfaceColor} borderColor={borderColor} direction={1} />}
      {block.shape === "slopeDown" && <SlopeGeometry color={surfaceColor} borderColor={borderColor} direction={-1} />}
      {block.shape === "checkpoint" && <CheckpointGeometry color={surfaceColor} />}
      {block.shape === "start" && <GateGeometry color={surfaceColor} label="PARTENZA" />}
      {block.shape === "finish" && <GateGeometry color={surfaceColor} label="ARRIVO" />}

      {testHighlight === "break" && (
        <mesh position={[0, 0.9, 0]}>
          <sphereGeometry args={[0.18, 16, 16]} />
          <meshStandardMaterial color="#ef4444" emissive="#ef4444" emissiveIntensity={0.6} />
        </mesh>
      )}

      {/* facing indicator: small arrow pointing the block's "forward" (exit) direction */}
      <mesh position={[0, 0.35, CELL_SIZE * 0.3]}>
        <coneGeometry args={[0.12, 0.25, 3]} />
        <meshStandardMaterial color={selected ? "#22d3ee" : "#e5e7eb"} />
      </mesh>
      </group>
    </group>
  );
}

function StraightGeometry({ color, borderColor }: { color: string; borderColor: string }) {
  return (
    <>
      <mesh position={[0, 0.05, 0]}>
        <boxGeometry args={[ROAD_WIDTH, 0.1, CELL_SIZE * 0.95]} />
        <meshStandardMaterial color={color} />
      </mesh>
      <mesh position={[-ROAD_WIDTH / 2 - 0.03, 0.15, 0]}>
        <boxGeometry args={[0.06, 0.3, CELL_SIZE * 0.95]} />
        <meshStandardMaterial color={borderColor} />
      </mesh>
      <mesh position={[ROAD_WIDTH / 2 + 0.03, 0.15, 0]}>
        <boxGeometry args={[0.06, 0.3, CELL_SIZE * 0.95]} />
        <meshStandardMaterial color={borderColor} />
      </mesh>
    </>
  );
}

// A quarter-circle turn: enters at the local south edge midpoint (0, -CELL_SIZE/2)
// and exits at the local east edge midpoint (CELL_SIZE/2, 0) - the exact same
// points ENTRY_SOCKET/EXIT_SOCKET use in lib/connectivity.ts. Built as a chain of
// small, UNROTATED tiles placed along the arc (no per-tile rotation math at all)
// on purpose: a compound Euler rotation on a single ring mesh was tried first and
// silently pointed the visual curve somewhere the connectivity logic didn't agree
// with, which is exactly the kind of bug that's easy to get wrong by hand and hard
// to catch without a human actually looking at the result - this approach can't
// have that class of bug because there's no rotation to get wrong.
function CurveGeometry({ color, borderColor }: { color: string; borderColor: string }) {
  const centerX = CELL_SIZE / 2;
  const centerZ = -CELL_SIZE / 2;
  const radius = CELL_SIZE / 2;
  const segments = 10;
  const tileSize = ROAD_WIDTH * 0.55;

  const angles = Array.from({ length: segments + 1 }, (_, i) => Math.PI / 2 + (Math.PI / 2) * (i / segments));

  return (
    <group>
      {angles.map((phi, i) => (
        <mesh key={`road-${i}`} position={[centerX + radius * Math.cos(phi), 0.05, centerZ + radius * Math.sin(phi)]}>
          <boxGeometry args={[tileSize, 0.1, tileSize]} />
          <meshStandardMaterial color={color} />
        </mesh>
      ))}
      {angles.map((phi, i) => {
        const outerR = radius + ROAD_WIDTH / 2;
        const innerR = radius - ROAD_WIDTH / 2;
        return (
          <group key={`curb-${i}`}>
            <mesh position={[centerX + outerR * Math.cos(phi), 0.15, centerZ + outerR * Math.sin(phi)]}>
              <boxGeometry args={[0.16, 0.3, 0.16]} />
              <meshStandardMaterial color={borderColor} />
            </mesh>
            <mesh position={[centerX + innerR * Math.cos(phi), 0.15, centerZ + innerR * Math.sin(phi)]}>
              <boxGeometry args={[0.16, 0.3, 0.16]} />
              <meshStandardMaterial color={borderColor} />
            </mesh>
          </group>
        );
      })}
    </group>
  );
}

function SlopeGeometry({ color, borderColor, direction }: { color: string; borderColor: string; direction: 1 | -1 }) {
  const rise = LEVEL_HEIGHT;
  const length = CELL_SIZE * 0.95;
  const angle = Math.atan2(rise, length) * direction;
  return (
    <group rotation={[angle, 0, 0]}>
      <mesh position={[0, 0.05, 0]}>
        <boxGeometry args={[ROAD_WIDTH, 0.1, length]} />
        <meshStandardMaterial color={color} />
      </mesh>
      <mesh position={[-ROAD_WIDTH / 2 - 0.03, 0.15, 0]}>
        <boxGeometry args={[0.06, 0.3, length]} />
        <meshStandardMaterial color={borderColor} />
      </mesh>
      <mesh position={[ROAD_WIDTH / 2 + 0.03, 0.15, 0]}>
        <boxGeometry args={[0.06, 0.3, length]} />
        <meshStandardMaterial color={borderColor} />
      </mesh>
    </group>
  );
}

function CheckpointGeometry({ color }: { color: string }) {
  return (
    <>
      <mesh position={[0, 0.05, 0]}>
        <boxGeometry args={[ROAD_WIDTH, 0.1, CELL_SIZE * 0.95]} />
        <meshStandardMaterial color="#6b7280" />
      </mesh>
      <mesh position={[-ROAD_WIDTH / 2, 0.6, 0]}>
        <boxGeometry args={[0.08, 1.2, 0.08]} />
        <meshStandardMaterial color={color} />
      </mesh>
      <mesh position={[ROAD_WIDTH / 2, 0.6, 0]}>
        <boxGeometry args={[0.08, 1.2, 0.08]} />
        <meshStandardMaterial color={color} />
      </mesh>
      <mesh position={[0, 1.15, 0]}>
        <boxGeometry args={[ROAD_WIDTH + 0.08, 0.08, 0.08]} />
        <meshStandardMaterial color={color} />
      </mesh>
    </>
  );
}

function GateGeometry({ color, label }: { color: string; label: string }) {
  return (
    <>
      <mesh position={[0, 0.05, 0]}>
        <boxGeometry args={[ROAD_WIDTH, 0.1, CELL_SIZE * 0.95]} />
        <meshStandardMaterial color="#6b7280" />
      </mesh>
      <mesh position={[0, 0.6, CELL_SIZE * 0.42]}>
        <boxGeometry args={[ROAD_WIDTH, 1.2, 0.1]} />
        <meshStandardMaterial color={color} transparent opacity={0.55} />
      </mesh>
      <Text
        position={[0, 1.4, CELL_SIZE * 0.42]}
        fontSize={0.22}
        color={color}
        anchorX="center"
        anchorY="middle"
      >
        {label}
      </Text>
    </>
  );
}

function GroundPlane({ onCellClick }: { onCellClick: (cellX: number, cellZ: number) => void }) {
  // Distinguish a genuine click from "the user was dragging the camera and the
  // mouse happened to lift over the ground": track where the left button went
  // down and only treat it as a placement click if the pointer barely moved.
  // OrbitControls is also reassigned to right-drag (below) so left-drag never
  // reaches it in the first place, but this guards the same thing at the
  // ground-plane level too, in case a click still fires after a tiny jitter.
  const downPoint = useRef<{ x: number; y: number } | null>(null);

  const handlePointerDown = (event: ThreeEvent<PointerEvent>) => {
    if (event.button !== 0) return; // only the left button places blocks
    downPoint.current = { x: event.clientX, y: event.clientY };
  };

  const handlePointerUp = (event: ThreeEvent<PointerEvent>) => {
    if (event.button !== 0 || !downPoint.current) return;
    const dx = event.clientX - downPoint.current.x;
    const dy = event.clientY - downPoint.current.y;
    downPoint.current = null;
    if (Math.hypot(dx, dy) > 6) return; // moved too much - this was a drag, not a click

    event.stopPropagation();
    const { x, z } = event.point;
    const cellX = Math.round(x / CELL_SIZE);
    const cellZ = Math.round(z / CELL_SIZE);
    onCellClick(cellX, cellZ);
  };

  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} onPointerDown={handlePointerDown} onPointerUp={handlePointerUp}>
      <planeGeometry args={[200, 200]} />
      <meshBasicMaterial visible={false} />
    </mesh>
  );
}

type Tool = "place" | "delete";

export default function Editor3D() {
  const [blocks, setBlocks] = useState<PlacedBlock[]>([
    { id: "start", cellX: 0, cellZ: 0, shape: "start", rotationSteps: 0, level: 0 },
  ]);
  const [activeShape, setActiveShape] = useState<ShapeType>("straight");
  const [nextRotation, setNextRotation] = useState(0);
  const [nextLevel, setNextLevel] = useState(0);
  const [tool, setTool] = useState<Tool>("place");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [testMode, setTestMode] = useState(false);

  const connectivity = useMemo(() => checkConnectivity(blocks), [blocks]);

  function handleCellClick(cellX: number, cellZ: number) {
    if (testMode) return; // editing is locked while trying out the track

    // Levels are matched exactly: a click on a cell that has a block on a
    // different level than the one you're currently placing at is treated as
    // empty (you're stacking a new block there), not as "rotate that other block".
    const existing = blocks.find((b) => b.cellX === cellX && b.cellZ === cellZ && b.level === nextLevel);

    if (tool === "delete") {
      if (existing) setBlocks((prev) => prev.filter((b) => b.id !== existing.id));
      return;
    }

    if (existing) {
      // clicking an existing block rotates it in place - one click, no separate
      // select-then-rotate step to learn, matches the "click a block to spin it" idea
      setBlocks((prev) =>
        prev.map((b) => (b.id === existing.id ? { ...b, rotationSteps: (b.rotationSteps + 1) % 4 } : b))
      );
      setSelectedId(existing.id);
      return;
    }

    setBlocks((prev) => [
      ...prev,
      {
        id: `${cellX}-${cellZ}-${prev.length}`,
        cellX,
        cellZ,
        shape: activeShape,
        rotationSteps: nextRotation,
        level: nextLevel,
      },
    ]);
  }

  function rotateNextPlacement() {
    setNextRotation((prev) => (prev + 1) % 4);
  }

  function reset() {
    setBlocks([{ id: "start", cellX: 0, cellZ: 0, shape: "start", rotationSteps: 0, level: 0 }]);
    setSelectedId(null);
    setNextRotation(0);
    setNextLevel(0);
    setTestMode(false);
  }

  function highlightFor(blockId: string): "connected" | "break" | null {
    if (!testMode) return null;
    if (connectivity.breakAfterId === blockId) return "break";
    if (connectivity.connectedIds.includes(blockId)) return "connected";
    return null;
  }

  return (
    <div className="relative h-full w-full">
      <Canvas camera={{ position: [8, 8, 8], fov: 50 }}>
        <color attach="background" args={["#0f172a"]} />
        <ambientLight intensity={0.7} />
        <directionalLight position={[10, 15, 5]} intensity={1} />
        <Grid
          args={[200, 200]}
          cellSize={CELL_SIZE}
          cellColor="#334155"
          sectionColor="#475569"
          fadeDistance={60}
          infiniteGrid
        />
        <GroundPlane onCellClick={handleCellClick} />
        {blocks.map((b) => (
          <BlockMesh key={b.id} block={b} selected={b.id === selectedId} testHighlight={highlightFor(b.id)} />
        ))}
        {/* Left button is reserved entirely for placing/rotating blocks - the
            camera never sees it, so orbiting can never accidentally place a
            block. Right-drag rotates the view, the wheel (or middle-drag) zooms. */}
        <OrbitControls makeDefault mouseButtons={{ LEFT: undefined, MIDDLE: MOUSE.DOLLY, RIGHT: MOUSE.ROTATE }} />
      </Canvas>

      <div className="pointer-events-none absolute left-4 top-4 flex flex-col gap-1 rounded-lg bg-black/60 px-4 py-3 text-sm text-white">
        <p className="font-semibold">TM Track Studio — editor 3D (prototipo)</p>
        <p className="text-slate-300">
          {testMode
            ? "Modalità prova: verde = collegato, rosso = qui si interrompe."
            : tool === "place"
              ? "Scegli una forma sotto, clicca (tasto sinistro) sulla griglia per piazzarla. Clicca un blocco per ruotarlo."
              : "Modalità cancella: clicca un blocco per rimuoverlo."}
        </p>
        <p className="text-slate-400">Tasto destro trascinato = ruota la visuale. Rotellina = zoom.</p>
      </div>

      {testMode && (
        <div
          className={`pointer-events-none absolute left-1/2 top-20 -translate-x-1/2 rounded-lg px-4 py-2 text-sm font-semibold text-white ${
            connectivity.status === "connected" ? "bg-green-600" : "bg-red-600"
          }`}
        >
          {connectivity.message}
        </div>
      )}

      <div className="pointer-events-auto absolute right-4 top-4 flex gap-2">
        <button
          onClick={() => setTestMode((v) => !v)}
          className={`rounded-lg px-4 py-2 text-sm font-medium text-white ${
            testMode ? "bg-cyan-600 hover:bg-cyan-500" : "bg-slate-700 hover:bg-slate-600"
          }`}
        >
          {testMode ? "◼ Torna a modificare" : "▶ Prova pista"}
        </button>
        <button
          onClick={reset}
          className="rounded-lg bg-slate-700 px-4 py-2 text-sm font-medium text-white hover:bg-slate-600"
        >
          Reset pista
        </button>
      </div>

      <div
        className={`pointer-events-auto absolute bottom-4 left-1/2 flex -translate-x-1/2 flex-col items-center gap-2 rounded-xl bg-black/70 p-3 ${
          testMode ? "pointer-events-none opacity-40" : ""
        }`}
      >
        <div className="flex flex-wrap items-center justify-center gap-2">
          {(Object.keys(SHAPE_LABELS) as ShapeType[]).map((shape) => (
            <button
              key={shape}
              onClick={() => {
                setActiveShape(shape);
                setTool("place");
              }}
              className={`rounded-lg px-3 py-2 text-xs font-medium text-white transition ${
                tool === "place" && activeShape === shape ? "ring-2 ring-cyan-400" : "opacity-80 hover:opacity-100"
              }`}
              style={{ backgroundColor: SHAPE_COLORS[shape] }}
            >
              {SHAPE_LABELS[shape]}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={rotateNextPlacement}
            className="flex items-center gap-2 rounded-lg bg-slate-600 px-3 py-2 text-xs font-medium text-white hover:bg-slate-500"
            title="Ruota l'orientamento del prossimo blocco da piazzare"
          >
            ⟳ Ruota ({nextRotation * 90}°)
          </button>
          <div className="flex items-center gap-1 rounded-lg bg-slate-600 px-2 py-1">
            <button
              onClick={() => setNextLevel((l) => l - 1)}
              className="rounded px-2 py-1 text-xs font-bold text-white hover:bg-slate-500"
              title="Abbassa il livello del prossimo blocco"
            >
              ▼
            </button>
            <span className="w-14 text-center text-xs text-white">Livello {nextLevel}</span>
            <button
              onClick={() => setNextLevel((l) => l + 1)}
              className="rounded px-2 py-1 text-xs font-bold text-white hover:bg-slate-500"
              title="Alza il livello del prossimo blocco"
            >
              ▲
            </button>
          </div>
          <button
            onClick={() => setTool((t) => (t === "place" ? "delete" : "place"))}
            className={`rounded-lg px-3 py-2 text-xs font-medium text-white ${
              tool === "delete" ? "bg-red-600 ring-2 ring-red-300" : "bg-slate-600 hover:bg-slate-500"
            }`}
          >
            🗑 {tool === "delete" ? "Cancella: attivo" : "Cancella blocchi"}
          </button>
        </div>
      </div>
    </div>
  );
}
