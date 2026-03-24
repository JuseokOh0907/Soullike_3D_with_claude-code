using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PlayerSetupWindow : EditorWindow
{
    // ── Constants ──────────────────────────────────────────────────────────────
    private static readonly Color[] PlayerHeaderColors =
    {
        new Color(0.15f, 0.35f, 0.75f, 1f),
        new Color(0.75f, 0.18f, 0.18f, 1f),
        new Color(0.18f, 0.65f, 0.18f, 1f),
        new Color(0.75f, 0.60f, 0.05f, 1f),
    };

    private static readonly string[] PlayerLabels = { "Player 1", "Player 2", "Player 3", "Player 4" };

    private const string SpawnedRootName = "--- Spawned Players ---";

    // ── State ──────────────────────────────────────────────────────────────────
    private PlayerSetupData _data;
    private Vector2 _scroll;
    private bool[] _foldouts = new bool[PlayerSetupData.MaxPlayers];
    private bool[] _jobPickerOpen = new bool[PlayerSetupData.MaxPlayers];
    private GUIStyle _headerStyle;
    private GUIStyle _descStyle;
    private GUIStyle _jobButtonStyle;
    private GUIStyle _jobButtonActiveStyle;
    private bool _stylesInitialized;

    // ── Menu entry ─────────────────────────────────────────────────────────────
    [MenuItem("Game/Player Setup")]
    public static void Open()
    {
        var win = GetWindow<PlayerSetupWindow>("Player Setup");
        win.minSize = new Vector2(400, 520);
        win.Show();
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        for (int i = 0; i < _foldouts.Length; i++)
            _foldouts[i] = true;

        TryLoadDefaultData();
    }

    private void OnGUI()
    {
        InitStyles();

        DrawToolbar();

        if (_data == null)
        {
            DrawNoDataMessage();
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        for (int i = 0; i < PlayerSetupData.MaxPlayers; i++)
            DrawPlayerSlot(i);

        EditorGUILayout.EndScrollView();

        GUILayout.Space(4);
        DrawActionButtons();

        if (GUI.changed)
            EditorUtility.SetDirty(_data);
    }

    // ── Toolbar ────────────────────────────────────────────────────────────────
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Configuration:", EditorStyles.toolbarButton, GUILayout.Width(90));
        var newData = (PlayerSetupData)EditorGUILayout.ObjectField(
            _data, typeof(PlayerSetupData), false,
            GUILayout.ExpandWidth(true));

        if (newData != _data)
            _data = newData;

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
            CreateNewData();

        EditorGUILayout.EndHorizontal();
    }

    // ── No-data placeholder ────────────────────────────────────────────────────
    private void DrawNoDataMessage()
    {
        GUILayout.FlexibleSpace();
        EditorGUILayout.HelpBox(
            "No PlayerSetupData asset loaded.\nPress \"New\" to create one, or drag an existing asset into the field above.",
            MessageType.Info);

        if (GUILayout.Button("Create PlayerSetupData Asset", GUILayout.Height(36)))
            CreateNewData();

        GUILayout.FlexibleSpace();
    }

    // ── Player slot ────────────────────────────────────────────────────────────
    private void DrawPlayerSlot(int index)
    {
        var cfg = _data.players[index];
        var headerColor = PlayerHeaderColors[index];

        // Foldout header
        var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, cfg.isActive ? headerColor : new Color(0.25f, 0.25f, 0.25f));

        var toggleRect = new Rect(rect.x + 6, rect.y + 6, 16, 16);
        bool newActive = EditorGUI.Toggle(toggleRect, cfg.isActive);
        if (newActive != cfg.isActive)
            cfg.isActive = newActive;

        var labelRect = new Rect(rect.x + 28, rect.y + 5, rect.width - 60, 18);
        string headerText = $"  {PlayerLabels[index]}  —  {cfg.playerName}  [{PlayerSetupData.GetJobDisplayName(cfg.job)}]";
        GUI.Label(labelRect, headerText, _headerStyle);

        var foldRect = new Rect(rect.xMax - 24, rect.y + 6, 18, 18);
        _foldouts[index] = EditorGUI.Foldout(foldRect, _foldouts[index], GUIContent.none);

        if (!_foldouts[index])
        {
            GUILayout.Space(2);
            return;
        }

        EditorGUI.BeginDisabledGroup(!cfg.isActive);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Space(2);

        // Name & color row
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Name", GUILayout.Width(50));
        cfg.playerName = EditorGUILayout.TextField(cfg.playerName, GUILayout.ExpandWidth(true));
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Color", GUILayout.Width(38));
        cfg.playerColor = EditorGUILayout.ColorField(GUIContent.none, cfg.playerColor, false, false, false, GUILayout.Width(48));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Job selector
        DrawJobSelector(index, cfg);

        GUILayout.Space(4);

        // Spawn transform
        EditorGUILayout.LabelField("Spawn Transform", EditorStyles.boldLabel);
        cfg.spawnPosition = EditorGUILayout.Vector3Field("Position", cfg.spawnPosition);
        cfg.spawnRotationY = EditorGUILayout.Slider("Rotation Y", cfg.spawnRotationY, -180f, 180f);

        GUILayout.Space(4);
        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(4);
    }

    // ── Job selector grid ──────────────────────────────────────────────────────
    private void DrawJobSelector(int playerIndex, PlayerConfig cfg)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Job", EditorStyles.boldLabel, GUILayout.Width(30));

        if (GUILayout.Button(
            $"  {PlayerSetupData.GetJobDisplayName(cfg.job)}  ▾",
            GUILayout.Height(22)))
        {
            _jobPickerOpen[playerIndex] = !_jobPickerOpen[playerIndex];
        }

        EditorGUILayout.EndHorizontal();

        // Description
        EditorGUILayout.LabelField(
            PlayerSetupData.GetJobDescription(cfg.job),
            _descStyle, GUILayout.MinHeight(30));

        if (!_jobPickerOpen[playerIndex]) return;

        // 3-column grid
        var jobs = (PlayerJob[])System.Enum.GetValues(typeof(PlayerJob));
        int columns = 3;
        int i = 0;
        while (i < jobs.Length)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < columns && i < jobs.Length; col++, i++)
            {
                var job = jobs[i];
                bool isSelected = cfg.job == job;
                var style = isSelected ? _jobButtonActiveStyle : _jobButtonStyle;

                if (GUILayout.Button(PlayerSetupData.GetJobDisplayName(job), style, GUILayout.Height(28)))
                {
                    cfg.job = job;
                    _jobPickerOpen[playerIndex] = false;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ── Action buttons ─────────────────────────────────────────────────────────
    private void DrawActionButtons()
    {
        int activePlayers = 0;
        foreach (var p in _data.players)
            if (p.isActive) activePlayers++;

        EditorGUILayout.HelpBox(
            $"Active players: {activePlayers} / {PlayerSetupData.MaxPlayers}",
            activePlayers == 0 ? MessageType.Warning : MessageType.Info);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("Spawn Players in Scene", GUILayout.Height(32)))
            SpawnPlayersInScene();

        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.3f);
        if (GUILayout.Button("Clear Spawned Players", GUILayout.Height(32)))
            ClearSpawnedPlayers();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUI.backgroundColor = new Color(0.6f, 0.4f, 0.9f);
        if (GUILayout.Button("Generate Animation Data Assets (all 9 jobs)", GUILayout.Height(28)))
            GenerateAllAnimationAssets();

        GUI.backgroundColor = Color.white;
    }

    // ── Animation asset generation ─────────────────────────────────────────────
    private static void GenerateAllAnimationAssets()
    {
        string folder = EditorUtility.SaveFolderPanel(
            "Choose folder for Animation Data assets", "Assets", "AnimationData");

        if (string.IsNullOrEmpty(folder)) return;

        // Convert absolute path to project-relative
        if (folder.StartsWith(Application.dataPath))
            folder = "Assets" + folder.Substring(Application.dataPath.Length);

        int created = 0;
        var jobs = (PlayerJob[])System.Enum.GetValues(typeof(PlayerJob));

        foreach (var job in jobs)
        {
            var paths = PlayerAnimationData.GetDefaultPaths(job);
            var asset = ScriptableObject.CreateInstance<PlayerAnimationData>();
            asset.job = job;

            asset.idle         = LoadClip(paths.idle);
            asset.walk         = LoadClip(paths.walk);
            asset.run          = LoadClip(paths.run);
            asset.dodge        = LoadClip(paths.dodge);
            asset.attack       = LoadClip(paths.attack);
            asset.attackAlt    = LoadClip(paths.attackAlt);
            asset.block        = LoadClip(paths.block);
            asset.shootPrepare = LoadClip(paths.shootPrepare);
            asset.shootCharge  = LoadClip(paths.shootCharge);
            asset.shootFire    = LoadClip(paths.shootFire);
            asset.prepareDelay  = paths.prepareDelay;
            asset.chargeDelay   = paths.chargeDelay;
            asset.fireEventTime = paths.fireEventTime;
            asset.hit          = LoadClip(paths.hit);
            asset.death        = LoadClip(paths.death);

            string assetPath = $"{folder}/Anim_{job}.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
            created++;

            Debug.Log($"[PlayerSetup] Created animation data: {assetPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PlayerSetup] Generated {created} PlayerAnimationData assets.");
    }

    private static AnimationClip LoadClip(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            Debug.LogWarning($"[PlayerSetup] Animation clip not found: {path}");
        return clip;
    }

    // ── Scene operations ───────────────────────────────────────────────────────
    private void SpawnPlayersInScene()
    {
        if (_data == null) return;

        ClearSpawnedPlayers();

        var root = new GameObject(SpawnedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Spawn Players");

        int spawned = 0;
        for (int i = 0; i < PlayerSetupData.MaxPlayers; i++)
        {
            var cfg = _data.players[i];
            if (!cfg.isActive) continue;

            string prefabPath = PlayerSetupData.GetPrefabPath(cfg.job);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PlayerSetup] Prefab not found: {prefabPath}");
                continue;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.name = $"[P{i + 1}] {cfg.playerName} ({PlayerSetupData.GetJobDisplayName(cfg.job)})";
            go.transform.position = cfg.spawnPosition;
            go.transform.rotation = Quaternion.Euler(0, cfg.spawnRotationY, 0);

            // Attach weapons to hand bones
            AttachJobLoadout(go, cfg.job);

            // Tint the renderers to the player color
            ApplyPlayerColor(go, cfg.playerColor, i);

            Undo.RegisterCreatedObjectUndo(go, "Spawn Player");
            spawned++;
        }

        Debug.Log($"[PlayerSetup] Spawned {spawned} player(s) in scene.");
        Selection.activeGameObject = root;
    }

    // ── Weapon attachment ──────────────────────────────────────────────────────
    private static void AttachJobLoadout(GameObject character, PlayerJob job)
    {
        var loadout = PlayerSetupData.GetJobLoadout(job);

        var rightSlot = FindBoneRecursive(character.transform, "handslot.r");
        var leftSlot  = FindBoneRecursive(character.transform, "handslot.l");

        if (!string.IsNullOrEmpty(loadout.rightHandPath))
            AttachAccessory(loadout.rightHandPath, rightSlot, "RightHand");

        if (!string.IsNullOrEmpty(loadout.leftHandPath))
            AttachAccessory(loadout.leftHandPath, leftSlot, "LeftHand");
    }

    private static void AttachAccessory(string prefabPath, Transform slot, string slotLabel)
    {
        if (slot == null)
        {
            Debug.LogWarning($"[PlayerSetup] Bone '{slotLabel}' not found — cannot attach {prefabPath}");
            return;
        }

        var accPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (accPrefab == null)
        {
            Debug.LogWarning($"[PlayerSetup] Accessory prefab not found: {prefabPath}");
            return;
        }

        var acc = (GameObject)PrefabUtility.InstantiatePrefab(accPrefab, slot);
        acc.transform.localPosition = Vector3.zero;
        acc.transform.localRotation = Quaternion.identity;
        acc.transform.localScale    = Vector3.one;
    }

    private static Transform FindBoneRecursive(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            var found = FindBoneRecursive(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

    private void ClearSpawnedPlayers()
    {
        var existing = GameObject.Find(SpawnedRootName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            Debug.Log("[PlayerSetup] Cleared spawned players.");
        }
    }

    private static void ApplyPlayerColor(GameObject go, Color color, int playerIndex)
    {
        // Create a material property block per renderer to avoid modifying shared materials
        var block = new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            // Subtle tint: blend toward player color at low alpha so model still looks good
            block.SetColor("_BaseColor", Color.Lerp(Color.white, color, 0.25f));
            r.SetPropertyBlock(block);
        }

        // Add a colored light as a visual indicator
        var lightGo = new GameObject("PlayerColorLight");
        lightGo.transform.SetParent(go.transform);
        lightGo.transform.localPosition = new Vector3(0, 2f, 0);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 1.5f;
        light.range = 3f;
    }

    // ── Asset management ───────────────────────────────────────────────────────
    private void CreateNewData()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save PlayerSetupData",
            "PlayerSetupData",
            "asset",
            "Choose where to save the player configuration asset.");

        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<PlayerSetupData>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _data = asset;
        EditorGUIUtility.PingObject(_data);
        Debug.Log($"[PlayerSetup] Created PlayerSetupData at {path}");
    }

    private void TryLoadDefaultData()
    {
        if (_data != null) return;

        string[] guids = AssetDatabase.FindAssets("t:PlayerSetupData");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _data = AssetDatabase.LoadAssetAtPath<PlayerSetupData>(path);
        }
    }

    // ── Style init ─────────────────────────────────────────────────────────────
    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleLeft,
        };

        _descStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
            wordWrap = true,
        };

        _jobButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
        };

        _jobButtonActiveStyle = new GUIStyle(_jobButtonStyle)
        {
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = Color.white,
                background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.9f)),
            },
        };

        _stylesInitialized = true;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
