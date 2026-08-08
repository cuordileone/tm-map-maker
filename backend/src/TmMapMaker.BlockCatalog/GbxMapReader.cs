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
