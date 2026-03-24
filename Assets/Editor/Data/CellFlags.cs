/// <summary>
/// Binary flags stored per-cell.
/// Multiple flags can be combined: CellFlags.Walkable | CellFlags.SpawnPoint
///
/// Design note: generation state flags (HasTerrain, HasVegetation) are stored here
/// so the registry and the data agree without needing an extra lookup. They are
/// written by FieldBuilder after generation and cleared on cell reset.
/// </summary>
[System.Flags]
public enum CellFlags
{
    None          = 0,

    // ── Gameplay / navigation ────────────────────────────────────────────────
    Walkable      = 1 << 0,   // NavMesh agents can traverse this cell
    Blocked       = 1 << 1,   // hard obstacle — mutually exclusive with Walkable

    // ── Editor behaviour ─────────────────────────────────────────────────────
    Empty         = 1 << 2,   // intentionally blank; skipped by all generation passes
    Locked        = 1 << 3,   // excluded from brush strokes and batch operations

    // ── Generation state (written by FieldBuilder, not by the user directly) ─
    HasTerrain    = 1 << 4,
    HasVegetation = 1 << 5,

    // ── World / spawn ────────────────────────────────────────────────────────
    SpawnPoint    = 1 << 6,   // valid spawn location for entities
    WaterSurface  = 1 << 7,   // treat as water (affects biome blending, culling)
}
