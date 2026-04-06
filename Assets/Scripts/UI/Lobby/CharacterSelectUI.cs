using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lobby 씬의 캐릭터 선택 패널 컨트롤러.
///
/// ── 씬 구조 예시 ───────────────────────────────────────────────────────────────
///  Canvas
///   └─ CharacterSelectPanel          ← 이 컴포넌트 부착
///       ├─ Header / Title
///       ├─ Left Panel
///       │   └─ ScrollView
///       │       └─ Content (GridLayoutGroup)   ← jobCardContainer
///       ├─ Right Panel (DetailPanel)            ← detailPanel
///       │   ├─ JobNameText
///       │   ├─ HpText
///       │   ├─ SpecialAbilityText
///       │   ├─ SpecialAbilityDetailText
///       │   └─ PresetContainer (VerticalLayoutGroup) ← presetContainer
///       └─ ConfirmButton                        ← confirmButton
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    // ── 데이터 ──────────────────────────────────────────────────────────────────
    [Header("직업 데이터 (Inspector에서 9개 드래그)")]
    public JobData[] allJobs;

    [Header("플레이어 설정 Asset")]
    public PlayerSetupData playerSetup;

    [Header("플레이어 인덱스 (0 = Player1, …)")]
    public int playerIndex = 0;

    // ── 왼쪽: 직업 카드 목록 ────────────────────────────────────────────────────
    [Header("직업 카드 목록")]
    public Transform   jobCardContainer;    // GridLayoutGroup 부모
    public JobCardUI   jobCardPrefab;

    // ── 오른쪽: 상세 패널 ────────────────────────────────────────────────────────
    [Header("상세 정보 패널")]
    public GameObject       detailPanel;
    public TextMeshProUGUI  jobNameText;
    public TextMeshProUGUI  hpText;
    public TextMeshProUGUI  specialAbilityText;
    public TextMeshProUGUI  specialAbilityDetailText;
    public Transform        presetContainer;    // VerticalLayoutGroup 부모
    public PresetRowUI      presetRowPrefab;

    // ── 확인 버튼 ────────────────────────────────────────────────────────────────
    [Header("확인 버튼")]
    public Button           confirmButton;
    public TextMeshProUGUI  confirmButtonText;

    // ── 내부 상태 ────────────────────────────────────────────────────────────────
    private JobData   _selectedJob;
    private JobCardUI _selectedCard;

    // ─────────────────────────────────────────────────────────────────────────────

    void Start()
    {
        confirmButton.interactable = false;
        confirmButton.onClick.AddListener(OnConfirm);
        detailPanel.SetActive(false);

        BuildJobCards();
    }

    // ── 직업 카드 동적 생성 ──────────────────────────────────────────────────────
    void BuildJobCards()
    {
        if (allJobs == null || allJobs.Length == 0)
        {
            Debug.LogWarning("[CharacterSelectUI] allJobs 배열이 비어 있습니다. Inspector에서 JobData를 연결해 주세요.");
            return;
        }

        foreach (var job in allJobs)
        {
            if (job == null) continue;
            var card = Instantiate(jobCardPrefab, jobCardContainer);
            card.Setup(job, OnCardSelected);
        }
    }

    // ── 카드 선택 콜백 ────────────────────────────────────────────────────────────
    void OnCardSelected(JobData job, JobCardUI card)
    {
        // 이전 카드 선택 해제
        if (_selectedCard != null)
            _selectedCard.SetSelected(false);

        _selectedJob  = job;
        _selectedCard = card;
        card.SetSelected(true);

        ShowDetail(job);
        confirmButton.interactable = true;
    }

    // ── 상세 정보 패널 갱신 ──────────────────────────────────────────────────────
    void ShowDetail(JobData job)
    {
        detailPanel.SetActive(true);

        jobNameText.text              = job.jobNameKR;
        hpText.text                   = $"HP  {job.hp:N0}";
        specialAbilityText.text       = $"특수 능력  {job.specialAbility}";
        specialAbilityDetailText.text = job.specialAbilityDetail;

        // 프리셋 행 재생성
        foreach (Transform child in presetContainer)
            Destroy(child.gameObject);

        foreach (var preset in job.presets)
        {
            if (preset == null) continue;
            var row = Instantiate(presetRowPrefab, presetContainer);
            row.Setup(preset);
        }
    }

    // ── 확인 버튼 ─────────────────────────────────────────────────────────────────
    void OnConfirm()
    {
        if (_selectedJob == null) return;

        if (playerSetup != null && playerIndex < playerSetup.players.Length)
        {
            playerSetup.players[playerIndex].job = _selectedJob.job;
            Debug.Log($"[CharacterSelect] Player {playerIndex + 1} → {_selectedJob.jobNameKR}");
        }

        // 패널 닫기 (씬 전환 로직은 필요에 따라 여기에 추가)
        gameObject.SetActive(false);
    }
}
