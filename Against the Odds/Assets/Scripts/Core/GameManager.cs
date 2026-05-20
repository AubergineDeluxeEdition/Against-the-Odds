using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// Global run state. Persists between scenes and carries the selected combat encounter.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public const int NombreBossCampagne = 10;

        [Header("Etat courant")]
        [SerializeField] private int currentBossIndex = 0;
        [SerializeField] private string selectedDeckId = "default";
        [SerializeField] private bool isInRun = false;
        [SerializeField] private int potionCount = 0;
        [SerializeField] private List<string> runRewardCardIds = new List<string>();
        [SerializeField, Min(0)] private int initialPotionCount = 0;

        [Header("Deck de depart")]
        [SerializeField] private string cardDatabaseResourcePath = "Data/Deck";
        [SerializeField] private DeckEntry[] initialDeck =
        {
            new DeckEntry { cardId = "entaille_de_braise", quantity = 2 },
            new DeckEntry { cardId = "cendres_voraces", quantity = 2 },
            new DeckEntry { cardId = "mur_de_charbon", quantity = 2 },
            new DeckEntry { cardId = "rempart_des_cendres", quantity = 1 },
            new DeckEntry { cardId = "offrande_impie", quantity = 1 }
        };

        public int CurrentBossIndex => currentBossIndex;
        public string SelectedDeckId => selectedDeckId;
        public bool IsInRun => isInRun;
        public int PotionCount => potionCount;
        public string CardDatabaseResourcePath => cardDatabaseResourcePath;
        public IReadOnlyList<DeckEntry> InitialDeck => initialDeck;
        public IReadOnlyList<string> RunRewardCardIds => runRewardCardIds;
        public global::BossEncounterConfig SelectedEncounter { get; private set; }
        public string SelectedCombatSceneName { get; private set; }
        public string PendingCinematicNextSceneName { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }

        public void StartNewRun(string deckId)
        {
            selectedDeckId = deckId;
            currentBossIndex = 0;
            isInRun = true;
            potionCount = initialPotionCount;
            runRewardCardIds.Clear();
            SelectedEncounter = null;
            SelectedCombatSceneName = string.Empty;
            PendingCinematicNextSceneName = string.Empty;
            Debug.Log($"[GameManager] New run started (deck='{deckId}').");
            SaveManager.Instance?.SaveRun(this);
        }

        public void LoadRun(SaveData saveData)
        {
            if (saveData == null)
            {
                Debug.LogWarning("[GameManager] LoadRun called with null save data.");
                return;
            }

            selectedDeckId = string.IsNullOrWhiteSpace(saveData.selectedDeckId) ? "default" : saveData.selectedDeckId;
            currentBossIndex = Mathf.Max(0, saveData.currentBossIndex);
            isInRun = saveData.isInRun || (SaveManager.Instance != null && SaveManager.Instance.HasSave());
            potionCount = Mathf.Max(0, saveData.potionCount);
            runRewardCardIds = saveData.runRewardCardIds != null
                ? new List<string>(saveData.runRewardCardIds.Where(cardId => !string.IsNullOrWhiteSpace(cardId)))
                : new List<string>();
            SelectedEncounter = null;
            SelectedCombatSceneName = string.Empty;
            PendingCinematicNextSceneName = string.Empty;

            SaveManager.Instance?.SetCurrentData(saveData);
            Debug.Log($"[GameManager] Run loaded at boss #{currentBossIndex} (deck='{selectedDeckId}').");
        }

        public void ConfigureRunDefaults(string databaseResourcePath, DeckEntry[] starterDeck, int starterPotionCount)
        {
            if (!string.IsNullOrWhiteSpace(databaseResourcePath))
            {
                cardDatabaseResourcePath = databaseResourcePath;
            }

            if (starterDeck != null && starterDeck.Length > 0)
            {
                initialDeck = starterDeck;
            }

            initialPotionCount = Mathf.Max(0, starterPotionCount);
        }

        public List<string> BuildRunDeckCardIds()
        {
            var cardIds = new List<string>();

            if (initialDeck != null)
            {
                foreach (DeckEntry entry in initialDeck)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.cardId)) continue;

                    int quantity = Mathf.Max(0, entry.quantity);
                    for (int i = 0; i < quantity; i++)
                    {
                        cardIds.Add(entry.cardId);
                    }
                }
            }

            cardIds.AddRange(runRewardCardIds.Where(cardId => !string.IsNullOrWhiteSpace(cardId)));
            return cardIds;
        }

        public bool ConsumePotion()
        {
            if (potionCount <= 0)
            {
                return false;
            }

            potionCount--;
            Debug.Log($"[GameManager] Potion used. Remaining: {potionCount}.");
            SaveManager.Instance?.SaveRun(this);
            return true;
        }

        public void AddPotions(int amount)
        {
            if (amount <= 0) return;

            potionCount += amount;
            Debug.Log($"[GameManager] Potions gained: +{amount}. Total: {potionCount}.");
            SaveManager.Instance?.SaveRun(this);
        }

        public void AddRewardCards(IEnumerable<string> cardIds)
        {
            if (cardIds == null) return;

            foreach (string cardId in cardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId)) continue;

                runRewardCardIds.Add(cardId);
                Debug.Log($"[GameManager] Reward card added: {cardId}.");
            }

            SaveManager.Instance?.SaveRun(this);
        }

        public void SelectEncounter(global::BossEncounterConfig encounter, string combatSceneName = null)
        {
            SelectedEncounter = encounter;
            SelectedCombatSceneName = string.IsNullOrWhiteSpace(combatSceneName)
                ? string.Empty
                : combatSceneName;
            if (encounter == null) return;

            isInRun = true;
            Debug.Log($"[GameManager] Encounter selected: {encounter.bossDisplayName} (run boss #{currentBossIndex}, encounter boss #{encounter.bossIndex}).");
        }

        public void SetPendingCinematicNextScene(string sceneName)
        {
            PendingCinematicNextSceneName = string.IsNullOrWhiteSpace(sceneName)
                ? string.Empty
                : sceneName;
        }

        public string ConsumePendingCinematicNextScene(string fallbackSceneName)
        {
            string sceneName = !string.IsNullOrWhiteSpace(PendingCinematicNextSceneName)
                ? PendingCinematicNextSceneName
                : fallbackSceneName;

            PendingCinematicNextSceneName = string.Empty;
            return sceneName;
        }

        public void EndRun()
        {
            Debug.Log($"[GameManager] Run ended at boss #{currentBossIndex}.");
            isInRun = false;
            SelectedEncounter = null;
            SelectedCombatSceneName = string.Empty;
            PendingCinematicNextSceneName = string.Empty;
            SaveManager.Instance?.DeleteSave();
        }

        public bool AdvanceToNextBoss()
        {
            if (!isInRun)
            {
                Debug.LogWarning("[GameManager] AdvanceToNextBoss called without an active run.");
                return false;
            }

            currentBossIndex++;
            Debug.Log($"[GameManager] Progression: boss #{currentBossIndex}.");

            if (currentBossIndex >= NombreBossCampagne)
            {
                Debug.Log("[GameManager] Campaign complete.");
                EndRun();
                return true;
            }

            SaveManager.Instance?.SaveRun(this);
            return false;
        }
    }
}
