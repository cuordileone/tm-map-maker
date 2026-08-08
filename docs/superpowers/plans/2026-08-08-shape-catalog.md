# Shape Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a small catalog of block "Shapes" (Straight, Curve1, Checkpoint, Start, Finish, Slope2Straight/Up/Down) with verified footprint and connector geometry, reusable across every block family the Block Catalog Reader already classifies (Road, Platform, etc.). This is the geometric knowledge layer the future Path Compiler and Validator will consume.

**Architecture:** A coordinate-rotation utility (pure math, no empirical verification needed) + a `ShapeHypothesisVerifier` that checks a candidate connector offset against every reference map's actual drivable-block adjacency (reusing the Block Catalog Reader's family classification to filter out decorations/structure) + a human-reviewed report gate before the verified geometry is locked into a final `ShapeCatalog` data file.

**Tech Stack:** .NET 8, C#, same solution as the Block Catalog Reader (`backend/TmMapMaker.sln`), GBX.NET (already referenced), xUnit, System.Text.Json.

## Global Constraints

- Fail-loud: a shape's connector geometry is never accepted into the final `ShapeCatalog` without empirical confirmation from real reference-map data. No numeric offset is hard-coded into the locked catalog without a human reviewing the verifier's evidence report first (see Task 3) — this is the specific failure mode (guessed/unverified geometry) that corrupted the retired Python pipeline.
- Reuse the existing, tested Block Catalog Reader (`BlockFamily`, `BlockNameClassifier`, `PlacedBlock`, `GbxMapReader`) rather than re-implementing block reading or classification.
- Direction convention: `0 = North, 1 = East, 2 = South, 3 = West` (GBX.NET's `Direction` enum, already used by `GbxMapReader` — same convention documented in the retired project and confirmed against real block data in the previous plan).
- All code/identifiers in English; any CLI/report output in Italian (matches project convention).
- v1 shape vocabulary is exactly: `Start`, `Finish`, `Checkpoint`, `Straight`, `Curve1`, `Slope2Straight`, `Slope2Up`, `Slope2Down`. No other shapes in scope for this plan.

---

## File Structure

```
backend/
  src/TmMapMaker.BlockCatalog/
    GridRotation.cs              <- NEW: rotates a local (dx,dz) offset by a Direction
    ShapeHypothesis.cs           <- NEW: candidate connector offset for one shape
    ShapeVerificationResult.cs   <- NEW: match/mismatch evidence for one hypothesis
    ShapeHypothesisVerifier.cs   <- NEW: checks a hypothesis against real reference-map data
    ShapeCatalog.cs              <- NEW (Task 4 only): final locked, verified shape definitions
    VerifyShapesProgram.cs       <- NEW: second CLI entry point, runs verification report
  tests/TmMapMaker.BlockCatalog.Tests/
    GridRotationTests.cs
    ShapeHypothesisVerifierTests.cs
    ShapeCatalogTests.cs         <- Task 4 only
```

`VerifyShapesProgram.cs` is a second `Main`-like entry point alongside the existing block-inventory CLI. Since a console project can only have one true entry point, Task 2 restructures `Program.cs` into a thin dispatcher that routes to either the existing inventory command or the new shape-verification command based on the first CLI argument — see Task 2 for the exact change.

---

### Task 1: Grid rotation utility

**Files:**
- Create: `backend/src/TmMapMaker.BlockCatalog/GridRotation.cs`
- Test: `backend/tests/TmMapMaker.BlockCatalog.Tests/GridRotationTests.cs`

**Interfaces:**
- Produces: `GridRotation.Rotate(int localDx, int localDz, string direction) -> (int WorldDx, int WorldDz)`. `direction` is one of `"North"`, `"East"`, `"South"`, `"West"` (matches `PlacedBlock.Direction`'s string values from `GbxMapReader`).

This is pure coordinate math, not empirical — the rotation convention (`North=0, East=1, South=2, West=3`, 90° clockwise per step) is already documented and used consistently by the existing `GbxMapReader`/`Direction` enum from GBX.NET, so no reference-map verification is needed for this task, only correctness tests.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/TmMapMaker.BlockCatalog.Tests/GridRotationTests.cs`:

```csharp
using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class GridRotationTests
{
    // A block's own "forward" is +Z (dz=1) in its unrotated local frame.
    // North = no rotation: forward stays (0, 1).
    // East = 90° clockwise: forward becomes (1, 0).
    // South = 180°: forward becomes (0, -1).
    // West = 270° clockwise: forward becomes (-1, 0).
    [Theory]
    [InlineData(0, 1, "North", 0, 1)]
    [InlineData(0, 1, "East", 1, 0)]
    [InlineData(0, 1, "South", 0, -1)]
    [InlineData(0, 1, "West", -1, 0)]
    public void Rotate_maps_local_forward_to_expected_world_offset(int localDx, int localDz, string direction, int expectedWorldDx, int expectedWorldDz)
    {
        var (worldDx, worldDz) = GridRotation.Rotate(localDx, localDz, direction);
        Assert.Equal(expectedWorldDx, worldDx);
        Assert.Equal(expectedWorldDz, worldDz);
    }

    [Theory]
    [InlineData(1, 0, "North", 1, 0)]
    [InlineData(1, 0, "East", 0, -1)]
    [InlineData(1, 0, "South", -1, 0)]
    [InlineData(1, 0, "West", 0, 1)]
    public void Rotate_maps_local_right_to_expected_world_offset(int localDx, int localDz, string direction, int expectedWorldDx, int expectedWorldDz)
    {
        var (worldDx, worldDz) = GridRotation.Rotate(localDx, localDz, direction);
        Assert.Equal(expectedWorldDx, worldDx);
        Assert.Equal(expectedWorldDz, worldDz);
    }

    [Fact]
    public void Rotate_of_zero_offset_is_always_zero()
    {
        foreach (var dir in new[] { "North", "East", "South", "West" })
        {
            var (worldDx, worldDz) = GridRotation.Rotate(0, 0, dir);
            Assert.Equal((0, 0), (worldDx, worldDz));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test --filter GridRotationTests`
Expected: compile error (`GridRotation` doesn't exist yet).

- [ ] **Step 3: Write `GridRotation.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public static class GridRotation
{
    public static (int WorldDx, int WorldDz) Rotate(int localDx, int localDz, string direction)
    {
        return direction switch
        {
            "North" => (localDx, localDz),
            "East" => (localDz, -localDx),
            "South" => (-localDx, -localDz),
            "West" => (-localDz, localDx),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Expected North, East, South, or West."),
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd backend && dotnet test --filter GridRotationTests`
Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/kry_2/Desktop/mappa trackmania/tm-track-studio"
git add backend/src/TmMapMaker.BlockCatalog/GridRotation.cs backend/tests/TmMapMaker.BlockCatalog.Tests/GridRotationTests.cs
git commit -m "feat: grid rotation utility for local-to-world connector offsets"
```

---

### Task 2: Shape hypothesis verifier

**Files:**
- Create: `backend/src/TmMapMaker.BlockCatalog/ShapeHypothesis.cs`
- Create: `backend/src/TmMapMaker.BlockCatalog/ShapeVerificationResult.cs`
- Create: `backend/src/TmMapMaker.BlockCatalog/ShapeHypothesisVerifier.cs`
- Test: `backend/tests/TmMapMaker.BlockCatalog.Tests/ShapeHypothesisVerifierTests.cs`

**Interfaces:**
- Consumes: `PlacedBlock`, `BlockFamily`, `PlacementType`, `GbxMapReader.ReadBlocks` (from the Block Catalog Reader plan), `GridRotation.Rotate` (Task 1).
- Produces:
  - `record ShapeHypothesis(string ShapeSuffix, int LocalForwardDx, int LocalForwardDz, int LocalForwardDy)` — a candidate: "a block whose name ends with `ShapeSuffix` should have a drivable neighbor at this local forward offset."
  - `record ShapeVerificationResult(string ShapeSuffix, int TotalOccurrences, int MatchCount, int MismatchCount, IReadOnlyList<string> ExampleMismatches)`.
  - `ShapeHypothesisVerifier.Verify(ShapeHypothesis hypothesis, IReadOnlyDictionary<string, IReadOnlyList<PlacedBlock>> blocksByMapFile) -> ShapeVerificationResult`.

**Shape name matching rule** (reused across this task and Task 3): a block's name is considered an instance of `ShapeSuffix` when `name.EndsWith(ShapeSuffix, StringComparison.Ordinal)` AND the name does not contain any of `"Tilt"`, `"Chicane"`, `"Diag"`, `"Loop"` (these are geometrically different variants that happen to share the suffix — e.g. `RoadTechTiltStraight` is not a plain `Straight`). Only consider blocks whose `Family` is `Road`, `Platform`, or `Gate` (the drivable families) and whose `Placement` is `Grid` (free-placed blocks are out of scope for this verifier — see the design spec's "Fuori scope v1").

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/TmMapMaker.BlockCatalog.Tests/ShapeHypothesisVerifierTests.cs`:

```csharp
using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class ShapeHypothesisVerifierTests
{
    private static PlacedBlock GridBlock(string name, BlockFamily family, int x, int y, int z, string direction) =>
        new(name, family, PlacementType.Grid, x, y, z, direction, null, null, null, null, null, null, 0, 0, false);

    [Fact]
    public void Verify_counts_a_match_when_expected_neighbor_exists()
    {
        // A Straight at (10, 5, 10) facing North, hypothesis says forward = (dx=0, dz=1).
        // A matching Straight neighbor sits at (10, 5, 11).
        var blocks = new List<PlacedBlock>
        {
            GridBlock("RoadTechStraight", BlockFamily.Road, 10, 5, 10, "North"),
            GridBlock("RoadTechStraight", BlockFamily.Road, 10, 5, 11, "North"),
        };
        var byMap = new Dictionary<string, IReadOnlyList<PlacedBlock>> { ["test.Map.Gbx"] = blocks };
        var hypothesis = new ShapeHypothesis("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0);

        var result = ShapeHypothesisVerifier.Verify(hypothesis, byMap);

        // Both blocks are independently valid candidates (each is itself a Straight-shaped
        // block in a drivable family). The first has a forward neighbor (the second block) and
        // matches; the second dead-ends at (10,5,12) with nothing there, so it mismatches. This
        // is expected and correct - the test still proves the match path works via the first block.
        Assert.Equal(2, result.TotalOccurrences);
        Assert.Equal(1, result.MatchCount);
        Assert.Equal(1, result.MismatchCount);
    }

    [Fact]
    public void Verify_counts_a_mismatch_when_no_neighbor_exists_at_hypothesized_offset()
    {
        var blocks = new List<PlacedBlock>
        {
            GridBlock("RoadTechStraight", BlockFamily.Road, 10, 5, 10, "North"),
        };
        var byMap = new Dictionary<string, IReadOnlyList<PlacedBlock>> { ["test.Map.Gbx"] = blocks };
        var hypothesis = new ShapeHypothesis("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0);

        var result = ShapeHypothesisVerifier.Verify(hypothesis, byMap);

        Assert.Equal(1, result.TotalOccurrences);
        Assert.Equal(0, result.MatchCount);
        Assert.Equal(1, result.MismatchCount);
        Assert.Single(result.ExampleMismatches);
    }

    [Fact]
    public void Verify_applies_rotation_before_checking_the_neighbor_cell()
    {
        // Same hypothesis (forward = dz+1 in local space), but this block faces East,
        // so the expected world neighbor is at dx+1 (per GridRotation), not dz+1.
        var blocks = new List<PlacedBlock>
        {
            GridBlock("RoadTechStraight", BlockFamily.Road, 10, 5, 10, "East"),
            GridBlock("RoadTechStraight", BlockFamily.Road, 11, 5, 10, "East"),
        };
        var byMap = new Dictionary<string, IReadOnlyList<PlacedBlock>> { ["test.Map.Gbx"] = blocks };
        var hypothesis = new ShapeHypothesis("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0);

        var result = ShapeHypothesisVerifier.Verify(hypothesis, byMap);

        // Same reasoning as the match test above: both blocks are independent candidates, and
        // the second one dead-ends since nothing sits at its own forward cell.
        Assert.Equal(2, result.TotalOccurrences);
        Assert.Equal(1, result.MatchCount);
        Assert.Equal(1, result.MismatchCount);
    }

    [Fact]
    public void Verify_ignores_Tilt_and_other_excluded_variants()
    {
        var blocks = new List<PlacedBlock>
        {
            GridBlock("RoadTechTiltStraight", BlockFamily.Road, 10, 5, 10, "North"),
        };
        var byMap = new Dictionary<string, IReadOnlyList<PlacedBlock>> { ["test.Map.Gbx"] = blocks };
        var hypothesis = new ShapeHypothesis("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0);

        var result = ShapeHypothesisVerifier.Verify(hypothesis, byMap);

        Assert.Equal(0, result.TotalOccurrences);
    }

    [Fact]
    public void Verify_ignores_non_drivable_families_as_the_source_block()
    {
        var blocks = new List<PlacedBlock>
        {
            GridBlock("DecoWallStraight", BlockFamily.Deco, 10, 5, 10, "North"),
        };
        var byMap = new Dictionary<string, IReadOnlyList<PlacedBlock>> { ["test.Map.Gbx"] = blocks };
        var hypothesis = new ShapeHypothesis("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0);

        var result = ShapeHypothesisVerifier.Verify(hypothesis, byMap);

        Assert.Equal(0, result.TotalOccurrences);
    }

    [Fact]
    public void Verify_accepts_any_drivable_family_as_the_neighbor_not_just_the_same_name()
    {
        // The neighbor doesn't have to be another Straight or even the same family -
        // any Road/Platform/Gate block continuing the path counts as a match.
        var blocks = new List<PlacedBlock>
        {
            GridBlock("RoadTechStraight", BlockFamily.Road, 10, 5, 10, "North"),
            GridBlock("PlatformTechCheckpoint", BlockFamily.Platform, 10, 5, 11, "North"),
        };
        var byMap = new Dictionary<string, IReadOnlyList<PlacedBlock>> { ["test.Map.Gbx"] = blocks };
        var hypothesis = new ShapeHypothesis("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0);

        var result = ShapeHypothesisVerifier.Verify(hypothesis, byMap);

        Assert.Equal(1, result.MatchCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test --filter ShapeHypothesisVerifierTests`
Expected: compile errors (`ShapeHypothesis`, `ShapeVerificationResult`, `ShapeHypothesisVerifier` don't exist yet).

- [ ] **Step 3: Write `ShapeHypothesis.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public sealed record ShapeHypothesis(string ShapeSuffix, int LocalForwardDx, int LocalForwardDz, int LocalForwardDy);
```

- [ ] **Step 4: Write `ShapeVerificationResult.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public sealed record ShapeVerificationResult(
    string ShapeSuffix,
    int TotalOccurrences,
    int MatchCount,
    int MismatchCount,
    IReadOnlyList<string> ExampleMismatches);
```

- [ ] **Step 5: Write `ShapeHypothesisVerifier.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public static class ShapeHypothesisVerifier
{
    private static readonly string[] ExcludedVariantMarkers = { "Tilt", "Chicane", "Diag", "Loop" };
    private static readonly BlockFamily[] DrivableFamilies = { BlockFamily.Road, BlockFamily.Platform, BlockFamily.Gate };

    public static bool IsPlainShapeInstance(string name, string shapeSuffix) =>
        name.EndsWith(shapeSuffix, StringComparison.Ordinal)
        && !ExcludedVariantMarkers.Any(marker => name.Contains(marker, StringComparison.Ordinal));

    public static ShapeVerificationResult Verify(
        ShapeHypothesis hypothesis,
        IReadOnlyDictionary<string, IReadOnlyList<PlacedBlock>> blocksByMapFile)
    {
        var totalOccurrences = 0;
        var matchCount = 0;
        var mismatchCount = 0;
        var exampleMismatches = new List<string>();

        foreach (var (mapFile, blocks) in blocksByMapFile)
        {
            var cellLookup = blocks
                .Where(b => b.Placement == PlacementType.Grid)
                .ToLookup(b => (b.GridX, b.GridY, b.GridZ));

            var candidates = blocks.Where(b =>
                b.Placement == PlacementType.Grid
                && DrivableFamilies.Contains(b.Family)
                && IsPlainShapeInstance(b.Name, hypothesis.ShapeSuffix));

            foreach (var block in candidates)
            {
                totalOccurrences++;

                var (worldDx, worldDz) = GridRotation.Rotate(hypothesis.LocalForwardDx, hypothesis.LocalForwardDz, block.Direction!);
                var expectedCell = (
                    GridX: (int?)(block.GridX! + worldDx),
                    GridY: (int?)(block.GridY! + hypothesis.LocalForwardDy),
                    GridZ: (int?)(block.GridZ! + worldDz));

                var neighborIsDrivable = cellLookup[expectedCell].Any(n => DrivableFamilies.Contains(n.Family));

                if (neighborIsDrivable)
                {
                    matchCount++;
                }
                else
                {
                    mismatchCount++;
                    if (exampleMismatches.Count < 5)
                        exampleMismatches.Add($"{mapFile}: {block.Name} @({block.GridX},{block.GridY},{block.GridZ}) dir={block.Direction}, expected neighbor at ({expectedCell.GridX},{expectedCell.GridY},{expectedCell.GridZ})");
                }
            }
        }

        return new ShapeVerificationResult(hypothesis.ShapeSuffix, totalOccurrences, matchCount, mismatchCount, exampleMismatches);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd backend && dotnet test --filter ShapeHypothesisVerifierTests`
Expected: all tests PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/src/TmMapMaker.BlockCatalog/ShapeHypothesis.cs backend/src/TmMapMaker.BlockCatalog/ShapeVerificationResult.cs backend/src/TmMapMaker.BlockCatalog/ShapeHypothesisVerifier.cs backend/tests/TmMapMaker.BlockCatalog.Tests/ShapeHypothesisVerifierTests.cs
git commit -m "feat: shape hypothesis verifier (checks candidate connector offsets against real block adjacency)"
```

---

### Task 3: Verification report CLI and evidence-gathering run

**Files:**
- Modify: `backend/src/TmMapMaker.BlockCatalog/Program.cs`
- Create: `backend/src/TmMapMaker.BlockCatalog/VerifyShapesCommand.cs`

**Interfaces:**
- Consumes: `GbxMapReader.ReadBlocks`, `ShapeHypothesis`, `ShapeHypothesisVerifier.Verify` (Task 2).
- Produces: `VerifyShapesCommand.Run(string inputDir) -> int` (exit code), printing one verification report line per hypothesis to the console.

This task does not modify any existing behavior of the current inventory CLI — it adds a second command reachable via a subcommand argument, and turns the existing inventory logic into its own path through the same dispatcher.

- [ ] **Step 1: Extract the existing inventory logic into its own method, add a dispatcher**

Read the current `backend/src/TmMapMaker.BlockCatalog/Program.cs` first — it currently has the inventory-scanning logic as top-level statements. Restructure it into:

```csharp
using TmMapMaker.BlockCatalog;

if (args.Length == 0)
{
    Console.WriteLine("uso: dotnet run -- inventory <cartella mappe .Map.Gbx> [cartella output JSON]");
    Console.WriteLine("     dotnet run -- verify-shapes <cartella mappe .Map.Gbx>");
    return 1;
}

return args[0] switch
{
    "inventory" => InventoryCommand.Run(args.Skip(1).ToArray()),
    "verify-shapes" => VerifyShapesCommand.Run(args.Length > 1 ? args[1] : ""),
    _ => PrintUnknownCommand(args[0]),
};

static int PrintUnknownCommand(string command)
{
    Console.WriteLine($"comando sconosciuto: {command} (usa 'inventory' o 'verify-shapes')");
    return 1;
}
```

Move the entire existing body of `Program.cs` (everything from the current `if (args.Length == 0)` check through the final `return failureCount > 0 ? 1 : 0;`) into a new file `backend/src/TmMapMaker.BlockCatalog/InventoryCommand.cs`, wrapped as:

```csharp
using System.Text.Json;

namespace TmMapMaker.BlockCatalog;

public static class InventoryCommand
{
    public static int Run(string[] args)
    {
        // <-- paste the existing Program.cs body here verbatim, with `args[0]` becoming
        // the input dir (since the "inventory" subcommand word is already consumed by
        // the dispatcher) and `args.Length > 1 ? args[1] : "inventory-output"` for output dir
        // adjust exactly as the current logic already does, just relocated into this method
        // with args.Length == 0 checked at the call site's dispatcher instead
    }
}
```

Keep every existing behavior identical (same Italian messages, same exit codes, same per-map try/catch) — this is a pure relocation, not a rewrite. After this step, run the existing smoke test to confirm nothing broke:

```bash
cd backend/src/TmMapMaker.BlockCatalog
dotnet run -- inventory "C:/Users/kry_2/Desktop/mappa trackmania/riferimenti" "C:/Users/kry_2/Desktop/mappa trackmania/tm-track-studio/backend/inventory-output"
```

Expected: same output as before (9 maps, exit code 0), just now invoked with the `inventory` subcommand prefix.

- [ ] **Step 2: Write `VerifyShapesCommand.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public static class VerifyShapesCommand
{
    // Candidate hypotheses for the v1 shape vocabulary. These are UNVERIFIED until this
    // command's report is reviewed by a human against the printed evidence - do not treat
    // these numbers as ground truth, they are starting guesses based on the standard TM2020
    // Stadium grid convention (32-unit cells, forward = +Z in local space before rotation).
    private static readonly ShapeHypothesis[] CandidateHypotheses =
    {
        new("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0),
        new("Curve1", LocalForwardDx: 1, LocalForwardDz: 0, LocalForwardDy: 0),
        new("Checkpoint", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0),
        new("Start", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0),
        new("Finish", LocalForwardDx: 0, LocalForwardDz: -1, LocalForwardDy: 0),
        new("Slope2Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 1),
        new("Slope2Up", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 1),
        new("Slope2Down", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: -1),
    };

    public static int Run(string inputDir)
    {
        if (string.IsNullOrWhiteSpace(inputDir) || !Directory.Exists(inputDir))
        {
            Console.WriteLine($"ERRORE: la cartella di input non esiste: {inputDir}");
            return 1;
        }

        var mapFiles = Directory.EnumerateFiles(inputDir, "*.Map.Gbx", SearchOption.AllDirectories).ToList();
        Console.WriteLine($"trovate {mapFiles.Count} mappe in {inputDir}");

        var blocksByMapFile = new Dictionary<string, IReadOnlyList<PlacedBlock>>();
        foreach (var mapFile in mapFiles)
        {
            try
            {
                blocksByMapFile[Path.GetFileName(mapFile)] = GbxMapReader.ReadBlocks(mapFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERRORE lettura {Path.GetFileName(mapFile)}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("-- VERIFICA IPOTESI FORME (da rivedere a mano prima di bloccare i valori) --");
        foreach (var hypothesis in CandidateHypotheses)
        {
            var result = ShapeHypothesisVerifier.Verify(hypothesis, blocksByMapFile);
            var verdict = result.TotalOccurrences == 0
                ? "NESSUNA OCCORRENZA (forma non trovata in queste mappe)"
                : result.MismatchCount == 0
                    ? "TUTTE CONFERMATE"
                    : $"{result.MismatchCount}/{result.TotalOccurrences} SENZA RISCONTRO";

            Console.WriteLine($"  {hypothesis.ShapeSuffix} (dx={hypothesis.LocalForwardDx},dy={hypothesis.LocalForwardDy},dz={hypothesis.LocalForwardDz}): {result.MatchCount}/{result.TotalOccurrences} confermate - {verdict}");
            foreach (var example in result.ExampleMismatches)
                Console.WriteLine($"      mismatch: {example}");
        }

        return 0;
    }
}
```

- [ ] **Step 3: Build and manually run the verification report**

```bash
cd backend
dotnet build
cd src/TmMapMaker.BlockCatalog
dotnet run -- verify-shapes "C:/Users/kry_2/Desktop/mappa trackmania/riferimenti"
```

Record the full console output. This is a **human review checkpoint, not an automated pass/fail** — read each hypothesis's match rate and example mismatches:
- A hypothesis with 0 occurrences means that plain shape name doesn't appear (grid-placed, in a drivable family, without excluded-variant markers) in any of the 9 reference maps — note it as "needs more reference maps" rather than guessing a replacement value.
- A hypothesis with matches but also mismatches needs judgment: mismatches can mean the hypothesis is wrong (try a different candidate offset — e.g. for a slope shape, try `LocalForwardDy` values of `-1`, `1`, `2`, `-2` and re-run to see which one raises the match rate), or can mean a genuine dead-end in that particular map (a Straight block placed right before a gap/jump) which is expected occasionally and not a sign the hypothesis is wrong if the match rate is still high (e.g. 90%+).
- A hypothesis with 100% match rate across a reasonable number of occurrences (double digits or more) is strong evidence the candidate offset is correct.

Write the findings to a new file `backend/docs/shape-verification-findings.md` (create the `backend/docs/` folder) summarizing, for each of the 8 v1 shapes: the final confirmed `(dx, dy, dz)` offset (adjusting from the candidate list above if evidence pointed to a different value), the match rate that supports it, and any shape still unconfirmed (0 occurrences) with a note that it's deferred until more reference maps are gathered. This file is the input to Task 4 — do not proceed to Task 4 until this file exists and every non-deferred shape has a confirmed offset backed by evidence.

- [ ] **Step 4: Commit**

```bash
git add backend/src/TmMapMaker.BlockCatalog/Program.cs backend/src/TmMapMaker.BlockCatalog/InventoryCommand.cs backend/src/TmMapMaker.BlockCatalog/VerifyShapesCommand.cs backend/docs/shape-verification-findings.md
git commit -m "feat: shape verification report CLI + evidence-gathering run against reference maps"
```

---

### Task 4: Lock the verified Shape Catalog

**Files:**
- Create: `backend/src/TmMapMaker.BlockCatalog/ShapeCatalog.cs`
- Test: `backend/tests/TmMapMaker.BlockCatalog.Tests/ShapeCatalogTests.cs`

**Interfaces:**
- Consumes: `backend/docs/shape-verification-findings.md` (Task 3's output — read it before writing this task's code, it contains the exact confirmed offsets to use).
- Produces: `record VerifiedShape(string ShapeSuffix, int Dx, int Dy, int Dz, int MatchCount, int TotalOccurrences)`, `ShapeCatalog.VerifiedShapes -> IReadOnlyList<VerifiedShape>` (a static, hard-coded list — this is intentional: these values are locked from Task 3's human-reviewed evidence, not recomputed at runtime).

**This task cannot be written with exact code in advance** — the confirmed `(dx, dy, dz)` values depend on Task 3's findings file, which does not exist until Task 3 runs. When implementing this task:

1. Read `backend/docs/shape-verification-findings.md`.
2. For every shape in the v1 vocabulary (`Start`, `Finish`, `Checkpoint`, `Straight`, `Curve1`, `Slope2Straight`, `Slope2Up`, `Slope2Down`) that has a confirmed offset in the findings file, add one `VerifiedShape` entry to `ShapeCatalog.VerifiedShapes` with that exact offset and the match/total counts from the findings.
3. For any shape marked "deferred" (0 occurrences, unconfirmed) in the findings file, do NOT add it to `ShapeCatalog.VerifiedShapes` — add a comment listing it as deferred with a one-line reason, matching the fail-loud principle (an unverified shape is absent from the catalog, not present with a guessed value).
4. Write one test per confirmed shape asserting `ShapeCatalog.VerifiedShapes` contains a `VerifiedShape` with that exact shape suffix and offset (copy the values straight from the findings file — do not recompute or re-derive them).
5. Write one test asserting that every deferred shape from the findings file is absent from `ShapeCatalog.VerifiedShapes` (by suffix).

- [ ] **Step 1: Read the findings file and confirm it's complete**

Run: read `backend/docs/shape-verification-findings.md`. If it's missing or doesn't cover all 8 v1 shapes (confirmed or explicitly deferred), STOP and report BLOCKED — Task 3 must be completed first.

- [ ] **Step 2: Write the failing tests**

Based on the findings file's actual content (exact test code depends on Task 3's output, per the note above), write `backend/tests/TmMapMaker.BlockCatalog.Tests/ShapeCatalogTests.cs` following this pattern for each confirmed shape (repeat per shape, substituting the real values from the findings file):

```csharp
using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class ShapeCatalogTests
{
    [Fact]
    public void VerifiedShapes_contains_Straight_with_confirmed_offset()
    {
        var shape = Assert.Single(ShapeCatalog.VerifiedShapes, s => s.ShapeSuffix == "Straight");
        // Replace these three values with the exact confirmed (Dx, Dy, Dz) from
        // backend/docs/shape-verification-findings.md - do not guess.
        Assert.Equal(0, shape.Dx);
        Assert.Equal(0, shape.Dy);
        Assert.Equal(1, shape.Dz);
    }

    // ... one such test per confirmed shape from the findings file ...

    [Fact]
    public void VerifiedShapes_excludes_deferred_shapes()
    {
        // Replace with the actual list of shapes the findings file marked deferred
        // (0 occurrences across all reference maps) - if the findings file has no
        // deferred shapes, delete this test instead of leaving it with an empty list.
        var deferredSuffixes = new[] { /* e.g. "Slope2Up" if it had 0 occurrences */ };
        foreach (var suffix in deferredSuffixes)
            Assert.DoesNotContain(ShapeCatalog.VerifiedShapes, s => s.ShapeSuffix == suffix);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd backend && dotnet test --filter ShapeCatalogTests`
Expected: compile error (`ShapeCatalog`, `VerifiedShape` don't exist yet).

- [ ] **Step 4: Write `VerifiedShape` record and `ShapeCatalog.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public sealed record VerifiedShape(string ShapeSuffix, int Dx, int Dy, int Dz, int MatchCount, int TotalOccurrences);

public static class ShapeCatalog
{
    // Every entry here was confirmed against real reference-map data by the
    // ShapeHypothesisVerifier (see backend/docs/shape-verification-findings.md for the
    // full evidence). Do not add an entry without that evidence trail - an unconfirmed
    // shape must stay out of this list (fail-loud), not be added with a guessed offset.
    public static readonly IReadOnlyList<VerifiedShape> VerifiedShapes = new List<VerifiedShape>
    {
        // Populate this list from backend/docs/shape-verification-findings.md - one
        // VerifiedShape per confirmed shape, with its exact Dx/Dy/Dz and match counts.
    };
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd backend && dotnet test --filter ShapeCatalogTests`
Expected: all tests PASS.

- [ ] **Step 6: Run the full test suite to confirm no regressions**

Run: `cd backend && dotnet test`
Expected: all tests pass (previous count plus the new `ShapeCatalogTests`).

- [ ] **Step 7: Commit**

```bash
git add backend/src/TmMapMaker.BlockCatalog/ShapeCatalog.cs backend/tests/TmMapMaker.BlockCatalog.Tests/ShapeCatalogTests.cs
git commit -m "feat: lock verified Shape Catalog from reference-map evidence"
```

---

## What this plan deliberately does NOT do

- Does not build the Path Compiler (percorso disegnato → blocchi) or Validator — those are future plans that will consume `ShapeCatalog.VerifiedShapes`.
- Does not cover shapes beyond the v1 vocabulary (Curve2/Curve3, chicanes, loops, diagonals, multi-cell blocks) — deliberately deferred per the design spec's scope.
- Does not handle free-placed blocks as verification sources — the verifier only checks grid-placed blocks (see design spec's "Fuori scope v1"). Free-placement connector handling is deferred to when the Path Compiler needs to emit free-placed blocks.
- Does not guarantee full coverage of the v1 vocabulary — if reference maps don't contain enough clean examples of a shape, Task 3's findings file will mark it deferred rather than the plan inventing a value. Gathering more reference maps to fill gaps is a follow-up, not part of this plan.
