using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Wall &amp; Door Maker — EditorWindow
///
/// Open via: Tools → Wall &amp; Door Maker
///
/// Workflow
/// ────────
///   1. Assign (or create) a WallDoorSettings asset in the toolbar.
///   2. Select a tool mode:
///        Draw Wall — click two points in the Scene view to define a wall run.
///        Add Door  — click an existing wall segment to toggle a door opening.
///        Erase     — click a wall run gizmo to delete it.
///   3. Press "Build All" to (re)generate scene objects from the wall data.
///   4. Press "Clear Scene" to destroy all generated objects (undo-safe).
///
/// Data is stored as a plain List&lt;WallSegmentData&gt; on this window (session-only).
/// For persistent data, save a WallDoorSettings asset and re-enter the points manually,
/// or extend with a serialized ScriptableObject layout asset.
/// </summary>
public class WallDoorMakerWindow : EditorWindow
{
    // ── Tool modes ────────────────────────────────────────────────────────────

    private enum ToolMode { None, DrawWall, AddDoor, Erase }

    // ── State ─────────────────────────────────────────────────────────────────

    [SerializeField] private WallDoorSettings _settings;

    private readonly List<WallSegmentData> _walls = new();

    private ToolMode _mode      = ToolMode.None;
    private bool     _placing   = false;   // true while waiting for second click (DrawWall)
    private Vector3  _wallStart;           // first click position (DrawWall)
    private Vector3  _previewEnd;          // mouse hover position (DrawWall preview)

    private Vector2  _scroll;

    // ── Menu entry ────────────────────────────────────────────────────────────

    [MenuItem("Tools/Wall & Door Maker")]
    public static void Open() =>
        GetWindow<WallDoorMakerWindow>("Wall & Door Maker").Show();

    // ── Unity callbacks ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        TryLoadDefaultSettings();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        CancelPlacement();
    }

    // ── Main GUI ──────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4);

        if (_settings == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a WallDoorSettings asset above, or create one via\n" +
                "right-click → Wall Door Maker → Settings.",
                MessageType.Info);
            return;
        }

        if (!_settings.IsValid)
        {
            EditorGUILayout.HelpBox(
                "WallDoorSettings needs Wall Prefab and Door Prefab assigned.",
                MessageType.Warning);
        }

        DrawModeButtons();
        EditorGUILayout.Space(4);
        DrawWallList();
        EditorGUILayout.Space(4);
        DrawBuildButtons();
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("Settings", GUILayout.Width(56));
            var next = (WallDoorSettings)EditorGUILayout.ObjectField(
                _settings, typeof(WallDoorSettings), allowSceneObjects: false,
                GUILayout.ExpandWidth(true));
            if (next != _settings) { _settings = next; Repaint(); }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(36)))
                CreateNewSettings();
        }
    }

    // ── Mode buttons ─────────────────────────────────────────────────────────

    private void DrawModeButtons()
    {
        EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawModeButton(ToolMode.DrawWall, "Draw Wall",
                "Click two points in the Scene view to add a wall run.");
            DrawModeButton(ToolMode.AddDoor,  "Add Door",
                "Click a wall segment in the Scene view to toggle a door.");
            DrawModeButton(ToolMode.Erase,    "Erase",
                "Click a wall run handle to delete it.");
        }

        if (_mode == ToolMode.DrawWall)
        {
            string hint = _placing
                ? "Click the end point in the Scene view."
                : "Click the start point in the Scene view.";
            EditorGUILayout.HelpBox(hint, MessageType.None);
        }
    }

    private void DrawModeButton(ToolMode mode, string label, string tooltip)
    {
        bool active = _mode == mode;
        var  style  = active ? StyleActiveMode() : GUI.skin.button;
        if (GUILayout.Button(new GUIContent(label, tooltip), style))
        {
            _mode = active ? ToolMode.None : mode;
            CancelPlacement();
            SceneView.RepaintAll();
        }
    }

    // ── Wall list ─────────────────────────────────────────────────────────────

    private void DrawWallList()
    {
        EditorGUILayout.LabelField($"Wall Runs  ({_walls.Count})", EditorStyles.boldLabel);

        if (_walls.Count == 0)
        {
            EditorGUILayout.HelpBox("No wall runs yet. Use Draw Wall mode in the Scene view.", MessageType.None);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(180));

        for (int i = 0; i < _walls.Count; i++)
        {
            var wall = _walls[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"Run {i}  {FormatVec(wall.start)} → {FormatVec(wall.end)}" +
                    $"  |  segs: {wall.SegmentCount(_settings?.segmentLength ?? 2f)}" +
                    $"  doors: {wall.doorSlots.Count}",
                    GUILayout.ExpandWidth(true));

                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    _walls.RemoveAt(i);
                    Repaint();
                    SceneView.RepaintAll();
                    break;
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Build buttons ─────────────────────────────────────────────────────────

    private void DrawBuildButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _settings != null && _settings.IsValid && _walls.Count > 0;
            if (GUILayout.Button("Build All", GUILayout.Height(28)))
                WallBuilder.RebuildAll(_walls, _settings);
            GUI.enabled = true;

            if (GUILayout.Button("Clear Scene", GUILayout.Height(28), GUILayout.Width(90)))
            {
                if (_settings != null) WallBuilder.ClearScene(_settings);
            }
        }
    }

    // ── Scene GUI ─────────────────────────────────────────────────────────────

    private void OnSceneGUI(SceneView sv)
    {
        if (_settings == null) return;

        DrawWallGizmos();

        if (_mode == ToolMode.None) return;

        // Consume input so we don't accidentally deselect objects
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Event e = Event.current;

        switch (_mode)
        {
            case ToolMode.DrawWall: HandleDrawWall(e, sv); break;
            case ToolMode.AddDoor:  HandleAddDoor(e);       break;
            case ToolMode.Erase:    HandleErase(e);          break;
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            CancelPlacement();
            _mode = ToolMode.None;
            Repaint();
            e.Use();
        }
    }

    // ── Draw Wall mode ────────────────────────────────────────────────────────

    private void HandleDrawWall(Event e, SceneView sv)
    {
        Vector3 mouseWorld = GetWorldPoint(e.mousePosition, sv);

        if (_placing)
        {
            _previewEnd = mouseWorld;

            // Preview line
            Handles.color = Color.yellow;
            Handles.DrawDottedLine(_wallStart, _previewEnd, 4f);
            Handles.SphereHandleCap(0, _previewEnd, Quaternion.identity, 0.15f, EventType.Repaint);
            sv.Repaint();
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (!_placing)
            {
                // First click — record start point
                _wallStart = mouseWorld;
                _placing   = true;
            }
            else
            {
                // Second click — finalise wall run
                var wall = new WallSegmentData
                {
                    start = _settings.Snap(_wallStart),
                    end   = _settings.Snap(mouseWorld),
                };

                if (wall.Length > 0.01f)
                {
                    _walls.Add(wall);
                    Repaint();
                }

                _placing = false;
            }
            e.Use();
        }

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            CancelPlacement();
            e.Use();
        }
    }

    // ── Add Door mode ─────────────────────────────────────────────────────────

    private void HandleAddDoor(Event e)
    {
        if (e.type != EventType.MouseDown || e.button != 0) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var (runIdx, segIdx) = WallBuilder.HitTest(ray, _walls, _settings);

        if (runIdx >= 0)
        {
            _walls[runIdx].ToggleDoor(segIdx);
            Repaint();
        }
        e.Use();
    }

    // ── Erase mode ────────────────────────────────────────────────────────────

    private void HandleErase(Event e)
    {
        if (e.type != EventType.MouseDown || e.button != 0) return;

        Ray   ray     = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        int   closest = -1;
        float bestD   = float.MaxValue;

        for (int i = 0; i < _walls.Count; i++)
        {
            // Check midpoint of the wall run
            Vector3 mid  = Vector3.Lerp(_walls[i].start, _walls[i].end, 0.5f);
            Vector3 proj = ray.origin + ray.direction * Vector3.Dot(mid - ray.origin, ray.direction);
            float   d    = Vector3.Distance(proj, mid);
            if (d < bestD) { bestD = d; closest = i; }
        }

        if (closest >= 0 && bestD < _settings.segmentLength)
        {
            _walls.RemoveAt(closest);
            Repaint();
            SceneView.RepaintAll();
        }
        e.Use();
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    private void DrawWallGizmos()
    {
        if (_settings == null) return;
        float segLen = _settings.segmentLength;

        for (int r = 0; r < _walls.Count; r++)
        {
            var  wall  = _walls[r];
            int  count = wall.SegmentCount(segLen);

            // Wall run line
            Handles.color = Color.white;
            Handles.DrawLine(wall.start, wall.end, 2f);

            // Start/end handles
            Handles.color = Color.cyan;
            Handles.SphereHandleCap(0, wall.start, Quaternion.identity, 0.12f, EventType.Repaint);
            Handles.SphereHandleCap(0, wall.end,   Quaternion.identity, 0.12f, EventType.Repaint);

            // Per-segment markers
            for (int s = 0; s < count; s++)
            {
                Vector3 center = wall.SegmentCenter(s, segLen);
                bool    isDoor = wall.HasDoor(s);

                Handles.color = isDoor ? Color.green : new Color(1f, 0.6f, 0f);
                Handles.DrawWireCube(center, new Vector3(segLen * 0.85f, 0.1f, 0.1f));

                if (isDoor)
                {
                    // Draw door arch indicator
                    Handles.color = Color.green;
                    Handles.DrawWireCube(center + Vector3.up * (_settings.wallHeight * 0.4f),
                        new Vector3(segLen * 0.6f, _settings.wallHeight * 0.8f, 0.05f));
                }
            }

            // Run label
            Vector3 mid = Vector3.Lerp(wall.start, wall.end, 0.5f) + Vector3.up * 0.4f;
            Handles.Label(mid, $"Run {r}  [{count} seg]");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector3 GetWorldPoint(Vector2 mousePos, SceneView sv)
    {
        // Raycast onto Y=0 plane (or Unity Terrain if present)
        Ray   ray   = HandleUtility.GUIPointToWorldRay(mousePos);
        var   plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float enter))
            return _settings.Snap(ray.GetPoint(enter));

        return _settings.Snap(ray.GetPoint(10f));
    }

    private void CancelPlacement()
    {
        _placing = false;
    }

    private void CreateNewSettings()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Wall Door Settings", "WallDoorSettings", "asset",
            "Choose where to save the WallDoorSettings asset.");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<WallDoorSettings>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _settings = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private void TryLoadDefaultSettings()
    {
        if (_settings != null) return;
        var guids = AssetDatabase.FindAssets("t:WallDoorSettings");
        if (guids.Length > 0)
            _settings = AssetDatabase.LoadAssetAtPath<WallDoorSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static string FormatVec(Vector3 v) =>
        $"({v.x:F1}, {v.z:F1})";

    private static GUIStyle StyleActiveMode()
    {
        var s = new GUIStyle(GUI.skin.button);
        s.normal.background  = Texture2D.grayTexture;
        s.normal.textColor   = Color.white;
        s.fontStyle          = FontStyle.Bold;
        return s;
    }
}
