using AgainstTheOdds.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AgainstTheOdds.MainMenu
{
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour, IPointerEnterHandler
    {
        public enum ActionBouton
        {
            ChargerScene,
            Quitter,
            ContinuerSave,
        }

        [Header("Action du bouton")]
        [SerializeField] private ActionBouton action = ActionBouton.ChargerScene;
        [SerializeField] private string sceneACharger = "03_CampaignMap";
        [SerializeField] private string defaultDeckId = "default";

        [Header("Intro nouvelle partie")]
        [SerializeField] private string introSceneName;
#if UNITY_EDITOR
        [SerializeField] private SceneAsset introScene;
#endif

        [Header("Sauvegarde")]
        [SerializeField] private bool masquerSiAucuneSauvegarde = true;
        [SerializeField] private bool avertirAvantEcrasement = true;
        [SerializeField] private float delaiConfirmationEcrasement = 3f;
        [SerializeField] private string texteAvertissementEcrasement = "Ecraser la sauvegarde ?";
        [SerializeField] private TMP_Text label;

        [Header("SFX (optionnels)")]
        [SerializeField] private AudioClip sfxHover;
        [SerializeField] private AudioClip sfxClick;

        private Button bouton;
        private string texteInitial;
        private float limiteConfirmationEcrasement;

        private void Awake()
        {
            bouton = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (label != null) texteInitial = label.text;

            bouton.onClick.AddListener(OnClic);
            RefreshVisibility();
        }

        private void OnEnable()
        {
            RefreshVisibility();
        }

        private void Update()
        {
            if (label != null && limiteConfirmationEcrasement > 0f && Time.unscaledTime > limiteConfirmationEcrasement)
            {
                limiteConfirmationEcrasement = 0f;
                label.text = texteInitial;
            }
        }

        private void OnDestroy()
        {
            if (bouton != null) bouton.onClick.RemoveListener(OnClic);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (bouton != null && !bouton.interactable) return;
            if (sfxHover != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(sfxHover);
        }

        private void OnClic()
        {
            if (sfxClick != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(sfxClick);

            switch (action)
            {
                case ActionBouton.ChargerScene:
                    DemarrerNouvellePartie();
                    break;
                case ActionBouton.Quitter:
                    Quitter();
                    break;
                case ActionBouton.ContinuerSave:
                    ContinuerPartie();
                    break;
            }
        }

        private void DemarrerNouvellePartie()
        {
            bool saveExiste = SaveManager.Instance != null && SaveManager.Instance.HasSave();
            if (saveExiste && avertirAvantEcrasement && Time.unscaledTime > limiteConfirmationEcrasement)
            {
                limiteConfirmationEcrasement = Time.unscaledTime + delaiConfirmationEcrasement;
                if (label != null) label.text = texteAvertissementEcrasement;
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartNewRun(defaultDeckId);
            }

            if (!string.IsNullOrWhiteSpace(introSceneName))
            {
                GameManager.Instance?.SetPendingCinematicNextScene(GetSceneToLoad());
                LoadScene(introSceneName);
                return;
            }

            ChargerSceneConfiguree();
        }

        private void ContinuerPartie()
        {
            if (SaveManager.Instance == null || !SaveManager.Instance.HasSave())
            {
                Debug.LogWarning("[MenuButton] Aucune sauvegarde a charger.");
                RefreshVisibility();
                return;
            }

            SaveData data = SaveManager.Instance.LoadGame();
            if (data == null)
            {
                Debug.LogWarning("[MenuButton] Sauvegarde illisible.");
                RefreshVisibility();
                return;
            }

            SaveManager.Instance.SetCurrentData(data);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadRun(data);
            }

            ChargerSceneConfiguree();
        }

        private void ChargerSceneConfiguree()
        {
            LoadScene(GetSceneToLoad());
        }

        private string GetSceneToLoad()
        {
            return string.IsNullOrWhiteSpace(sceneACharger) ? "03_CampaignMap" : sceneACharger;
        }

        private static void LoadScene(string sceneName)
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(sceneName);
            }
            else
            {
                Debug.LogError("[MenuButton] SceneLoader.Instance absent. Demarre depuis 00_Bootstrap.");
            }
        }

        private void RefreshVisibility()
        {
            if (action != ActionBouton.ContinuerSave || !masquerSiAucuneSauvegarde) return;

            bool saveExiste = SaveManager.Instance != null && SaveManager.Instance.HasSave();
            gameObject.SetActive(saveExiste);
        }

        private static void Quitter()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (introScene == null) return;

            string scenePath = AssetDatabase.GetAssetPath(introScene);
            introSceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
#endif
    }
}
