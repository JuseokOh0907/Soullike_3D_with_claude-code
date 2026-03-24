/// <summary>
/// High-level biome classification for a cell.
/// Used by generation passes to select vegetation profiles and by the
/// GridView to tint cell previews.
///
/// Design note: this is a plain enum, not flags — a cell belongs to exactly
/// one biome. Blending between biomes (e.g. forest edge) is handled at
/// generation time by reading neighbouring cells, not by storing two values.
/// </summary>
public enum BiomeType
{
    None      = 0,   // unassigned — falls back to grid default
    Grassland = 1,
    Forest    = 2,
    Desert    = 3,
    Tundra    = 4,
    Rocky     = 5,
    Swamp     = 6,
    Custom    = 99,  // fully user-defined; pair with CellTag for extra metadata
}
