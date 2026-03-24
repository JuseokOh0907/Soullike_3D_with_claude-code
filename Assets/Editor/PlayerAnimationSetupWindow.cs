using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Game > Player Animation Setup
///
/// 9개 직업 각각에 대해 애니메이션을 [무장 / 비무장 / 공통] 섹션으로 나눠 선택합니다.
/// 추후 전투 모션 등 추가 섹션도 여기서 확장합니다.
/// </summary>
public class PlayerAnimationSetupWindow : EditorWindow
{
    // ── 슬롯 정의 ───────────────────────────────────────────────────────────────
    private enum Section { Armed, Unarmed }

    private class SlotDef
    {
        public string  label;
        public string  tooltip;
        public Section section;
    }

    private static readonly SlotDef[] Slots =
    {
        // 무장 상태
        new SlotDef { label="Armed Idle",  tooltip="무기를 든 채 서 있는 대기 모션",              section=Section.Armed   },
        new SlotDef { label="Armed Walk",  tooltip="무기를 든 채 걷는 모션",                        section=Section.Armed   },
        new SlotDef { label="Armed Run",   tooltip="무기를 든 채 달리는 모션",                      section=Section.Armed   },
        // 비무장 상태 — 이동 + 점프/피격/사망 포함
        new SlotDef { label="Unarmed Idle",tooltip="맨손 대기 모션 (무기 파괴/없음)",              section=Section.Unarmed },
        new SlotDef { label="Unarmed Walk",tooltip="맨손 걷기 모션",                                section=Section.Unarmed },
        new SlotDef { label="Unarmed Run", tooltip="맨손 달리기 모션",                              section=Section.Unarmed },
        new SlotDef { label="Jump",        tooltip="점프 모션 — 비무장 상태에 포함",               section=Section.Unarmed },
        new SlotDef { label="Hit",         tooltip="피격 모션 — 비무장 상태에 포함",               section=Section.Unarmed },
        new SlotDef { label="Death",       tooltip="사망 모션 — 비무장 상태에 포함",               section=Section.Unarmed },
    };

    private static readonly (string label, Color color)[] SectionHeaders =
    {
        ( "무장 상태 (Armed)",            new Color(0.70f, 0.30f, 0.15f) ),
        ( "비무장 상태 (Unarmed)  ·  점프 / 피격 / 사망 포함", new Color(0.20f, 0.40f, 0.20f) ),
    };

    // ── 직업 색상 ────────────────────────────────────────────────────────────────
    private static readonly Color[] JobColors =
    {
        new Color(0.55f, 0.20f, 0.20f),
        new Color(0.70f, 0.25f, 0.10f),
        new Color(0.20f, 0.35f, 0.65f),
        new Color(0.15f, 0.50f, 0.20f),
        new Color(0.40f, 0.15f, 0.50f),
        new Color(0.30f, 0.10f, 0.40f),
        new Color(0.10f, 0.35f, 0.55f),
        new Color(0.15f, 0.45f, 0.30f),
        new Color(0.45f, 0.40f, 0.10f),
    };

    private const string KayKitAnimRoot =
        "Assets/KayKit/Characters/Animations/Animations";

    // ── 런타임 상태 ──────────────────────────────────────────────────────────────
    private int      _tab = 0;
    private Vector2  _scroll;
    private bool     _loaded;

    // [직업 인덱스][슬롯 인덱스]
    private AnimationClip[][] _clips;
    private PlayerAnimationData[] _assets;

    private List<AnimationClip>         _allClips  = new();
    private string[]                    _allLabels;
    private Dictionary<AnimationClip,int> _clipIdx = new();

    // 스타일
    private GUIStyle _tabActive, _tab_, _sectionHeader, _slotLabel, _pathLabel, _emptyLabel;
    private bool     _stylesReady;

    // ── 메뉴 ─────────────────────────────────────────────────────────────────────
    [MenuItem("Game/Player Animation Setup")]
    public static void Open()
    {
        var w = GetWindow<PlayerAnimationSetupWindow>("Player Animation Setup");
        w.minSize = new Vector2(560, 520);
        w.Show();
    }

    // ── OnGUI ────────────────────────────────────────────────────────────────────
    private void OnEnable() => _loaded = false;

    private void OnGUI()
    {
        InitStyles();
        if (!_loaded) LoadAll();

        DrawGuide();
        DrawTabs();
        GUILayout.Space(4);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawJobContent(_tab);
        EditorGUILayout.EndScrollView();
        GUILayout.Space(6);
        DrawFooter();
    }

    // ── 안내 문구 ────────────────────────────────────────────────────────────────
    private void DrawGuide()
    {
        EditorGUILayout.HelpBox(
            "상단 탭에서 직업을 선택하세요.\n" +
            "각 섹션(무장 / 비무장 / 공통)에서 드롭다운 또는 드래그로 클립을 지정합니다.\n" +
            "추후 전투·스킬 모션은 별도 섹션으로 추가될 예정입니다.",
            MessageType.Info);
        GUILayout.Space(2);
    }

    // ── 직업 탭 ──────────────────────────────────────────────────────────────────
    private void DrawTabs()
    {
        var jobs = AllJobs();
        EditorGUILayout.BeginHorizontal();
        for (int j = 0; j < jobs.Length; j++)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = _tab == j ? JobColors[j] * 1.7f : JobColors[j] * 0.65f;
            if (GUILayout.Button(PlayerSetupData.GetJobDisplayName(jobs[j]),
                    _tab == j ? _tabActive : _tab_))
                _tab = j;
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();
    }

    // ── 직업 콘텐츠 ──────────────────────────────────────────────────────────────
    private void DrawJobContent(int ji)
    {
        var jobs = AllJobs();
        var job  = jobs[ji];

        // 직업 제목 바
        var titleRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(titleRect, JobColors[ji]);
        GUI.Label(titleRect,
            $"   {PlayerSetupData.GetJobDisplayName(job)}  —  애니메이션 설정",
            _sectionHeader);

        GUILayout.Space(6);

        Section? lastSection = null;
        for (int s = 0; s < Slots.Length; s++)
        {
            var slot = Slots[s];

            // 섹션 헤더 (섹션이 바뀔 때만)
            if (slot.section != lastSection)
            {
                lastSection = slot.section;
                GUILayout.Space(4);
                var (hLabel, hColor) = SectionHeaders[(int)slot.section];
                var hr = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(hr, hColor);
                GUI.Label(hr, $"  {hLabel}", _sectionHeader);
                GUILayout.Space(2);
            }

            DrawSlotRow(ji, s, slot);
        }
    }

    // ── 슬롯 한 행 ───────────────────────────────────────────────────────────────
    private void DrawSlotRow(int ji, int si, SlotDef slot)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 슬롯 이름
        EditorGUILayout.LabelField(new GUIContent(slot.label, slot.tooltip), _slotLabel);

        EditorGUILayout.BeginHorizontal();

        // 오브젝트 필드 (드래그 앤 드롭)
        var direct = (AnimationClip)EditorGUILayout.ObjectField(
            _clips[ji][si], typeof(AnimationClip), false, GUILayout.Width(200));
        if (direct != _clips[ji][si]) _clips[ji][si] = direct;

        GUILayout.Space(4);

        // 드롭다운 (이름 검색)
        int cur = _clips[ji][si] != null && _clipIdx.TryGetValue(_clips[ji][si], out int ci) ? ci : 0;
        int nxt = EditorGUILayout.Popup(cur, _allLabels, GUILayout.ExpandWidth(true));
        if (nxt != cur) _clips[ji][si] = nxt == 0 ? null : _allClips[nxt - 1];

        EditorGUILayout.EndHorizontal();

        // 경로 표시
        if (_clips[ji][si] != null)
        {
            string p = AssetDatabase.GetAssetPath(_clips[ji][si]);
            EditorGUILayout.LabelField(Path.GetFileName(p), _pathLabel);
        }
        else
        {
            EditorGUILayout.LabelField("선택 없음", _emptyLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(1);
    }

    // ── 하단 버튼 ────────────────────────────────────────────────────────────────
    private void DrawFooter()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.4f);
        if (GUILayout.Button("현재 직업 저장", GUILayout.Height(30)))
            SaveJob(_tab);

        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.85f);
        if (GUILayout.Button("전체 9개 직업 저장", GUILayout.Height(30)))
        {
            for (int j = 0; j < 9; j++) SaveJob(j);
        }

        GUI.backgroundColor = new Color(0.7f, 0.50f, 0.20f);
        if (GUILayout.Button("기본값으로 초기화", GUILayout.Height(30)))
        {
            var jobs = AllJobs();
            ApplyDefaults(_tab, jobs[_tab]);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ── 데이터 로드 ──────────────────────────────────────────────────────────────
    private void LoadAll()
    {
        var jobs = AllJobs();
        int n    = jobs.Length;

        // KayKit 전체 클립 수집
        _allClips.Clear();
        foreach (var g in AssetDatabase.FindAssets("t:AnimationClip", new[] { KayKitAnimRoot }))
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null) _allClips.Add(c);
        }
        _allClips.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        _allLabels = new string[_allClips.Count + 1];
        _allLabels[0] = "── 없음 ──";
        for (int i = 0; i < _allClips.Count; i++) _allLabels[i + 1] = _allClips[i].name;

        _clipIdx.Clear();
        for (int i = 0; i < _allClips.Count; i++) _clipIdx[_allClips[i]] = i + 1;

        // 에셋 & 클립 배열 초기화
        _assets = new PlayerAnimationData[n];
        _clips  = new AnimationClip[n][];
        for (int j = 0; j < n; j++)
        {
            _clips[j] = new AnimationClip[Slots.Length];

            // 기존 에셋 검색
            var existing = AssetDatabase.FindAssets($"t:PlayerAnimationData Anim_{jobs[j]}");
            if (existing.Length > 0)
                _assets[j] = AssetDatabase.LoadAssetAtPath<PlayerAnimationData>(
                    AssetDatabase.GUIDToAssetPath(existing[0]));

            if (_assets[j] != null)
                PullFromAsset(j);
            else
                ApplyDefaults(j, jobs[j]);
        }

        _loaded = true;
    }

    // 에셋 → _clips 복사
    private void PullFromAsset(int ji)
    {
        var d = _assets[ji];
        _clips[ji][0] = d.armedIdle;
        _clips[ji][1] = d.armedWalk;
        _clips[ji][2] = d.armedRun;
        _clips[ji][3] = d.unarmedIdle;
        _clips[ji][4] = d.unarmedWalk;
        _clips[ji][5] = d.unarmedRun;
        _clips[ji][6] = d.jump;
        _clips[ji][7] = d.hit;
        _clips[ji][8] = d.death;
    }

    // KayKit 기본값 적용
    private void ApplyDefaults(int ji, PlayerJob job)
    {
        var p = PlayerAnimationData.GetDefaultPaths(job);
        _clips[ji][0] = Load(p.armedIdle);
        _clips[ji][1] = Load(p.armedWalk);
        _clips[ji][2] = Load(p.armedRun);
        _clips[ji][3] = Load(p.unarmedIdle);
        _clips[ji][4] = Load(p.unarmedWalk);
        _clips[ji][5] = Load(p.unarmedRun);
        _clips[ji][6] = Load(p.jump);
        _clips[ji][7] = Load(p.hit);
        _clips[ji][8] = Load(p.death);
    }

    // ── 저장 ─────────────────────────────────────────────────────────────────────
    private void SaveJob(int ji)
    {
        var jobs = AllJobs();
        var job  = jobs[ji];

        if (_assets[ji] == null)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                $"저장 — {PlayerSetupData.GetJobDisplayName(job)}",
                $"Anim_{job}", "asset",
                "PlayerAnimationData 에셋 저장 위치를 선택하세요.");
            if (string.IsNullOrEmpty(path)) return;

            _assets[ji] = ScriptableObject.CreateInstance<PlayerAnimationData>();
            AssetDatabase.CreateAsset(_assets[ji], path);
        }

        var d = _assets[ji];
        d.job        = job;
        d.armedIdle  = _clips[ji][0];
        d.armedWalk  = _clips[ji][1];
        d.armedRun   = _clips[ji][2];
        d.unarmedIdle= _clips[ji][3];
        d.unarmedWalk= _clips[ji][4];
        d.unarmedRun = _clips[ji][5];
        d.jump       = _clips[ji][6];
        d.hit        = _clips[ji][7];
        d.death      = _clips[ji][8];

        EditorUtility.SetDirty(d);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AnimSetup] '{PlayerSetupData.GetJobDisplayName(job)}' 저장 완료.");
        EditorGUIUtility.PingObject(d);
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────────
    private static PlayerJob[] AllJobs() =>
        (PlayerJob[])System.Enum.GetValues(typeof(PlayerJob));

    private static AnimationClip Load(string path) =>
        string.IsNullOrEmpty(path) ? null :
        AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

    // ── 스타일 초기화 ────────────────────────────────────────────────────────────
    private void InitStyles()
    {
        if (_stylesReady) return;

        _tab_ = new GUIStyle(EditorStyles.toolbarButton)
            { fontSize = 10, fixedHeight = 22 };

        _tabActive = new GUIStyle(_tab_)
            { fontStyle = FontStyle.Bold, fontSize = 11 };

        _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Color.white },
        };

        _slotLabel = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 11 };

        _pathLabel = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.4f, 0.8f, 0.5f) } };

        _emptyLabel = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.75f, 0.35f, 0.35f) } };

        _stylesReady = true;
    }
}
