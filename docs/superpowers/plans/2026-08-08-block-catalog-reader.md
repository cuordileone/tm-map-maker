# Block Catalog Reader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an offline C# CLI tool that reads real TM2020 Stadium reference maps (`.Map.Gbx`) via GBX.NET, extracts every placed block (grid-placed and free-placed) with its position/rotation, classifies each into a known Stadium block family, and flags any block name it doesn't recognize — producing one JSON inventory report per map. This is the ground-truth data layer the future Block Catalog (footprint/connector extraction) and everything downstream (Path Compiler, Validator) will be built on.

**Architecture:** A single C# console tool (`TmMapMaker.BlockCatalog`) wraps GBX.NET to parse `.Map.Gbx` files into a normalized `PlacedBlock` list, a pure `BlockNameClassifier` sorts each block into a `BlockFamily` (or flags it unrecognized), and `MapInventoryReport` aggregates the result into JSON. No web/backend/frontend code in this plan — this is a standalone, independently testable tool.

**Tech Stack:** .NET 8, C#, GBX.NET 2.4.3 (+ GBX.NET.LZO 2.1.5), xUnit, System.Text.Json.

## Global Constraints

- Fail-loud, never guess: any block name that isn't a recognized custom-block pattern and doesn't match a known family prefix must be reported in `UnrecognizedNames`, never silently classified or dropped. This is the specific failure mode that corrupted the previous (retired) Python pipeline — do not reintroduce it.
- Must correctly handle **both** grid-placed blocks (`Coord`/`Direction`) and free-placed blocks (`AbsolutePositionInMap`/`YawPitchRoll`) — verified necessary because real reference maps place the majority of blocks FREE (see spec, section "Rischi noti").
- Must explicitly exclude custom/modded blocks (community item-blocks, identifiable by `\` in the name or a `CustomBlock` suffix) from family classification — v1 scope is official Nadeo Stadium blocks only.
- All code and comments in English (identifiers/code), console/CLI output in Italian (matches the existing `analizzatore` tool's convention in this project).

---

## File Structure

```
tm-map-maker/
  backend/
    TmMapMaker.sln
    src/
      TmMapMaker.BlockCatalog/
        TmMapMaker.BlockCatalog.csproj
        BlockFamily.cs            <- enum of recognized Stadium block families
        PlacedBlock.cs            <- normalized record: one block placement (grid or free)
        BlockNameClassifier.cs    <- pure functions: IsCustomBlock, ClassifyFamily
        GbxMapReader.cs           <- GBX.NET wrapper: .Map.Gbx -> IReadOnlyList<PlacedBlock>
        MapInventoryReport.cs     <- aggregates PlacedBlocks -> JSON-serializable report
        Program.cs                <- CLI entry point
    tests/
      TmMapMaker.BlockCatalog.Tests/
        TmMapMaker.BlockCatalog.Tests.csproj
        BlockNameClassifierTests.cs
        GbxMapReaderTests.cs
        MapInventoryReportTests.cs
        TestData/
          R_g Avatar.Map.Gbx      <- committed reference fixture (388 blocks, clean, no custom blocks)
```

Reference fixture source: `C:\Users\kry_2\Desktop\mappa trackmania\riferimenti\R_g Avatar.Map.Gbx`. It was chosen because it's the smallest available reference map (~660KB), has zero custom/modded blocks, and is 72% free-placed blocks (280 of 388) — it exercises the free-placement path that the old grid-only tracer couldn't handle.

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `backend/TmMapMaker.sln`
- Create: `backend/src/TmMapMaker.BlockCatalog/TmMapMaker.BlockCatalog.csproj`
- Create: `backend/tests/TmMapMaker.BlockCatalog.Tests/TmMapMaker.BlockCatalog.Tests.csproj`

**Interfaces:**
- Produces: two buildable, empty .NET 8 projects wired into one solution, with GBX.NET referenced by the main project and the test project referencing the main project.

- [ ] **Step 1: Scaffold the solution and both projects**

Run from `C:\Users\kry_2\Desktop\mappa trackmania\tm-track-studio`:

```bash
mkdir -p backend/src/TmMapMaker.BlockCatalog backend/tests/TmMapMaker.BlockCatalog.Tests
cd backend
dotnet new sln -n TmMapMaker
cd src/TmMapMaker.BlockCatalog
dotnet new console -n TmMapMaker.BlockCatalog -o .
dotnet add package GBX.NET --version 2.4.3
dotnet add package GBX.NET.LZO --version 2.1.5
cd ../../tests/TmMapMaker.BlockCatalog.Tests
dotnet new xunit -o .
dotnet add reference ../../src/TmMapMaker.BlockCatalog/TmMapMaker.BlockCatalog.csproj
cd ../..
dotnet sln add src/TmMapMaker.BlockCatalog/TmMapMaker.BlockCatalog.csproj
dotnet sln add tests/TmMapMaker.BlockCatalog.Tests/TmMapMaker.BlockCatalog.Tests.csproj
```

- [ ] **Step 2: Silence the GBX.NET experimental-API warning**

Edit `backend/src/TmMapMaker.BlockCatalog/TmMapMaker.BlockCatalog.csproj`, add inside the existing `<PropertyGroup>`:

```xml
<NoWarn>$(NoWarn);GBXNET10001</NoWarn>
```

- [ ] **Step 3: Verify the solution builds**

Run: `cd backend && dotnet build`
Expected: `Build succeeded.` with 0 errors (the default `Program.cs` from the console template and the default sample test from the xUnit template are both present and compile).

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/kry_2/Desktop/mappa trackmania/tm-track-studio"
git add backend
git commit -m "chore: scaffold TmMapMaker.BlockCatalog solution (GBX.NET, xUnit)"
```

---

### Task 2: Block family classifier

**Files:**
- Create: `backend/src/TmMapMaker.BlockCatalog/BlockFamily.cs`
- Create: `backend/src/TmMapMaker.BlockCatalog/BlockNameClassifier.cs`
- Test: `backend/tests/TmMapMaker.BlockCatalog.Tests/BlockNameClassifierTests.cs`

**Interfaces:**
- Produces: `enum BlockFamily { Road, Platform, Gate, Deco, Structure, Technics, Canopy, Stage, TrackWall, Water, Grass, Unknown }`, `BlockNameClassifier.IsCustomBlock(string name) -> bool`, `BlockNameClassifier.ClassifyFamily(string name) -> BlockFamily`.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/TmMapMaker.BlockCatalog.Tests/BlockNameClassifierTests.cs`:

```csharp
using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class BlockNameClassifierTests
{
    [Theory]
    [InlineData("RoadTechStart", BlockFamily.Road)]
    [InlineData("PlatformTechCheckpoint", BlockFamily.Platform)]
    [InlineData("GateFinish", BlockFamily.Gate)]
    [InlineData("DecoWallBasePillar", BlockFamily.Deco)]
    [InlineData("StructurePillar", BlockFamily.Structure)]
    [InlineData("TechnicsScreen1x1Straight", BlockFamily.Technics)]
    [InlineData("StageStructureStraight", BlockFamily.Stage)]
    [InlineData("TrackWall4mA", BlockFamily.TrackWall)]
    [InlineData("CanopyCenterFlatBase", BlockFamily.Canopy)]
    public void ClassifyFamily_returns_expected_family_for_known_prefixes(string name, BlockFamily expected)
    {
        Assert.Equal(expected, BlockNameClassifier.ClassifyFamily(name));
    }

    [Fact]
    public void ClassifyFamily_returns_Unknown_for_unrecognized_prefix()
    {
        Assert.Equal(BlockFamily.Unknown, BlockNameClassifier.ClassifyFamily("SomeNewBlockTypeNobodyHasSeen"));
    }

    [Theory]
    [InlineData("2-Weird\\zMiniSets\\PlatformHoles\\PlatformSlopeWithHole24m.Block.Gbx_CustomBlock", true)]
    [InlineData("A-BlockGBX\\Magnet2\\M2_PlatformTechBaseFlip.Block.Gbx_CustomBlock", true)]
    [InlineData("RoadTechStart", false)]
    [InlineData("PlatformTechBase", false)]
    public void IsCustomBlock_detects_modded_block_names(string name, bool expected)
    {
        Assert.Equal(expected, BlockNameClassifier.IsCustomBlock(name));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test --filter BlockNameClassifierTests`
Expected: compile error (`BlockFamily`/`BlockNameClassifier` don't exist yet) or test failures — either confirms the code under test doesn't exist yet.

- [ ] **Step 3: Write `BlockFamily.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public enum BlockFamily
{
    Road,
    Platform,
    Gate,
    Deco,
    Structure,
    Technics,
    Canopy,
    Stage,
    TrackWall,
    Water,
    Grass,
    Unknown
}
```

- [ ] **Step 4: Write `BlockNameClassifier.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public static class BlockNameClassifier
{
    // Ordered longest-prefix-first so no shorter prefix can shadow a longer, more specific one.
    private static readonly (string Prefix, BlockFamily Family)[] FamiliesByPrefixLengthDesc =
    {
        ("TrackWall", BlockFamily.TrackWall),
        ("Structure", BlockFamily.Structure),
        ("Technics", BlockFamily.Technics),
        ("Platform", BlockFamily.Platform),
        ("Canopy", BlockFamily.Canopy),
        ("Stage", BlockFamily.Stage),
        ("Water", BlockFamily.Water),
        ("Grass", BlockFamily.Grass),
        ("Road", BlockFamily.Road),
        ("Gate", BlockFamily.Gate),
        ("Deco", BlockFamily.Deco),
    };

    public static bool IsCustomBlock(string name) =>
        name.Contains('\\') || name.Contains("CustomBlock");

    public static BlockFamily ClassifyFamily(string name)
    {
        foreach (var (prefix, family) in FamiliesByPrefixLengthDesc)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return family;
        }
        return BlockFamily.Unknown;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd backend && dotnet test --filter BlockNameClassifierTests`
Expected: all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/TmMapMaker.BlockCatalog/BlockFamily.cs backend/src/TmMapMaker.BlockCatalog/BlockNameClassifier.cs backend/tests/TmMapMaker.BlockCatalog.Tests/BlockNameClassifierTests.cs
git commit -m "feat: block name classifier (family + custom-block detection)"
```

---

### Task 3: GBX.NET map reader

**Files:**
- Create: `backend/src/TmMapMaker.BlockCatalog/PlacedBlock.cs`
- Create: `backend/src/TmMapMaker.BlockCatalog/GbxMapReader.cs`
- Test: `backend/tests/TmMapMaker.BlockCatalog.Tests/GbxMapReaderTests.cs`
- Add: `backend/tests/TmMapMaker.BlockCatalog.Tests/TestData/R_g Avatar.Map.Gbx` (copied fixture)
- Modify: `backend/tests/TmMapMaker.BlockCatalog.Tests/TmMapMaker.BlockCatalog.Tests.csproj`

**Interfaces:**
- Consumes: `BlockNameClassifier.IsCustomBlock`, `BlockNameClassifier.ClassifyFamily` (Task 2).
- Produces: `enum PlacementType { Grid, Free }`, `record PlacedBlock(string Name, BlockFamily Family, PlacementType Placement, int? GridX, int? GridY, int? GridZ, string? Direction, float? WorldX, float? WorldY, float? WorldZ, float? YawRad, float? PitchRad, float? RollRad, int Variant, int SubVariant)`, `GbxMapReader.ReadBlocks(string mapFilePath) -> IReadOnlyList<PlacedBlock>`.

- [ ] **Step 1: Copy the reference fixture into the test project**

```bash
cp "C:/Users/kry_2/Desktop/mappa trackmania/riferimenti/R_g Avatar.Map.Gbx" "C:/Users/kry_2/Desktop/mappa trackmania/tm-track-studio/backend/tests/TmMapMaker.BlockCatalog.Tests/TestData/R_g Avatar.Map.Gbx"
```

(Create the `TestData` folder first if `cp` doesn't create it: `mkdir -p "C:/Users/kry_2/Desktop/mappa trackmania/tm-track-studio/backend/tests/TmMapMaker.BlockCatalog.Tests/TestData"`.)

- [ ] **Step 2: Make the fixture copy to the test output directory on build**

Edit `backend/tests/TmMapMaker.BlockCatalog.Tests/TmMapMaker.BlockCatalog.Tests.csproj`, add a new `<ItemGroup>`:

```xml
<ItemGroup>
  <None Include="TestData\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 3: Write the failing tests**

Create `backend/tests/TmMapMaker.BlockCatalog.Tests/GbxMapReaderTests.cs`:

```csharp
using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class GbxMapReaderTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "R_g Avatar.Map.Gbx");

    [Fact]
    public void ReadBlocks_returns_all_blocks_from_reference_map()
    {
        var blocks = GbxMapReader.ReadBlocks(FixturePath);
        Assert.Equal(388, blocks.Count);
    }

    [Fact]
    public void ReadBlocks_splits_grid_and_free_placements_correctly()
    {
        var blocks = GbxMapReader.ReadBlocks(FixturePath);
        Assert.Equal(108, blocks.Count(b => b.Placement == PlacementType.Grid));
        Assert.Equal(280, blocks.Count(b => b.Placement == PlacementType.Free));
    }

    [Fact]
    public void ReadBlocks_classifies_families_matching_verified_reference_counts()
    {
        var blocks = GbxMapReader.ReadBlocks(FixturePath);
        Assert.Equal(156, blocks.Count(b => b.Family == BlockFamily.Deco));
        Assert.Equal(2, blocks.Count(b => b.Family == BlockFamily.Gate));
        Assert.Equal(191, blocks.Count(b => b.Family == BlockFamily.Platform));
        Assert.Equal(10, blocks.Count(b => b.Family == BlockFamily.Road));
        Assert.Equal(8, blocks.Count(b => b.Family == BlockFamily.Structure));
        Assert.Equal(21, blocks.Count(b => b.Family == BlockFamily.Technics));
        Assert.Equal(0, blocks.Count(b => b.Family == BlockFamily.Unknown));
    }

    [Fact]
    public void ReadBlocks_reads_grid_coordinates_and_direction_for_a_known_block()
    {
        var blocks = GbxMapReader.ReadBlocks(FixturePath);
        var start = Assert.Single(blocks, b => b.Name == "RoadTechStart" && b.Placement == PlacementType.Grid);
        Assert.Equal(32, start.GridX);
        Assert.Equal(32, start.GridY);
        Assert.Equal(31, start.GridZ);
        Assert.Equal("East", start.Direction);
    }

    [Fact]
    public void ReadBlocks_reads_world_position_and_rotation_for_free_blocks()
    {
        var blocks = GbxMapReader.ReadBlocks(FixturePath);
        var freeBlock = blocks.First(b => b.Placement == PlacementType.Free);
        Assert.NotNull(freeBlock.WorldX);
        Assert.NotNull(freeBlock.WorldY);
        Assert.NotNull(freeBlock.WorldZ);
        Assert.NotNull(freeBlock.YawRad);
        Assert.Null(freeBlock.GridX);
        Assert.Null(freeBlock.Direction);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `cd backend && dotnet test --filter GbxMapReaderTests`
Expected: compile error (`PlacedBlock`/`GbxMapReader`/`PlacementType` don't exist yet).

- [ ] **Step 5: Write `PlacedBlock.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public enum PlacementType { Grid, Free }

public sealed record PlacedBlock(
    string Name,
    BlockFamily Family,
    PlacementType Placement,
    int? GridX,
    int? GridY,
    int? GridZ,
    string? Direction,
    float? WorldX,
    float? WorldY,
    float? WorldZ,
    float? YawRad,
    float? PitchRad,
    float? RollRad,
    int Variant,
    int SubVariant);
```

- [ ] **Step 6: Write `GbxMapReader.cs`**

```csharp
using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;

namespace TmMapMaker.BlockCatalog;

public static class GbxMapReader
{
    private static bool _lzoInitialized;

    public static IReadOnlyList<PlacedBlock> ReadBlocks(string mapFilePath)
    {
        EnsureLzoInitialized();
        var map = Gbx.ParseNode<CGameCtnChallenge>(mapFilePath);
        var blocks = map.Blocks ?? new List<CGameCtnBlock>();
        var result = new List<PlacedBlock>(blocks.Count);

        foreach (var b in blocks)
        {
            var family = BlockNameClassifier.IsCustomBlock(b.Name)
                ? BlockFamily.Unknown
                : BlockNameClassifier.ClassifyFamily(b.Name);

            result.Add(b.IsFree
                ? new PlacedBlock(
                    Name: b.Name,
                    Family: family,
                    Placement: PlacementType.Free,
                    GridX: null, GridY: null, GridZ: null, Direction: null,
                    WorldX: b.AbsolutePositionInMap?.X,
                    WorldY: b.AbsolutePositionInMap?.Y,
                    WorldZ: b.AbsolutePositionInMap?.Z,
                    YawRad: b.YawPitchRoll?.X,
                    PitchRad: b.YawPitchRoll?.Y,
                    RollRad: b.YawPitchRoll?.Z,
                    Variant: b.Variant,
                    SubVariant: b.SubVariant)
                : new PlacedBlock(
                    Name: b.Name,
                    Family: family,
                    Placement: PlacementType.Grid,
                    GridX: b.Coord.X, GridY: b.Coord.Y, GridZ: b.Coord.Z,
                    Direction: b.Direction.ToString(),
                    WorldX: null, WorldY: null, WorldZ: null,
                    YawRad: null, PitchRad: null, RollRad: null,
                    Variant: b.Variant,
                    SubVariant: b.SubVariant));
        }

        return result;
    }

    private static void EnsureLzoInitialized()
    {
        if (_lzoInitialized) return;
        Gbx.LZO = new Lzo();
        _lzoInitialized = true;
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd backend && dotnet test --filter GbxMapReaderTests`
Expected: all tests PASS.

- [ ] **Step 8: Commit**

```bash
git add backend/src/TmMapMaker.BlockCatalog/PlacedBlock.cs backend/src/TmMapMaker.BlockCatalog/GbxMapReader.cs backend/tests/TmMapMaker.BlockCatalog.Tests/GbxMapReaderTests.cs "backend/tests/TmMapMaker.BlockCatalog.Tests/TestData/R_g Avatar.Map.Gbx" backend/tests/TmMapMaker.BlockCatalog.Tests/TmMapMaker.BlockCatalog.Tests.csproj
git commit -m "feat: GBX.NET map reader, verified against real reference map (grid + free blocks)"
```

---

### Task 4: Inventory report, JSON output, and CLI

**Files:**
- Create: `backend/src/TmMapMaker.BlockCatalog/MapInventoryReport.cs`
- Modify: `backend/src/TmMapMaker.BlockCatalog/Program.cs`
- Test: `backend/tests/TmMapMaker.BlockCatalog.Tests/MapInventoryReportTests.cs`

**Interfaces:**
- Consumes: `PlacedBlock`, `BlockFamily`, `PlacementType`, `BlockNameClassifier.IsCustomBlock` (Tasks 2–3).
- Produces: `record FamilyBreakdown(int Count, IReadOnlyList<string> DistinctNames)`, `record MapInventoryReport(string MapFile, int TotalBlocks, int GridBlocks, int FreeBlocks, IReadOnlyList<string> UnrecognizedNames, IReadOnlyDictionary<string, FamilyBreakdown> Families)`, `MapInventoryReport.From(string mapFilePath, IReadOnlyList<PlacedBlock> blocks) -> MapInventoryReport`.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/TmMapMaker.BlockCatalog.Tests/MapInventoryReportTests.cs`:

```csharp
using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class MapInventoryReportTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "R_g Avatar.Map.Gbx");

    [Fact]
    public void From_builds_report_with_no_unrecognized_names_for_clean_reference_map()
    {
        var blocks = GbxMapReader.ReadBlocks(FixturePath);
        var report = MapInventoryReport.From(FixturePath, blocks);

        Assert.Empty(report.UnrecognizedNames);
        Assert.Equal(388, report.TotalBlocks);
        Assert.Equal(108, report.GridBlocks);
        Assert.Equal(280, report.FreeBlocks);
        Assert.Equal(191, report.Families["Platform"].Count);
        Assert.Equal(17, report.Families["Platform"].DistinctNames.Count);
    }

    [Fact]
    public void From_flags_unrecognized_official_looking_block_names()
    {
        var blocks = new List<PlacedBlock>
        {
            new("TotallyNewBlockType", BlockFamily.Unknown, PlacementType.Grid,
                0, 0, 0, "North", null, null, null, null, null, null, 0, 0)
        };

        var report = MapInventoryReport.From("fake.Map.Gbx", blocks);

        Assert.Single(report.UnrecognizedNames);
        Assert.Equal("TotallyNewBlockType", report.UnrecognizedNames[0]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test --filter MapInventoryReportTests`
Expected: compile error (`MapInventoryReport` doesn't exist yet).

- [ ] **Step 3: Write `MapInventoryReport.cs`**

```csharp
namespace TmMapMaker.BlockCatalog;

public sealed record FamilyBreakdown(int Count, IReadOnlyList<string> DistinctNames);

public sealed record MapInventoryReport(
    string MapFile,
    int TotalBlocks,
    int GridBlocks,
    int FreeBlocks,
    IReadOnlyList<string> UnrecognizedNames,
    IReadOnlyDictionary<string, FamilyBreakdown> Families)
{
    public static MapInventoryReport From(string mapFilePath, IReadOnlyList<PlacedBlock> blocks)
    {
        var families = blocks
            .GroupBy(b => b.Family)
            .ToDictionary(
                g => g.Key.ToString(),
                g => new FamilyBreakdown(
                    g.Count(),
                    g.Select(b => b.Name).Distinct().OrderBy(n => n).ToList()));

        var unrecognized = blocks
            .Where(b => b.Family == BlockFamily.Unknown && !BlockNameClassifier.IsCustomBlock(b.Name))
            .Select(b => b.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        return new MapInventoryReport(
            MapFile: Path.GetFileName(mapFilePath),
            TotalBlocks: blocks.Count,
            GridBlocks: blocks.Count(b => b.Placement == PlacementType.Grid),
            FreeBlocks: blocks.Count(b => b.Placement == PlacementType.Free),
            UnrecognizedNames: unrecognized,
            Families: families);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd backend && dotnet test --filter MapInventoryReportTests`
Expected: all tests PASS.

- [ ] **Step 5: Write the CLI (`Program.cs`)**

Replace the contents of `backend/src/TmMapMaker.BlockCatalog/Program.cs`:

```csharp
using System.Text.Json;
using TmMapMaker.BlockCatalog;

if (args.Length == 0)
{
    Console.WriteLine("uso: dotnet run -- <cartella mappe .Map.Gbx> [cartella output JSON]");
    return 1;
}

var inputDir = args[0];
var outputDir = args.Length > 1 ? args[1] : "inventory-output";
Directory.CreateDirectory(outputDir);

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
var mapFiles = Directory.EnumerateFiles(inputDir, "*.Map.Gbx", SearchOption.AllDirectories).ToList();

Console.WriteLine($"trovate {mapFiles.Count} mappe in {inputDir}");

foreach (var mapFile in mapFiles)
{
    try
    {
        var blocks = GbxMapReader.ReadBlocks(mapFile);
        var report = MapInventoryReport.From(mapFile, blocks);

        var outFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(mapFile) + ".inventory.json");
        File.WriteAllText(outFile, JsonSerializer.Serialize(report, jsonOptions));

        var flag = report.UnrecognizedNames.Count > 0
            ? $"  ATTENZIONE: {report.UnrecognizedNames.Count} nomi non riconosciuti"
            : "";
        Console.WriteLine($"  OK {Path.GetFileName(mapFile)}: {report.TotalBlocks} blocchi ({report.GridBlocks} griglia, {report.FreeBlocks} free){flag}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ERRORE {Path.GetFileName(mapFile)}: {ex.Message}");
    }
}

return 0;
```

- [ ] **Step 6: Manual smoke test against all real reference maps**

Run:

```bash
cd "C:/Users/kry_2/Desktop/mappa trackmania/tm-track-studio/backend/src/TmMapMaker.BlockCatalog"
dotnet run -- "C:/Users/kry_2/Desktop/mappa trackmania/riferimenti" "C:/Users/kry_2/Desktop/mappa trackmania/tm-track-studio/backend/inventory-output"
```

Expected: one line per map (9 maps: Alpha Valley 1, Aram, Jeskai, Mile Zero, R_g Avatar, `[FS] Cliffhanger`, `[MiniFS] First`, `weekly5/5 - FLOAT`, `weekly5/5 - spin`), each showing block counts, and one `.inventory.json` file per map in `backend/inventory-output/`. Read the ATTENZIONE lines (if any) — any map reporting unrecognized names needs manual review before its blocks are trusted in the future Block Catalog; this is expected and not a bug (e.g. `Jeskai.Map.Gbx` is known to contain custom/modded blocks, which should show as 0 unrecognized since they're correctly excluded rather than misclassified — verify this specifically for Jeskai's output).

- [ ] **Step 7: Commit**

```bash
git add backend/src/TmMapMaker.BlockCatalog/MapInventoryReport.cs backend/src/TmMapMaker.BlockCatalog/Program.cs backend/tests/TmMapMaker.BlockCatalog.Tests/MapInventoryReportTests.cs
git commit -m "feat: map inventory report (JSON) and CLI entry point"
```

(Do not commit `backend/inventory-output/` — it's generated output, not source. Add `backend/inventory-output/` to `backend/.gitignore` — create that file with the single line `inventory-output/` if it doesn't exist yet — before this commit.)

---

## What this plan deliberately does NOT do

- Does not infer block footprint/connector geometry (which blocks physically connect to which, in what relative offset) — that's the next plan, built on top of this reader's verified output.
- Does not persist per-block position/rotation in the JSON inventory report — `MapInventoryReport` is a summary/audit artifact (counts + distinct names per family) for reviewing family/style coverage across reference maps, not a full data dump. The full per-block data (grid coords, world position, yaw/pitch/roll) already exists in `PlacedBlock` and `GbxMapReader.ReadBlocks(...)` — the next plan (footprint/connector extraction) should call that C# API in-process, not re-parse the JSON files. Flagged by final review as a plan-clarity gap (the Goal section says the JSON is "the ground-truth data layer" — that's true of the reader's in-memory output, not the JSON specifically); resolved this way by the controller while the project owner was away, worth a quick confirmation on return.
- Does not write `.Map.Gbx` files (Export Service) — separate future plan.
- Does not touch the frontend, backend web API, accounts, or gallery — separate future plans per the design spec's subsystem breakdown.
- Does not fix the `analizzatore` tool's remaining path-tracing limitations (already noted as a known architectural gap in the design spec) — this plan builds a clean reader from scratch instead of reusing that tool's flawed BFS tracer.
