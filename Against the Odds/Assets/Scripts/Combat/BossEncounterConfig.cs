using UnityEngine;

[CreateAssetMenu(fileName = "BossEncounterConfig", menuName = "Against the Odds/Combat/Boss Encounter")]
public class BossEncounterConfig : ScriptableObject
{
    [Header("Identity")]
    public int bossIndex;
    public string bossDisplayName = "Boss";

    [Header("Combat")]
    public int playerStartHP = 50;
    public int bossStartHP = 80;
    [Tooltip("Optional override for the card database used by this encounter. Leave empty to use the bootstrap/GameManager database.")]
    public string cardDatabaseResourcePath;

    [Header("Boss Visual")]
    [Tooltip("Default sprite used by the combat scene for this boss. The scene SpriteRenderer is only a display slot.")]
    public Sprite bossSprite;
    [Min(0.01f)]
    [Tooltip("Multiplies the scene boss slot scale for this encounter.")]
    public float bossVisualScale = 1f;

    [Header("Music")]
    public AudioClip combatMusic;
    public bool fadeInCombatMusic = true;
    public CombatCardAudioProfile cardAudioProfile;

    [Header("Boss Behaviour")]
    [Tooltip("Fallback damage used if no boss phases/actions are configured.")]
    public int defaultAttackDamage = 10;

    [Header("Boss Dialogue")]
    [TextArea] public string introDialogue;
    public AudioClip introVoiceLine;
    public BossHpDialogueConfig[] hpDialogues;
    [TextArea] public string deathDialogue;
    public AudioClip deathVoiceLine;
    [TextArea] public string defeatResultText = "L'aventure s'arrete ici.";
    [TextArea] public string victoryResultText;

    [Header("Result Audio")]
    [Tooltip("Played immediately when the boss reaches its final death state.")]
    public AudioClip bossDeathSfx;
    [Tooltip("Played when the victory panel appears after its reveal delay.")]
    public AudioClip victorySfx;
    [Tooltip("Played when the defeat panel appears after its reveal delay.")]
    public AudioClip defeatSfx;
    [Tooltip("Played when pressing the defeat/end adventure button.")]
    public AudioClip defeatButtonSfx;

    [Header("Advanced Boss Sequences")]
    public BossPhaseConfig[] phases;

    [Header("Rewards")]
    [Tooltip("How many rewards the player can pick after victory.")]
    public int rewardPickCount = 1;
    [Tooltip("Card ids displayed as victory rewards for this boss. Max 3 should be used in the scene.")]
    public string[] rewardCardIds;
    [Tooltip("How many potions are gained after this boss is defeated.")]
    public int potionRewardCount = 1;
}

[System.Serializable]
public class BossPhaseConfig
{
    public string phaseId = "phase_1";
    public string displayName = "Phase 1";
    [Range(0f, 1f)] public float startsWhenHpPercentAtOrBelow = 1f;
    [Header("Duration")]
    [Tooltip("If > 0 and another phase exists, advances after this many boss turns spent in this phase.")]
    [Min(0)] public int advanceAfterBossTurns;
    [Tooltip("If > 0 and another phase exists, advances after this many complete loops through this phase action list.")]
    [Min(0)] public int advanceAfterPatternIterations;
    [Tooltip("If enabled, killing this phase ends the fight instead of forcing the next phase. Useful for optional phase branches.")]
    public bool deathEndsCombatBeforeNextPhase;
    [Tooltip("Optional max HP replacement when this phase starts. 0 keeps the encounter max HP.")]
    public int maxHpOverride;
    [Tooltip("If > 0, restores HP to this percent when the phase starts. Useful for death-triggered phase changes.")]
    [Range(0f, 1f)] public float healToHpPercentOnStart;
    public int shieldOnStart;
    public int invulnerableTurnsOnStart;
    [Tooltip("Optional phase sprite. Leave empty to keep the encounter default/current boss sprite.")]
    public Sprite bossSprite;
    [Min(0f)]
    [Tooltip("Optional phase scale. 0 keeps the current encounter/phase scale.")]
    public float bossVisualScale;
    [Tooltip("Optional sound played when this phase starts. The first phase does not use it on combat start.")]
    public AudioClip phaseStartSfx;
    public string dialogueOnStart;
    public BossActionConfig[] actions;
}

[System.Serializable]
public class BossHpDialogueConfig
{
    [Range(0f, 1f)] public float triggerWhenHpPercentAtOrBelow = 0.5f;
    [TextArea] public string line;
    public AudioClip voiceLine;
}

[System.Serializable]
public class BossActionConfig
{
    public string actionId = "sequence";
    public BossActionType actionType = BossActionType.Attack;
    [TextArea]
    public string bossLine;
    public AudioClip bossVoiceLine;
    [TextArea]
    public string chargeLine;
    public AudioClip chargeVoiceLine;
    [Min(0)] public int chargeTurns;
    public bool vulnerableWhileCharging;
    [Tooltip("Applied once when the sequence starts, before charge turns.")]
    public BossEffectConfig[] startEffects;
    [Tooltip("Applied every boss turn while the sequence is charging.")]
    public BossEffectConfig[] chargeTurnEffects;
    [Tooltip("Applied when the sequence resolves after its charge, or immediately if chargeTurns is 0.")]
    public BossEffectConfig[] resolveEffects;
    [Header("Interrupt")]
    [Min(0)] public int interruptIfPlayerBlockAtLeast;
    [Min(0)] public int interruptBossStunTurns;
    [TextArea]
    public string interruptLine;
    public AudioClip interruptVoiceLine;
    [Min(0)] public int damage;
    [Min(0)] public int shield;
    [Min(0)] public int invulnerableTurns;
    [Min(0)] public int vulnerableTurns;
}

public enum BossActionType
{
    Attack,
    GainShield,
    BecomeInvulnerable,
    BecomeVulnerable,
    ClearInvulnerability,
    Wait
}

[System.Serializable]
public class BossEffectConfig
{
    public BossEffectType effectType = BossEffectType.AttackPlayer;
    [Min(0)] public int value;
    [TextArea]
    public string line;
    public AudioClip voiceLine;
}

public enum BossEffectType
{
    AttackPlayer,
    GainShield,
    LoseSelfHp,
    HealSelf,
    BecomeInvulnerable,
    BecomeVulnerable,
    ClearInvulnerability,
    StunSelf,
    ClearStun,
    Wait
}
