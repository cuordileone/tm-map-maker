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
                0, 0, 0, "North", null, null, null, null, null, null, 0, 0)
        };

        var report = MapInventoryReport.From("fake.Map.Gbx", blocks);

        Assert.Single(report.UnrecognizedNames);
        Assert.Equal("TotallyNewBlockType", report.UnrecognizedNames[0]);
    }
}
