using UnityEngine;
using UnityEditor;

/// <summary>
/// Places or removes the terrain tile for a single grid cell.
///
/// Naming convention: every terrain tile is named "Cell_{x}_{z}" under the
/// Terrain parent.  UpdateDirtyCells relies on this name to find and remove
/// stale objects before re-placing them.
///
/// Undo registration is the caller's responsibility.
/// This class only instantiates / destroys — it does not group undo operations.
/// </summary>
public static class FieldTerrainPlacer
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates a terrain prefab for <paramref name="cell"/> under
    /// <paramref name="terrainRoot"/> and applies position / rotation / scale.
    /// Returns the created GameObject, or null if no prefab could be resolved.
    /// </summary>
    public static GameObject Place(
        CellData cell, FieldGridData data, Transform terrainRoot, System.Random rng)
    {
        var prefab = FieldPrefabResolver.ResolveTerrainPrefab(
            cell, data.defaultTerrainProfile, rng);

        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, terrainRoot);
        go.name               = CellName(cell.coord.x, cell.coord.y);
        go.transform.position = data.CellToWorldPosition(cell.coord.x, cell.coord.y);
        go.transform.rotation = Quaternion.Euler(0f, cell.rotationY, 0f);
        go.transform.localScale = Vector3.one * cell.scale;
        return go;
    }

    /// <summary>
    /// Destroys the terrain tile for cell (x, z) if one exists under
    /// <paramref name="terrainRoot"/>.  Uses FieldEditorUndo so the
    /// destruction is undoable.
    /// </summary>
    public static void Remove(int x, int z, Transform terrainRoot)
    {
        var child = terrainRoot.Find(CellName(x, z));
        if (child != null)
            FieldEditorUndo.DestroySceneObject(child.gameObject);
    }

    // ── Naming ────────────────────────────────────────────────────────────────
    public static string CellName(int x, int z) => $"Cell_{x}_{z}";
}
