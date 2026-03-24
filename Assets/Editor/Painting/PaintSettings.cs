using UnityEngine;

/// <summary>
/// Tool mode active when the user interacts with the grid.
///
///   Select  – no data written; clicks/drags only update SelectionState.
///   Paint   – writes profile / biome to every touched cell.
///   Erase   – marks touched cells Empty and clears their profiles.
///   Replace – like Paint but only touches cells that match a biome filter.
/// </summary>
public enum PaintTool { Select, Paint, Erase, Replace }

/// <summary>
/// Runtime configuration for the paint brush.
/// Owned by FieldBuilderWindow; read by BrushPainter and PaintToolPanel.
///
/// Not serialized — paint settings reset when the window is closed,
/// which is intentional (they are a tool configuration, not asset data).
/// </summary>
public class PaintSettings
{
    // ── Active tool ───────────────────────────────────────────────────────────
    public PaintTool tool = PaintTool.Select;

    // ── Brush geometry ────────────────────────────────────────────────────────
    /// <summary>0 = single cell, 1 = 3×3, 2 = 5×5 …</summary>
    public int        brushRadius = 0;
    public BrushShape brushShape  = BrushShape.Square;

    // ── What to apply (Paint / Replace) ──────────────────────────────────────

    /// <summary>Profile to assign to painted cells.</summary>
    public TerrainProfile    terrainProfile;
    /// <summary>When true, terrainProfile is written. When false, it is left unchanged.</summary>
    public bool              applyProfile      = true;

    public BiomeType         biome             = BiomeType.Grassland;
    /// <summary>When true, biome is written.</summary>
    public bool              applyBiome        = false;

    public VegetationProfile vegetProfile;
    /// <summary>When true, vegetProfile is written.</summary>
    public bool              applyVegetProfile = false;

    /// <summary>
    /// Reset each painted cell's randomSeed to -1 (re-derived from globalSeed).
    /// Gives varied tile/prefab selection without needing manual seed tweaking.
    /// </summary>
    public bool              randomizeSeed     = true;

    // ── Replace filter ────────────────────────────────────────────────────────
    /// <summary>When true, Replace only paints cells whose biome matches filterBiome.</summary>
    public bool      filterByBiome = false;
    public BiomeType filterBiome   = BiomeType.Grassland;
}
