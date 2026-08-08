namespace TmMapMaker.BlockCatalog;

public enum PlacementType { Grid, Free }

public sealed record PlacedBlock(
    string Name,
    BlockFamily Family,
    PlacementType Placement,
    int? GridX,
    int? GridY,
    int? GridZ,
    string? Direction,
    float? WorldX,
    float? WorldY,
    float? WorldZ,
    float? YawRad,
    float? PitchRad,
    float? RollRad,
    int Variant,
    int SubVariant,
    bool IsCustom);
