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
