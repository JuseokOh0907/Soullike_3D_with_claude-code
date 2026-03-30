using UnityEngine;

/// <summary>
/// Configuration asset for the Wall & Door Maker tool.
///
/// Stores all prefab references and dimensional defaults in one place.
/// Create via: right-click Project → Wall Door Maker → Settings
/// </summary>
[CreateAssetMenu(menuName = "Wall Door Maker/Settings", fileName = "WallDoorSettings")]
public class WallDoorSettings : ScriptableObject
{
    // ── Prefabs ───────────────────────────────────────────────────────────────

    [Header("Prefabs")]
    [Tooltip("Prefab instantiated for each solid wall segment.")]
    public GameObject wallPrefab;

    [Tooltip("Prefab instantiated in place of a wall segment when a door is added.\n" +
             "Should match the wall segment dimensions.")]
    public GameObject doorPrefab;

    [Tooltip("Optional pillar/post prefab placed at each wall joint.\n" +
             "Leave null to skip pillars.")]
    public GameObject pillarPrefab;

    // ── Wall dimensions ───────────────────────────────────────────────────────

    [Header("Wall Dimensions")]
    [Min(0.1f)]
    [Tooltip("Height of each wall segment in world units.")]
    public float wallHeight = 3f;

    [Min(0.1f)]
    [Tooltip("Length of one wall segment prefab in world units (X-axis of the prefab).")]
    public float segmentLength = 2f;

    [Min(0.01f)]
    [Tooltip("Depth / thickness of the wall prefab in world units (Z-axis of the prefab).")]
    public float wallThickness = 0.2f;

    // ── Door dimensions ───────────────────────────────────────────────────────

    [Header("Door Dimensions")]
    [Min(1)]
    [Tooltip("Width of a door opening measured in wall segments (1 = one segment wide).")]
    public int doorWidthInSegments = 1;

    // ── Snap & grid ───────────────────────────────────────────────────────────

    [Header("Grid Snap")]
    [Tooltip("Snap start/end points to a world-space grid when placing walls.")]
    public bool enableSnap = true;

    [Min(0.1f)]
    [Tooltip("Grid cell size used when snap is enabled.")]
    public float snapSize = 1f;

    // ── Scene root ────────────────────────────────────────────────────────────

    [Header("Scene")]
    [Tooltip("Name of the root GameObject created in the scene to hold all wall objects.")]
    public string sceneRootName = "Walls";

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Snaps <paramref name="worldPos"/> to the grid when snap is enabled.
    /// Only X and Z are snapped; Y is preserved.
    /// </summary>
    public Vector3 Snap(Vector3 worldPos)
    {
        if (!enableSnap) return worldPos;
        float s = snapSize;
        return new Vector3(
            Mathf.Round(worldPos.x / s) * s,
            worldPos.y,
            Mathf.Round(worldPos.z / s) * s);
    }

    /// <summary>True when the minimum required prefabs are assigned.</summary>
    public bool IsValid => wallPrefab != null && doorPrefab != null;
}
