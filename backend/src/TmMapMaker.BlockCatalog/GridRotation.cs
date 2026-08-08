namespace TmMapMaker.BlockCatalog;

public static class GridRotation
{
    public static (int WorldDx, int WorldDz) Rotate(int localDx, int localDz, string direction)
    {
        return direction switch
        {
            "North" => (localDx, localDz),
            "East" => (localDz, -localDx),
            "South" => (-localDx, -localDz),
            "West" => (-localDz, localDx),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Expected North, East, South, or West."),
        };
    }
}
