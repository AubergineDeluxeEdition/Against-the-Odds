using System;
using System.Collections.Generic;
using UnityEngine;

public class EffectResolver
{
    private readonly System.Random random = new System.Random(Guid.NewGuid().GetHashCode());

    public event Action<string> OnLogMessage;
    public event Action<int> OnDiscardRequested;
    public event Action<PlayerDamageInfo> OnPlayerDamaged;

    public void ResolveCard(CardInstance card, CombatState state)
    {
        CardDefinition def = card.Definition;

        if (def.effects == null) return;

        foreach (CardEffect effect in def.effects)
        {
            ResolveEffect(effect, def, state);
        }

        ResolveTerrainCardTriggers(def, state);
    }

    public void ResolveEnemyTurnStart(CombatState state)
    {
        ResolveStatusTurnDamage("burn", state);
    }

    public int DealDamageToEnemy(int damage, CombatState state, string source)
    {
        if (damage <= 0) return 0;

        if (state.EnemyInvulnerableTurns > 0)
        {
            Log($"{source} : ennemi invulnerable");
            return 0;
        }

        int finalDamage = state.EnemyVulnerableTurns > 0
            ? Mathf.CeilToInt(damage * 1.5f)
            : damage;

        int shielded = Mathf.Min(state.EnemyShield, finalDamage);
        state.EnemyShield -= shielded;
        finalDamage -= shielded;

        int blocked = Mathf.Min(state.EnemyBlock, finalDamage);
        state.EnemyBlock -= blocked;
        finalDamage -= blocked;

        state.EnemyHP = Mathf.Max(0, state.EnemyHP - finalDamage);

        string shieldMessage = shielded > 0 ? $" ({shielded} shield)" : string.Empty;
        string blockMessage = blocked > 0 ? $" ({blocked} bloques)" : string.Empty;
        Log($"{source} inflige {damage} degats{shieldMessage}{blockMessage} -> ennemi {state.EnemyHP}/{state.EnemyMaxHP}");
        return finalDamage;
    }

    public void ResolveEnemyAttack(int damage, CombatState state)
    {
        int absorbed = Mathf.Min(state.PlayerBlock, damage);
        state.PlayerBlock -= absorbed;

        int remainder = damage - absorbed;
        state.PlayerHP = Mathf.Max(0, state.PlayerHP - remainder);
        OnPlayerDamaged?.Invoke(new PlayerDamageInfo(damage, absorbed, remainder));
        Log($"Ennemi inflige {damage} degats ({absorbed} bouclier, {remainder} recus)");

        if (state.PendingRetaliation?.Trigger == "on_attacked_before_next_turn")
        {
            ResolveDelayedPayload(state.PendingRetaliation.Payload, state);
            state.PendingRetaliation = null;
        }
    }

    public void DrawCards(int count, CombatState state)
    {
        for (int i = 0; i < count; i++)
        {
            if (state.DrawPile.Count == 0)
            {
                if (state.DiscardPile.Count == 0) break;
                ReshuffleDiscard(state);
            }

            CardInstance card = state.DrawPile[0];
            state.DrawPile.RemoveAt(0);
            state.Hand.Add(card);
            Log($"Pioche : {card.Definition.name}");
        }
    }

    private void ResolveEffect(CardEffect effect, CardDefinition card, CombatState state)
    {
        if (effect.condition != null && effect.condition.IsActive() && !CheckCondition(effect.condition, state))
        {
            return;
        }

        switch (effect.action)
        {
            case "deal_damage":
                ResolveDealDamage(effect.value, card, state);
                break;

            case "apply_status":
                ResolveApplyStatus(effect.status, effect.value, state);
                break;

            case "multiply_status":
                ResolveMultiplyStatus(effect.status, effect.multiplier, state);
                break;

            case "consume_status_for_damage":
                ResolveConsumeStatusForDamage(effect.status, effect.damagePerStack, state);
                break;

            case "gain_block":
                ResolveGainBlock(effect.value, card, state);
                break;

            case "gain_max_mana":
                state.PermanentManaBonus += effect.value;
                state.MaxMana += effect.value;
                state.CurrentMana += effect.value;
                Log($"Mana max augmente a {state.MaxMana}");
                break;

            case "gain_temporary_mana":
                state.CurrentMana += effect.value;
                Log($"Gagne {effect.value} mana temporaire");
                break;

            case "lose_health":
                state.PlayerHP = Mathf.Max(0, state.PlayerHP - effect.value);
                Log($"Joueur perd {effect.value} PV -> {state.PlayerHP}/{state.PlayerMaxHP}");
                break;

            case "draw_cards":
                DrawCards(effect.value, state);
                break;

            case "discard_cards":
                RequestDiscard(effect.value, state);
                break;

            case "apply_temporary_modifier":
                if (effect.modifier?.appliesTo == "next_attack_this_turn")
                {
                    state.NextAttackDamageBonus += effect.modifier.damageBonus;
                    Log($"Prochain sort d'attaque : +{effect.modifier.damageBonus} degats");
                }
                break;

            case "apply_delayed_retaliation":
                state.PendingRetaliation = new DelayedTrigger
                {
                    Trigger = effect.trigger,
                    Payload = effect.payload
                };
                Log("Riposte retardee activee");
                break;

            case "set_terrain":
                state.ActiveTerrain = card;
                Log($"Terrain actif : {card.name}");
                break;

            case "exhaust":
                break;
        }
    }

    private void ResolveTerrainCardTriggers(CardDefinition def, CombatState state)
    {
        if (def.type == "defense" && !state.FirstDefensePlayedThisTurn)
        {
            state.FirstDefensePlayedThisTurn = true;
            if (state.ActiveTerrain?.id == "forteresse_de_cendres")
            {
                int burn = state.ActiveTerrain.terrainEffect.firstDefenseEachTurnAppliesBurn;
                AddStatus("burn", burn, state);
                Log($"Forteresse de Cendres : applique {burn} Brulure a l'ennemi");
            }
        }

        if (def.subtype == "ritual" && !state.FirstRitualPlayedThisTurn)
        {
            state.FirstRitualPlayedThisTurn = true;
            if (state.ActiveTerrain?.id == "autel_profane")
            {
                int mana = state.ActiveTerrain.terrainEffect.firstRitualEachTurnGainMana;
                state.CurrentMana += mana;
                Log($"Autel Profane : gagne {mana} mana");
            }
        }
    }

    private void ResolveDealDamage(int baseDamage, CardDefinition card, CombatState state)
    {
        int damage = baseDamage;

        if (card.type == "attack")
        {
            if (state.ActiveTerrain?.id == "champ_de_braises")
            {
                damage += state.ActiveTerrain.terrainEffect.attackDamageBonus;
            }

            if (state.NextAttackDamageBonus > 0)
            {
                damage += state.NextAttackDamageBonus;
                state.NextAttackDamageBonus = 0;
            }
        }

        DealDamageToEnemy(damage, state, "Carte");
    }

    private void ResolveApplyStatus(string status, int amount, CombatState state)
    {
        AddStatus(status, amount, state);
    }

    private void ResolveMultiplyStatus(string status, float multiplier, CombatState state)
    {
        if (!state.EnemyStatuses.TryGetValue(status, out int stacks) || stacks <= 0) return;

        int newValue = Mathf.RoundToInt(stacks * multiplier);
        state.EnemyStatuses[status] = newValue;
        Log($"{GetStatusDisplayName(status)} : {stacks} -> {newValue} stacks");
    }

    private void ResolveConsumeStatusForDamage(string status, int damagePerStack, CombatState state)
    {
        if (!state.EnemyStatuses.TryGetValue(status, out int stacks) || stacks <= 0) return;

        int damage = stacks * damagePerStack;
        state.EnemyStatuses[status] = 0;
        DealDamageToEnemy(damage, state, $"Consomme {stacks} stacks de {GetStatusDisplayName(status)}");
    }

    private void ResolveGainBlock(int amount, CardDefinition card, CombatState state)
    {
        int block = amount;

        if (card.type == "defense" && state.ActiveTerrain?.id == "forteresse_de_cendres")
        {
            block += state.ActiveTerrain.terrainEffect.defenseBlockBonus;
        }

        state.PlayerBlock += block;
        Log($"Gagne {block} Bouclier (total : {state.PlayerBlock})");
    }

    private void ResolveStatusTurnDamage(string status, CombatState state)
    {
        if (!state.EnemyStatuses.TryGetValue(status, out int stacks) || stacks <= 0) return;

        int damage = stacks;
        DealDamageToEnemy(damage, state, GetStatusDisplayName(status));
    }

    private void RequestDiscard(int requestedCount, CombatState state)
    {
        int count = Mathf.Min(requestedCount, state.Hand.Count);
        if (count <= 0)
        {
            Log("Aucune carte a defausser");
            return;
        }

        state.WaitingForDiscard = true;
        state.CardsToDiscard += count;
        OnDiscardRequested?.Invoke(count);
        Log($"Choisissez {count} carte(s) a defausser");
    }

    private void ResolveDelayedPayload(DelayedPayload payload, CombatState state)
    {
        if (payload == null) return;

        switch (payload.action)
        {
            case "apply_status":
                AddStatus(payload.status, payload.value, state);
                Log($"Riposte : applique {payload.value} {GetStatusDisplayName(payload.status)}");
                break;
        }
    }

    private void ReshuffleDiscard(CombatState state)
    {
        state.DrawPile.AddRange(state.DiscardPile);
        state.DiscardPile.Clear();
        Shuffle(state.DrawPile);
        Log("Defausse melangee dans la pioche");
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void AddStatus(string status, int amount, CombatState state)
    {
        if (amount <= 0) return;
        amount = ApplyStatusModifiers(status, amount, state);
        if (amount <= 0) return;

        if (!state.EnemyStatuses.ContainsKey(status))
        {
            state.EnemyStatuses[status] = 0;
        }

        state.EnemyStatuses[status] += amount;
        Log($"Ennemi : {GetStatusDisplayName(status)} {state.EnemyStatuses[status]} stack(s)");
    }

    private static int ApplyStatusModifiers(string status, int amount, CombatState state)
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

    private bool CheckCondition(EffectCondition condition, CombatState state)
    {
        if (!string.IsNullOrEmpty(condition.hasStatus))
        {
            Dictionary<string, int> dictionary = condition.target == "enemy" ? state.EnemyStatuses : null;
            return dictionary != null
                && dictionary.TryGetValue(condition.hasStatus, out int value)
                && value > 0;
        }

        if (condition.hasActiveTerrain)
        {
            return state.ActiveTerrain != null;
        }

        return true;
    }

    private static string GetStatusDisplayName(string status)
    {
        return status == "burn" ? "Brulure" : status;
    }

    private void Log(string message)
    {
        OnLogMessage?.Invoke(message);
    }
}

public readonly struct PlayerDamageInfo
{
    public PlayerDamageInfo(int incomingDamage, int absorbedByShield, int damageReceived)
    {
        IncomingDamage = incomingDamage;
        AbsorbedByShield = absorbedByShield;
        DamageReceived = damageReceived;
    }

    public int IncomingDamage { get; }
    public int AbsorbedByShield { get; }
    public int DamageReceived { get; }
    public bool WasAbsorbedByShield => AbsorbedByShield > 0;
}
