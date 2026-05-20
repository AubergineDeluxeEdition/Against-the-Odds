using System;

[Serializable]
public class DeckDefinition
{
    public string deckId;
    public string deckName;
    public int totalCards;
    public DeckEntry[] deck;
    public CardDefinition[] cards;
}

[Serializable]
public class DeckEntry
{
    public string cardId;
    public int quantity;
}

[Serializable]
public class CardDefinition
{
    public string id;
    public string name;
    public int cost;
    public string type;
    public string subtype;
    public string[] tags;
    public string artResourcePath;
    public string description;
    public string descriptionTemplate;
    public CardEffect[] effects;
    public TerrainEffect terrainEffect;
}

[Serializable]
public class CardEffect
{
    public string action;
    public string status;
    public string target;
    public string terrainId;
    public string trigger;
    public int value;
    public float multiplier;
    public int damagePerStack;
    public EffectCondition condition;
    public DelayedPayload payload;
    public TemporaryModifier modifier;
}

// JsonUtility ne retourne jamais null pour les classes imbriquées :
// vérifier IsActive() avant d'utiliser.
[Serializable]
public class EffectCondition
{
    public string target;
    public string hasStatus;
    public int minStatusStacks;
    public bool hasActiveTerrain;

    public bool IsActive() => !string.IsNullOrEmpty(hasStatus) || minStatusStacks > 0 || hasActiveTerrain;
}

[Serializable]
public class TerrainEffect
{
    public int attackDamageBonus;
    public int burnAppliedBonus;
    public float burnAppliedMultiplier;
    public int defenseBlockBonus;
    public int firstDefenseEachTurnAppliesBurn;
    public int ritualCostReduction;
    public int firstRitualEachTurnGainMana;
    public int firstRitualEachTurnDrawCards;
    public int endTurnIfSpentManaAtLeast6DealDamage;
    public int endTurnIfSpentManaAtLeast8GainBlock;
}

[Serializable]
public class DelayedPayload
{
    public string action;
    public string status;
    public int value;
    public string target;
}

[Serializable]
public class TemporaryModifier
{
    public string appliesTo;
    public int damageBonus;
}
