using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class MapInventoryReportTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "R_g Avatar.Map.Gbx");

    [Fact]
    public void From_builds_report_with_no_unrecognized_names_for_clean_reference_map()
    {
        var blocks = GbxMapReader.ReadBlocks(FixturePath);
        var report = MapInventoryReport.From(FixturePath, blocks);

        Assert.Empty(report.UnrecognizedNames);
        Assert.Equal(388, report.TotalBlocks);
        Assert.Equal(108, report.GridBlocks);
        Assert.Equal(280, report.FreeBlocks);
        Assert.Equal(191, report.Families["Platform"].Count);
        Assert.Equal(17, report.Families["Platform"].DistinctNames.Count);
    }

    [Fact]
    public void From_flags_unrecognized_official_looking_block_names()
    {
        var blocks = new List<PlacedBlock>
        {
            new("TotallyNewBlockType", BlockFamily.Unknown, PlacementType.Grid,
                0, 0, 0, "North", null, null, null, null, null, null, 0, 0, false)
        };

        var report = MapInventoryReport.From("fake.Map.Gbx", blocks);

        Assert.Single(report.UnrecognizedNames);
        Assert.Equal("TotallyNewBlockType", report.UnrecognizedNames[0]);
    }

    [Fact]
    public void From_separates_custom_blocks_from_genuinely_unrecognized_blocks()
    {
        var blocks = new List<PlacedBlock>
        {
            new("Skin\\CustomBlockFoo", BlockFamily.Unknown, PlacementType.Grid,
                0, 0, 0, "North", null, null, null, null, null, null, 0, 0, true),
            new("TotallyNewBlockType", BlockFamily.Unknown, PlacementType.Grid,
                1, 1, 1, "North", null, null, null, null, null, null, 0, 0, false)
        };

        var report = MapInventoryReport.From("fake.Map.Gbx", blocks);

        Assert.True(report.Families.ContainsKey("Custom"));
        Assert.True(report.Families.ContainsKey("Unknown"));
        Assert.Equal(1, report.Families["Custom"].Count);
        Assert.Equal(1, report.Families["Unknown"].Count);
        Assert.Contains("Skin\\CustomBlockFoo", report.Families["Custom"].DistinctNames);
        Assert.Contains("TotallyNewBlockType", report.Families["Unknown"].DistinctNames);
        Assert.Single(report.UnrecognizedNames);
        Assert.Equal("TotallyNewBlockType", report.UnrecognizedNames[0]);
    }
}
