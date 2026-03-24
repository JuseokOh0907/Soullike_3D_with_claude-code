using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Orchestrates scene generation from a FieldGridData asset.
///
/// Scene hierarchy produced:
///   {data.sceneRootName}          ← registered with Undo as one unit
///     Terrain/
///       Cell_0_0                  ← terrain prefab instance
///       Cell_0_1  …
///     Vegetation/
///       Cell_0_0/                 ← container with grass/bush/flower children
///       Cell_0_1/  …
///
/// Two generation modes:
///   RebuildAll       – destroys existing root, regenerates every cell.
///   UpdateDirtyCells – only regenerates cells whose isDirty flag is set
///                      (session-only; resets after each generate pass).
///
/// Terrain is always placed before vegetation so Physics.SyncTransforms()
/// can register colliders before culling raycasts are fired.
///
/// Cells with the Empty flag are skipped entirely (no terrain, no vegetation).
/// </summary>
public static class FieldBuilder
{
    private const string GROUP_TERRAIN    = "Terrain";
    private const string GROUP_VEGETATION = "Vegetation";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears any existing generated objects and rebuilds every non-empty cell.
    /// Entire operation is a single Ctrl-Z step.
    /// </summary>
    public static void RebuildAll(FieldGridData data)
    {
        if (data == null) return;

        ClearAll(data);

        // Load vegetation pools once — AssetDatabase.FindAssets is expensive
        var grassPool  = FieldPrefabResolver.LoadGrassPool();
        var bushPool   = FieldPrefabResolver.LoadBushPool();
        var flowerPool = FieldPrefabResolver.LoadFlowerPool();

        // Build scene hierarchy containers
        var root      = new GameObject(data.sceneRootName);
        var terrainGO = new GameObject(GROUP_TERRAIN);
        var vegetGO   = new GameObject(GROUP_VEGETATION);
        terrainGO.transform.SetParent(root.transform);
        vegetGO.transform.SetParent(root.transform);

        // ── Pass 1: terrain tiles ─────────────────────────────────────────────
        for (int z = 0; z < data.height; z++)
        {
            for (int x = 0; x < data.width; x++)
            {
                var cell = data.GetCell(x, z);
                if (cell == null || cell.HasFlag(CellFlags.Empty)) continue;

                var rng = new System.Random(cell.ResolveRandomSeed(data.globalSeed));
                FieldTerrainPlacer.Place(cell, data, terrainGO.transform, rng);
            }
        }

        // Sync physics so freshly placed colliders are visible to raycasts
        Physics.SyncTransforms();

        // ── Pass 2: vegetation ────────────────────────────────────────────────
        for (int z = 0; z < data.height; z++)
        {
            for (int x = 0; x < data.width; x++)
            {
                var cell = data.GetCell(x, z);
                if (cell == null || cell.HasFlag(CellFlags.Empty)) continue;

                // Different seed so terrain pick doesn't influence veg pick
                var rng = new System.Random(~cell.ResolveRandomSeed(data.globalSeed));
                FieldVegetationPlacer.Place(cell, data, vegetGO.transform,
                    grassPool, bushPool, flowerPool, rng);

                cell.isDirty = false;
            }
        }

        // Register root — Ctrl-Z destroys the whole hierarchy in one step
        Undo.RegisterCreatedObjectUndo(root, "Generate Field");
        UnityEditor.Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log($"[FieldBuilder] RebuildAll: {data.width}×{data.height} grid  " +
                  $"from asset '{data.name}'.");
    }

    /// <summary>
    /// Re-generates only cells whose <c>isDirty</c> flag is true.
    /// Falls back to RebuildAll when no scene root exists yet.
    /// The entire operation collapses into one Ctrl-Z step.
    /// </summary>
    public static void UpdateDirtyCells(FieldGridData data)
    {
        if (data == null) return;

        // Gather dirty cells before touching the scene
        var dirtyCells = new List<CellData>();
        foreach (var cell in data.Cells)
            if (cell != null && cell.isDirty) dirtyCells.Add(cell);

        if (dirtyCells.Count == 0) return;

        var root = GameObject.Find(data.sceneRootName);
        if (root == null) { RebuildAll(data); return; }

        var terrainRoot = root.transform.Find(GROUP_TERRAIN);
        var vegetRoot   = root.transform.Find(GROUP_VEGETATION);
        if (terrainRoot == null || vegetRoot == null) { RebuildAll(data); return; }

        var grassPool  = FieldPrefabResolver.LoadGrassPool();
        var bushPool   = FieldPrefabResolver.LoadBushPool();
        var flowerPool = FieldPrefabResolver.LoadFlowerPool();

        FieldEditorUndo.BeginGroup("Update Changed Cells");

        // ── Pass 1: remove stale objects + place new terrain ──────────────────
        foreach (var cell in dirtyCells)
        {
            FieldTerrainPlacer.Remove(cell.coord.x, cell.coord.y, terrainRoot);
            FieldVegetationPlacer.Remove(cell.coord.x, cell.coord.y, vegetRoot);

            if (cell.HasFlag(CellFlags.Empty)) continue;

            var rng      = new System.Random(cell.ResolveRandomSeed(data.globalSeed));
            var terrainGO = FieldTerrainPlacer.Place(cell, data, terrainRoot, rng);
            if (terrainGO != null)
                Undo.RegisterCreatedObjectUndo(terrainGO, "Update Cell Terrain");
        }

        // Sync physics after all new terrain is in place
        Physics.SyncTransforms();

        // ── Pass 2: vegetation ────────────────────────────────────────────────
        foreach (var cell in dirtyCells)
        {
            if (cell.HasFlag(CellFlags.Empty)) { cell.isDirty = false; continue; }

            var rng    = new System.Random(~cell.ResolveRandomSeed(data.globalSeed));
            var vegetGO = FieldVegetationPlacer.Place(cell, data, vegetRoot,
                grassPool, bushPool, flowerPool, rng);

            if (vegetGO != null)
                Undo.RegisterCreatedObjectUndo(vegetGO, "Update Cell Vegetation");

            cell.isDirty = false;
        }

        FieldEditorUndo.EndGroup();
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log($"[FieldBuilder] UpdateDirtyCells: regenerated {dirtyCells.Count} cell(s).");
    }

    /// <summary>Destroys the entire generated scene hierarchy for this data asset.</summary>
    public static void ClearAll(FieldGridData data)
    {
        if (data == null) return;
        var existing = GameObject.Find(data.sceneRootName);
        if (existing == null) return;
        FieldEditorUndo.DestroySceneObject(existing);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // ── Status queries (used by the editor window UI) ─────────────────────────

    /// <summary>True if a scene root for this data asset currently exists.</summary>
    public static bool HasSceneObjects(FieldGridData data) =>
        data != null && GameObject.Find(data.sceneRootName) != null;

    /// <summary>Number of cells marked isDirty (need regeneration this session).</summary>
    public static int DirtyCellCount(FieldGridData data)
    {
        if (data == null) return 0;
        int n = 0;
        foreach (var c in data.Cells)
            if (c != null && c.isDirty) n++;
        return n;
    }
}
