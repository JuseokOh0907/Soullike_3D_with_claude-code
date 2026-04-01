// ============================================================
// VegetationPlacerWindow.cs
// Unity Editor tool for mesh-based vegetation placement.
//
// First-hit rule:
//   Ray hits TerrainSurface or LoadSurface  →  plant tree
//   Ray hits GuideSurface first             →  reject (blocked)
//   No hit at all                           →  skip
//
// Required layers: TerrainSurface, LoadSurface, GuideSurface
// Menu: Tools / Vegetation Placer
// ============================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class VegetationPlacerWindow : EditorWindow
{
    // ── Serialized fields for SerializedObject trick ─────────────────────────
    [SerializeField] private List<GameObject> treePrefabs = new List<GameObject>();

    // ── Scene references ──────────────────────────────────────────────────────
    private Transform _parentTransform;

    // ── Area settings ─────────────────────────────────────────────────────────
    private Vector3 _areaCenter           = Vector3.zero;
    private Vector2 _areaSize             = new Vector2(100f, 100f);
    private float   _raycastOriginHeight  = 300f;

    // ── Placement settings ────────────────────────────────────────────────────
    private int   _targetCount   = 100;
    private int   _maxAttempts   = 500;
    private float _minSpacing    = 2f;
    private float _maxSlopeDeg   = 30f;
    private float _minScale      = 0.8f;
    private float _maxScale      = 1.2f;

    // ── Layer names (must match project Layer settings exactly) ───────────────
    private const string LayerNameTerrain = "TerrainSurface";
    private const string LayerNameLoad    = "LoadSurface";
    private const string LayerNameGuide   = "GuideSurface";

    // ── UI state ──────────────────────────────────────────────────────────────
    private Vector2          _scroll;
    private SerializedObject  _serializedSelf;
    private SerializedProperty _prefabListProp;
    private bool _showAreaGizmo = true;

    // ─────────────────────────────────────────────────────────────────────────
    // Open window
    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Vegetation Placer")]
    public static void Open()
    {
        var window = GetWindow<VegetationPlacerWindow>("Vegetation Placer");
        window.minSize = new Vector2(370f, 520f);
    }

    private void OnEnable()
    {
        _serializedSelf  = new SerializedObject(this);
        _prefabListProp  = _serializedSelf.FindProperty("treePrefabs");
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main GUI
    // ─────────────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSection("Scene References",  DrawSceneReferences);
        DrawSection("Area Settings",     DrawAreaSettings);
        DrawSection("Tree Prefabs",      DrawPrefabList);
        DrawSection("Placement Rules",   DrawPlacementRules);

        EditorGUILayout.Space(10);

        GUI.color  = CanPlace() ? Color.white : Color.gray;
        GUI.enabled = CanPlace();
        if (GUILayout.Button("▶  Place Vegetation", GUILayout.Height(38)))
            RunPlacement();
        GUI.enabled = true;
        GUI.color   = Color.white;

        EditorGUILayout.Space(4);

        ValidateLayersGUI();

        EditorGUILayout.EndScrollView();
    }

    // ── GUI sections ──────────────────────────────────────────────────────────

    private void DrawSceneReferences()
    {
        _parentTransform = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("Parent Object", "e.g. Objects/Vagetable"),
            _parentTransform, typeof(Transform), allowSceneObjects: true);

        if (_parentTransform == null)
            EditorGUILayout.HelpBox("Assign a parent Transform (e.g. Objects/Vagetable).", MessageType.Warning);
    }

    private void DrawAreaSettings()
    {
        EditorGUILayout.BeginHorizontal();
        _areaCenter = EditorGUILayout.Vector3Field("Area Center", _areaCenter);
        if (GUILayout.Button("Get Selection", GUILayout.Width(96)))
            SnapCenterToSelection();
        EditorGUILayout.EndHorizontal();

        _areaSize = EditorGUILayout.Vector2Field(
            new GUIContent("Area Size (X / Z)", "Width and depth of the scatter rectangle"), _areaSize);

        _raycastOriginHeight = EditorGUILayout.FloatField(
            new GUIContent("Ray Origin Height", "How high above Area Center to start rays"),
            _raycastOriginHeight);

        _showAreaGizmo = EditorGUILayout.Toggle("Show Area Gizmo", _showAreaGizmo);
    }

    private void DrawPrefabList()
    {
        _serializedSelf.Update();
        EditorGUILayout.PropertyField(
            _prefabListProp, new GUIContent("Tree Prefabs"), includeChildren: true);
        _serializedSelf.ApplyModifiedProperties();

        if (treePrefabs.Count == 0)
            EditorGUILayout.HelpBox("Add at least one tree prefab.", MessageType.Warning);
    }

    private void DrawPlacementRules()
    {
        _targetCount = EditorGUILayout.IntField(
            new GUIContent("Trees to Place", "Target number of successfully placed trees"),
            _targetCount);

        _maxAttempts = EditorGUILayout.IntField(
            new GUIContent("Max Attempts", "Give up after this many random samples"),
            _maxAttempts);

        _minSpacing = EditorGUILayout.FloatField(
            new GUIContent("Min Spacing (m)", "Minimum distance between any two trees"),
            _minSpacing);

        _maxSlopeDeg = EditorGUILayout.Slider(
            new GUIContent("Max Slope (°)", "Reject placement steeper than this angle"),
            _maxSlopeDeg, 0f, 90f);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Scale Range", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Min", GUILayout.Width(28));
        _minScale = EditorGUILayout.FloatField(_minScale, GUILayout.Width(52));
        EditorGUILayout.LabelField("Max", GUILayout.Width(28));
        _maxScale = EditorGUILayout.FloatField(_maxScale, GUILayout.Width(52));
        EditorGUILayout.EndHorizontal();
    }

    // ── Layer validation HUD ──────────────────────────────────────────────────

    private void ValidateLayersGUI()
    {
        bool terrainOk = LayerMask.NameToLayer(LayerNameTerrain) != -1;
        bool loadOk    = LayerMask.NameToLayer(LayerNameLoad)    != -1;
        bool guideOk   = LayerMask.NameToLayer(LayerNameGuide)   != -1;

        if (!terrainOk || !loadOk || !guideOk)
        {
            string missing = "";
            if (!terrainOk) missing += $"  • {LayerNameTerrain}\n";
            if (!loadOk)    missing += $"  • {LayerNameLoad}\n";
            if (!guideOk)   missing += $"  • {LayerNameGuide}\n";
            EditorGUILayout.HelpBox(
                $"Missing project layers:\n{missing}Add them in Edit > Project Settings > Tags and Layers.",
                MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "All required layers found: TerrainSurface, LoadSurface, GuideSurface",
                MessageType.Info);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core placement logic
    // ─────────────────────────────────────────────────────────────────────────

    private bool CanPlace()
    {
        return _parentTransform != null
            && treePrefabs != null
            && treePrefabs.Exists(p => p != null);
    }

    private void RunPlacement()
    {
        // ── Resolve layer indices ─────────────────────────────────────────────
        int terrainLayerIndex = LayerMask.NameToLayer(LayerNameTerrain);
        int loadLayerIndex    = LayerMask.NameToLayer(LayerNameLoad);
        int guideLayerIndex   = LayerMask.NameToLayer(LayerNameGuide);

        if (terrainLayerIndex == -1 || loadLayerIndex == -1 || guideLayerIndex == -1)
        {
            EditorUtility.DisplayDialog("Layer Error",
                $"One or more required layers not found.\n" +
                $"Required: {LayerNameTerrain}, {LayerNameLoad}, {LayerNameGuide}\n\n" +
                "Add them in Edit > Project Settings > Tags and Layers.",
                "OK");
            return;
        }

        // Single combined mask: raycast hits ALL three surfaces
        // We then decide validity from the FIRST hit's layer only.
        LayerMask combinedMask =
            (1 << terrainLayerIndex) |
            (1 << loadLayerIndex)    |
            (1 << guideLayerIndex);

        // ── Valid prefab list ─────────────────────────────────────────────────
        var validPrefabs = treePrefabs.FindAll(p => p != null);
        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[VegetationPlacer] No valid prefabs assigned.");
            return;
        }

        // ── Placement loop ────────────────────────────────────────────────────
        var placedPositions = new List<Vector3>(_targetCount);
        int placed   = 0;
        int attempts = 0;

        Undo.SetCurrentGroupName("Place Vegetation");
        int undoGroup = Undo.GetCurrentGroup();

        while (placed < _targetCount && attempts < _maxAttempts)
        {
            attempts++;

            Vector3 rayOrigin = GetRandomRayOrigin();

            // ── FIRST-HIT RULE ────────────────────────────────────────────────
            if (!TryGetValidPlantHit(rayOrigin, combinedMask, guideLayerIndex, loadLayerIndex, out RaycastHit hit))
                continue;

            // ── Slope check ───────────────────────────────────────────────────
            if (!IsSlopeAcceptable(hit.normal))
                continue;

            // ── Spacing check ─────────────────────────────────────────────────
            if (!IsSpacingAcceptable(hit.point, placedPositions))
                continue;

            // ── Place tree ────────────────────────────────────────────────────
            PlaceTreeInstance(hit.point, validPrefabs);
            placedPositions.Add(hit.point);
            placed++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        string summary = $"Placed {placed} / {_targetCount} trees  ({attempts} attempts)";
        Debug.Log($"[VegetationPlacer] {summary}");
        EditorUtility.DisplayDialog("Vegetation Placer", summary, "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: ray origin
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 GetRandomRayOrigin()
    {
        float x = _areaCenter.x + Random.Range(-_areaSize.x * 0.5f,  _areaSize.x * 0.5f);
        float z = _areaCenter.z + Random.Range(-_areaSize.y * 0.5f,  _areaSize.y * 0.5f);
        float y = _areaCenter.y + _raycastOriginHeight;
        return new Vector3(x, y, z);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core first-hit validation
    //
    // The combined mask means Physics.Raycast will stop at the VERY FIRST
    // surface from any of the three layers.
    // Explicitly allow ONLY TerrainSurface or LoadSurface.
    // GuideSurface (or anything else) as first hit → rejected.
    // ─────────────────────────────────────────────────────────────────────────
    private static bool TryGetValidPlantHit(
        Vector3    rayOrigin,
        LayerMask  combinedMask,
        int        guideLayerIndex,
        int        loadLayerIndex,
        out RaycastHit hit)
    {
        // Single Raycast — NOT RaycastAll
        if (!Physics.Raycast(rayOrigin, Vector3.down, out hit, Mathf.Infinity, combinedMask))
            return false; // no surface below this point

        int hitLayer = hit.collider.gameObject.layer;

        // Allow GuideSurface or LoadSurface as first hit
        // TerrainSurface first hit → rejected
        return hitLayer == guideLayerIndex || hitLayer == loadLayerIndex;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: slope check
    // ─────────────────────────────────────────────────────────────────────────
    private bool IsSlopeAcceptable(Vector3 surfaceNormal)
    {
        float slopeDeg = Vector3.Angle(Vector3.up, surfaceNormal);
        return slopeDeg <= _maxSlopeDeg;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: spacing check  (XZ distance only)
    // ─────────────────────────────────────────────────────────────────────────
    private bool IsSpacingAcceptable(Vector3 newPoint, List<Vector3> existing)
    {
        float sqrMin = _minSpacing * _minSpacing;
        foreach (Vector3 p in existing)
        {
            Vector3 diff = new Vector3(newPoint.x - p.x, 0f, newPoint.z - p.z);
            if (diff.sqrMagnitude < sqrMin)
                return false;
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: instantiate one tree
    // ─────────────────────────────────────────────────────────────────────────
    private void PlaceTreeInstance(Vector3 position, List<GameObject> validPrefabs)
    {
        GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

        float yRotation  = Random.Range(0f, 360f);
        float uniformScale = Random.Range(_minScale, _maxScale);

        // Use InstantiatePrefab to preserve prefab link
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _parentTransform);
        Undo.RegisterCreatedObjectUndo(instance, "Place Tree");

        instance.transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(0f, yRotation, 0f));
        instance.transform.localScale = Vector3.one * uniformScale;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene gizmo (area preview)
    // ─────────────────────────────────────────────────────────────────────────
    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_showAreaGizmo) return;

        Handles.color = new Color(0.3f, 1f, 0.4f, 0.25f);
        Vector3 center = _areaCenter;
        Vector3 size   = new Vector3(_areaSize.x, 1f, _areaSize.y);
        Handles.DrawWireCube(center, size);

        Handles.color = new Color(0.3f, 1f, 0.4f, 0.08f);
        Handles.DrawSolidRectangleWithOutline(
            new Vector3[]
            {
                center + new Vector3(-_areaSize.x * 0.5f, 0f, -_areaSize.y * 0.5f),
                center + new Vector3( _areaSize.x * 0.5f, 0f, -_areaSize.y * 0.5f),
                center + new Vector3( _areaSize.x * 0.5f, 0f,  _areaSize.y * 0.5f),
                center + new Vector3(-_areaSize.x * 0.5f, 0f,  _areaSize.y * 0.5f),
            },
            new Color(0.3f, 1f, 0.4f, 0.08f),
            new Color(0.3f, 1f, 0.4f, 0.6f));

        Handles.color = Color.white;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────────────

    private void SnapCenterToSelection()
    {
        if (Selection.activeTransform != null)
        {
            _areaCenter = Selection.activeTransform.position;
            Repaint();
        }
    }

    private static void DrawSection(string title, System.Action drawContent)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        drawContent();
        EditorGUI.indentLevel--;
    }
}
