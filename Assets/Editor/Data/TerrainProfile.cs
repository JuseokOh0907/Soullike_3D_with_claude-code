using UnityEngine;

/// <summary>
/// Reusable ScriptableObject that describes which terrain tiles to pick from
/// the LMHPOLY asset pack for a cell or a whole grid.
///
/// Design note: ScriptableObject (not [Serializable] class) so that:
///   • Multiple cells and grids can share the same profile asset.
///   • Profiles can be edited in the Project window independently.
///   • Undo.RecordObject works correctly per-profile.
///
/// Create via: right-click Project window → Field Builder → Terrain Profile
/// </summary>
[CreateAssetMenu(menuName = "Field Builder/Terrain Profile", fileName = "TerrainProfile")]
public class TerrainProfile : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────
    [Tooltip("Human-readable label shown in the inspector and grid preview.")]
    public string displayName = "Default Terrain";

    [Tooltip("Color used to tint this cell in the 2D grid preview.")]
    public Color previewColor = new Color(0.45f, 0.75f, 0.35f, 1f);   // muted green

    // ── Asset pack settings ───────────────────────────────────────────────────
    [Tooltip("MT = Modular Terrain (open edges), CPT = Clipped Plane Terrain (solid bottom).")]
    public TerrainPackType packType = TerrainPackType.MT;

    [Tooltip("Tile size variant to load from the asset pack.")]
    public TerrainTileSize tileSize = TerrainTileSize.Large;

    [Tooltip("Which style letters (a–f) to include in the random tile pool for this profile.")]
    public bool[] enabledStyles = { true, false, false, false, false, false };

    // ── Convenience ───────────────────────────────────────────────────────────
    /// <summary>True if at least one style letter is enabled.</summary>
    public bool IsValid
    {
        get
        {
            if (enabledStyles == null) return false;
            foreach (var s in enabledStyles)
                if (s) return true;
            return false;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Supporting enums — kept in this file because they are only meaningful in the
// context of TerrainProfile and the LMHPOLY asset pack paths.

/// <summary>Asset sub-folder prefix used when building the prefab search path.</summary>
public enum TerrainPackType
{
    MT  = 0,   // Assets/…/Terrain/MT/NoLOD/{size}/
    CPT = 1,   // Assets/…/Terrain/CPT/NoLOD/{size}/
}

/// <summary>Tile size folder (L = Large, M = Medium, S = Small).</summary>
public enum TerrainTileSize
{
    Large  = 0,   // subfolder "L"
    Medium = 1,   // subfolder "M"
    Small  = 2,   // subfolder "S"
}
