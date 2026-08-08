namespace TmMapMaker.BlockCatalog;

public sealed record ShapeVerificationResult(
    string ShapeSuffix,
    int TotalOccurrences,
    int MatchCount,
    int MismatchCount,
    IReadOnlyList<string> ExampleMismatches);
