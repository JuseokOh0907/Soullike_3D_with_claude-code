using System;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Draws the paint-tool configuration panel inside a settings column.
///
/// Layout (condensed):
///   ┌─ Paint Tool ───────────────────────────────────────────────┐
///   │  [Select]  [Paint]  [Erase]  [Replace]                    │
///   │                                                            │
///   │  Radius  [slider 0-5]   Shape [Square ▼]                  │
///   │                                                            │
///   │  (Paint / Replace)                                         │
///   │  [✓] Profile    [──────────────────────────────]          │
///   │  [ ] Biome      [Grassland                     ▼]         │
///   │  [ ] Vegetation [──────────────────────────────]          │
///   │  [✓] Randomize seed                                        │
///   │                                                            │
///   │  (Replace only)                                            │
///   │  [✓] Only replace  [Grassland ▼]                          │
///   └────────────────────────────────────────────────────────────┘
///
/// PaintToolPanel owns no data — it reads and writes PaintSettings.
/// Call Draw() from OnGUI; wire OnSettingsChanged to sync SelectionState.brush.
/// </summary>
public class PaintToolPanel
{
    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired whenever any setting changes (caller should sync brush + Repaint).</summary>
    public event Action OnSettingsChanged;

    // ── Public state ──────────────────────────────────────────────────────────
    public PaintSettings Settings { get; }

    public PaintToolPanel(PaintSettings settings) =>
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));

    // ── Public entry point ────────────────────────────────────────────────────

    public void Draw()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // Title omitted here — the window draws a header label above this panel

            DrawToolbar();

            if (Settings.tool == PaintTool.Select) return;

            EditorGUILayout.Space(2);
            DrawBrushSection();

            if (Settings.tool == PaintTool.Erase) return;

            EditorGUILayout.Space(2);
            DrawApplySection();

            if (Settings.tool == PaintTool.Replace)
            {
                EditorGUILayout.Space(2);
                DrawReplaceFilter();
            }
        }
    }

    // ── Private sections ──────────────────────────────────────────────────────

    private void DrawToolbar()
    {
        int cur  = (int)Settings.tool;
        int next = GUILayout.Toolbar(cur,
            new[] { "Select", "Paint", "Erase", "Replace" },
            EditorStyles.miniButton);
        if (next != cur)
        {
            Settings.tool = (PaintTool)next;
            OnSettingsChanged?.Invoke();
        }
    }

    private void DrawBrushSection()
    {
        EditorGUI.BeginChangeCheck();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Radius", GUILayout.Width(48));
            Settings.brushRadius = EditorGUILayout.IntSlider(Settings.brushRadius, 0, 5);
        }
        Settings.brushShape = (BrushShape)EditorGUILayout.EnumPopup("Shape", Settings.brushShape);

        if (EditorGUI.EndChangeCheck())
            OnSettingsChanged?.Invoke();
    }

    private void DrawApplySection()
    {
        EditorGUILayout.LabelField("Apply", EditorStyles.miniLabel);

        EditorGUI.BeginChangeCheck();

        // Profile toggle + field
        using (new EditorGUILayout.HorizontalScope())
        {
            Settings.applyProfile = EditorGUILayout.Toggle(Settings.applyProfile, GUILayout.Width(14));
            using (new EditorGUI.DisabledScope(!Settings.applyProfile))
                Settings.terrainProfile = (TerrainProfile)EditorGUILayout.ObjectField(
                    "Profile", Settings.terrainProfile, typeof(TerrainProfile),
                    allowSceneObjects: false);
        }

        // Biome toggle + enum
        using (new EditorGUILayout.HorizontalScope())
        {
            Settings.applyBiome = EditorGUILayout.Toggle(Settings.applyBiome, GUILayout.Width(14));
            using (new EditorGUI.DisabledScope(!Settings.applyBiome))
                Settings.biome = (BiomeType)EditorGUILayout.EnumPopup("Biome", Settings.biome);
        }

        // Vegetation toggle + field
        using (new EditorGUILayout.HorizontalScope())
        {
            Settings.applyVegetProfile = EditorGUILayout.Toggle(Settings.applyVegetProfile, GUILayout.Width(14));
            using (new EditorGUI.DisabledScope(!Settings.applyVegetProfile))
                Settings.vegetProfile = (VegetationProfile)EditorGUILayout.ObjectField(
                    "Vegetation", Settings.vegetProfile, typeof(VegetationProfile),
                    allowSceneObjects: false);
        }

        Settings.randomizeSeed = EditorGUILayout.ToggleLeft(
            new GUIContent("Randomize seed",
                "Resets per-cell seed so each Generate call picks a fresh random tile."),
            Settings.randomizeSeed);

        if (EditorGUI.EndChangeCheck())
            OnSettingsChanged?.Invoke();
    }

    private void DrawReplaceFilter()
    {
        EditorGUILayout.LabelField("Replace Filter", EditorStyles.miniLabel);

        EditorGUI.BeginChangeCheck();

        using (new EditorGUILayout.HorizontalScope())
        {
            Settings.filterByBiome = EditorGUILayout.Toggle(Settings.filterByBiome, GUILayout.Width(14));
            using (new EditorGUI.DisabledScope(!Settings.filterByBiome))
                Settings.filterBiome = (BiomeType)EditorGUILayout.EnumPopup(
                    "Only replace", Settings.filterBiome);
        }

        if (EditorGUI.EndChangeCheck())
            OnSettingsChanged?.Invoke();
    }
}
