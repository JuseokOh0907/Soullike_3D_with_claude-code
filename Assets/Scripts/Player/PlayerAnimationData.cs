using UnityEngine;

/// <summary>
/// 직업 하나의 애니메이션 클립을 보관하는 ScriptableObject.
///
/// ┌ Armed   : 무기를 장착한 상태의 이동 모션 3종
/// └ Unarmed : 무기가 없거나 파괴된 상태 — 이동 3종 + 점프/피격/사망 포함
///
/// 점프·피격·사망은 비무장 상태에 속합니다.
/// (무기가 없을 때 발생하는 행동이므로 Unarmed 안에서 관리)
/// </summary>
[CreateAssetMenu(fileName = "Anim_Job", menuName = "Game/Player Animation Data")]
public class PlayerAnimationData : ScriptableObject
{
    [Header("직업")]
    public PlayerJob job;

    // ════════════════════════════════════════════════════════════════════════════
    // 무장 상태 (Armed) — 무기를 들고 있을 때
    // ════════════════════════════════════════════════════════════════════════════
    [Header("── 무장 (Armed) ────────────────────────")]
    [Tooltip("무기를 든 채 서 있는 대기 모션")]
    public AnimationClip armedIdle;

    [Tooltip("무기를 든 채 걷는 모션")]
    public AnimationClip armedWalk;

    [Tooltip("무기를 든 채 달리는 모션")]
    public AnimationClip armedRun;

    // ════════════════════════════════════════════════════════════════════════════
    // 비무장 상태 (Unarmed) — 무기가 없거나 파괴됐을 때
    // 이동 모션 + 점프 / 피격 / 사망 모두 포함
    // ════════════════════════════════════════════════════════════════════════════
    [Header("── 비무장 (Unarmed) — 이동 ───────────────")]
    [Tooltip("맨손 대기 모션")]
    public AnimationClip unarmedIdle;

    [Tooltip("맨손 걷기 모션")]
    public AnimationClip unarmedWalk;

    [Tooltip("맨손 달리기 모션")]
    public AnimationClip unarmedRun;

    [Header("── 비무장 (Unarmed) — 액션 ──────────────")]
    [Tooltip("점프 모션 (비무장 상태에 포함)")]
    public AnimationClip jump;

    [Tooltip("피격 모션 (비무장 기준)")]
    public AnimationClip hit;

    [Tooltip("사망 모션 (비무장 기준)")]
    public AnimationClip death;

    // ════════════════════════════════════════════════════════════════════════════
    // KayKit 기본 클립 경로 (에디터 전용)
    // ════════════════════════════════════════════════════════════════════════════
    private const string M = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/";
    private const string L = "Assets/KayKit/Characters/Animations/Animations/Rig_Large/";

    public static AnimClipPaths GetDefaultPaths(PlayerJob job) => job switch
    {
        PlayerJob.Barbarian => new AnimClipPaths
        {
            armedIdle   = M + "Combat Melee/Melee_2H_Idle.anim",
            armedWalk   = M + "Movement Basic/Walking_A.anim",
            armedRun    = M + "Movement Basic/Running_A.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Basic/Walking_A.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_A.anim",
            death       = M + "General/Death_A.anim",
        },
        PlayerJob.BarbararianLarge => new AnimClipPaths
        {
            armedIdle   = L + "Combat Melee/Melee_2H_Idle.anim",
            armedWalk   = L + "Movement Basic/Walking_A.anim",
            armedRun    = L + "Movement Basic/Running_A.anim",
            unarmedIdle = L + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = L + "Movement Basic/Walking_A.anim",
            unarmedRun  = L + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = L + "General/Hit_A.anim",
            death       = L + "General/Death_A.anim",
        },
        PlayerJob.Knight => new AnimClipPaths
        {
            armedIdle   = M + "Combat Melee/Melee_Blocking.anim",
            armedWalk   = M + "Movement Basic/Walking_A.anim",
            armedRun    = M + "Movement Basic/Running_A.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Basic/Walking_A.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_A.anim",
            death       = M + "General/Death_A.anim",
        },
        PlayerJob.Ranger => new AnimClipPaths
        {
            armedIdle   = M + "Combat Ranged/Ranged_Bow_Idle.anim",
            armedWalk   = M + "Movement Advanced/Running_HoldingBow.anim",
            armedRun    = M + "Movement Advanced/Running_HoldingBow.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Basic/Walking_A.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_A.anim",
            death       = M + "General/Death_A.anim",
        },
        PlayerJob.Rogue => new AnimClipPaths
        {
            armedIdle   = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            armedWalk   = M + "Movement Advanced/Sneaking.anim",
            armedRun    = M + "Movement Basic/Running_A.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Advanced/Sneaking.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_A.anim",
            death       = M + "General/Death_A.anim",
        },
        PlayerJob.RogueHooded => new AnimClipPaths
        {
            armedIdle   = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            armedWalk   = M + "Movement Advanced/Sneaking.anim",
            armedRun    = M + "Movement Basic/Running_A.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Advanced/Sneaking.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_A.anim",
            death       = M + "General/Death_A.anim",
        },
        PlayerJob.Mage => new AnimClipPaths
        {
            armedIdle   = M + "Combat Ranged/Ranged_Magic_Raise.anim",
            armedWalk   = M + "Movement Basic/Walking_C.anim",
            armedRun    = M + "Movement Basic/Running_B.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Basic/Walking_A.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_B.anim",
            death       = M + "General/Death_A.anim",
        },
        PlayerJob.Druid => new AnimClipPaths
        {
            armedIdle   = M + "Combat Ranged/Ranged_Magic_Raise.anim",
            armedWalk   = M + "Movement Basic/Walking_C.anim",
            armedRun    = M + "Movement Basic/Running_B.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Basic/Walking_A.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_B.anim",
            death       = M + "General/Death_A.anim",
        },
        PlayerJob.Engineer => new AnimClipPaths
        {
            armedIdle   = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            armedWalk   = M + "Movement Basic/Walking_A.anim",
            armedRun    = M + "Movement Basic/Running_A.anim",
            unarmedIdle = M + "Combat Melee/Melee_Unarmed_Idle.anim",
            unarmedWalk = M + "Movement Basic/Walking_A.anim",
            unarmedRun  = M + "Movement Basic/Running_A.anim",
            jump        = M + "Movement Basic/Jump_Full_Short.anim",
            hit         = M + "General/Hit_A.anim",
            death       = M + "General/Death_A.anim",
        },
        _ => new AnimClipPaths(),
    };
}

public class AnimClipPaths
{
    public string armedIdle;
    public string armedWalk;
    public string armedRun;
    public string unarmedIdle;
    public string unarmedWalk;
    public string unarmedRun;
    public string jump;
    public string hit;
    public string death;
}
