using System;
using UnityEngine;

public class TurnManager
{
    public enum TurnPhase { PlayerTurn, EnemyTurn }

    public TurnPhase CurrentPhase { get; private set; }
    public int TurnNumber { get; private set; }

    private const int InitialHandSize = 4;
    private const int DrawPerTurn = 1;
    public const int ManaCap = 8;

    private readonly EffectResolver resolver;
    private bool redrawFullHandEachTurn;

    public event Action<TurnPhase> OnPhaseChanged;
    public event Action<int, int> OnPlayerTurnStarted;

    public TurnManager(EffectResolver resolver)
    {
        this.resolver = resolver;
    }

    public void SetRedrawFullHandEachTurn(bool enabled)
    {
        redrawFullHandEachTurn = enabled;
    }

    public void StartPlayerTurn(CombatState state)
    {
        TurnNumber++;
        CurrentPhase = TurnPhase.PlayerTurn;

        state.BaseMana = Mathf.Min(TurnNumber, ManaCap);
        state.MaxMana = Mathf.Min(ManaCap, state.BaseMana + state.PermanentManaBonus);
        state.CurrentMana = state.MaxMana;
        state.ManaSpentThisTurn = 0;

        state.NextAttackDamageBonus = 0;
        state.FirstDefensePlayedThisTurn = false;
        state.FirstRitualPlayedThisTurn = false;

        int cardsToDraw = TurnNumber == 1 || redrawFullHandEachTurn ? InitialHandSize : DrawPerTurn;
        resolver.DrawCards(cardsToDraw, state);

        OnPlayerTurnStarted?.Invoke(TurnNumber, state.CurrentMana);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    public void EndPlayerTurn(CombatState state)
    {
        if (state.ActiveTerrain?.id == "trone_des_restes")
        {
            TerrainEffect terrainEffect = state.ActiveTerrain.terrainEffect;
            if (state.ManaSpentThisTurn >= 6)
            {
                resolver.DealDamageToEnemy(terrainEffect.endTurnIfSpentManaAtLeast6DealDamage, state, "Trone des Restes");
            }

            if (state.ManaSpentThisTurn >= 8)
            {
                state.PlayerBlock += terrainEffect.endTurnIfSpentManaAtLeast8GainBlock;
            }
        }

        if (redrawFullHandEachTurn)
        {
            DiscardHand(state);
        }

        StartEnemyTurn(state);
    }

    private static void DiscardHand(CombatState state)
    {
        if (state.Hand.Count == 0) return;

        state.DiscardPile.AddRange(state.Hand);
        state.Hand.Clear();
        state.WaitingForDiscard = false;
        state.CardsToDiscard = 0;
    }

    public void StartEnemyTurn(CombatState state)
    {
        CurrentPhase = TurnPhase.EnemyTurn;
        state.EnemyBlock = 0;
        state.EnemyInvulnerableTurns = Mathf.Max(0, state.EnemyInvulnerableTurns - 1);
        state.EnemyVulnerableTurns = Mathf.Max(0, state.EnemyVulnerableTurns - 1);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    public void EndEnemyTurn(CombatState state)
    {
        state.PendingRetaliation = null;
        StartPlayerTurn(state);
    }
}
