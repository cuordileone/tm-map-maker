using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class BlockNameClassifierTests
{
    [Theory]
    [InlineData("RoadTechStart", BlockFamily.Road)]
    [InlineData("PlatformTechCheckpoint", BlockFamily.Platform)]
    [InlineData("GateFinish", BlockFamily.Gate)]
    [InlineData("DecoWallBasePillar", BlockFamily.Deco)]
    [InlineData("StructurePillar", BlockFamily.Structure)]
    [InlineData("TechnicsScreen1x1Straight", BlockFamily.Technics)]
    [InlineData("StageStructureStraight", BlockFamily.Stage)]
    [InlineData("TrackWall4mA", BlockFamily.TrackWall)]
    [InlineData("CanopyCenterFlatBase", BlockFamily.Canopy)]
    public void ClassifyFamily_returns_expected_family_for_known_prefixes(string name, BlockFamily expected)
    {
        Assert.Equal(expected, BlockNameClassifier.ClassifyFamily(name));
    }

    [Fact]
    public void ClassifyFamily_returns_Unknown_for_unrecognized_prefix()
    {
        Assert.Equal(BlockFamily.Unknown, BlockNameClassifier.ClassifyFamily("SomeNewBlockTypeNobodyHasSeen"));
    }

    [Theory]
    [InlineData("2-Weird\\zMiniSets\\PlatformHoles\\PlatformSlopeWithHole24m.Block.Gbx_CustomBlock", true)]
    [InlineData("A-BlockGBX\\Magnet2\\M2_PlatformTechBaseFlip.Block.Gbx_CustomBlock", true)]
    [InlineData("RoadTechStart", false)]
    [InlineData("PlatformTechBase", false)]
    public void IsCustomBlock_detects_modded_block_names(string name, bool expected)
    {
        Assert.Equal(expected, BlockNameClassifier.IsCustomBlock(name));
    }
}
