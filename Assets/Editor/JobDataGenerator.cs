using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Game > Generate Job Data Assets
///
/// 기획서(3D_AI_Game_기획서.xlsx) 내용을 기반으로
/// Assets/Jobs/[직업명]/ 폴더에 JobData .asset 파일을 자동 생성합니다.
/// </summary>
public static class JobDataGenerator
{
    private const string JobsRoot = "Assets/Jobs";

    [MenuItem("Game/Generate Job Data Assets")]
    public static void Generate()
    {
        EnsureFolder(JobsRoot);

        CreateKnight();
        CreateBarbarianSmall();
        CreateBarbarianLarge();
        CreateMage();
        CreateDruid();
        CreateEngineer();
        CreateRanger();
        CreateRogue();
        CreateRogueHooded();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var folder = AssetDatabase.LoadAssetAtPath<Object>(JobsRoot);
        EditorGUIUtility.PingObject(folder);

        Debug.Log("[JobDataGenerator] 9개 직업 데이터 생성 완료 → " + JobsRoot);
        EditorUtility.DisplayDialog("Job Data 생성 완료",
            "9개 직업 데이터 에셋이 생성되었습니다.\n\n저장 위치: " + JobsRoot,
            "확인");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 기사 (Knight)
    // HP 3500 | 특수: 방패 막기 데미지 90% 감소
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateKnight()
    {
        var d = Make("Knight", PlayerJob.Knight, "기사", 3500);

        d.presets[0] = Preset("양손대검",
            speed: 10f, atk: "1800", mp: null, stamina: 1000f,
            note: "양손검 전용 — 강력한 단일 공격",
            W("sword_2handed_color", HandSlot.Right));

        d.presets[1] = Preset("대검 + 원형 방패",
            speed: 7f, atk: "1500", mp: null, stamina: 1000f,
            note: "sword_2handed scale * 70% 적용",
            W("sword_2handed", HandSlot.Right),
            W("shield_round", HandSlot.Left));

        d.presets[2] = Preset("한손검 + 대형 방패",
            speed: 7f, atk: "1300", mp: null, stamina: 1000f,
            note: "방어력 최우선 세팅",
            W("sword_1handed", HandSlot.Right),
            W("shield_square", HandSlot.Left));

        d.specialAbility       = "방패 막기 데미지 90% 감소";
        d.specialAbilityDetail = "대검 3연격 시 atk 50% 증가";

        Save(d, "Knight");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 바바리안 Small
    // HP 4000 | 특수: 맥주 이용 시 체력 및 공격력 증가
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateBarbarianSmall()
    {
        var d = Make("Barbarian", PlayerJob.Barbarian, "바바리안 (소형)", 4000);

        d.presets[0] = Preset("양손도끼",
            speed: 12f, atk: "1250", mp: null, stamina: 1200f,
            note: null,
            W("axe_2handed", HandSlot.Right));

        d.presets[1] = Preset("한손도끼 + 맥주",
            speedStr: "13(15)", atk: "1000(*1.5)", mp: null, stamina: 1200f,
            note: "맥주 사용 후 mug_empty로 교체됨 / 맥주 중 속도 13→15, 공격 *1.5",
            W("axe_1handed", HandSlot.Right),
            WChange("mug_full", HandSlot.Left, "mug_empty"));

        d.presets[2] = Preset("한손도끼 + 방패",
            speed: 13f, atk: "1000", mp: null, stamina: 1200f,
            note: null,
            W("axe_1handed", HandSlot.Right),
            W("shield_round_barbarian", HandSlot.Left));

        d.specialAbility       = "맥주 이용 시 체력 및 공격력 증가";
        d.specialAbilityDetail = "체력 40% 이하인 경우 스태미나 소모량 50% 감소";

        Save(d, "Barbarian");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 바바리안 Large
    // HP 6000 | 특수: 맥주 이용 시 체력 및 공격력 증가
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateBarbarianLarge()
    {
        var d = Make("BarbararianLarge", PlayerJob.BarbararianLarge, "바바리안 (대형)", 6000);

        d.presets[0] = Preset("양손도끼",
            speed: 6f, atk: "2500", mp: null, stamina: 2000f,
            note: null,
            W("axe_2handed", HandSlot.Right));

        d.presets[1] = Preset("한손도끼 + 맥주",
            speedStr: "6(9)", atk: "2000(*1.5)", mp: null, stamina: 2000f,
            note: "맥주 사용 후 mug_empty_Large로 교체됨 / 맥주 중 속도 6→9, 공격 *1.5",
            W("axe_1handed_Large", HandSlot.Right),
            WChange("mug_full_Large", HandSlot.Left, "mug_empty_Large"));

        d.presets[2] = Preset("한손도끼 + 방패",
            speed: 6f, atk: "2000", mp: null, stamina: 2000f,
            note: null,
            W("axe_1handed_Large", HandSlot.Right),
            W("shield_round_barbarian_Large", HandSlot.Left));

        d.specialAbility       = "맥주 이용 시 체력 및 공격력 증가";
        d.specialAbilityDetail = "체력 40% 이하인 경우 스태미나 소모량 50% 감소";

        Save(d, "BarbararianLarge");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 마법사 (Mage)
    // HP 2000 | 특수: 마법 시전 시간에 따른 데미지 차별화
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateMage()
    {
        var d = Make("Mage", PlayerJob.Mage, "마법사", 2000);

        d.presets[0] = Preset("완드",
            speed: 13f, atk: "150~1500", mp: "(1s당 mp 1/100)", stamina: 800f,
            note: "스태프와 동시 장착 불가",
            W("wand", HandSlot.Right));

        d.presets[1] = Preset("마도서",
            speed: 11f, atk: "크리티컬 확률 16% per 1s 누적", mp: "MpCost*1.5", stamina: 800f,
            note: "완드·스태프와 동시 장착 불가 / 크리티컬 발생 시 Mp 50% 회복, 캐스팅 시간 +3s",
            WChange("spellbook_closed", HandSlot.Left, "spellbook_open"));

        d.presets[2] = Preset("스태프",
            speed: 9f, atk: "450~4500", mp: "(1s당 mp 3/100)", stamina: 800f,
            note: "완드와 동시 장착 불가 / 스태프 타격 시 데미지의 1% Mp 회복",
            W("staff", HandSlot.Right));

        d.specialAbility       = "마법 시전 시간에 따른 데미지 차별화";
        d.specialAbilityDetail =
            "마도서 장착 시 크리티컬 확률 초당 10% 누적\n" +
            "크리티컬 발생 시 Mp 50% 회복, 캐스팅 시간 3초 증가(총 데미지량 동일)";

        Save(d, "Mage");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 드루이드 (Druid)
    // HP 3500 | 특수: 버프/디버프 물약 생성
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateDruid()
    {
        var d = Make("Druid", PlayerJob.Druid, "드루이드", 3500);

        d.presets[0] = Preset("회복 물약 (빨강/파랑)",
            speed: 13f, atk: "Hp+5~25% / Mp+5~25%", mp: "등급별 소진/80", stamina: 1500f,
            note: "크기별 효과율 상이. 빨강=HP 회복, 파랑=MP 회복\n" +
                  "potion_small/medium/large/huge × red, blue",
            W("potion_small_red",  HandSlot.Left),
            W("potion_medium_red", HandSlot.Left),
            W("potion_large_red",  HandSlot.Left),
            W("potion_huge_red",   HandSlot.Left),
            W("potion_small_blue",  HandSlot.Left),
            W("potion_medium_blue", HandSlot.Left),
            W("potion_large_blue",  HandSlot.Left),
            W("potion_huge_blue",   HandSlot.Left));

        d.presets[1] = Preset("공격/속도 버프 물약 (초록/주황)",
            speed: 15f, atk: "atk+10~50% / 속도+10~50%", mp: "등급별 소진/80", stamina: 1500f,
            note: "크기별 효과율 상이. 초록=공격 증가, 주황=속도 증가\n" +
                  "potion_small/medium/large/huge × green, orange",
            W("potion_small_green",   HandSlot.Left),
            W("potion_medium_green",  HandSlot.Left),
            W("potion_large_green",   HandSlot.Left),
            W("potion_huge_green",    HandSlot.Left),
            W("potion_small_orange",  HandSlot.Left),
            W("potion_medium_orange", HandSlot.Left),
            W("potion_large_orange",  HandSlot.Left),
            W("potion_huge_orange",   HandSlot.Left));

        d.presets[2] = Preset("드루이드 스태프",
            speed: 10f, atk: "800", mp: "2/80", stamina: 1500f,
            note: "스태프 타격 시 데미지의 1% Mp 회복",
            W("druid_staff", HandSlot.Right));

        d.specialAbility       = "(디)버프 물약 생성 (제조 시간 상이: 2/4/6/9s — 효과 5/10/15/25%)";
        d.specialAbilityDetail =
            "물약 효과 자신에게 50% 추가 적용\n" +
            "마나 소모량에 따라 물약 등급 자동 변경\n" +
            "물약 최대 소지 10개\n" +
            "포탑 설치 시 2초당 1회 자동 공격 / 범위 슬로우 (설치시간 15초)";

        Save(d, "Druid");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 엔지니어 (Engineer)
    // HP 2500 | 특수: 무기 내구도 수리 가능
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateEngineer()
    {
        var d = Make("Engineer", PlayerJob.Engineer, "엔지니어", 2500);

        d.presets[0] = Preset("샷건",
            speed: 12f, atk: "탄환 수 비례 (Bullet per 150/20)", mp: "50", stamina: 1200f,
            note: "피통 50% 이하일 때 바톤터치 가능",
            W("shotgun", HandSlot.Right));

        d.presets[1] = Preset("수리공 + 탄약 생성",
            speed: 9f, atk: "무기 내구도 수리 (2s/5%/mp2)", mp: "50", stamina: 1200f,
            note: "탄약 제작 중 ammo_crate_withLid로 교체됨\n" +
                  "준비 시간 2s, 2s당 5% 수리, 준비 제외 mp2씩 사용",
            W("engineer_Wrench",  HandSlot.Right),
            WChange("ammo_crate", HandSlot.Left, "ammo_crate_withLid"));

        d.presets[2] = Preset("포탑 설치",
            speed: 5f, atk: "2Bullet / Bullet per 1500", mp: "50", stamina: 1200f,
            note: "포탑 설치 시 2초당 1회 자동 공격 / 범위 슬로우 (설치시간 15초)",
            W("turret_base", HandSlot.Deploy));

        d.specialAbility       = "무기 내구도 수리 가능";
        d.specialAbilityDetail =
            "준비 시간 2s, 수리 2s당 5%, 준비 제외 mp2씩 사용\n" +
            "바톤터치 된 파트너 체력 100% 이상 시 공격력 버프로 환산";

        Save(d, "Engineer");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 레인저 (Ranger)
    // HP 2800 | 특수: 화살 총 40개, 수리 불가
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateRanger()
    {
        var d = Make("Ranger", PlayerJob.Ranger, "레인저", 2800);

        d.presets[0] = Preset("활 + 화살 (차징)",
            speed: 18f, atk: "1000 * 차징시간 (max 5s)", mp: null, stamina: 1050f,
            note: "차징 중 arrow_bow 표시 / bow_withString + arrow_bow_bundle 기본 장착",
            W("bow_withString",    HandSlot.Right),
            W("arrow_bow_bundle",  HandSlot.Left),
            W("arrow_bow",         HandSlot.Right)); // 차징 시 표시

        d.presets[1] = Preset("연사 화살",
            speed: 20f, atk: "800 / (화살 개수 40)", mp: null, stamina: 1050f,
            note: "arrow_bow 단독 사용",
            W("arrow_bow", HandSlot.Right));

        d.presets[2] = Preset("활 (단순)",
            speed: 20f, atk: "1200", mp: null, stamina: 1050f,
            note: "활 차징 시간 동안 이동속도 4.5",
            W("bow", HandSlot.Right));

        d.specialAbility       = "화살 총 40개 / 수리 불가";
        d.specialAbilityDetail = "데미지 최대 5초 차징 / 활 차징 시간 동안 이동속도 4.5";

        Save(d, "Ranger");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 악동 — Rogue
    // HP 1600 (개별) | 특수: 체력 50% 이하 시 바톤터치
    // ════════════════════════════════════════════════════════════════════════════
    private static void CreateRogue()
    {
        var d = Make("Rogue", PlayerJob.Rogue, "악동 (로그)", 1600);
        FillRoguePresets(d);
        Save(d, "Rogue");
    }

    private static void CreateRogueHooded()
    {
        var d = Make("RogueHooded", PlayerJob.RogueHooded, "악동 (로그 후드)", 1600);
        FillRoguePresets(d);
        Save(d, "RogueHooded");
    }

    // Rogue & Rogue_Hooded 공용 프리셋
    private static void FillRoguePresets(JobData d)
    {
        d.presets[0] = Preset("한손 석궁",
            speed: 13f, atk: "1200", mp: null, stamina: 1800f,
            note: "차징 중 arrow_crossbow 표시",
            W("crossbow_1handed", HandSlot.Right),
            W("quiver",           HandSlot.Left),
            W("arrow_crossbow",   HandSlot.Right)); // 차징 시 표시

        d.presets[1] = Preset("양손 석궁",
            speed: 11f, atk: "1800", mp: null, stamina: 1800f,
            note: "차징 중 arrow_crossbow 표시",
            W("crossbow_2handed", HandSlot.Right),
            W("quiver",           HandSlot.Left),
            W("arrow_crossbow",   HandSlot.Right));

        d.presets[2] = Preset("단검 + 연막탄",
            speed: 18f, atk: "투척 1500+도트*2 / 근접 1000+도트", mp: null, stamina: 1800f,
            note: "단검: 베기 가능, 던진 후 지면에서 회수 가능\n연막탄: 투척 전용",
            W("dagger",     HandSlot.Right),
            W("smokebomb",  HandSlot.Left));

        d.specialAbility       = "악동 체력 50% 이하일 때 바톤터치 가능";
        d.specialAbilityDetail =
            "도트 데미지 초당 100 (디폴트)\n" +
            "바톤터치 된 파트너 체력 100% 이상 시 공격력 버프로 환산\n" +
            "무기 선택 자유 / 파트너가 죽어도 남은 캐릭터로 플레이 가능";
    }

    // ════════════════════════════════════════════════════════════════════════════
    // 헬퍼 메서드
    // ════════════════════════════════════════════════════════════════════════════

    private static JobData Make(string assetName, PlayerJob job, string nameKR, int hp)
    {
        var d = ScriptableObject.CreateInstance<JobData>();
        d.job       = job;
        d.jobNameKR = nameKR;
        d.hp        = hp;
        d.presets   = new JobPreset[3];
        return d;
    }

    // speed float 버전
    private static JobPreset Preset(string name, float speed, string atk, string mp,
                                    float stamina, string note, params WeaponSlot[] weapons)
    {
        return new JobPreset
        {
            presetName   = name,
            speed        = speed,
            attackValue  = atk,
            mpCostValue  = mp,
            stamina      = stamina,
            weaponSlots  = weapons,
            note         = note,
        };
    }

    // speed string 버전 (예: "13(15)")
    private static JobPreset Preset(string name, string speedStr, string atk, string mp,
                                    float stamina, string note, params WeaponSlot[] weapons)
    {
        float.TryParse(speedStr.Split('(')[0], out float spd);
        return new JobPreset
        {
            presetName   = name,
            speed        = spd,
            attackValue  = atk,
            mpCostValue  = mp,
            stamina      = stamina,
            weaponSlots  = weapons,
            note         = (note ?? "") + (speedStr.Contains("(") ? $"\n속도 범위: {speedStr}" : ""),
        };
    }

    private static WeaponSlot W(string prefab, HandSlot hand) =>
        new WeaponSlot { prefabName = prefab, hand = hand };

    private static WeaponSlot WChange(string prefab, HandSlot hand, string changeTo) =>
        new WeaponSlot { prefabName = prefab, hand = hand, changeToOnUse = changeTo };

    private static void Save(JobData data, string jobFolderName)
    {
        string folder = $"{JobsRoot}/{jobFolderName}";
        EnsureFolder(folder);

        string path = $"{folder}/{jobFolderName}_JobData.asset";

        // 이미 있으면 덮어쓰기
        var existing = AssetDatabase.LoadAssetAtPath<JobData>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(data, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(data, path);
        }

        Debug.Log($"[JobDataGenerator] 저장: {path}");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
        string child  = Path.GetFileName(folderPath);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, child);
    }
}
