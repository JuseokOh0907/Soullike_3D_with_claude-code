using UnityEngine;

/// <summary>
/// Reusable ScriptableObject that describes how vegetation is scattered
/// over a cell during generation.
///
/// Design note: mirrors the same ScriptableObject rationale as TerrainProfile —
/// shared across cells, independently editable, Undo-safe.
///
/// Create via: right-click Project window → Field Builder → Vegetation Profile
/// </summary>
[CreateAssetMenu(menuName = "Field Builder/Vegetation Profile", fileName = "VegetationProfile")]
public class VegetationProfile : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────
    [Tooltip("Human-readable label shown in the inspector.")]
    public string displayName = "Default Vegetation";

    // ── Density ───────────────────────────────────────────────────────────────
    [Tooltip("Number of grass objects placed per terrain tile.")]
    [Min(0)] public int grassPerTile = 10;

    [Tooltip("Total bushes spread across all tiles that use this profile.")]
    [Min(0)] public int bushCount = 5;

    [Tooltip("Total flowers spread across all tiles that use this profile.")]
    [Min(0)] public int flowerCount = 7;

    // ── Scale ─────────────────────────────────────────────────────────────────
    [Tooltip("Minimum uniform scale applied to each vegetation object.")]
    [Min(0.01f)] public float scaleMin = 1.5f;

    [Tooltip("Maximum uniform scale. Set equal to scaleMin for no randomisation.")]
    [Min(0.01f)] public float scaleMax = 3.0f;

    // ── Culling ───────────────────────────────────────────────────────────────
    [Tooltip("Raycast downward before placing; discard objects that miss terrain.")]
    public bool  enableCulling  = true;

    [Tooltip("World-space Y from which the downward culling ray is cast.")]
    public float raycastOriginY = 100f;

    // ── Validation ────────────────────────────────────────────────────────────
    private void OnValidate()
    {
        if (scaleMax < scaleMin) scaleMax = scaleMin;
    }
}
