using TmMapMaker.BlockCatalog;
using Xunit;

namespace TmMapMaker.BlockCatalog.Tests;

public class GridRotationTests
{
    // A block's own "forward" is +Z (dz=1) in its unrotated local frame.
    // North = no rotation: forward stays (0, 1).
    // East = 90° clockwise: forward becomes (1, 0).
    // South = 180°: forward becomes (0, -1).
    // West = 270° clockwise: forward becomes (-1, 0).
    [Theory]
    [InlineData(0, 1, "North", 0, 1)]
    [InlineData(0, 1, "East", 1, 0)]
    [InlineData(0, 1, "South", 0, -1)]
    [InlineData(0, 1, "West", -1, 0)]
    public void Rotate_maps_local_forward_to_expected_world_offset(int localDx, int localDz, string direction, int expectedWorldDx, int expectedWorldDz)
    {
        var (worldDx, worldDz) = GridRotation.Rotate(localDx, localDz, direction);
        Assert.Equal(expectedWorldDx, worldDx);
        Assert.Equal(expectedWorldDz, worldDz);
    }

    [Theory]
    [InlineData(1, 0, "North", 1, 0)]
    [InlineData(1, 0, "East", 0, -1)]
    [InlineData(1, 0, "South", -1, 0)]
    [InlineData(1, 0, "West", 0, 1)]
    public void Rotate_maps_local_right_to_expected_world_offset(int localDx, int localDz, string direction, int expectedWorldDx, int expectedWorldDz)
    {
        var (worldDx, worldDz) = GridRotation.Rotate(localDx, localDz, direction);
        Assert.Equal(expectedWorldDx, worldDx);
        Assert.Equal(expectedWorldDz, worldDz);
    }

    [Fact]
    public void Rotate_of_zero_offset_is_always_zero()
    {
        foreach (var dir in new[] { "North", "East", "South", "West" })
        {
            var (worldDx, worldDz) = GridRotation.Rotate(0, 0, dir);
            Assert.Equal((0, 0), (worldDx, worldDz));
        }
    }
}
