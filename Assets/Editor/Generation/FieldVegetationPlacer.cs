using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Scatters vegetation prefabs within a single cell's world-space bounds.
///
/// Scene structure produced per cell:
///   Vegetation/
///     Cell_{x}_{z}/        ← container returned by Place()
///       Grass_0
///       Grass_1
///       Bush_0
///       Flower_0  …
///
/// The container is named with the same CellName convention as FieldTerrainPlacer
/// so UpdateDirtyCells can locate and destroy stale containers by name.
///
/// Vegetation pools (grass / bush / flower) are loaded once by the caller
/// (FieldBuilder) and passed in — loading per cell would be prohibitively slow.
///
/// Physics.SyncTransforms() must be called by the caller BEFORE invoking Place()
/// when enableCulling is true; otherwise raycasts will miss freshly placed terrain.
/// </summary>
public static class FieldVegetationPlacer
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a vegetation container for <paramref name="cell"/> and populates it.
    /// Returns the container, or null if the cell has no vegetation profile or
    /// all candidates were culled.
    /// </summary>
    public static GameObject Place(
        CellData         cell,
        FieldGridData    data,
        Transform        vegetRoot,
        List<GameObject> grassPool,
        List<GameObject> bushPool,
        List<GameObject> flowerPool,
        System.Random    rng)
    {
        var profile = cell.ResolveVegetProfile(data.defaultVegetProfile);
        if (profile == null) return null;

        // Per-cell prefab overrides replace the shared pools entirely
        bool hasOverrides = cell.HasVegetPrefabOverrides;
        var  gPool = hasOverrides ? cell.vegetPrefabOverrides : grassPool;
        var  bPool = hasOverrides ? cell.vegetPrefabOverrides : bushPool;
        var  fPool = hasOverrides ? cell.vegetPrefabOverrides : flowerPool;

        if (gPool.Count == 0 && bPool.Count == 0 && fPool.Count == 0)
            return null;

        var container = new GameObject(CellName(cell.coord.x, cell.coord.y));
        container.transform.SetParent(vegetRoot, worldPositionStays: false);

        Vector3 origin = data.CellToWorldPosition(cell.coord.x, cell.coord.y);
        float   half   = data.tileSize * 0.5f;

        Scatter(gPool, profile.grassPerTile, "Grass",  container.transform, origin, half, profile, rng);
        Scatter(bPool, profile.bushCount,    "Bush",   container.transform, origin, half, profile, rng);
        Scatter(fPool, profile.flowerCount,  "Flower", container.transform, origin, half, profile, rng);

        // Don't leave an empty container in the scene
        if (container.transform.childCount == 0)
        {
            Object.DestroyImmediate(container);
            return null;
        }

        return container;
    }

    /// <summary>Destroys the vegetation container for cell (x, z) if it exists.</summary>
    public static void Remove(int x, int z, Transform vegetRoot)
    {
        var child = vegetRoot.Find(CellName(x, z));
        if (child != null)
            FieldEditorUndo.DestroySceneObject(child.gameObject);
    }

    // ── Naming ────────────────────────────────────────────────────────────────
    public static string CellName(int x, int z) => $"Cell_{x}_{z}";

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void Scatter(
        List<GameObject>  pool,
        int               count,
        string            baseName,
        Transform         parent,
        Vector3           origin,
        float             halfSize,
        VegetationProfile profile,
        System.Random     rng)
    {
        if (pool.Count == 0 || count == 0) return;

        for (int i = 0; i < count; i++)
        {
            float cx = origin.x + RandRange(rng, -halfSize, halfSize);
            float cz = origin.z + RandRange(rng, -halfSize, halfSize);

            Vector3 pos = new Vector3(cx, origin.y, cz);

            if (profile.enableCulling && !TryRaycast(cx, cz, profile.raycastOriginY, out pos))
                continue;

            var prefab = pool[rng.Next(pool.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name               = $"{baseName}_{i}";
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, RandRange(rng, 0f, 360f), 0f);
            go.transform.localScale = Vector3.one * RandRange(rng, profile.scaleMin, profile.scaleMax);
        }
    }

    private static bool TryRaycast(float cx, float cz, float originY, out Vector3 worldPos)
    {
        var ray = new Ray(new Vector3(cx, originY, cz), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, originY + 10f))
        {
            worldPos = hit.point;
            return true;
        }
        worldPos = default;
        return false;
    }

    private static float RandRange(System.Random rng, float min, float max) =>
        min + (float)(rng.NextDouble() * (max - min));
}
