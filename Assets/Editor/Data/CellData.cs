using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All per-cell data stored inside FieldGridData.
///
/// Design notes
/// ────────────
/// • [Serializable] plain class, NOT ScriptableObject.
///   Cells are owned by the grid — they are not independent assets.
///
/// • Two parallel override workflows coexist:
///     Profile-based : terrainProfile / vegetProfile (reusable ScriptableObject)
///     Direct prefab : terrainPrefabOverride / vegetPrefabOverrides (drag-and-drop)
///   Resolution order per field: direct prefab → cell profile → grid default.
///   The helper methods below encapsulate that rule so callers never branch.
///
/// • isDirty is [NonSerialized] — it tracks "needs regeneration" at runtime.
///   It must NOT be persisted; Undo should revert data, not dirty state.
///   CellDirtyTracker (a separate runtime class) is the authoritative store.
///
/// • Rotation, height and scale are stored as plain floats intentionally:
///   - rotationY: Y-axis only — terrain tiles aren't tilted on X/Z
///   - heightOffset: additive Y offset from the tile's grid position
///   - scale: uniform — non-uniform scale on terrain looks wrong
///   Extend with a Vector3 scale field if you need non-uniform later.
/// </summary>
[Serializable]
public class CellData
{
    // ── Identity ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Grid-space coordinate (x = column, y = row).
    /// Stored here for convenience — matches the flat-list index in FieldGridData.
    /// Must be kept in sync by FieldGridData.Resize / SetCell.
    /// </summary>
    public Vector2Int coord;

    // ── Transform ─────────────────────────────────────────────────────────────
    /// <summary>Additive Y offset from the cell's computed world position.</summary>
    public float heightOffset = 0f;

    /// <summary>Y-axis rotation in degrees applied to the terrain tile.</summary>
    public float rotationY = 0f;

    /// <summary>Uniform scale applied to the terrain tile. 1 = natural size.</summary>
    [Min(0.01f)]
    public float scale = 1f;

    // ── Terrain — profile-based workflow ─────────────────────────────────────
    /// <summary>
    /// Terrain profile for this cell.
    /// null → falls back to FieldGridData.defaultTerrainProfile.
    /// </summary>
    public TerrainProfile terrainProfile;

    // ── Terrain — direct prefab override ─────────────────────────────────────
    /// <summary>
    /// When non-null, this exact prefab is instantiated instead of picking
    /// randomly from the profile's tile pool.
    /// Takes priority over terrainProfile.
    /// </summary>
    public GameObject terrainPrefabOverride;

    // ── Vegetation — profile-based workflow ───────────────────────────────────
    /// <summary>
    /// Vegetation profile for this cell.
    /// null → falls back to FieldGridData.defaultVegetProfile.
    /// </summary>
    public VegetationProfile vegetProfile;

    // ── Vegetation — direct prefab override ──────────────────────────────────
    /// <summary>
    /// When non-empty, these specific prefabs are used INSTEAD OF the profile's
    /// pool. Useful for placing unique props on individual cells.
    /// </summary>
    public List<GameObject> vegetPrefabOverrides = new();

    // ── Classification ────────────────────────────────────────────────────────
    /// <summary>Binary flags: walkability, generation state, editor locks, etc.</summary>
    public CellFlags flags = CellFlags.Walkable;

    /// <summary>Biome this cell belongs to. Affects vegetation selection and preview tint.</summary>
    public BiomeType biome = BiomeType.Grassland;

    // ── Custom metadata ───────────────────────────────────────────────────────
    /// <summary>
    /// Open-ended key-value pairs for game-specific data.
    /// Use GetTag / SetTag / RemoveTag rather than accessing the list directly.
    /// </summary>
    public List<CellTag> tags = new();

    // ── Randomisation ─────────────────────────────────────────────────────────
    /// <summary>
    /// Per-cell random seed.
    /// -1 = derive from FieldGridData.globalSeed combined with the cell coordinate,
    ///      giving deterministic but varied results across the grid.
    /// Any other value = fixed, fully reproducible placement for this cell.
    /// </summary>
    public int randomSeed = -1;

    // ── Runtime state (NOT serialized) ────────────────────────────────────────
    /// <summary>
    /// Marks this cell as needing scene regeneration.
    /// Managed exclusively by CellDirtyTracker — do not set directly.
    /// </summary>
    [NonSerialized] public bool isDirty;

    // ── Resolution helpers ────────────────────────────────────────────────────
    /// <summary>True when a direct prefab override is set for terrain.</summary>
    public bool HasTerrainPrefabOverride => terrainPrefabOverride != null;

    /// <summary>True when direct vegetation prefabs are provided.</summary>
    public bool HasVegetPrefabOverrides  => vegetPrefabOverrides != null && vegetPrefabOverrides.Count > 0;

    /// <summary>
    /// Returns the effective terrain profile:
    /// cell override → <paramref name="gridDefault"/> → null.
    /// Caller must handle null (means no profile available).
    /// </summary>
    public TerrainProfile ResolveTerrainProfile(TerrainProfile gridDefault) =>
        terrainProfile != null ? terrainProfile : gridDefault;

    /// <summary>
    /// Returns the effective vegetation profile:
    /// cell override → <paramref name="gridDefault"/> → null.
    /// </summary>
    public VegetationProfile ResolveVegetProfile(VegetationProfile gridDefault) =>
        vegetProfile != null ? vegetProfile : gridDefault;

    /// <summary>
    /// Returns the effective random seed for generation.
    /// If randomSeed == -1, derives a deterministic value from the global seed
    /// and the cell's coordinate so each cell is unique but reproducible.
    /// </summary>
    public int ResolveRandomSeed(int globalSeed) =>
        randomSeed >= 0
            ? randomSeed
            : globalSeed ^ (coord.x * 73856093) ^ (coord.y * 19349663);   // spatial hash

    // ── Flag helpers ──────────────────────────────────────────────────────────
    public bool HasFlag(CellFlags flag) =>  (flags & flag) != 0;
    public void SetFlag(CellFlags flag)  => flags |=  flag;
    public void ClearFlag(CellFlags flag)=> flags &= ~flag;

    // ── Tag helpers ───────────────────────────────────────────────────────────
    /// <summary>Returns the value for <paramref name="key"/>, or null if absent.</summary>
    public string GetTag(string key)
    {
        if (tags == null) return null;
        foreach (var t in tags)
            if (t.key == key) return t.value;
        return null;
    }

    /// <summary>Adds or updates the tag with <paramref name="key"/>.</summary>
    public void SetTag(string key, string value)
    {
        tags ??= new List<CellTag>();
        foreach (var t in tags)
        {
            if (t.key != key) continue;
            t.value = value;
            return;
        }
        tags.Add(new CellTag(key, value));
    }

    /// <summary>Removes the tag with <paramref name="key"/> if present.</summary>
    public void RemoveTag(string key) =>
        tags?.RemoveAll(t => t.key == key);
}
