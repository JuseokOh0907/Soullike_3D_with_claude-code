using System;
using UnityEngine;

public enum PlayerJob
{
    Barbarian = 0,
    BarbararianLarge = 1,
    Knight = 2,
    Ranger = 3,
    Rogue = 4,
    RogueHooded = 5,
    Mage = 6,
    Druid = 7,
    Engineer = 8
}

[Serializable]
public class PlayerConfig
{
    public bool isActive = false;
    public string playerName = "Player";
    public PlayerJob job = PlayerJob.Knight;
    public Color playerColor = Color.white;
    public Vector3 spawnPosition = Vector3.zero;
    public float spawnRotationY = 0f;
}

/// <summary>Weapon/accessory slots to attach to handslot.r and handslot.l bones.</summary>
[Serializable]
public class JobLoadout
{
    /// <summary>Asset path for the right-hand weapon. Null means empty hand.</summary>
    public string rightHandPath;
    /// <summary>Asset path for the left-hand item. Null means empty hand.</summary>
    public string leftHandPath;
}

[CreateAssetMenu(fileName = "PlayerSetupData", menuName = "Game/Player Setup Data")]
public class PlayerSetupData : ScriptableObject
{
    public const int MaxPlayers = 4;

    private const string CharRoot = "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Characters/";
    private const string AccRoot  = "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/";

    public PlayerConfig[] players = new PlayerConfig[MaxPlayers]
    {
        new PlayerConfig { isActive = true,  playerName = "Player 1", playerColor = new Color(0.2f, 0.5f, 1f),   spawnPosition = new Vector3(-3, 0, -3) },
        new PlayerConfig { isActive = false, playerName = "Player 2", playerColor = new Color(1f,  0.3f, 0.3f),  spawnPosition = new Vector3( 3, 0, -3) },
        new PlayerConfig { isActive = false, playerName = "Player 3", playerColor = new Color(0.3f, 1f,  0.3f),  spawnPosition = new Vector3(-3, 0,  3) },
        new PlayerConfig { isActive = false, playerName = "Player 4", playerColor = new Color(1f,  0.8f, 0.1f),  spawnPosition = new Vector3( 3, 0,  3) },
    };

    // ── Character prefab paths ────────────────────────────────────────────────
    public static string GetPrefabPath(PlayerJob job) => job switch
    {
        PlayerJob.Barbarian        => CharRoot + "Barbarian.prefab",
        PlayerJob.BarbararianLarge => CharRoot + "Barbarian_Large.prefab",
        PlayerJob.Knight           => CharRoot + "Knight.prefab",
        PlayerJob.Ranger           => CharRoot + "Ranger.prefab",
        PlayerJob.Rogue            => CharRoot + "Rogue.prefab",
        PlayerJob.RogueHooded      => CharRoot + "Rogue_Hooded.prefab",
        PlayerJob.Mage             => CharRoot + "Mage.prefab",
        PlayerJob.Druid            => CharRoot + "Druid.prefab",
        PlayerJob.Engineer         => CharRoot + "Engineer.prefab",
        _                          => CharRoot + "Knight.prefab",
    };

    // ── Weapon / accessory loadout per job ────────────────────────────────────
    //
    //  Bone names confirmed from prefab inspection:
    //    handslot.r  →  right hand weapon socket
    //    handslot.l  →  left hand weapon/shield socket
    //
    //  Job            Right hand          Left hand
    //  ─────────────────────────────────────────────────────────────────────
    //  Barbarian      axe_2handed         —           (two-handed grip)
    //  BarbLarge      axe_2handed_Large   —           (oversized two-handed)
    //  Knight         sword_1handed       shield_round
    //  Ranger         bow_withString      quiver
    //  Rogue          dagger              dagger      (dual wield)
    //  RogueHooded    dagger              smokebomb
    //  Mage           staff               spellbook_open
    //  Druid          druid_staff         —
    //  Engineer       engineer_Wrench     crossbow_1handed
    //  ─────────────────────────────────────────────────────────────────────
    public static JobLoadout GetJobLoadout(PlayerJob job) => job switch
    {
        PlayerJob.Barbarian        => new JobLoadout { rightHandPath = AccRoot + "axe_2handed.prefab" },
        PlayerJob.BarbararianLarge => new JobLoadout { rightHandPath = AccRoot + "axe_2handed_Large.prefab" },
        PlayerJob.Knight           => new JobLoadout { rightHandPath = AccRoot + "sword_1handed.prefab",
                                                       leftHandPath  = AccRoot + "shield_round.prefab" },
        PlayerJob.Ranger           => new JobLoadout { rightHandPath = AccRoot + "bow_withString.prefab",
                                                       leftHandPath  = AccRoot + "quiver.prefab" },
        PlayerJob.Rogue            => new JobLoadout { rightHandPath = AccRoot + "dagger.prefab",
                                                       leftHandPath  = AccRoot + "dagger.prefab" },
        PlayerJob.RogueHooded      => new JobLoadout { rightHandPath = AccRoot + "dagger.prefab",
                                                       leftHandPath  = AccRoot + "smokebomb.prefab" },
        PlayerJob.Mage             => new JobLoadout { rightHandPath = AccRoot + "staff.prefab",
                                                       leftHandPath  = AccRoot + "spellbook_open.prefab" },
        PlayerJob.Druid            => new JobLoadout { rightHandPath = AccRoot + "druid_staff.prefab" },
        PlayerJob.Engineer         => new JobLoadout { rightHandPath = AccRoot + "engineer_Wrench.prefab",
                                                       leftHandPath  = AccRoot + "crossbow_1handed.prefab" },
        _                          => new JobLoadout(),
    };

    // ── Display helpers ───────────────────────────────────────────────────────
    public static string GetJobDisplayName(PlayerJob job) => job switch
    {
        PlayerJob.Barbarian        => "Barbarian",
        PlayerJob.BarbararianLarge => "Barbarian (Large)",
        PlayerJob.Knight           => "Knight",
        PlayerJob.Ranger           => "Ranger",
        PlayerJob.Rogue            => "Rogue",
        PlayerJob.RogueHooded      => "Rogue (Hooded)",
        PlayerJob.Mage             => "Mage",
        PlayerJob.Druid            => "Druid",
        PlayerJob.Engineer         => "Engineer",
        _                          => job.ToString(),
    };

    public static string GetJobDescription(PlayerJob job) => job switch
    {
        PlayerJob.Barbarian        => "Fierce warrior — axe_2handed  |  —",
        PlayerJob.BarbararianLarge => "Oversized barbarian — axe_2handed_Large  |  —",
        PlayerJob.Knight           => "Armored defender — sword_1handed  |  shield_round",
        PlayerJob.Ranger           => "Swift marksman — bow_withString  |  quiver",
        PlayerJob.Rogue            => "Agile duelist — dagger  |  dagger (dual wield)",
        PlayerJob.RogueHooded      => "Shadowy assassin — dagger  |  smokebomb",
        PlayerJob.Mage             => "Arcane scholar — staff  |  spellbook_open",
        PlayerJob.Druid            => "Nature healer — druid_staff  |  —",
        PlayerJob.Engineer         => "Inventive tinkerer — engineer_Wrench  |  crossbow_1handed",
        _                          => string.Empty,
    };
}
