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
            .GroupBy(b => b.IsCustom ? "Custom" : b.Family.ToString())
            .ToDictionary(
                g => g.Key,
                g => new FamilyBreakdown(
                    g.Count(),
                    g.Select(b => b.Name).Distinct().OrderBy(n => n).ToList()));

        var unrecognized = blocks
            .Where(b => b.Family == BlockFamily.Unknown && !b.IsCustom)
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
