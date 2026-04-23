using UnityEngine;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// État global de la run en cours (campagne, deck équipé, progression). Persiste entre toutes les scènes.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public const int NombreBossCampagne = 10;

        [Header("État courant")]
        [SerializeField] private int currentBossIndex = 0;
        [SerializeField] private string selectedDeckId = "default";
        [SerializeField] private bool isInRun = false;

        public int CurrentBossIndex => currentBossIndex;
        public string SelectedDeckId => selectedDeckId;
        public bool IsInRun => isInRun;

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

        /// <summary>Démarre une nouvelle run avec le deck choisi et remet la progression à zéro.</summary>
        public void StartNewRun(string deckId)
        {
            selectedDeckId = deckId;
            currentBossIndex = 0;
            isInRun = true;
            Debug.Log($"[GameManager] Nouvelle run démarrée (deck='{deckId}').");
        }

        /// <summary>Termine la run (victoire finale, défaite ou abandon).</summary>
        public void EndRun()
        {
            Debug.Log($"[GameManager] Run terminée au boss #{currentBossIndex}.");
            isInRun = false;
        }

        /// <summary>Avance d'un boss dans la progression de la campagne. Termine la run si on atteint le dernier.</summary>
        public void AdvanceToNextBoss()
        {
            if (!isInRun)
            {
                Debug.LogWarning("[GameManager] AdvanceToNextBoss appelé alors qu'aucune run n'est en cours.");
                return;
            }

            currentBossIndex++;
            Debug.Log($"[GameManager] Progression : boss #{currentBossIndex}.");

            if (currentBossIndex >= NombreBossCampagne)
            {
                Debug.Log("[GameManager] Campagne terminée !");
                EndRun();
            }
        }
    }
}
