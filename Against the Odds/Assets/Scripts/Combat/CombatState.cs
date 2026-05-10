using System.Collections.Generic;

public class CombatState
{
    public int PlayerHP;
    public int PlayerMaxHP;
    public int PlayerBlock;

    public int BaseMana;
    public int PermanentManaBonus;
    public int MaxMana;
    public int CurrentMana;
    public int ManaSpentThisTurn;

    public int EnemyHP;
    public int EnemyMaxHP;
    public int EnemyBlock;
    public int EnemyShield;
    public int EnemyInvulnerableTurns;
    public int EnemyVulnerableTurns;
    public int EnemyStunnedTurns;
    public int BossPhaseIndex;
    public bool[] BossHpDialoguePlayed;
    public Dictionary<string, int> EnemyStatuses = new Dictionary<string, int>();

    public List<CardInstance> Hand = new List<CardInstance>();
    public List<CardInstance> DrawPile = new List<CardInstance>();
    public List<CardInstance> DiscardPile = new List<CardInstance>();
    public List<CardInstance> ExhaustedPile = new List<CardInstance>();

    public CardDefinition ActiveTerrain;

    public int NextAttackDamageBonus;
    public DelayedTrigger PendingRetaliation;

    public bool FirstDefensePlayedThisTurn;
    public bool FirstRitualPlayedThisTurn;

    public bool WaitingForDiscard;
    public int CardsToDiscard;
}

public class DelayedTrigger
{
    public string Trigger;
    public DelayedPayload Payload;
}
