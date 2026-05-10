using AgainstTheOdds.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AgainstTheOdds.CampaignMap
{
    [RequireComponent(typeof(Collider2D))]
    public class CampaignMapButton : MonoBehaviour
    {
        public enum ButtonAction
        {
            OpenDeck,
            CloseDeck,
            ToggleDeck,
            LoadMainMenu
        }

        private const float ClickMoveTolerancePixels = 12f;

        [Header("Action")]
        [SerializeField] private ButtonAction action = ButtonAction.ToggleDeck;
        [SerializeField] private CampaignDeckWindow deckWindow;
        [SerializeField] private string mainMenuSceneName = "01_MainMenu";
        [SerializeField] private bool endRunWhenLoadingMainMenu;

        [Header("Feedback")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.65f, 1f);
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private AudioClip clickSfx;

        private Collider2D buttonCollider;
        private Vector3 restScale;
        private bool isHovered;
        private bool pressStartedOnButton;
        private Vector2 pressScreenPosition;

        private void Awake()
        {
            buttonCollider = GetComponent<Collider2D>();
            restScale = transform.localScale;

            if (deckWindow == null
                && (action == ButtonAction.OpenDeck
                    || action == ButtonAction.CloseDeck
                    || action == ButtonAction.ToggleDeck))
            {
                deckWindow = FindFirstObjectByType<CampaignDeckWindow>(FindObjectsInactive.Include);
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            ApplyHover(false);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || buttonCollider == null) return;

            Vector2 pointerPosition = mouse.position.ReadValue();
            bool pointerOverButton = IsPointerOverButton(pointerPosition);
            if (pointerOverButton != isHovered)
            {
                ApplyHover(pointerOverButton);
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressStartedOnButton = pointerOverButton;
                pressScreenPosition = pointerPosition;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                bool isClick = pressStartedOnButton
                    && pointerOverButton
                    && Vector2.Distance(pressScreenPosition, pointerPosition) <= ClickMoveTolerancePixels;

                pressStartedOnButton = false;

                if (isClick)
                {
                    Use();
                }
            }
        }

        private void Use()
        {
            if (clickSfx != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clickSfx);
            }

            switch (action)
            {
                case ButtonAction.OpenDeck:
                    deckWindow?.Open();
                    break;
                case ButtonAction.CloseDeck:
                    deckWindow?.Close();
                    break;
                case ButtonAction.ToggleDeck:
                    deckWindow?.Toggle();
                    break;
                case ButtonAction.LoadMainMenu:
                    LoadMainMenu();
                    break;
            }
        }

        private void LoadMainMenu()
        {
            if (endRunWhenLoadingMainMenu && GameManager.Instance != null)
            {
                GameManager.Instance.EndRun();
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(mainMenuSceneName);
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void ApplyHover(bool hovered)
        {
            isHovered = hovered;
            transform.localScale = restScale * (hovered ? hoverScale : 1f);

            if (targetRenderer != null)
            {
                targetRenderer.color = hovered ? hoverColor : normalColor;
            }
        }

        private bool IsPointerOverButton(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;

            Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
            return buttonCollider.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
        }
    }
}
