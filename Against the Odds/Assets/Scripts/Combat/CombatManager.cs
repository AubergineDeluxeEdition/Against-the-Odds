using System;
using System.Collections.Generic;
using AgainstTheOdds.Core;
using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public CombatState State { get; private set; }
    public TurnManager.TurnPhase CurrentPhase => turnManager.CurrentPhase;
    public int TurnNumber => turnManager.TurnNumber;
    public string BossDisplayName => encounter != null ? encounter.bossDisplayName : string.Empty;
    public bool HasEncounter => encounter != null;
    public BossEncounterConfig Encounter => encounter;
    public int PotionRewardCount => encounter != null ? encounter.potionRewardCount : 0;
    public int RewardPickCount => encounter != null ? Mathf.Max(0, encounter.rewardPickCount) : 0;
    public string DefeatResultText => encounter != null ? encounter.defeatResultText : string.Empty;
    public string VictoryResultText => encounter != null ? encounter.victoryResultText : string.Empty;
    public AudioClip VictorySfx => encounter != null ? encounter.victorySfx : null;
    public AudioClip DefeatSfx => encounter != null ? encounter.defeatSfx : null;
    public AudioClip DefeatButtonSfx => encounter != null ? encounter.defeatButtonSfx : null;
    public bool CombatEnded => combatEnded;
    public string LastBossDialogue { get; private set; }

    private TurnManager turnManager;
    private EffectResolver resolver;
    private BossPatternRunner bossPattern;
    private BossEncounterConfig encounter;
    private DeckDefinition currentDeck;
    private bool combatEnded;

    [Header("Audio")]
    [SerializeField] private AudioSource bossAudioSource;
    [SerializeField] private AudioClip playerDamageSfx;
    [SerializeField] private AudioClip playerHeavyDamageSfx;
    [SerializeField] private int heavyDamageThreshold = 20;
    [SerializeField] private AudioClip playerShieldAbsorbSfx;

    [Header("Experimental Draw Rules")]
    [SerializeField] private bool redrawFullHandEachTurn;

    public event Action OnStateChanged;
    public event Action<bool> OnCombatEnded;
    public event Action<int> OnDiscardRequested;
    public event Action<int, int> OnPlayerTurnStarted;
    public event Action<int> OnPlayerTurnEnded;
    public event Action<string, string> OnLogMessage;
    public event Action<CardDefinition> OnCardPlayed;
    public event Action<string> OnBossDialogue;
    public event Action<string> OnBossAction;
    public event Action<Sprite> OnBossSpriteChanged;
    public event Action<Sprite, float> OnBossVisualChanged;
    public event Action OnPotionsChanged;

    private void Awake()
    {
        resolver = new EffectResolver();
        resolver.OnLogMessage += message =>
        {
            Debug.Log($"[Combat] {message}");
            OnLogMessage?.Invoke(message, "effect");
        };
        resolver.OnDiscardRequested += count => OnDiscardRequested?.Invoke(count);
        resolver.OnPlayerDamaged += PlayPlayerDamageAudio;

        bossPattern = new BossPatternRunner(resolver);
        bossPattern.OnBossDialogue += line =>
        {
            LastBossDialogue = line;
            Debug.Log($"[BossDialogue] {line}");
            OnBossDialogue?.Invoke(line);
        };
        bossPattern.OnBossAction += action => OnBossAction?.Invoke(action);
        bossPattern.OnBossAudio += PlayBossAudio;
        bossPattern.OnBossSpriteChanged += sprite => OnBossSpriteChanged?.Invoke(sprite);
        bossPattern.OnBossVisualChanged += (sprite, scale) => OnBossVisualChanged?.Invoke(sprite, scale);

        turnManager = new TurnManager(resolver);
        turnManager.SetRedrawFullHandEachTurn(redrawFullHandEachTurn);
        turnManager.OnPhaseChanged += _ => OnStateChanged?.Invoke();
        turnManager.OnPlayerTurnStarted += (turn, mana) =>
        {
            OnPlayerTurnStarted?.Invoke(turn, mana);
            OnLogMessage?.Invoke($"Tour {turn}", "system");
        };
    }

    public void Configure(BossEncounterConfig encounter)
    {
        this.encounter = encounter;
    }

    public void StartCombat()
    {
        if (encounter == null)
        {
            Debug.LogError("[CombatManager] Cannot start combat without a BossEncounterConfig selected by a campaign pin.");
            return;
        }

        string cardDatabasePath = GetCardDatabaseResourcePath();
        currentDeck = DeckLoader.Load(cardDatabasePath);
        if (currentDeck == null)
        {
            Debug.LogError($"[CombatManager] Cannot load card database at Resources/{cardDatabasePath}.json.");
            return;
        }

        State = BuildInitialState(currentDeck);
        combatEnded = false;
        bossPattern.Initialize(encounter, State);
        turnManager.StartPlayerTurn(State);
        OnStateChanged?.Invoke();
    }

    public bool TryPlayCard(CardInstance card)
    {
        if (State == null) return false;
        if (combatEnded) return false;
        if (turnManager.CurrentPhase != TurnManager.TurnPhase.PlayerTurn) return false;
        if (State.WaitingForDiscard) return false;
        if (!State.Hand.Contains(card)) return false;

        int cost = card.GetEffectiveCost(State);
        if (State.CurrentMana < cost) return false;

        State.CurrentMana -= cost;
        State.ManaSpentThisTurn += cost;
        State.Hand.Remove(card);

        PlayCardAudio(card.Definition);
        OnLogMessage?.Invoke($"Vous jouez {card.Definition.name} ({cost} mana)", "player");
        OnCardPlayed?.Invoke(card.Definition);
        resolver.ResolveCard(card, State);
        ClearImpossibleDiscardRequest();

        if (card.HasEffect("exhaust"))
        {
            State.ExhaustedPile.Add(card);
        }
        else
        {
            State.DiscardPile.Add(card);
        }

        OnStateChanged?.Invoke();
        TryAdvanceBossPhase();
        bossPattern.CheckHpDialogues(State);
        CheckEndConditions();
        return true;
    }

    public List<CardDefinition> CreateRewardChoices()
    {
        var rewards = new List<CardDefinition>();
        if (encounter == null || currentDeck?.cards == null) return rewards;
        if (encounter.rewardCardIds == null || encounter.rewardCardIds.Length == 0) return rewards;

        var cardById = new Dictionary<string, CardDefinition>();
        foreach (CardDefinition card in currentDeck.cards)
        {
            if (card != null)
            {
                cardById[card.id] = card;
            }
        }

        for (int i = 0; i < encounter.rewardCardIds.Length; i++)
        {
            string cardId = encounter.rewardCardIds[i];
            if (string.IsNullOrWhiteSpace(cardId)) continue;

            if (cardById.TryGetValue(cardId, out CardDefinition rewardCard))
            {
                rewards.Add(rewardCard);
            }
            else
            {
                Debug.LogWarning($"[CombatManager] Unknown configured reward card: {cardId}");
            }
        }

        return rewards;
    }

    public bool TryUsePotion()
    {
        if (State == null) return false;
        if (combatEnded) return false;
        if (State.PlayerHP >= State.PlayerMaxHP) return false;
        if (GameManager.Instance == null) return false;
        if (!GameManager.Instance.ConsumePotion()) return false;

        State.PlayerHP = State.PlayerMaxHP;
        OnLogMessage?.Invoke("Potion utilisee : PV restaurés.", "player");
        OnPotionsChanged?.Invoke();
        OnStateChanged?.Invoke();
        return true;
    }

    public void ResolveDiscard(CardInstance card)
    {
        if (State == null) return;
        if (combatEnded) return;
        if (!State.WaitingForDiscard) return;
        if (!State.Hand.Contains(card)) return;

        State.Hand.Remove(card);
        State.DiscardPile.Add(card);
        State.CardsToDiscard--;
        OnLogMessage?.Invoke($"Defausse : {card.Definition.name}", "player");

        if (State.CardsToDiscard <= 0)
        {
            State.WaitingForDiscard = false;
            State.CardsToDiscard = 0;
        }

        OnStateChanged?.Invoke();
    }

    public void EndTurn()
    {
        if (State == null) return;
        if (combatEnded) return;
        if (turnManager.CurrentPhase != TurnManager.TurnPhase.PlayerTurn) return;
        ClearImpossibleDiscardRequest();
        if (State.WaitingForDiscard) return;

        OnPlayerTurnEnded?.Invoke(TurnNumber);
        turnManager.EndPlayerTurn(State);
        OnStateChanged?.Invoke();

        resolver.ResolveEnemyTurnStart(State);
        OnStateChanged?.Invoke();
        TryAdvanceBossPhase();
        bossPattern.CheckHpDialogues(State);
        CheckEndConditions();
        if (combatEnded) return;

        ExecuteEnemyTurn();
    }

    private void ExecuteEnemyTurn()
    {
        OnLogMessage?.Invoke("Tour du boss", "boss");
        bossPattern.ExecuteEnemyTurn(State);
        OnStateChanged?.Invoke();
        TryAdvanceBossPhase();
        bossPattern.CheckHpDialogues(State);
        CheckEndConditions();
        if (combatEnded) return;

        turnManager.EndEnemyTurn(State);
        OnStateChanged?.Invoke();
    }

    private CombatState BuildInitialState(DeckDefinition deckDefinition)
    {
        var state = new CombatState
        {
            PlayerHP = encounter.playerStartHP,
            PlayerMaxHP = encounter.playerStartHP,
            EnemyHP = encounter.bossStartHP,
            EnemyMaxHP = encounter.bossStartHP
        };

        var cardById = new Dictionary<string, CardDefinition>();
        foreach (CardDefinition card in deckDefinition.cards)
        {
            cardById[card.id] = card;
        }

        var drawPile = new List<CardInstance>();
        List<string> runCardIds = GameManager.Instance != null
            ? GameManager.Instance.BuildRunDeckCardIds()
            : BuildFallbackDeckCardIds(deckDefinition);

        foreach (string cardId in runCardIds)
        {
            if (!cardById.TryGetValue(cardId, out CardDefinition definition))
            {
                Debug.LogWarning($"[CombatManager] Unknown card in run deck: {cardId}");
                continue;
            }

            drawPile.Add(new CardInstance(definition));
        }

        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
        }

        state.DrawPile = drawPile;
        return state;
    }

    private string GetCardDatabaseResourcePath()
    {
        if (encounter != null && !string.IsNullOrWhiteSpace(encounter.cardDatabaseResourcePath))
        {
            return encounter.cardDatabaseResourcePath;
        }

        if (GameManager.Instance != null && !string.IsNullOrWhiteSpace(GameManager.Instance.CardDatabaseResourcePath))
        {
            return GameManager.Instance.CardDatabaseResourcePath;
        }

        return "Data/Deck";
    }

    private static List<string> BuildFallbackDeckCardIds(DeckDefinition deckDefinition)
    {
        var cardIds = new List<string>();
        if (deckDefinition?.deck == null) return cardIds;

        foreach (DeckEntry entry in deckDefinition.deck)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.cardId)) continue;

            int quantity = Mathf.Max(0, entry.quantity);
            for (int i = 0; i < quantity; i++)
            {
                cardIds.Add(entry.cardId);
            }
        }

        return cardIds;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool TryAdvanceBossPhase()
    {
        if (State == null || encounter == null) return false;

        bool advanced = bossPattern.TryAdvancePhase(State);
        if (advanced)
        {
            OnStateChanged?.Invoke();
        }

        return advanced;
    }

    private void CheckEndConditions()
    {
        if (State == null) return;
        if (combatEnded) return;

        if (State.EnemyHP <= 0)
        {
            if (TryAdvanceBossPhase()) return;

            bossPattern.SayDeathDialogue();
            combatEnded = true;
            if (GameManager.Instance != null && encounter != null)
            {
                GameManager.Instance.AddPotions(encounter.potionRewardCount);
                OnPotionsChanged?.Invoke();
            }

            PlaySfx(encounter != null ? encounter.bossDeathSfx : null);
            OnCombatEnded?.Invoke(true);
        }
        else if (State.PlayerHP <= 0)
        {
            combatEnded = true;
            OnCombatEnded?.Invoke(false);
        }
    }

    private void PlayPlayerDamageAudio(PlayerDamageInfo damageInfo)
    {
        if (damageInfo.WasAbsorbedByShield)
        {
            PlaySfx(playerShieldAbsorbSfx);
        }

        if (damageInfo.DamageReceived <= 0) return;

        AudioClip clip = damageInfo.DamageReceived >= heavyDamageThreshold && playerHeavyDamageSfx != null
            ? playerHeavyDamageSfx
            : playerDamageSfx;

        PlaySfx(clip);
    }

    private void ClearImpossibleDiscardRequest()
    {
        if (State == null || !State.WaitingForDiscard) return;
        if (State.Hand.Count > 0 && State.CardsToDiscard > 0) return;

        State.WaitingForDiscard = false;
        State.CardsToDiscard = 0;
    }

    private void PlayBossAudio(AudioClip clip)
    {
        if (clip == null) return;

        PlaySfx(clip);
    }

    private void PlayCardAudio(CardDefinition card)
    {
        AudioClip clip = encounter != null && encounter.cardAudioProfile != null
            ? encounter.cardAudioProfile.GetClip(card)
            : null;

        PlaySfx(clip);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
            return;
        }

        if (bossAudioSource != null)
        {
            bossAudioSource.PlayOneShot(clip);
        }
    }
}
