using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Applies paint operations to CellData inside a FieldGridData asset.
///
/// Rules
/// ─────
/// • ApplyStroke is the only public entry point.
/// • It calls Undo.RecordObject / EditorUtility.SetDirty directly rather than
///   routing through FieldEditorUndo, because grouping is managed externally by
///   the caller (brush-stroke begin / end events in FieldBuilderWindow).
/// • Each call is a single atomic record — Unity's undo system will collapse
///   all records in the caller's group into one Ctrl-Z step.
/// • Cells filtered out (out of bounds or failing Replace filter) are silently
///   skipped; the method never throws.
/// </summary>
public static class BrushPainter
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="settings"/> to every valid cell at
    /// <paramref name="coords"/> inside <paramref name="data"/>.
    /// Returns the number of cells actually modified.
    /// Does nothing when tool == Select.
    /// </summary>
    public static int ApplyStroke(
        FieldGridData           data,
        IEnumerable<Vector2Int> coords,
        PaintSettings           settings)
    {
        if (data == null || settings == null || settings.tool == PaintTool.Select)
            return 0;

        var targets = CollectTargets(data, coords, settings);
        if (targets.Count == 0) return 0;

        Undo.RecordObject(data, UndoLabel(settings.tool));

        foreach (var cell in targets)
        {
            ApplyToCell(cell, settings);
            cell.isDirty = true;
        }

        EditorUtility.SetDirty(data);
        return targets.Count;
    }

    // ── Private: per-cell mutation ────────────────────────────────────────────

    private static void ApplyToCell(CellData cell, PaintSettings s)
    {
        switch (s.tool)
        {
            case PaintTool.Paint:
            case PaintTool.Replace:
                if (s.applyProfile)      cell.terrainProfile = s.terrainProfile;
                if (s.applyBiome)        cell.biome          = s.biome;
                if (s.applyVegetProfile) cell.vegetProfile   = s.vegetProfile;
                if (s.randomizeSeed)     cell.randomSeed     = -1;   // re-derive each generate
                cell.ClearFlag(CellFlags.Empty);                      // painting un-empties a cell
                break;

            case PaintTool.Erase:
                cell.terrainProfile        = null;
                cell.terrainPrefabOverride = null;
                cell.vegetProfile          = null;
                cell.vegetPrefabOverrides?.Clear();
                cell.SetFlag(CellFlags.Empty);
                break;
        }
    }

    // ── Private: filtering ────────────────────────────────────────────────────

    private static List<CellData> CollectTargets(
        FieldGridData           data,
        IEnumerable<Vector2Int> coords,
        PaintSettings           settings)
    {
        var list = new List<CellData>();
        foreach (var coord in coords)
        {
            var cell = data.GetCell(coord.x, coord.y);
            if (cell == null) continue;

            // Replace mode: skip cells that don't match the biome filter
            if (settings.tool == PaintTool.Replace
                && settings.filterByBiome
                && cell.biome != settings.filterBiome)
                continue;

            list.Add(cell);
        }
        return list;
    }

    private static string UndoLabel(PaintTool tool) => tool switch
    {
        PaintTool.Paint   => "Paint Cells",
        PaintTool.Erase   => "Erase Cells",
        PaintTool.Replace => "Replace Cells",
        _                 => "Paint Operation",
    };
}
