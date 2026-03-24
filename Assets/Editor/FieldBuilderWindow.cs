using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor window that builds a low-poly field from LMHPOLY terrain and vegetation prefabs.
/// Menu: Tools > Field Builder
///
/// Layout
///   ┌──────────────┬──────────────────────────────────────┐
///   │ [toolbar: Grid Data asset field]                    │
///   ├──────────────┼──────────────────────────────────────┤
///   │              │                                      │
///   │  2D Grid     │  Generation settings (existing)      │
///   │  Preview     │                                      │
///   │              │                                      │
///   └──────────────┴──────────────────────────────────────┘
/// </summary>
public class FieldBuilderWindow : EditorWindow
{
    // ════════════════════════════════════════════════════════════════════════════
    // EXISTING FIELDS — unchanged
    // ════════════════════════════════════════════════════════════════════════════

    // ── Terrain ───────────────────────────────────────────────────────────────
    private int   gridWidth  = 5;
    private int   gridHeight = 5;
    private float tileSize   = 10f;

    private enum TerrainType { MT, CPT }
    private TerrainType terrainType = TerrainType.MT;

    private static readonly char[]   ALL_STYLES    = { 'a', 'b', 'c', 'd', 'e', 'f' };
    private bool[]                   styleEnabled  = { true, false, false, false, false, false };
    private static readonly string[] STYLE_LABELS  =
    {
        "a – Green flat",
        "b – Mixed green",
        "c – Rocky",
        "d – Sandy/warm",
        "e – Snow/cold",
        "f – Dark/volcanic",
    };

    // ── Vegetation ────────────────────────────────────────────────────────────
    private int   grassPerTile  = 10;
    private int   bushesTotal   = 15;
    private int   flowersTotal  = 20;
    private float vegetScaleMin = 1.5f;
    private float vegetScaleMax = 3.0f;
    private bool  scaleVariance = true;

    // ── Culling ───────────────────────────────────────────────────────────────
    private bool  enableCulling  = true;
    private float raycastOriginY = 100f;

    // ── Misc ──────────────────────────────────────────────────────────────────
    private int     randomSeed = 42;
    private Vector2 scroll;

    // ── Asset paths ───────────────────────────────────────────────────────────
    const string TERRAIN_BASE = "Assets/LMHPOLY/Low Poly Nature Bundle/Modular Terrain/Terrain_Assets/Prefabs/Terrain";
    const string GRASS_PATH   = "Assets/LMHPOLY/Low Poly Nature Bundle/Vegetation/Vegetation Assets/Prefabs/Grass/Grass3D";
    const string BUSH_PATH    = "Assets/LMHPOLY/Low Poly Nature Bundle/Vegetation/Vegetation Assets/Prefabs/Bushes/Bush";
    const string FLOWER_PATH  = "Assets/LMHPOLY/Low Poly Nature Bundle/Vegetation/Vegetation Assets/Prefabs/Flowers/TwoSided";
    const string ROOT_NAME    = "Field";

    // ════════════════════════════════════════════════════════════════════════════
    // NEW FIELDS — preview system
    // ════════════════════════════════════════════════════════════════════════════

    // Persisted across domain reloads by [SerializeField]
    [SerializeField] private FieldGridData _gridData;
    [SerializeField] private float         _previewPanelWidth = 320f;

    // Runtime-only (recreated in OnEnable)
    private SelectionState      _selection;
    private GridView            _gridView;
    private GridInputController _inputController;

    private InspectorPanel  _inspectorPanel;
    private PaintSettings   _paintSettings = new PaintSettings();
    private PaintToolPanel  _paintToolPanel;

    // Room layout preset state
    private RoomLayoutPreset.RoomConfig _roomCfg  = RoomLayoutPreset.RoomConfig.Default;
    private bool                        _roomFold = true;

    // Internal state
    private bool _hasInitializedView;
    private bool _isDraggingSplitter;

    // Splitter config
    private const float SplitterWidth   = 4f;
    private const float MinPreviewWidth = 140f;
    private const float MinSettingsWidth = 200f;

    // ════════════════════════════════════════════════════════════════════════════
    // MENU
    // ════════════════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Field Builder")]
    public static void ShowWindow() =>
        GetWindow<FieldBuilderWindow>("Field Builder");

    // ════════════════════════════════════════════════════════════════════════════
    // NEW — lifecycle
    // ════════════════════════════════════════════════════════════════════════════

    private void OnEnable()
    {
        titleContent  = new GUIContent("Field Builder");
        wantsMouseMove = true;   // required for hover detection

        _selection       = new SelectionState();
        _gridView        = new GridView();
        _inputController = new GridInputController(_gridView, _selection);
        _inspectorPanel  = new InspectorPanel();
        _paintToolPanel  = new PaintToolPanel(_paintSettings);

        // Restore grid data reference that survived domain reload
        if (_gridData != null)
            _gridView.Data = _gridData;

        _gridView.Selection = _selection;

        // Wire controller events
        _inputController.OnCellClicked   += HandleCellClicked;
        _inputController.OnCellHovered   += HandleCellHovered;
        _inputController.OnRectCommitted += HandleRectCommitted;
        _inputController.OnDeletePressed += HandleDeletePressed;
        _inputController.OnFitRequested  += HandleFitRequested;
        _inputController.OnBrushStarted  += HandleBrushStarted;
        _inputController.OnBrushStroke   += HandleBrushStroke;
        _inputController.OnBrushEnded    += HandleBrushEnded;

        // Wire inspector + paint panel events
        _inspectorPanel.OnDataChanged      += Repaint;
        _paintToolPanel.OnSettingsChanged  += SyncBrushToSelection;
    }

    private void OnDisable()
    {
        if (_inputController != null)
        {
            _inputController.OnCellClicked   -= HandleCellClicked;
            _inputController.OnCellHovered   -= HandleCellHovered;
            _inputController.OnRectCommitted -= HandleRectCommitted;
            _inputController.OnDeletePressed -= HandleDeletePressed;
            _inputController.OnFitRequested  -= HandleFitRequested;
            _inputController.OnBrushStarted  -= HandleBrushStarted;
            _inputController.OnBrushStroke   -= HandleBrushStroke;
            _inputController.OnBrushEnded    -= HandleBrushEnded;
        }

        if (_inspectorPanel != null)
            _inspectorPanel.OnDataChanged -= Repaint;

        if (_paintToolPanel != null)
            _paintToolPanel.OnSettingsChanged -= SyncBrushToSelection;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // MODIFIED — OnGUI restructured; all original content moved to DrawSettingsPanel
    // ════════════════════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        DrawHeader();

        using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
        {
            DrawPreviewPanel();
            DrawSplitter();
            DrawSettingsPanel();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // NEW — layout panels
    // ════════════════════════════════════════════════════════════════════════════

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Field Builder", EditorStyles.boldLabel, GUILayout.Width(95));
            GUILayout.FlexibleSpace();

            // Grid Data asset field
            EditorGUI.BeginChangeCheck();
            var picked = (FieldGridData)EditorGUILayout.ObjectField(
                _gridData, typeof(FieldGridData), allowSceneObjects: false,
                GUILayout.Width(185));
            if (EditorGUI.EndChangeCheck())
                AssignGridData(picked);

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(36)))
                CreateNewGridData();
        }
    }

    private void DrawPreviewPanel()
    {
        // Allocate a rect from the layout system
        Rect panelRect = GUILayoutUtility.GetRect(
            _previewPanelWidth, 10f,
            GUILayout.Width(_previewPanelWidth),
            GUILayout.ExpandHeight(true));

        // Auto-fit the first time data is available and the panel has a real size
        if (_gridData != null && !_hasInitializedView
            && Event.current.type == EventType.Repaint
            && panelRect.width > 1f)
        {
            _gridView.FitToView(panelRect);
            _hasInitializedView = true;
        }

        // Draw grid (always, even without data — shows placeholder)
        _gridView.Draw(panelRect);

        // Overlay mode toolbar (Single / Rect / Brush)
        DrawModeOverlay(panelRect);

        // Feed input events to the controller
        if (_gridData != null && Event.current.type != EventType.Layout)
            _inputController.HandleEvent(Event.current, panelRect);
    }

    private void DrawModeOverlay(Rect panelRect)
    {
        const float BtnW = 48f;
        const float BtnH = 18f;

        var toolbarRect = new Rect(panelRect.x + 4f, panelRect.y + 4f, BtnW * 3f, BtnH);

        using (new EditorGUI.DisabledScope(_gridData == null))
        {
            int current  = (int)_selection.mode;
            int selected = GUI.Toolbar(toolbarRect, current,
                new[] { "Single", "Rect", "Brush" }, EditorStyles.miniButton);
            if (selected != current)
            {
                _selection.mode = (SelectionMode)selected;
                Repaint();
            }
        }
    }

    // ── Step 1: Grid Setup ───────────────────────────────────────────────────
    private void DrawGridSetupPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("① Grid Setup", EditorStyles.boldLabel);

            // Grid dimensions — editable inline, applied immediately via Resize
            EditorGUI.BeginChangeCheck();
            int   newW = EditorGUILayout.IntSlider("Width",     _gridData.width,     1, 20);
            int   newH = EditorGUILayout.IntSlider("Height",    _gridData.height,    1, 20);
            float newT = EditorGUILayout.FloatField("Tile Size", _gridData.tileSize);
            if (EditorGUI.EndChangeCheck())
            {
                FieldEditorUndo.RecordDataChange(_gridData, "Resize Grid");
                _gridData.tileSize = Mathf.Max(0.1f, newT);
                _gridData.Resize(newW, newH);
                FieldEditorUndo.MarkDataDirty(_gridData);
                _hasInitializedView = false;
                Repaint();
            }

            EditorGUILayout.Space(4);

            // Default profiles — most important settings
            GUILayout.Label("Default Profiles  (applied to cells with no override)",
                EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            var newTerrain = (TerrainProfile)EditorGUILayout.ObjectField(
                "Terrain Profile", _gridData.defaultTerrainProfile,
                typeof(TerrainProfile), allowSceneObjects: false);
            var newVeget = (VegetationProfile)EditorGUILayout.ObjectField(
                "Vegetation Profile", _gridData.defaultVegetProfile,
                typeof(VegetationProfile), allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                FieldEditorUndo.RecordDataChange(_gridData, "Set Default Profile");
                _gridData.defaultTerrainProfile = newTerrain;
                _gridData.defaultVegetProfile   = newVeget;
                FieldEditorUndo.MarkDataDirty(_gridData);
            }

            // Warn when no default profile is set
            if (_gridData.defaultTerrainProfile == null)
                EditorGUILayout.HelpBox(
                    "Default Terrain Profile is not set.\n" +
                    "Cells with no per-cell profile will generate nothing.\n" +
                    "→ Create: right-click Project → Field Builder → Terrain Profile",
                    MessageType.Warning);
        }
    }

    // ── Step 1b: Room Layout Preset ──────────────────────────────────────────
    private void DrawRoomLayoutPanel()
    {
        _roomFold = EditorGUILayout.BeginFoldoutHeaderGroup(_roomFold, "Room Layout  (Souls-like)");
        if (!_roomFold) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // Visual diagram
            EditorGUILayout.LabelField(
                "W=Wall  F=Floor  D=Door(empty)",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(
                "[ W ][ W ][ D ][ W ][ W ]",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(
                "[ D ][ F ][ F ][ F ][ D ]",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(
                "[ W ][ W ][ D ][ W ][ W ]",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(6);

            // Wall profile
            _roomCfg.wallProfile = (TerrainProfile)EditorGUILayout.ObjectField(
                new GUIContent("Wall Profile",
                    "Terrain profile for edge cells (hills / cliffs). " +
                    "Recommended: rocky style (c or f)."),
                _roomCfg.wallProfile, typeof(TerrainProfile), allowSceneObjects: false);

            // Floor profile
            _roomCfg.floorProfile = (TerrainProfile)EditorGUILayout.ObjectField(
                new GUIContent("Floor Profile",
                    "Terrain profile for interior cells. " +
                    "Recommended: flat style (a)."),
                _roomCfg.floorProfile, typeof(TerrainProfile), allowSceneObjects: false);

            EditorGUILayout.Space(4);

            // Heights
            _roomCfg.wallHeight = EditorGUILayout.FloatField(
                new GUIContent("Wall Height",
                    "Y offset added to every perimeter cell. " +
                    "Higher value = steeper surrounding hills."),
                _roomCfg.wallHeight);

            _roomCfg.floorHeight = EditorGUILayout.FloatField(
                new GUIContent("Floor Height",
                    "Y offset for interior cells (usually 0)."),
                _roomCfg.floorHeight);

            // Door width
            _roomCfg.doorWidth = EditorGUILayout.IntSlider(
                new GUIContent("Door Width",
                    "How many cells wide each entrance is. " +
                    "1 = tight passage, 2-3 = open gateway."),
                _roomCfg.doorWidth, 1, Mathf.Max(1, Mathf.Min(_gridData.width, _gridData.height) / 2 - 1));

            EditorGUILayout.Space(2);

            // Door position preview
            EditorGUILayout.LabelField(DoorPreviewLabel(), EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(6);

            // Warnings
            if (_roomCfg.wallProfile == null)
                EditorGUILayout.HelpBox(
                    "Wall Profile not set — edge cells will use the grid default.",
                    MessageType.Warning);
            if (_roomCfg.floorProfile == null)
                EditorGUILayout.HelpBox(
                    "Floor Profile not set — interior cells will use the grid default.",
                    MessageType.Warning);

            // Apply button
            if (GUILayout.Button(
                new GUIContent("Apply Room Layout",
                    "Automatically sets wall / floor / door cells on the whole grid.\n" +
                    "You can still adjust individual cells afterward.\n" +
                    "Ctrl+Z to undo."),
                GUILayout.Height(28)))
            {
                RoomLayoutPreset.Apply(_gridData, _roomCfg);
                Repaint();
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private string DoorPreviewLabel()
    {
        if (_gridData == null) return string.Empty;
        int w = _gridData.width;
        int h = _gridData.height;
        int dw = Mathf.Max(1, _roomCfg.doorWidth);

        int topStart    = (w - dw) / 2;
        int sideStart   = (h - dw) / 2;

        string topCols  = dw == 1 ? $"x={topStart}"  : $"x={topStart}–{topStart + dw - 1}";
        string sideRows = dw == 1 ? $"z={sideStart}" : $"z={sideStart}–{sideStart + dw - 1}";

        return $"Top/Bottom door: {topCols}   Left/Right door: {sideRows}";
    }

    // ── Step 2: Paint Tool header ────────────────────────────────────────────
    private void DrawPaintToolHeader()
    {
        // Concise guide label that changes based on current tool
        string hint = _paintSettings.tool switch
        {
            PaintTool.Select  => "② Paint Tool  —  Select a cell to edit its properties below.",
            PaintTool.Paint   => "② Paint Tool  —  Click or drag cells to apply the profile.",
            PaintTool.Erase   => "② Paint Tool  —  Click or drag cells to mark them Empty.",
            PaintTool.Replace => "② Paint Tool  —  Paints only cells matching the filter biome.",
            _                 => "② Paint Tool",
        };
        EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
    }

    // ── Step 3: Grid Generation ──────────────────────────────────────────────
    private void DrawGridGenerationPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("③ Generate", EditorStyles.boldLabel);

            // Cell coverage stats
            CountCellStats(out int total, out int withProfile, out int empty, out int dirty);
            int noProfile = total - withProfile - empty;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Total: {total}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Profiled: {withProfile}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Empty: {empty}", EditorStyles.miniLabel);
            }

            // Warn if any non-empty cells have no profile AND no default set
            if (noProfile > 0 && _gridData.defaultTerrainProfile == null)
                EditorGUILayout.HelpBox(
                    $"{noProfile} cell(s) have no Terrain Profile and no Default is set — " +
                    "they will be skipped during generation.",
                    MessageType.Warning);

            EditorGUILayout.Space(4);

            bool hasScene  = FieldBuilder.HasSceneObjects(_gridData);
            bool readyToGenerate = _gridData.defaultTerrainProfile != null || withProfile > 0;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!readyToGenerate))
                {
                    if (GUILayout.Button(
                        new GUIContent("Generate All",
                            "Destroy existing objects and regenerate every cell."),
                        GUILayout.Height(26)))
                        FieldBuilder.RebuildAll(_gridData);
                }

                using (new EditorGUI.DisabledScope(dirty == 0))
                {
                    if (GUILayout.Button(
                        new GUIContent(dirty > 0 ? $"Update ({dirty})" : "Update Changed",
                            "Regenerate only cells changed since last generate."),
                        GUILayout.Height(26)))
                        FieldBuilder.UpdateDirtyCells(_gridData);
                }

                using (new EditorGUI.DisabledScope(!hasScene))
                {
                    if (GUILayout.Button(
                        new GUIContent("Clear", "Destroy all generated scene objects."),
                        GUILayout.Height(26)))
                        FieldBuilder.ClearAll(_gridData);
                }
            }

            string status = hasScene ? "Scene: generated" : "Scene: empty";
            if (dirty > 0) status += $"  |  {dirty} cell(s) need update";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        }
    }

    // ── Cell stats helper ────────────────────────────────────────────────────
    private void CountCellStats(out int total, out int withProfile,
                                out int empty,  out int dirty)
    {
        total = withProfile = empty = dirty = 0;
        if (_gridData == null) return;

        foreach (var cell in _gridData.Cells)
        {
            if (cell == null) continue;
            total++;
            if (cell.HasFlag(CellFlags.Empty))  { empty++;  continue; }
            if (cell.terrainProfile != null || cell.terrainPrefabOverride != null)
                withProfile++;
            if (cell.isDirty) dirty++;
        }
    }

    private void DrawSplitter()
    {
        Rect splitter = GUILayoutUtility.GetRect(
            SplitterWidth, 10f,
            GUILayout.Width(SplitterWidth),
            GUILayout.ExpandHeight(true));

        EditorGUI.DrawRect(splitter, new Color(0f, 0f, 0f, 0.35f));
        EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);

        var e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && splitter.Contains(e.mousePosition))
        {
            _isDraggingSplitter = true;
            e.Use();
        }

        if (_isDraggingSplitter)
        {
            if (e.type == EventType.MouseDrag)
            {
                _previewPanelWidth = Mathf.Clamp(
                    e.mousePosition.x,
                    MinPreviewWidth,
                    position.width - MinSettingsWidth - SplitterWidth);
                e.Use();
                Repaint();
            }

            if (e.type == EventType.MouseUp)
            {
                _isDraggingSplitter = false;
                e.Use();
            }
        }
    }

    /// <summary>
    /// Settings panel — contains the original OnGUI body verbatim.
    /// Nothing inside this method has changed from the original implementation.
    /// </summary>
    private void DrawSettingsPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            if (_gridData == null)
            {
                // ── No data loaded — show getting-started guide ───────────────
                EditorGUILayout.HelpBox(
                    "No Grid Data loaded.\n\n" +
                    "① Click [New] in the toolbar to create a Grid Data asset.\n" +
                    "② Set width, height and a Default Terrain Profile.\n" +
                    "③ Use Paint Tool to assign profiles to zones.\n" +
                    "④ Click [Generate All] to build the scene.",
                    MessageType.Info);
            }
            else
            {
                // ── Step 1: Grid Setup ────────────────────────────────────────
                DrawGridSetupPanel();
                EditorGUILayout.Space(4);

                // ── Step 1b: Room Layout Preset ───────────────────────────────
                DrawRoomLayoutPanel();
                EditorGUILayout.Space(4);

                // ── Step 2: Paint Tool ────────────────────────────────────────
                DrawPaintToolHeader();
                _paintToolPanel.Draw();
                EditorGUILayout.Space(4);

                // ── Step 2b: Cell inspector (when a cell is selected) ─────────
                if (_selection.HasSelection)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        _inspectorPanel.Draw(_gridData, _selection);
                    EditorGUILayout.Space(4);
                }

                // ── Step 3: Generate ──────────────────────────────────────────
                DrawGridGenerationPanel();
                EditorGUILayout.Space(4);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            GUILayout.Label("Field Builder", EditorStyles.largeLabel);
            EditorGUILayout.Space(4);

            // ── Terrain section ──────────────────────────────────────────────
            GUILayout.Label("Terrain Grid", EditorStyles.boldLabel);
            gridWidth   = EditorGUILayout.IntSlider("Grid Width",       gridWidth,  1, 12);
            gridHeight  = EditorGUILayout.IntSlider("Grid Height",      gridHeight, 1, 12);
            tileSize    = EditorGUILayout.FloatField("Tile Size (units)", tileSize);
            terrainType = (TerrainType)EditorGUILayout.EnumPopup("Terrain Type", terrainType);

            EditorGUILayout.Space(4);
            GUILayout.Label("Terrain Styles  (select one or more)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int i = 0; i < ALL_STYLES.Length; i++)
                    styleEnabled[i] = EditorGUILayout.ToggleLeft(
                        $"  {STYLE_LABELS[i]}", styleEnabled[i]);
            }

            bool anyStyle = false;
            foreach (var s in styleEnabled) anyStyle |= s;
            if (!anyStyle)
                EditorGUILayout.HelpBox("Select at least one terrain style.", MessageType.Warning);

            EditorGUILayout.Space(6);

            // ── Vegetation section ───────────────────────────────────────────
            GUILayout.Label("Vegetation", EditorStyles.boldLabel);
            grassPerTile = EditorGUILayout.IntSlider("Grass per Tile", grassPerTile, 0, 40);
            bushesTotal  = EditorGUILayout.IntSlider("Total Bushes",   bushesTotal,  0, 80);
            flowersTotal = EditorGUILayout.IntSlider("Total Flowers",  flowersTotal, 0, 80);

            EditorGUILayout.Space(2);
            scaleVariance = EditorGUILayout.Toggle("Randomise Scale", scaleVariance);
            using (new EditorGUI.DisabledScope(!scaleVariance))
            {
                vegetScaleMin = EditorGUILayout.FloatField("  Scale Min", vegetScaleMin);
                vegetScaleMax = EditorGUILayout.FloatField("  Scale Max", vegetScaleMax);
            }
            if (!scaleVariance)
            {
                vegetScaleMin = EditorGUILayout.FloatField("  Scale", vegetScaleMin);
                vegetScaleMax = vegetScaleMin;
            }

            EditorGUILayout.Space(6);

            // ── Culling section ──────────────────────────────────────────────
            GUILayout.Label("Vegetation Culling", EditorStyles.boldLabel);
            enableCulling = EditorGUILayout.Toggle(
                new GUIContent("Cull Off-Terrain Objects",
                    "Raycasts down from above each candidate position.\n" +
                    "Objects that don't land on a terrain tile are removed."),
                enableCulling);
            using (new EditorGUI.DisabledScope(!enableCulling))
                raycastOriginY = EditorGUILayout.FloatField("  Raycast Origin Y", raycastOriginY);

            EditorGUILayout.Space(6);

            // ── Misc ─────────────────────────────────────────────────────────
            randomSeed = EditorGUILayout.IntField("Random Seed", randomSeed);

            EditorGUILayout.Space(10);

            // ── Buttons ──────────────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!anyStyle))
                {
                    if (GUILayout.Button("Build Field", GUILayout.Height(30)))
                        BuildField();
                }
                if (GUILayout.Button("Clear Field", GUILayout.Height(30)))
                    ClearField();
            }

            EditorGUILayout.Space(4);
            int totalGrass = grassPerTile * gridWidth * gridHeight;
            EditorGUILayout.HelpBox(
                $"Grid: {gridWidth}×{gridHeight}  |  " +
                $"Grass: ~{totalGrass}  |  Bushes: {bushesTotal}  |  Flowers: {flowersTotal}",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // NEW — controller event handlers
    // ════════════════════════════════════════════════════════════════════════════

    private void HandleCellClicked(Vector2Int coord)
    {
        bool additive = Event.current != null && Event.current.control;

        if (_gridData != null && _paintSettings.tool != PaintTool.Select)
        {
            IEnumerable<Vector2Int> stamp;

            if (_selection.mode == SelectionMode.Single)
            {
                // Single mode: always paint exactly the one clicked cell,
                // regardless of brush radius — this is the per-cell workflow.
                stamp = new[] { coord };
            }
            else
            {
                // Brush / Rect mode: apply the full brush radius stamp.
                var bounds = new RectInt(0, 0, _gridData.width, _gridData.height);
                stamp = _selection.brush.GetAffectedCells(coord, bounds);
            }

            FieldEditorUndo.BeginGroup($"{_paintSettings.tool} Cell");
            BrushPainter.ApplyStroke(_gridData, stamp, _paintSettings);
            FieldEditorUndo.EndGroup();
        }

        _selection.SelectSingle(coord, additive);
        Repaint();
    }

    private void HandleCellHovered(Vector2Int? coord)
    {
        // hoveredCell is already updated by the controller; just trigger a repaint.
        Repaint();
    }

    private void HandleRectCommitted(RectInt rect)
    {
        bool additive = Event.current != null && Event.current.control;
        _selection.CommitRect(rect, additive);

        if (_gridData != null && _paintSettings.tool != PaintTool.Select)
        {
            // Fill the entire committed rectangle
            var cells = new List<Vector2Int>(rect.width * rect.height);
            for (int ry = rect.y; ry < rect.y + rect.height; ry++)
                for (int rx = rect.x; rx < rect.x + rect.width; rx++)
                    cells.Add(new Vector2Int(rx, ry));

            FieldEditorUndo.BeginGroup($"Fill Rect {rect.width}×{rect.height}");
            BrushPainter.ApplyStroke(_gridData, cells, _paintSettings);
            FieldEditorUndo.EndGroup();
        }

        Repaint();
    }

    private void HandleDeletePressed()
    {
        _selection.ClearAll();
        Repaint();
    }

    private void HandleFitRequested()
    {
        _hasInitializedView = false;   // triggers FitToView on next Repaint
        Repaint();
    }

    // ── Paint brush event handlers ────────────────────────────────────────────

    private void HandleBrushStarted()
    {
        if (_paintSettings.tool == PaintTool.Select) return;
        FieldEditorUndo.BeginGroup($"{_paintSettings.tool} Stroke");
    }

    private void HandleBrushStroke(IEnumerable<Vector2Int> stamp)
    {
        if (_gridData != null && _paintSettings.tool != PaintTool.Select)
            BrushPainter.ApplyStroke(_gridData, stamp, _paintSettings);

        Repaint();
    }

    private void HandleBrushEnded()
    {
        if (_paintSettings.tool == PaintTool.Select) return;
        FieldEditorUndo.EndGroup();
        Repaint();
    }

    /// <summary>
    /// Mirrors PaintSettings brush geometry into SelectionState.brush so the
    /// visual brush indicator and GridInputController stamp size stay in sync.
    /// </summary>
    private void SyncBrushToSelection()
    {
        _selection.brush.radius = _paintSettings.brushRadius;
        _selection.brush.shape  = _paintSettings.brushShape;
        // Do NOT force SelectionMode here — the user controls the mode
        // independently.  Forcing Brush mode breaks Single-cell painting
        // because every click would stamp a radius around the target cell.
        Repaint();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // NEW — grid data helpers
    // ════════════════════════════════════════════════════════════════════════════

    private void AssignGridData(FieldGridData data)
    {
        _gridData = data;
        _gridView.Data = data;
        _selection.ClearAll();
        _hasInitializedView = false;
    }

    private void CreateNewGridData()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Grid Data Asset", "NewFieldGridData", "asset",
            "Choose where to save the new Grid Data asset");

        if (string.IsNullOrEmpty(path)) return;

        var asset = ScriptableObject.CreateInstance<FieldGridData>();
        asset.Initialize();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        AssignGridData(asset);
        Repaint();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // EXISTING — BuildField, ClearField, ActiveStyleString, LoadPrefabs
    // All methods below are 100 % unchanged from the original.
    // ════════════════════════════════════════════════════════════════════════════

    private void BuildField()
    {
        ClearField();

        var rng = new System.Random(randomSeed);

        string typePrefix   = terrainType == TerrainType.MT ? "MT" : "CPT";
        string terrainFolder = $"{TERRAIN_BASE}/{typePrefix}/NoLOD/L";

        var terrainPool = new List<GameObject>();
        for (int i = 0; i < ALL_STYLES.Length; i++)
        {
            if (!styleEnabled[i]) continue;
            string prefix = $"{typePrefix}_Terrain_L_{ALL_STYLES[i]}_";
            terrainPool.AddRange(LoadPrefabs(terrainFolder, prefix));
        }

        if (terrainPool.Count == 0)
        {
            EditorUtility.DisplayDialog("Field Builder",
                "No terrain prefabs found for the selected styles.\n" +
                "Make sure the LMHPOLY asset pack is imported.", "OK");
            return;
        }

        var grassPool  = LoadPrefabs(GRASS_PATH,  "Grass3D_");
        var bushPool   = LoadPrefabs(BUSH_PATH,   "Bush_");
        var flowerPool = LoadPrefabs(FLOWER_PATH, "Flower_");

        var root        = new GameObject(ROOT_NAME);
        var terrainRoot = new GameObject("Terrain");    terrainRoot.transform.SetParent(root.transform);
        var vegetRoot   = new GameObject("Vegetation"); vegetRoot.transform.SetParent(root.transform);

        float halfW = (gridWidth  - 1) * tileSize * 0.5f;
        float halfH = (gridHeight - 1) * tileSize * 0.5f;

        for (int z = 0; z < gridHeight; z++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                var prefab = terrainPool[rng.Next(terrainPool.Count)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(terrainRoot.transform);
                go.transform.position = new Vector3(x * tileSize - halfW, 0f, z * tileSize - halfH);
                go.name = $"Tile_{x}_{z}";
            }
        }

        Physics.SyncTransforms();

        float fieldHalfX = halfW + tileSize * 0.45f;
        float fieldHalfZ = halfH + tileSize * 0.45f;
        int   culledCount = 0;

        bool TryPlaceOnTerrain(float cx, float cz, out Vector3 worldPos)
        {
            worldPos = new Vector3(cx, 0f, cz);
            if (!enableCulling) return true;
            var ray = new Ray(new Vector3(cx, raycastOriginY, cz), Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastOriginY + 10f))
            { worldPos = hit.point; return true; }
            culledCount++;
            return false;
        }

        float RandInRange(System.Random r, float min, float max) =>
            min + (float)r.NextDouble() * (max - min);
        float RandY(System.Random r) => (float)(r.NextDouble() * 360.0);

        void PlaceVegetation(List<GameObject> pool, Transform parent, int count, string baseName)
        {
            if (pool.Count == 0 || count == 0) return;
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                float cx = RandInRange(rng, -fieldHalfX, fieldHalfX);
                float cz = RandInRange(rng, -fieldHalfZ, fieldHalfZ);
                if (!TryPlaceOnTerrain(cx, cz, out Vector3 pos)) continue;
                var prefab = pool[rng.Next(pool.Count)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(parent);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, RandY(rng), 0f);
                float scale = scaleVariance ? RandInRange(rng, vegetScaleMin, vegetScaleMax) : vegetScaleMin;
                go.transform.localScale = Vector3.one * scale;
                go.name = $"{baseName}_{placed++}";
            }
        }

        int totalGrass = grassPerTile * gridWidth * gridHeight;

        var grassParent  = new GameObject("Grass");   grassParent.transform.SetParent(vegetRoot.transform);
        var bushParent   = new GameObject("Bushes");  bushParent.transform.SetParent(vegetRoot.transform);
        var flowerParent = new GameObject("Flowers"); flowerParent.transform.SetParent(vegetRoot.transform);

        PlaceVegetation(grassPool,  grassParent.transform,  totalGrass,  "Grass");
        PlaceVegetation(bushPool,   bushParent.transform,   bushesTotal, "Bush");
        PlaceVegetation(flowerPool, flowerParent.transform, flowersTotal, "Flower");

        Undo.RegisterCreatedObjectUndo(root, "Build Field");
        UnityEditor.Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log(
            $"[FieldBuilder] {gridWidth}×{gridHeight} grid  |  " +
            $"Styles: [{ActiveStyleString()}]  |  " +
            $"Grass: {totalGrass}  Bushes: {bushesTotal}  Flowers: {flowersTotal}  " +
            $"Culled: {culledCount}");
    }

    private void ClearField()
    {
        var existing = GameObject.Find(ROOT_NAME);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    private string ActiveStyleString()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ALL_STYLES.Length; i++)
            if (styleEnabled[i]) { if (sb.Length > 0) sb.Append(','); sb.Append(ALL_STYLES[i]); }
        return sb.ToString();
    }

    private static List<GameObject> LoadPrefabs(string folder, string prefix)
    {
        var list  = new List<GameObject>();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var file = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(prefix) && !file.StartsWith(prefix)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) list.Add(prefab);
        }
        return list;
    }
}
