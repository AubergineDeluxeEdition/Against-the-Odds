using System.Text.RegularExpressions;
using UnityEngine;

public static class CardTextFormatter
{
    private static readonly Regex DamageToken = new Regex(@"\{damage:(\d+)\}", RegexOptions.Compiled);
    private static readonly Regex BlockToken = new Regex(@"\{block:(\d+)\}", RegexOptions.Compiled);
    private static readonly Regex StatusToken = new Regex(@"\{status:([a-zA-Z0-9_]+):(\d+)\}", RegexOptions.Compiled);
    private static readonly Regex ValueToken = new Regex(@"\{value:([a-zA-Z0-9_]+):(\d+)\}", RegexOptions.Compiled);
    private static readonly Regex DamagePerStackToken = new Regex(@"\{damage_per_stack:(\d+)\}", RegexOptions.Compiled);
    private static readonly Regex ModifierDamageToken = new Regex(@"\{modifier_damage:(\d+)\}", RegexOptions.Compiled);
    private static readonly Regex RetaliationStatusToken = new Regex(@"\{retaliation_status:([a-zA-Z0-9_]+):(\d+)\}", RegexOptions.Compiled);
    private static readonly Regex TerrainToken = new Regex(@"\{terrain:([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

    public static string FormatDescription(CardInstance card, CombatState state)
    {
        if (card?.Definition == null) return string.Empty;

        CardDefinition definition = card.Definition;
        string template = !string.IsNullOrWhiteSpace(definition.descriptionTemplate)
            ? definition.descriptionTemplate
            : definition.description;

        if (string.IsNullOrWhiteSpace(template)) return string.Empty;

        if (template.Contains("{cost}"))
        {
            template = template.Replace("{cost}", Colorize(card.GetEffectiveCost(state), "cost"));
        }
        template = DamageToken.Replace(template, match => Colorize(GetDealDamageValue(definition, state, int.Parse(match.Groups[1].Value)), "damage"));
        template = BlockToken.Replace(template, match => Colorize(GetBlockValue(definition, state, int.Parse(match.Groups[1].Value)), "block"));
        template = StatusToken.Replace(template, match => Colorize(GetStatusValue(definition, state, match.Groups[1].Value, int.Parse(match.Groups[2].Value)), "status"));
        template = ValueToken.Replace(template, match => Colorize(GetEffectValue(definition, match.Groups[1].Value, int.Parse(match.Groups[2].Value)), "value"));
        template = DamagePerStackToken.Replace(template, match => Colorize(GetDamagePerStackValue(definition, int.Parse(match.Groups[1].Value)), "damage"));
        template = ModifierDamageToken.Replace(template, match => Colorize(GetModifierDamageValue(definition, int.Parse(match.Groups[1].Value)), "damage"));
        template = RetaliationStatusToken.Replace(template, match => Colorize(GetRetaliationStatusValue(definition, state, match.Groups[1].Value, int.Parse(match.Groups[2].Value)), "status"));
        template = TerrainToken.Replace(template, match => Colorize(GetTerrainValue(definition, match.Groups[1].Value), GetTerrainSemantic(match.Groups[1].Value)));
        return template;
    }

    public static string FormatDescription(CardDefinition definition)
    {
        return FormatDescription(definition != null ? new CardInstance(definition) : null, null);
    }

    private static int GetDealDamageValue(CardDefinition definition, CombatState state, int effectIndex)
    {
        CardEffect effect = GetEffect(definition, "deal_damage", effectIndex);
        if (effect == null) return 0;

        int damage = effect.value;
        if (state == null || definition.type != "attack") return damage;

        if (state.ActiveTerrain?.id == "champ_de_braises")
        {
            damage += state.ActiveTerrain.terrainEffect.attackDamageBonus;
        }

        if (effectIndex == 0 && state.NextAttackDamageBonus > 0)
        {
            damage += state.NextAttackDamageBonus;
        }

        return damage;
    }

    private static int GetBlockValue(CardDefinition definition, CombatState state, int effectIndex)
    {
        CardEffect effect = GetEffect(definition, "gain_block", effectIndex);
        if (effect == null) return 0;

        int block = effect.value;
        if (state != null && definition.type == "defense" && state.ActiveTerrain?.id == "forteresse_de_cendres")
        {
            block += state.ActiveTerrain.terrainEffect.defenseBlockBonus;
        }

        return block;
    }

    private static int GetStatusValue(CardDefinition definition, CombatState state, string status, int effectIndex)
    {
        int seen = 0;
        if (definition.effects == null) return 0;

        foreach (CardEffect effect in definition.effects)
        {
            if (effect.action != "apply_status" || effect.status != status) continue;
            if (seen++ != effectIndex) continue;

            return GetAdjustedStatusValue(status, effect.value, state);
        }

        return 0;
    }

    private static int GetEffectValue(CardDefinition definition, string action, int effectIndex)
    {
        CardEffect effect = GetEffect(definition, action, effectIndex);
        return effect != null ? effect.value : 0;
    }

    private static int GetDamagePerStackValue(CardDefinition definition, int effectIndex)
    {
        CardEffect effect = GetEffect(definition, "consume_status_for_damage", effectIndex);
        return effect != null ? effect.damagePerStack : 0;
    }

    private static int GetModifierDamageValue(CardDefinition definition, int effectIndex)
    {
        int seen = 0;
        if (definition.effects == null) return 0;

        foreach (CardEffect effect in definition.effects)
        {
            if (effect.action != "apply_temporary_modifier" || effect.modifier == null) continue;
            if (seen++ == effectIndex) return effect.modifier.damageBonus;
        }

        return 0;
    }

    private static int GetRetaliationStatusValue(CardDefinition definition, CombatState state, string status, int effectIndex)
    {
        int seen = 0;
        if (definition.effects == null) return 0;

        foreach (CardEffect effect in definition.effects)
        {
            if (effect.action != "apply_delayed_retaliation") continue;
            if (effect.payload == null || effect.payload.action != "apply_status" || effect.payload.status != status) continue;
            if (seen++ == effectIndex) return GetAdjustedStatusValue(status, effect.payload.value, state);
        }

        return 0;
    }

    private static int GetTerrainValue(CardDefinition definition, string field)
    {
        TerrainEffect effect = definition.terrainEffect;
        if (effect == null) return 0;

        return field switch
        {
            "attackDamageBonus" => effect.attackDamageBonus,
            "burnAppliedBonus" => effect.burnAppliedBonus,
            "burnAppliedMultiplier" => Mathf.RoundToInt(effect.burnAppliedMultiplier),
            "defenseBlockBonus" => effect.defenseBlockBonus,
            "firstDefenseEachTurnAppliesBurn" => effect.firstDefenseEachTurnAppliesBurn,
            "ritualCostReduction" => effect.ritualCostReduction,
            "firstRitualEachTurnGainMana" => effect.firstRitualEachTurnGainMana,
            "endTurnIfSpentManaAtLeast6DealDamage" => effect.endTurnIfSpentManaAtLeast6DealDamage,
            "endTurnIfSpentManaAtLeast8GainBlock" => effect.endTurnIfSpentManaAtLeast8GainBlock,
            _ => 0
        };
    }

    private static int GetAdjustedStatusValue(string status, int amount, CombatState state)
    {
        if (state == null || status != "burn") return amount;

        TerrainEffect terrainEffect = state.ActiveTerrain?.terrainEffect;
        if (terrainEffect == null) return amount;

        if (terrainEffect.burnAppliedMultiplier > 0f)
        {
            amount = Mathf.RoundToInt(amount * terrainEffect.burnAppliedMultiplier);
        }

        amount += terrainEffect.burnAppliedBonus;
        return amount;
    }

    private static CardEffect GetEffect(CardDefinition definition, string action, int effectIndex)
    {
        int seen = 0;
        if (definition.effects == null) return null;

        foreach (CardEffect effect in definition.effects)
        {
            if (effect.action != action) continue;
            if (seen++ == effectIndex) return effect;
        }

        return null;
    }

    private static string Colorize(int value, string semantic)
    {
        string color = semantic switch
        {
            "damage" => "#ff6a3d",
            "block" => "#58d7ff",
            "status" => "#ff9b2f",
            "cost" => "#ffd75e",
            _ => "#f2e5c4"
        };

        return $"<color={color}>{value}</color>";
    }

    private static string GetTerrainSemantic(string field)
    {
        if (field.Contains("Damage")) return "damage";
        if (field.Contains("Block")) return "block";
        if (field.Contains("Burn")) return "status";
        if (field.Contains("Mana") || field.Contains("Cost")) return "cost";
        return "value";
    }
}
