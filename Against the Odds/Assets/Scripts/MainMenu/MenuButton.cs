using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AgainstTheOdds.Core;

namespace AgainstTheOdds.MainMenu
{
    /// <summary>
    /// À attacher sur un GameObject qui possède déjà un composant uGUI Button (avec un label TextMeshProUGUI enfant).
    /// Déclenche une action configurable au clic : charger une scène, quitter, continuer depuis une save.
    /// Gère aussi les SFX hover/click via les events du système UI.
    /// </summary>
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
        [SerializeField] private string sceneACharger = "02_Campaign";

        [Header("SFX (optionnels)")]
        [SerializeField] private AudioClip sfxHover;
        [SerializeField] private AudioClip sfxClick;

        private Button bouton;

        private void Awake()
        {
            bouton = GetComponent<Button>();
            bouton.onClick.AddListener(OnClic);
        }

        private void OnDestroy()
        {
            if (bouton != null) bouton.onClick.RemoveListener(OnClic);
        }

        // IPointerEnterHandler : déclenché quand la souris entre sur le Button (ou le RectTransform qui a un Graphic)
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!bouton.interactable) return;
            if (sfxHover != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(sfxHover);
        }

        private void OnClic()
        {
            if (sfxClick != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(sfxClick);
            ExecuterAction();
        }

        private void ExecuterAction()
        {
            switch (action)
            {
                case ActionBouton.ChargerScene:
                    if (SceneLoader.Instance != null)
                    {
                        SceneLoader.Instance.LoadScene(sceneACharger);
                    }
                    else
                    {
                        Debug.LogError("[MenuButton] SceneLoader.Instance absent — as-tu démarré depuis 00_Bootstrap ?");
                    }
                    break;

                case ActionBouton.Quitter:
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #else
                    Application.Quit();
                    #endif
                    break;

                case ActionBouton.ContinuerSave:
                    if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
                    {
                        var data = SaveManager.Instance.CurrentData;
                        GameManager.Instance.StartNewRun(data.selectedDeckId);
                        SceneLoader.Instance.LoadScene("02_Campaign");
                    }
                    else
                    {
                        Debug.LogWarning("[MenuButton] Aucune sauvegarde à charger.");
                    }
                    break;
            }
        }
    }
}
