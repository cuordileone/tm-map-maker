namespace TmMapMaker.BlockCatalog;

public sealed record VerifiedShape(string ShapeSuffix, int Dx, int Dy, int Dz, int MatchCount, int TotalOccurrences);

public static class ShapeCatalog
{
    // Every entry here must be confirmed against real reference-map data by the
    // ShapeHypothesisVerifier (see backend/docs/shape-verification-findings.md for the
    // full evidence). Do not add an entry without that evidence trail - an unconfirmed
    // shape must stay out of this list (fail-loud), not be added with a guessed offset.
    //
    // As of this commit, ALL 8 shapes in the v1 vocabulary (Start, Finish, Checkpoint,
    // Straight, Curve1, Slope2Straight, Slope2Up, Slope2Down) are deferred - none were
    // confirmed against the current 9-map reference corpus. This is a deliberate,
    // carefully-reviewed outcome (see the findings file's "Revision note" section): two
    // shapes that initially looked confirmable (Straight, Checkpoint) were walked back
    // after applying the fail-loud standard consistently - symmetric, non-directional
    // match patterns were correctly read as decorative mesh reuse rather than genuine
    // track-connector geometry. Unblocking this list needs (1) additional reference maps
    // with more plain, unambiguous track sections, and (2) likely a shape-matching
    // precision improvement (the current EndsWith-based suffix match pulls in unrelated
    // compound block names). Both points are documented in
    // backend/docs/shape-verification-findings.md.
    public static readonly IReadOnlyList<VerifiedShape> VerifiedShapes = new List<VerifiedShape>
    {
        // Intentionally empty - see comment above.
    };
}
