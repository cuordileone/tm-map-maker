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
