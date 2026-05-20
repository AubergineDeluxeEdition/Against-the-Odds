using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// Generic controller for intro/interlude/ending scenes.
    /// Put it in a cinematic scene, then call Skip from a button or let input/auto-advance continue.
    /// </summary>
    [DisallowMultipleComponent]
    public class CinematicSceneController : MonoBehaviour
    {
        public enum CinematicMusicMode
        {
            KeepCurrentMusic,
            PlayCinematicMusic,
            StopMusic
        }

        [Header("Navigation")]
        [SerializeField] private bool usePendingNextScene = true;
        [SerializeField] private string fallbackNextSceneName = "03_CampaignMap";

        [Header("Music")]
        [SerializeField] private CinematicMusicMode musicMode = CinematicMusicMode.KeepCurrentMusic;
        [SerializeField] private AudioClip cinematicMusic;
        [SerializeField] private bool fadeMusic = true;
        [SerializeField] private bool restoreMusicVolumeOnStart = true;
        [SerializeField] private AudioSource fallbackMusicSource;

        [Header("Skip")]
        [SerializeField] private bool allowSkip = true;
        [SerializeField, Min(0f)] private float skipEnabledAfterSeconds = 0.25f;
        [SerializeField] private bool holdToSkip = true;
        [SerializeField, Min(0.1f)] private float holdSkipDuration = 2f;
        [SerializeField] private bool showHoldSkipPrompt = true;
        [SerializeField] private string holdSkipLabel = "Passer la cinématique";
        [SerializeField] private bool skipOnAnyKeyboardKey = true;
        [SerializeField] private bool skipOnMouseClick = true;
        [SerializeField] private bool skipOnTouch = true;

        [Header("Auto Advance")]
        [Tooltip("0 disables auto advance.")]
        [SerializeField, Min(0f)] private float autoAdvanceAfterSeconds;

        private float startTime;
        private float skipHoldTime;
        private bool isLeaving;
        private CanvasGroup skipPromptGroup;
        private Image skipProgressImage;
        private TMP_Text skipPromptText;
        private Sprite skipCircleSprite;

        private void OnEnable()
        {
            startTime = Time.unscaledTime;
            skipHoldTime = 0f;
            isLeaving = false;
            ApplyMusic();
            UpdateSkipPrompt(0f, false);
        }

        private void OnDisable()
        {
            if (skipPromptGroup != null)
            {
                Destroy(skipPromptGroup.gameObject);
            }

            skipPromptGroup = null;
            skipProgressImage = null;
            skipPromptText = null;
        }

        private void Update()
        {
            if (isLeaving) return;

            float elapsed = Time.unscaledTime - startTime;
            if (autoAdvanceAfterSeconds > 0f && elapsed >= autoAdvanceAfterSeconds)
            {
                Continue();
                return;
            }

            if (!allowSkip || elapsed < skipEnabledAfterSeconds) return;

            if (holdToSkip)
            {
                UpdateHoldSkip();
                return;
            }

            UpdateSkipPrompt(0f, showHoldSkipPrompt);
            if (WasSkipPressed())
            {
                Continue();
            }
        }

        public void Skip()
        {
            if (!allowSkip || isLeaving) return;
            Continue();
        }

        public void Exit()
        {
            Continue();
        }

        public void Continue()
        {
            if (isLeaving) return;
            isLeaving = true;
            UpdateSkipPrompt(0f, false);

            string nextScene = fallbackNextSceneName;
            if (usePendingNextScene && GameManager.Instance != null)
            {
                nextScene = GameManager.Instance.ConsumePendingCinematicNextScene(fallbackNextSceneName);
            }

            if (string.IsNullOrWhiteSpace(nextScene))
            {
                Debug.LogError("[CinematicSceneController] No next scene configured.");
                return;
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(nextScene);
            }
            else
            {
                SceneManager.LoadScene(nextScene);
            }
        }

        private bool WasSkipPressed()
        {
            if (skipOnAnyKeyboardKey && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            if (skipOnMouseClick && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            return skipOnTouch
                && Touchscreen.current != null
                && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        }

        private void UpdateHoldSkip()
        {
            bool held = IsSkipHeld();
            if (held)
            {
                skipHoldTime += Time.unscaledDeltaTime;
            }
            else
            {
                skipHoldTime = 0f;
            }

            float progress = Mathf.Clamp01(skipHoldTime / holdSkipDuration);
            UpdateSkipPrompt(progress, showHoldSkipPrompt);

            if (progress >= 1f)
            {
                Continue();
            }
        }

        private bool IsSkipHeld()
        {
            if (skipOnAnyKeyboardKey && Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            {
                return true;
            }

            if (skipOnMouseClick && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                return true;
            }

            return skipOnTouch
                && Touchscreen.current != null
                && Touchscreen.current.primaryTouch.press.isPressed;
        }

        private void UpdateSkipPrompt(float progress, bool visible)
        {
            if (!showHoldSkipPrompt || !allowSkip)
            {
                visible = false;
            }

            if (visible)
            {
                EnsureSkipPrompt();
            }

            if (skipPromptGroup != null)
            {
                skipPromptGroup.alpha = visible ? 1f : 0f;
            }

            if (skipProgressImage != null)
            {
                skipProgressImage.fillAmount = progress;
            }

            if (skipPromptText != null)
            {
                skipPromptText.text = holdSkipLabel;
            }
        }

        private void EnsureSkipPrompt()
        {
            if (skipPromptGroup != null) return;

            GameObject canvasObject = new GameObject("CinematicSkipPromptCanvas");

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            skipPromptGroup = canvasObject.AddComponent<CanvasGroup>();
            skipPromptGroup.blocksRaycasts = false;
            skipPromptGroup.interactable = false;

            GameObject root = new GameObject("SkipPrompt");
            root.transform.SetParent(canvasObject.transform, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(28f, -24f);
            rootRect.sizeDelta = new Vector2(390f, 44f);

            GameObject backCircle = new GameObject("CircleBack");
            backCircle.transform.SetParent(root.transform, false);
            Image backImage = backCircle.AddComponent<Image>();
            backImage.sprite = GetSkipCircleSprite();
            backImage.color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform backRect = backCircle.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = Vector2.zero;
            backRect.sizeDelta = new Vector2(32f, 32f);

            GameObject progressCircle = new GameObject("CircleProgress");
            progressCircle.transform.SetParent(root.transform, false);
            skipProgressImage = progressCircle.AddComponent<Image>();
            skipProgressImage.sprite = GetSkipCircleSprite();
            skipProgressImage.color = new Color(0.92f, 0.78f, 0.52f, 0.95f);
            skipProgressImage.type = Image.Type.Filled;
            skipProgressImage.fillMethod = Image.FillMethod.Radial360;
            skipProgressImage.fillOrigin = 2;
            skipProgressImage.fillClockwise = true;
            skipProgressImage.fillAmount = 0f;
            RectTransform progressRect = progressCircle.GetComponent<RectTransform>();
            progressRect.anchorMin = backRect.anchorMin;
            progressRect.anchorMax = backRect.anchorMax;
            progressRect.pivot = backRect.pivot;
            progressRect.anchoredPosition = backRect.anchoredPosition;
            progressRect.sizeDelta = backRect.sizeDelta;

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            skipPromptText = labelObject.AddComponent<TextMeshProUGUI>();
            skipPromptText.text = holdSkipLabel;
            skipPromptText.fontSize = 24f;
            skipPromptText.color = new Color(0.92f, 0.86f, 0.75f, 0.95f);
            skipPromptText.alignment = TextAlignmentOptions.MidlineLeft;
            skipPromptText.raycastTarget = false;
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.offsetMin = new Vector2(44f, 0f);
            labelRect.offsetMax = Vector2.zero;
        }

        private Sprite GetSkipCircleSprite()
        {
            if (skipCircleSprite != null) return skipCircleSprite;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CinematicSkipCircle"
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outerRadius = size * 0.45f;
            float innerRadius = size * 0.30f;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float outer = Mathf.InverseLerp(outerRadius + 1f, outerRadius - 1f, distance);
                    float inner = Mathf.InverseLerp(innerRadius - 1f, innerRadius + 1f, distance);
                    float alpha = Mathf.Clamp01(outer * inner);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            skipCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            skipCircleSprite.name = "CinematicSkipCircleSprite";
            return skipCircleSprite;
        }

        private void ApplyMusic()
        {
            if (AudioManager.Instance != null && restoreMusicVolumeOnStart)
            {
                AudioManager.Instance.RestoreMusic(fadeMusic ? 0.35f : 0f);
            }

            switch (musicMode)
            {
                case CinematicMusicMode.KeepCurrentMusic:
                    break;
                case CinematicMusicMode.PlayCinematicMusic:
                    PlayCinematicMusic();
                    break;
                case CinematicMusicMode.StopMusic:
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.StopMusic(fadeMusic);
                    }
                    break;
            }
        }

        private void PlayCinematicMusic()
        {
            if (cinematicMusic == null) return;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(cinematicMusic, fadeMusic);
                return;
            }

            if (fallbackMusicSource == null)
            {
                fallbackMusicSource = GetComponent<AudioSource>();
            }

            if (fallbackMusicSource == null)
            {
                fallbackMusicSource = gameObject.AddComponent<AudioSource>();
            }

            fallbackMusicSource.playOnAwake = false;
            fallbackMusicSource.loop = true;
            fallbackMusicSource.clip = cinematicMusic;
            fallbackMusicSource.Play();
        }
    }
}
