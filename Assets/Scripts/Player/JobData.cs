using System;
using UnityEngine;

// ── 무기 장착 위치 ──────────────────────────────────────────────────────────────
public enum HandSlot
{
    Right,   // 오른손
    Left,    // 왼손
    Deploy,  // 설치형 (포탑 등)
}

// ── 무기 슬롯 하나 ──────────────────────────────────────────────────────────────
[Serializable]
public class WeaponSlot
{
    [Tooltip("KayKit Accessories 폴더의 프리팹 이름 (확장자 제외)")]
    public string prefabName;

    [Tooltip("장착 위치")]
    public HandSlot hand;

    [Tooltip("사용 후 교체될 프리팹 이름 (없으면 공란). 예: mug_full → mug_empty")]
    public string changeToOnUse;
}

// ── 프리셋 (직업당 3개) ─────────────────────────────────────────────────────────
[Serializable]
public class JobPreset
{
    [Tooltip("프리셋 이름 (무기 조합 설명)")]
    public string presetName;

    public float speed;

    [Tooltip("공격력 — 숫자 또는 수식. 예: '150~1500', '1000*차징시간(max 5s)'")]
    public string attackValue;

    [Tooltip("Mp 소모 — 없으면 공란. 예: '(1s-1mp/100)', '50'")]
    public string mpCostValue;

    public float stamina;

    [Tooltip("이 프리셋의 무기 구성")]
    public WeaponSlot[] weaponSlots;

    [Tooltip("프리셋 관련 특이 사항")]
    public string note;
}

// ── 직업 데이터 ─────────────────────────────────────────────────────────────────
[CreateAssetMenu(fileName = "JobData", menuName = "Game/Job Data")]
public class JobData : ScriptableObject
{
    [Header("기본 정보")]
    public PlayerJob job;

    [Tooltip("직업 한글 이름")]
    public string jobNameKR;

    public int hp;

    [Header("프리셋 (3개)")]
    public JobPreset[] presets = new JobPreset[3];

    [Header("특수 능력")]
    [TextArea(2, 4)]
    public string specialAbility;

    [TextArea(3, 6)]
    public string specialAbilityDetail;
}
