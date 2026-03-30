using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Instantiates or removes scene objects for wall runs and door openings.
///
/// Flow (one-way: data → scene)
/// ─────────────────────────────
///   1. For each WallSegmentData, compute segment count + positions.
///   2. Instantiate wallPrefab or doorPrefab per slot.
///   3. Optionally place pillarPrefab at wall joints.
///   4. Group everything under a named root in the scene hierarchy.
///
/// Undo: entire build registers as one Ctrl-Z step via Undo.RegisterCreatedObjectUndo.
/// </summary>
public static class WallBuilder
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds all wall runs from <paramref name="walls"/> under a fresh scene root.
    /// Destroys the old root first (undo-safe).
    /// </summary>
    public static void RebuildAll(IReadOnlyList<WallSegmentData> walls, WallDoorSettings settings)
    {
        if (settings == null || !settings.IsValid) return;

        ClearScene(settings);

        if (walls == null || walls.Count == 0) return;

        var root = new GameObject(settings.sceneRootName);

        foreach (var wall in walls)
            BuildWallRun(wall, settings, root.transform);

        Undo.RegisterCreatedObjectUndo(root, "Build Walls");
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log($"[WallBuilder] Built {walls.Count} wall run(s) under '{settings.sceneRootName}'.");
    }

    /// <summary>
    /// Rebuilds a single wall run in-place.
    /// Finds the existing run container by name and replaces it, or creates it.
    /// </summary>
    public static void RebuildRun(int runIndex, WallSegmentData wall,
                                  WallDoorSettings settings)
    {
        if (settings == null || !settings.IsValid) return;

        var root = GetOrCreateRoot(settings);
        string runName = RunName(runIndex);

        // Remove old run container
        var old = root.transform.Find(runName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        BuildWallRun(wall, settings, root.transform, runName);
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    /// <summary>Destroys the scene root and all wall objects in it.</summary>
    public static void ClearScene(WallDoorSettings settings)
    {
        if (settings == null) return;
        var existing = GameObject.Find(settings.sceneRootName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);
    }

    /// <summary>True when a wall root currently exists in the scene.</summary>
    public static bool HasSceneObjects(WallDoorSettings settings) =>
        settings != null && GameObject.Find(settings.sceneRootName) != null;

    // ── Scene query helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Given a world-space ray, returns the wall run index and segment index
    /// of the closest wall segment hit, or (-1, -1) when nothing is hit.
    /// Used by the door-placement tool to identify which segment was clicked.
    /// </summary>
    public static (int runIdx, int segIdx) HitTest(
        Ray ray, IReadOnlyList<WallSegmentData> walls, WallDoorSettings settings)
    {
        float   bestDist = float.MaxValue;
        int     bestRun  = -1;
        int     bestSeg  = -1;

        for (int r = 0; r < walls.Count; r++)
        {
            var wall  = walls[r];
            int count = wall.SegmentCount(settings.segmentLength);

            for (int s = 0; s < count; s++)
            {
                Vector3 center = wall.SegmentCenter(s, settings.segmentLength);
                float   dist;

                // Approximate hit test: distance from ray to segment center
                if (RayPointDistance(ray, center, out dist) && dist < bestDist)
                {
                    bestDist = dist;
                    bestRun  = r;
                    bestSeg  = s;
                }
            }
        }

        // Threshold: within half a segment length counts as a hit
        float threshold = settings.segmentLength * 0.6f;
        return bestDist <= threshold ? (bestRun, bestSeg) : (-1, -1);
    }

    // ── Building helpers ──────────────────────────────────────────────────────

    private static void BuildWallRun(
        WallSegmentData wall, WallDoorSettings settings,
        Transform parent, string runName = null)
    {
        int   count  = wall.SegmentCount(settings.segmentLength);
        float segLen = settings.segmentLength;

        var container = new GameObject(runName ?? RunName(-1));
        container.transform.SetParent(parent, worldPositionStays: true);

        // Rotation: wall faces along its direction
        Quaternion rot = wall.Direction != Vector3.zero
            ? Quaternion.LookRotation(wall.Direction, Vector3.up)
            : Quaternion.identity;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos    = wall.SegmentCenter(i, segLen);
            pos.y         += settings.wallHeight * 0.5f;   // pivot at base → raise to centre
            bool    isDoor = wall.HasDoor(i);

            var prefab = isDoor ? settings.doorPrefab : settings.wallPrefab;
            var go     = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            go.name              = isDoor ? $"Door_{i}" : $"Wall_{i}";
            go.transform.position = pos;
            go.transform.rotation = rot;
        }

        // Optional pillars at joints
        if (settings.pillarPrefab != null)
        {
            PlacePillar(wall.start, settings, container.transform, "Pillar_Start");
            PlacePillar(wall.end,   settings, container.transform, "Pillar_End");
        }
    }

    private static void PlacePillar(
        Vector3 pos, WallDoorSettings settings, Transform parent, string name)
    {
        pos.y += settings.wallHeight * 0.5f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(settings.pillarPrefab, parent);
        go.name               = name;
        go.transform.position = pos;
    }

    private static GameObject GetOrCreateRoot(WallDoorSettings settings)
    {
        var root = GameObject.Find(settings.sceneRootName);
        return root != null ? root : new GameObject(settings.sceneRootName);
    }

    private static string RunName(int index) =>
        index >= 0 ? $"WallRun_{index}" : "WallRun";

    // ── Ray maths ─────────────────────────────────────────────────────────────

    private static bool RayPointDistance(Ray ray, Vector3 point, out float dist)
    {
        // Closest distance from a point to an infinite ray
        Vector3 v  = point - ray.origin;
        float   t  = Vector3.Dot(v, ray.direction);
        if (t < 0f) { dist = float.MaxValue; return false; }
        Vector3 closest = ray.origin + ray.direction * t;
        dist = Vector3.Distance(closest, point);
        return true;
    }
}
