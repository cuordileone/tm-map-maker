using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class ShapeCatalogTests
{
    [Fact]
    public void VerifiedShapes_is_empty_pending_further_reference_data()
    {
        // As of this commit, 0 of the 8 v1 vocabulary shapes (Start, Finish, Checkpoint,
        // Straight, Curve1, Slope2Straight, Slope2Up, Slope2Down) were confirmed against
        // the current 9-map reference corpus - see
        // backend/docs/shape-verification-findings.md for the full per-shape evidence and
        // the "Revision note" explaining why two initially-promising shapes (Straight,
        // Checkpoint) were walked back to deferred. This is the correct, fail-loud state:
        // an unconfirmed shape must be absent from the catalog, not present with a
        // guessed offset. This test should stay green until a future task adds real
        // confirmed entries backed by updated findings.
        Assert.Empty(ShapeCatalog.VerifiedShapes);
    }

    [Fact]
    public void VerifiedShapes_excludes_all_deferred_v1_shape_suffixes()
    {
        // Regression guard for the fail-loud property this project depends on: if someone
        // later adds an unverified shape to ShapeCatalog.VerifiedShapes without also
        // updating this test (and without a corresponding confirmed entry in
        // backend/docs/shape-verification-findings.md), this test fails loudly instead of
        // silently letting a guessed offset slip into the catalog.
        var deferredSuffixes = new[]
        {
            "Start",
            "Finish",
            "Checkpoint",
            "Straight",
            "Curve1",
            "Slope2Straight",
            "Slope2Up",
            "Slope2Down",
        };

        foreach (var suffix in deferredSuffixes)
            Assert.DoesNotContain(ShapeCatalog.VerifiedShapes, s => s.ShapeSuffix == suffix);
    }
}
