using UnityEngine;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// Point d'entrée du jeu. Attaché au GameObject racine "__Managers" dans la scène 00_Bootstrap.
    /// Vérifie que tous les managers sont présents et lance la première scène via SceneLoader.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [Header("Scène de démarrage")]
        [SerializeField] private string premiereSceneACharger = "01_MainMenu";
        [SerializeField] private float dureeFonduDemarrage = 0.5f;

        [Header("Deck de depart")]
        [SerializeField] private string cardDatabaseResourcePath = "Data/Deck";
        [SerializeField] private DeckEntry[] initialDeck;

        [Header("Run de depart")]
        [SerializeField, Min(0)] private int initialPotionCount = 0;

        // Protège contre une double-initialisation si la scène Bootstrap est rechargée.
        private static bool bootstrapDejaInitialise = false;

        private void Start()
        {
            if (bootstrapDejaInitialise)
            {
                Debug.LogWarning("[Bootstrap] Déjà initialisé, destruction du doublon.");
                Destroy(gameObject);
                return;
            }

            bootstrapDejaInitialise = true;
            DontDestroyOnLoad(gameObject);

            if (!TousLesManagersSontPresents())
            {
                Debug.LogError("[Bootstrap] Un ou plusieurs managers sont manquants. Initialisation avortée.");
                return;
            }

            GameManager.Instance.ConfigureRunDefaults(cardDatabaseResourcePath, initialDeck, initialPotionCount);

            SceneLoader.Instance.LoadScene(premiereSceneACharger, dureeFonduDemarrage);
        }

        private bool TousLesManagersSontPresents()
        {
            bool tousPresents = true;

            if (GameManager.Instance == null) { Debug.LogError("[Bootstrap] GameManager manquant."); tousPresents = false; }
            if (AudioManager.Instance == null) { Debug.LogError("[Bootstrap] AudioManager manquant."); tousPresents = false; }
            if (SceneLoader.Instance == null) { Debug.LogError("[Bootstrap] SceneLoader manquant."); tousPresents = false; }
            if (SaveManager.Instance == null) { Debug.LogError("[Bootstrap] SaveManager manquant."); tousPresents = false; }
            if (SettingsManager.Instance == null) { Debug.LogError("[Bootstrap] SettingsManager manquant."); tousPresents = false; }

            return tousPresents;
        }
    }
}
