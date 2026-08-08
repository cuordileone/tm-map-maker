using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;

namespace TmMapMaker.BlockCatalog;

public static class GbxMapReader
{
    private static readonly bool _lzoInitialized = InitializeLzo();

    // Explicit static constructor removes the "beforefieldinit" flag, which guarantees
    // the CLR runs the type initializer (and therefore the _lzoInitialized field
    // initializer) before the first call to ReadBlocks, not just before the first
    // access to a static field. Without this, the field initializer's timing relative
    // to ReadBlocks would be unspecified.
    static GbxMapReader()
    {
    }

    private static bool InitializeLzo()
    {
        Gbx.LZO = new Lzo();
        return true;
    }

    public static IReadOnlyList<PlacedBlock> ReadBlocks(string mapFilePath)
    {
        var map = Gbx.ParseNode<CGameCtnChallenge>(mapFilePath);
        var blocks = map.Blocks ?? new List<CGameCtnBlock>();
        var result = new List<PlacedBlock>(blocks.Count);

        foreach (var b in blocks)
        {
            var isCustom = BlockNameClassifier.IsCustomBlock(b.Name);
            var family = isCustom
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
                    SubVariant: b.SubVariant,
                    IsCustom: isCustom)
                : new PlacedBlock(
                    Name: b.Name,
                    Family: family,
                    Placement: PlacementType.Grid,
                    GridX: b.Coord.X, GridY: b.Coord.Y, GridZ: b.Coord.Z,
                    Direction: b.Direction.ToString(),
                    WorldX: null, WorldY: null, WorldZ: null,
                    YawRad: null, PitchRad: null, RollRad: null,
                    Variant: b.Variant,
                    SubVariant: b.SubVariant,
                    IsCustom: isCustom));
        }

        return result;
    }
}
