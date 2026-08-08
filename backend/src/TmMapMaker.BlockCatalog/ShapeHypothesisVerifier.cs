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
