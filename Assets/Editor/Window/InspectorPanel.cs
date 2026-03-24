using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Draws an inspector-style side panel for editing selected cell properties.
///
/// Responsibilities
///   • Collect the CellData objects for all selected cells.
///   • Render each editable field with showMixedValue support for multi-cell selection.
///   • Route every mutation through FieldEditorUndo so changes are undoable and
///     the asset is marked dirty.
///   • Fire OnDataChanged so the caller (FieldBuilderWindow) can trigger Repaint.
///
/// Single-cell vs multi-cell behaviour
///   • Single cell: all fields shown, including the Tags section.
///   • Multi-cell:  Tags section is hidden; all other fields use showMixedValue
///     when values differ across the selection.
///
/// This class owns no data — it reads from FieldGridData / CellData and writes
/// back through FieldEditorUndo only.
/// </summary>
public class InspectorPanel
{
    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired whenever any cell property is mutated.</summary>
    public event Action OnDataChanged;

    // ── Foldout state (runtime-only, no serialization needed) ─────────────────
    private bool _foldTransform = true;
    private bool _foldTerrain   = true;
    private bool _foldFlags     = true;
    private bool _foldTags      = true;

    private Vector2 _scroll;

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Draw the inspector panel. Must be called from an EditorWindow.OnGUI context.
    /// </summary>
    public void Draw(FieldGridData data, SelectionState selection)
    {
        if (data == null)
        {
            EditorGUILayout.HelpBox("Assign a Grid Data asset to edit cells.", MessageType.None);
            return;
        }

        if (!selection.HasSelection)
        {
            EditorGUILayout.HelpBox("Click a cell to select it.", MessageType.None);
            return;
        }

        var cells = CollectCells(data, selection);
        if (cells.Count == 0) return;

        bool multi = cells.Count > 1;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // ── Header ────────────────────────────────────────────────────────────
        string header = multi
            ? $"{cells.Count} cells selected"
            : $"Cell  ({cells[0].coord.x}, {cells[0].coord.y})";
        EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // ── Sections ──────────────────────────────────────────────────────────
        DrawTransformSection(data, cells, multi);
        DrawTerrainSection(data, cells, multi);
        DrawFlagsSection(data, cells, multi);

        if (!multi)
            DrawTagsSection(data, cells[0]);

        // ── Reset button ──────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        if (GUILayout.Button(multi ? "Reset Selected Cells to Defaults" : "Reset Cell to Defaults"))
        {
            FieldEditorUndo.BeginGroup("Reset Cell(s)");
            foreach (var cell in cells)
            {
                FieldEditorUndo.RecordDataChange(data, "Reset Cell");
                ResetToDefaults(cell);
                cell.isDirty = true;
            }
            FieldEditorUndo.MarkDataDirty(data);
            FieldEditorUndo.EndGroup();
            OnDataChanged?.Invoke();
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Section: Transform ────────────────────────────────────────────────────

    private void DrawTransformSection(FieldGridData data, List<CellData> cells, bool multi)
    {
        _foldTransform = EditorGUILayout.BeginFoldoutHeaderGroup(_foldTransform, "Transform");
        if (!_foldTransform) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        // Height offset
        DrawFloatField(data, cells, multi,
            label:    "Height Offset",
            getValue: c => c.heightOffset,
            setValue: (c, v) => c.heightOffset = v);

        // Rotation Y (slider 0–360)
        DrawSliderField(data, cells, multi,
            label:    "Rotation Y",
            min: 0f, max: 360f,
            getValue: c => c.rotationY,
            setValue: (c, v) => c.rotationY = v);

        // Scale (min 0.01)
        DrawFloatField(data, cells, multi,
            label:    "Scale",
            getValue: c => c.scale,
            setValue: (c, v) => c.scale = Mathf.Max(0.01f, v));

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Section: Terrain ──────────────────────────────────────────────────────

    private void DrawTerrainSection(FieldGridData data, List<CellData> cells, bool multi)
    {
        _foldTerrain = EditorGUILayout.BeginFoldoutHeaderGroup(_foldTerrain, "Terrain");
        if (!_foldTerrain) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        // TerrainProfile ObjectField
        DrawObjectField<TerrainProfile>(data, cells, multi,
            label:    "Terrain Profile",
            getValue: c => c.terrainProfile,
            setValue: (c, v) => c.terrainProfile = v);

        // TerrainPrefab override
        DrawObjectField<GameObject>(data, cells, multi,
            label:    "Prefab Override",
            getValue: c => c.terrainPrefabOverride,
            setValue: (c, v) => c.terrainPrefabOverride = v);

        // Biome
        DrawEnumField<BiomeType>(data, cells, multi,
            label:    "Biome",
            getValue: c => c.biome,
            setValue: (c, v) => c.biome = v);

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Section: Flags ────────────────────────────────────────────────────────

    private void DrawFlagsSection(FieldGridData data, List<CellData> cells, bool multi)
    {
        _foldFlags = EditorGUILayout.BeginFoldoutHeaderGroup(_foldFlags, "Flags");
        if (!_foldFlags) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        // Draw each flag as a toggle; mixed = at least one cell differs
        DrawFlagToggle(data, cells, multi, CellFlags.Walkable,     "Walkable");
        DrawFlagToggle(data, cells, multi, CellFlags.Blocked,      "Blocked");
        DrawFlagToggle(data, cells, multi, CellFlags.Empty,        "Empty");
        DrawFlagToggle(data, cells, multi, CellFlags.Locked,       "Locked");
        DrawFlagToggle(data, cells, multi, CellFlags.HasTerrain,   "Has Terrain");
        DrawFlagToggle(data, cells, multi, CellFlags.HasVegetation,"Has Vegetation");
        DrawFlagToggle(data, cells, multi, CellFlags.SpawnPoint,   "Spawn Point");
        DrawFlagToggle(data, cells, multi, CellFlags.WaterSurface, "Water Surface");

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Section: Tags (single cell only) ─────────────────────────────────────

    private void DrawTagsSection(FieldGridData data, CellData cell)
    {
        _foldTags = EditorGUILayout.BeginFoldoutHeaderGroup(_foldTags, "Tags  (single cell)");
        if (!_foldTags) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        int removeIndex = -1;

        for (int i = 0; i < cell.tags.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string oldKey = cell.tags[i].key;
                string oldVal = cell.tags[i].value;

                EditorGUI.BeginChangeCheck();
                string newKey = EditorGUILayout.DelayedTextField(oldKey, GUILayout.Width(90));
                string newVal = EditorGUILayout.DelayedTextField(oldVal, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    FieldEditorUndo.RecordDataChange(data, "Edit Tag");
                    cell.tags[i] = new CellTag { key = newKey, value = newVal };
                    FieldEditorUndo.MarkDataDirty(data);
                    OnDataChanged?.Invoke();
                }

                if (GUILayout.Button("✕", GUILayout.Width(20)))
                    removeIndex = i;
            }
        }

        if (removeIndex >= 0)
        {
            FieldEditorUndo.RecordDataChange(data, "Remove Tag");
            cell.tags.RemoveAt(removeIndex);
            FieldEditorUndo.MarkDataDirty(data);
            OnDataChanged?.Invoke();
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button("+ Add Tag"))
        {
            FieldEditorUndo.RecordDataChange(data, "Add Tag");
            cell.tags.Add(new CellTag { key = "key", value = "value" });
            FieldEditorUndo.MarkDataDirty(data);
            OnDataChanged?.Invoke();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Generic field helpers ─────────────────────────────────────────────────

    private void DrawFloatField(
        FieldGridData data, List<CellData> cells, bool multi,
        string label,
        Func<CellData, float>   getValue,
        Action<CellData, float> setValue)
    {
        float first  = getValue(cells[0]);
        bool  mixed  = multi && ValuesAreMixed(cells, getValue);

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        float next = EditorGUILayout.FloatField(label, mixed ? 0f : first);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck())
            ApplyToAll(data, cells, setValue, next, label);
    }

    private void DrawSliderField(
        FieldGridData data, List<CellData> cells, bool multi,
        string label, float min, float max,
        Func<CellData, float>   getValue,
        Action<CellData, float> setValue)
    {
        float first = getValue(cells[0]);
        bool  mixed = multi && ValuesAreMixed(cells, getValue);

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        float next = EditorGUILayout.Slider(label, mixed ? min : first, min, max);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck())
            ApplyToAll(data, cells, setValue, next, label);
    }

    private void DrawObjectField<T>(
        FieldGridData data, List<CellData> cells, bool multi,
        string label,
        Func<CellData, T>   getValue,
        Action<CellData, T> setValue)
        where T : UnityEngine.Object
    {
        T    first = getValue(cells[0]);
        bool mixed = multi && ObjectValuesAreMixed(cells, getValue);

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        var next = (T)EditorGUILayout.ObjectField(
            label, mixed ? null : first, typeof(T), allowSceneObjects: false);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck())
            ApplyToAll(data, cells, setValue, next, label);
    }

    private void DrawEnumField<TEnum>(
        FieldGridData data, List<CellData> cells, bool multi,
        string label,
        Func<CellData, TEnum>   getValue,
        Action<CellData, TEnum> setValue)
        where TEnum : Enum
    {
        TEnum first = getValue(cells[0]);
        bool  mixed = multi && EnumValuesAreMixed(cells, getValue);

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        var next = (TEnum)EditorGUILayout.EnumPopup(label, mixed ? first : first);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck())
            ApplyToAll(data, cells, setValue, next, label);
    }

    private void DrawFlagToggle(
        FieldGridData data, List<CellData> cells, bool multi,
        CellFlags flag, string label)
    {
        bool firstSet = cells[0].HasFlag(flag);
        bool mixed    = false;
        if (multi)
        {
            foreach (var c in cells)
                if (c.HasFlag(flag) != firstSet) { mixed = true; break; }
        }

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        bool next = EditorGUILayout.Toggle(label, mixed ? false : firstSet);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck())
        {
            FieldEditorUndo.BeginGroup($"Toggle {flag}");
            foreach (var cell in cells)
            {
                FieldEditorUndo.RecordDataChange(data, $"Toggle {flag}");
                if (next) cell.SetFlag(flag);
                else      cell.ClearFlag(flag);
                cell.isDirty = true;
            }
            FieldEditorUndo.MarkDataDirty(data);
            FieldEditorUndo.EndGroup();
            OnDataChanged?.Invoke();
        }
    }

    // ── Batch apply helper ────────────────────────────────────────────────────

    private void ApplyToAll<T>(
        FieldGridData data, List<CellData> cells,
        Action<CellData, T> setValue, T value, string label)
    {
        FieldEditorUndo.BeginGroup($"Edit {label}");
        foreach (var cell in cells)
        {
            FieldEditorUndo.RecordDataChange(data, $"Edit {label}");
            setValue(cell, value);
            cell.isDirty = true;   // mark for incremental scene regeneration
        }
        FieldEditorUndo.MarkDataDirty(data);
        FieldEditorUndo.EndGroup();
        OnDataChanged?.Invoke();
    }

    // ── Mixed-value detection helpers ─────────────────────────────────────────

    private static bool ValuesAreMixed<T>(List<CellData> cells, Func<CellData, T> get)
        where T : IEquatable<T>
    {
        T first = get(cells[0]);
        for (int i = 1; i < cells.Count; i++)
            if (!get(cells[i]).Equals(first)) return true;
        return false;
    }

    private static bool ObjectValuesAreMixed<T>(List<CellData> cells, Func<CellData, T> get)
        where T : UnityEngine.Object
    {
        T first = get(cells[0]);
        for (int i = 1; i < cells.Count; i++)
            if (get(cells[i]) != first) return true;
        return false;
    }

    private static bool EnumValuesAreMixed<TEnum>(List<CellData> cells, Func<CellData, TEnum> get)
        where TEnum : Enum
    {
        TEnum first = get(cells[0]);
        for (int i = 1; i < cells.Count; i++)
            if (!get(cells[i]).Equals(first)) return true;
        return false;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static List<CellData> CollectCells(FieldGridData data, SelectionState selection)
    {
        var list = new List<CellData>(selection.SelectionCount);
        foreach (var coord in selection.SelectedCells)
        {
            var cell = data.GetCell(coord.x, coord.y);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    private static void ResetToDefaults(CellData cell)
    {
        cell.heightOffset          = 0f;
        cell.rotationY             = 0f;
        cell.scale                 = 1f;
        cell.terrainProfile        = null;
        cell.terrainPrefabOverride = null;
        cell.biome                 = BiomeType.Grassland;
        cell.flags                 = CellFlags.Walkable;
        cell.tags.Clear();
        cell.randomSeed            = -1;
    }
}
