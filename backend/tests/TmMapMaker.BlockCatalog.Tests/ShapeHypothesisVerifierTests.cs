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
