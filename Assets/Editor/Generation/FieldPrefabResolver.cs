using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Resolves which prefabs to use for terrain and vegetation based on
/// FieldGridData settings and per-cell CellData overrides.
///
/// All methods are static — this class holds no state.
/// It is the single source of truth for asset-pack path construction and
/// prefab loading, so path constants live here and nowhere else.
///
/// Resolution order (terrain):
///   1. CellData.terrainPrefabOverride  (direct drag-and-drop)
///   2. Random pick from TerrainProfile tile pool
///   3. null → caller skips placement for this cell
/// </summary>
public static class FieldPrefabResolver
{
    // ── LMHPOLY asset paths ───────────────────────────────────────────────────
    private const string TERRAIN_BASE =
        "Assets/LMHPOLY/Low Poly Nature Bundle/Modular Terrain/Terrain_Assets/Prefabs/Terrain";

    private const string GRASS_PATH   =
        "Assets/LMHPOLY/Low Poly Nature Bundle/Vegetation/Vegetation Assets/Prefabs/Grass/Grass3D";
    private const string BUSH_PATH    =
        "Assets/LMHPOLY/Low Poly Nature Bundle/Vegetation/Vegetation Assets/Prefabs/Bushes/Bush";
    private const string FLOWER_PATH  =
        "Assets/LMHPOLY/Low Poly Nature Bundle/Vegetation/Vegetation Assets/Prefabs/Flowers/TwoSided";

    private static readonly char[] AllStyles = { 'a', 'b', 'c', 'd', 'e', 'f' };

    // ── Terrain ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the single terrain prefab to instantiate for <paramref name="cell"/>.
    /// Uses <paramref name="rng"/> only when a profile pool pick is needed.
    /// Returns null if no prefab can be resolved (cell will be skipped).
    /// </summary>
    public static GameObject ResolveTerrainPrefab(
        CellData cell, TerrainProfile gridDefault, System.Random rng)
    {
        if (cell.HasTerrainPrefabOverride)
            return cell.terrainPrefabOverride;

        var profile = cell.ResolveTerrainProfile(gridDefault);
        if (profile == null || !profile.IsValid) return null;

        var pool = BuildTerrainPool(profile);
        return pool.Count > 0 ? pool[rng.Next(pool.Count)] : null;
    }

    /// <summary>
    /// Loads all terrain prefabs matching <paramref name="profile"/>'s enabled styles.
    /// Results are not cached — call once per build, not per cell.
    /// </summary>
    public static List<GameObject> BuildTerrainPool(TerrainProfile profile)
    {
        var pool = new List<GameObject>();
        if (profile == null || !profile.IsValid) return pool;

        string typePrefix = profile.packType == TerrainPackType.MT ? "MT" : "CPT";
        string sizeChar   = ToSizeChar(profile.tileSize);
        string folder     = $"{TERRAIN_BASE}/{typePrefix}/NoLOD/{sizeChar}";

        for (int i = 0; i < AllStyles.Length; i++)
        {
            if (profile.enabledStyles == null || i >= profile.enabledStyles.Length) break;
            if (!profile.enabledStyles[i]) continue;

            string prefix = $"{typePrefix}_Terrain_{sizeChar}_{AllStyles[i]}_";
            pool.AddRange(LoadPrefabs(folder, prefix));
        }
        return pool;
    }

    // ── Vegetation ────────────────────────────────────────────────────────────

    public static List<GameObject> LoadGrassPool()   => LoadPrefabs(GRASS_PATH,  "Grass3D_");
    public static List<GameObject> LoadBushPool()    => LoadPrefabs(BUSH_PATH,   "Bush_");
    public static List<GameObject> LoadFlowerPool()  => LoadPrefabs(FLOWER_PATH, "Flower_");

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string ToSizeChar(TerrainTileSize size) => size switch
    {
        TerrainTileSize.Large  => "L",
        TerrainTileSize.Medium => "M",
        TerrainTileSize.Small  => "S",
        _                      => "L",
    };

    internal static List<GameObject> LoadPrefabs(string folder, string prefix)
    {
        var list  = new List<GameObject>();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(prefix) &&
                !Path.GetFileName(path).StartsWith(prefix, System.StringComparison.Ordinal))
                continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) list.Add(prefab);
        }
        return list;
    }
}
