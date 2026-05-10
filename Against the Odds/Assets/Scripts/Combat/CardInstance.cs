using UnityEngine;

public class CardInstance
{
    public CardDefinition Definition { get; }
    public bool IsExhausted { get; set; }

    public CardInstance(CardDefinition definition)
    {
        Definition = definition;
    }

    public int GetEffectiveCost(CombatState state)
    {
        int cost = Definition.cost;
        if (state == null)
        {
            return cost;
        }

        // Autel Profané : les cartes Rituel coûtent 1 mana de moins
        if (Definition.subtype == "ritual" && state.ActiveTerrain?.id == "autel_profane")
            cost = Mathf.Max(0, cost - 1);

        return cost;
    }

    public bool HasEffect(string action)
    {
        foreach (var effect in Definition.effects)
            if (effect.action == action) return true;
        return false;
    }
}
