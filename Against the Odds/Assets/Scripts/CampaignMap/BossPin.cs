using AgainstTheOdds.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AgainstTheOdds.CampaignMap
{
    /// <summary>
    /// Campaign map pin. Registers itself for camera focus and launches its configured encounter on click.
    /// Add a Collider2D on the same GameObject for OnMouseDown to work.
    /// </summary>
    public class BossPin : MonoBehaviour
    {
        private const float ClickMoveTolerancePixels = 10f;

        [Header("Progression")]
        [Tooltip("Boss index represented by this pin. Must be unique in the campaign scene.")]
        [SerializeField] private int bossIndex = 0;
        [SerializeField] private bool hideLockedPins = true;
        [SerializeField] private SpriteRenderer pinRenderer;
        [SerializeField] private Sprite currentSprite;
        [SerializeField] private Sprite completedSprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Color currentColor = Color.white;
        [SerializeField] private Color completedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 0.35f);

        [Header("Hover Highlight")]
        [SerializeField] private SpriteRenderer hoverHighlightRenderer;
        [SerializeField] private Color hoverHighlightColor = new Color(1f, 0.75f, 0.2f, 0.9f);
        [SerializeField] private Color hoverPinTintColor = new Color(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private float hoverHighlightScale = 1f;
        [SerializeField] private float hoverPinBreathAmplitude = 0.08f;
        [SerializeField] private float hoverPulseAmplitude = 0.35f;
        [SerializeField] private float hoverPulseSpeed = 5f;
        [SerializeField] private int hoverHighlightSortingOffset = 20;

        [Header("Combat")]
        [SerializeField] private global::BossEncounterConfig encounterConfig;
        [SerializeField] private string combatSceneName;
#if UNITY_EDITOR
        [SerializeField] private SceneAsset combatScene;
#endif

        [Header("Click Feedback")]
        [SerializeField] private GameObject clickVfxPrefab;
        [SerializeField] private Transform clickVfxAnchor;
        [SerializeField] private AudioClip clickSfx;
        [SerializeField] private float clickVfxLifetime = 2f;
        [SerializeField] private float delayBeforeCombatLoad = 0.15f;

        public int BossIndex => bossIndex;
        public global::BossEncounterConfig EncounterConfig => encounterConfig;
        public bool IsCompleted => GameManager.Instance != null && bossIndex < GameManager.Instance.CurrentBossIndex;
        public bool IsCurrent => GameManager.Instance == null ? bossIndex == 0 : bossIndex == GameManager.Instance.CurrentBossIndex;
        public bool IsUnlocked => GameManager.Instance == null || bossIndex <= GameManager.Instance.CurrentBossIndex;
        public bool IsVisibleOnCampaign => isActiveAndEnabled && IsUnlocked;

        private Collider2D pinCollider;
        private bool pressStartedOnPin;
        private Vector2 pressScreenPosition;
        private bool isLaunching;
        private bool isHovered;
        private Vector3 pinRendererBaseScale = Vector3.one;

        private void Awake()
        {
            pinCollider = GetComponent<Collider2D>();
            if (pinRenderer == null) pinRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (pinRenderer != null) pinRendererBaseScale = pinRenderer.transform.localScale;
            EnsureHoverHighlightRenderer();
            if (pinCollider == null)
            {
                Debug.LogError($"[BossPin] Missing Collider2D on '{name}'.");
            }
        }

        private void OnEnable()
        {
            CampaignPinRegistry.Register(this);
            ApplyProgressionState();
        }

        private void OnDisable()
        {
            CampaignPinRegistry.Unregister(this);
        }

        private void Update()
        {
            ApplyProgressionState();

            Mouse mouse = Mouse.current;
            if (mouse == null || pinCollider == null || !IsCurrent)
            {
                SetHoverHighlight(false);
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            bool pointerOverPin = IsPointerOverPin(pointerPosition);
            SetHoverHighlight(pointerOverPin);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressStartedOnPin = pointerOverPin;
                pressScreenPosition = pointerPosition;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                bool isClick = pressStartedOnPin
                    && Vector2.Distance(pressScreenPosition, pointerPosition) <= ClickMoveTolerancePixels
                    && IsPointerOverPin(pointerPosition);

                pressStartedOnPin = false;

                if (isClick)
                {
                    LaunchCombat();
                }
            }
        }

        public void LaunchCombat()
        {
            if (isLaunching) return;
            if (!IsCurrent)
            {
                Debug.Log($"[BossPin] '{name}' is not the current boss pin and cannot launch combat.");
                return;
            }

            if (encounterConfig == null)
            {
                Debug.LogError($"[BossPin] Missing BossEncounterConfig on '{name}'.");
                return;
            }

            if (string.IsNullOrWhiteSpace(combatSceneName))
            {
                Debug.LogError($"[BossPin] Missing combat scene on '{name}'.");
                return;
            }

            isLaunching = true;
            PlayClickFeedback();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SelectEncounter(encounterConfig, combatSceneName);
            }

            if (delayBeforeCombatLoad > 0f)
            {
                StartCoroutine(LoadCombatAfterDelay());
                return;
            }

            LoadCombatScene();
        }

        private IEnumerator LoadCombatAfterDelay()
        {
            yield return new WaitForSeconds(delayBeforeCombatLoad);
            LoadCombatScene();
        }

        private void LoadCombatScene()
        {
            string sceneToLoad = combatSceneName;
            if (encounterConfig != null && !string.IsNullOrWhiteSpace(encounterConfig.preCombatSceneName))
            {
                sceneToLoad = encounterConfig.preCombatSceneName;
                GameManager.Instance?.SetPendingCinematicNextScene(combatSceneName);
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(sceneToLoad);
            }
            else
            {
                SceneManager.LoadScene(sceneToLoad);
            }
        }

        private void PlayClickFeedback()
        {
            if (clickSfx != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clickSfx);
            }

            if (clickVfxPrefab == null) return;

            Vector3 spawnPosition = clickVfxAnchor != null ? clickVfxAnchor.position : transform.position;
            Quaternion spawnRotation = clickVfxAnchor != null ? clickVfxAnchor.rotation : Quaternion.identity;
            GameObject instance = Instantiate(clickVfxPrefab, spawnPosition, spawnRotation);

            if (clickVfxLifetime > 0f)
            {
                Destroy(instance, clickVfxLifetime);
            }
        }

        private bool IsPointerOverPin(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;

            Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
            Vector2 worldPoint = new Vector2(worldPosition.x, worldPosition.y);
            return pinCollider.OverlapPoint(worldPoint);
        }

        private void EnsureHoverHighlightRenderer()
        {
            if (hoverHighlightRenderer != null || pinRenderer == null) return;

            GameObject highlight = new GameObject("HoverHighlight");
            highlight.transform.SetParent(pinRenderer.transform, false);
            highlight.transform.localPosition = Vector3.zero;
            highlight.transform.localRotation = Quaternion.identity;
            highlight.transform.localScale = Vector3.one;

            hoverHighlightRenderer = highlight.AddComponent<SpriteRenderer>();
            hoverHighlightRenderer.enabled = false;
        }

        private void SetHoverHighlight(bool hovered)
        {
            isHovered = hovered && IsCurrent && !isLaunching;
            UpdateHoverHighlightVisual();
        }

        private void UpdateHoverHighlightVisual()
        {
            if (pinRenderer == null)
            {
                return;
            }

            EnsureHoverHighlightRenderer();
            if (hoverHighlightRenderer == null)
            {
                return;
            }

            hoverHighlightRenderer.enabled = isHovered && pinRenderer.enabled;
            if (!hoverHighlightRenderer.enabled)
            {
                ResetPinHoverScale();
                return;
            }

            float pulse = (Mathf.Sin(Time.time * hoverPulseSpeed) + 1f) * 0.5f;
            float pulseStrength = Mathf.Lerp(1f - hoverPulseAmplitude, 1f, pulse);
            Color highlightColor = hoverHighlightColor;
            highlightColor.a *= pulseStrength;

            hoverHighlightRenderer.sprite = pinRenderer.sprite;
            hoverHighlightRenderer.color = highlightColor;
            hoverHighlightRenderer.sortingLayerID = pinRenderer.sortingLayerID;
            hoverHighlightRenderer.sortingOrder = pinRenderer.sortingOrder + hoverHighlightSortingOffset;
            hoverHighlightRenderer.transform.localScale = Vector3.one * hoverHighlightScale;

            pinRenderer.color = Color.Lerp(currentColor, hoverPinTintColor, pulseStrength);
            pinRenderer.transform.localScale = pinRendererBaseScale * (1f + pulse * hoverPinBreathAmplitude);
        }

        private void ApplyProgressionState()
        {
            bool unlocked = IsUnlocked;
            bool shouldShowPin = unlocked || !hideLockedPins;

            if (pinCollider != null)
            {
                pinCollider.enabled = shouldShowPin && IsCurrent;
            }

            if (pinRenderer == null) return;
            pinRenderer.enabled = shouldShowPin;
            if (!shouldShowPin)
            {
                SetHoverHighlight(false);
                ResetPinHoverScale();
                return;
            }

            if (IsCompleted)
            {
                if (completedSprite != null) pinRenderer.sprite = completedSprite;
                pinRenderer.color = completedColor;
                SetHoverHighlight(false);
                ResetPinHoverScale();
                return;
            }

            if (IsCurrent)
            {
                if (currentSprite != null) pinRenderer.sprite = currentSprite;
                pinRenderer.color = currentColor;
                UpdateHoverHighlightVisual();
                return;
            }

            if (lockedSprite != null) pinRenderer.sprite = lockedSprite;
            pinRenderer.color = lockedColor;
            SetHoverHighlight(false);
            ResetPinHoverScale();
        }

        private void ResetPinHoverScale()
        {
            if (pinRenderer != null)
            {
                pinRenderer.transform.localScale = pinRendererBaseScale;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (combatScene != null)
            {
                string scenePath = AssetDatabase.GetAssetPath(combatScene);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                combatSceneName = sceneName;
            }
        }
#endif
    }
}
