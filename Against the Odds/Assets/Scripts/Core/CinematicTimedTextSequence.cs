using TMPro;
using UnityEngine;

namespace AgainstTheOdds.Core
{
    [DisallowMultipleComponent]
    public class CinematicTimedTextSequence : MonoBehaviour
    {
        [System.Serializable]
        public class TimedTextLine
        {
            [TextArea(2, 4)] public string text;
            [Min(0.01f)] public float duration = 3f;
            [Tooltip("If enabled, credits start when this line starts unless Credits Start Time Override is set.")]
            public bool startCreditsHere;
        }

        [Header("Text")]
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TimedTextLine[] lines;

        [Header("Timing")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool startAfterSceneLoaderReveal = true;
        [Tooltip("Delay before the first line, counted after the scene is actually revealed to the player.")]
        [SerializeField, Min(0f)] private float initialDelay;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField] private bool clearTextWhenFinished = true;

        [Header("Credits")]
        [SerializeField] private bool enableCredits;
        [SerializeField] private TMP_Text creditsText;
        [TextArea(8, 18)]
        [SerializeField] private string creditsContent =
            "Against the Odds\n\n" +
            "Thank you for playing";
        [Tooltip("If >= 0, credits start at this sequence time after Initial Delay. If < 0, credits start after the last lore line.")]
        [SerializeField] private float creditsStartTimeOverride = -1f;
        [SerializeField, Min(0.01f)] private float creditsDuration = 10f;
        [SerializeField] private Vector3 creditsStartLocalPosition = new Vector3(0f, -5f, 0f);
        [SerializeField] private Vector3 creditsEndLocalPosition = new Vector3(0f, 5f, 0f);
        [SerializeField, Min(0f)] private float creditsFadeInDuration = 0.35f;
        [SerializeField, Min(0f)] private float creditsFadeOutDuration = 0.5f;

        [Header("Completion")]
        [SerializeField] private bool continueAfterLastLine;
        [SerializeField, Min(0f)] private float continueAfterLastLineDelay;
        [SerializeField] private CinematicSceneController sceneController;

        private float sequenceStartTime;
        private int activeLineIndex = -1;
        private Color baseColor = Color.white;
        private Color creditsBaseColor = Color.white;
        private bool creditsStarted;
        private float creditsStartTime = -1f;
        private bool sequenceStarted;
        private bool continueRequested;

        private void Awake()
        {
            if (targetText == null)
            {
                targetText = GetComponent<TMP_Text>();
            }

            if (targetText != null)
            {
                baseColor = targetText.color;
                SetTextAlpha(0f);
                targetText.text = string.Empty;
            }

            if (creditsText != null)
            {
                creditsBaseColor = creditsText == targetText ? baseColor : creditsText.color;
                creditsText.text = string.Empty;
                SetCreditsAlpha(0f);
                if (creditsText != targetText)
                {
                    creditsText.gameObject.SetActive(false);
                }
            }
            else if (enableCredits)
            {
                creditsText = targetText;
            }
        }

        private void OnEnable()
        {
            ResetSequenceVisuals();
        }

        private void ResetSequenceVisuals()
        {
            sequenceStarted = false;
            sequenceStartTime = 0f;
            activeLineIndex = -1;
            creditsStarted = false;
            creditsStartTime = -1f;
            continueRequested = false;

            if (targetText != null)
            {
                SetTextAlpha(0f);
                targetText.text = string.Empty;
            }

            if (creditsText != null)
            {
                creditsBaseColor = creditsText == targetText ? baseColor : creditsText.color;
                creditsText.text = creditsText == targetText ? string.Empty : creditsContent;
                if (creditsText != targetText)
                {
                    creditsText.transform.localPosition = creditsStartLocalPosition;
                }
                SetCreditsAlpha(0f);
                creditsText.gameObject.SetActive(creditsText == targetText);
            }
        }

        private void StartSequence()
        {
            sequenceStarted = true;
            sequenceStartTime = GetNow();
            activeLineIndex = -1;
            creditsStarted = false;
            creditsStartTime = ResolveCreditsStartTime();
        }

        private void Update()
        {
            if (!sequenceStarted)
            {
                if (ShouldWaitForSceneReveal())
                {
                    return;
                }

                StartSequence();
            }

            float elapsed = GetNow() - sequenceStartTime;
            float sequenceElapsed = elapsed - initialDelay;

            if (sequenceElapsed < 0f)
            {
                SetTextAlpha(0f);
                UpdateCredits(sequenceElapsed);
                return;
            }

            UpdateCredits(sequenceElapsed);

            if (targetText == null || lines == null || lines.Length == 0) return;

            if (creditsStarted && creditsText == targetText)
            {
                return;
            }

            int lineIndex = FindLineIndex(sequenceElapsed, out float lineStartTime);

            if (lineIndex < 0)
            {
                activeLineIndex = -1;
                if (!enableCredits || creditsStartTime < 0f || sequenceElapsed < creditsStartTime)
                {
                    if (clearTextWhenFinished) targetText.text = string.Empty;
                    SetTextAlpha(0f);
                }

                TryContinueAfterLastLine(sequenceElapsed);
                return;
            }

            TimedTextLine line = lines[lineIndex];
            if (line.startCreditsHere)
            {
                TryStartCredits(sequenceElapsed);
            }

            if (activeLineIndex != lineIndex)
            {
                activeLineIndex = lineIndex;
                targetText.text = line.text;
            }

            targetText.text = line.text;
            SetTextAlpha(CalculateAlpha(line, sequenceElapsed - lineStartTime));
            TryContinueAfterLastLine(sequenceElapsed);
        }

        private int FindLineIndex(float sequenceElapsed, out float lineStartTime)
        {
            lineStartTime = 0f;
            float cursor = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                TimedTextLine line = lines[i];
                if (line == null) continue;

                float duration = Mathf.Max(0.01f, line.duration);
                if (sequenceElapsed >= cursor && sequenceElapsed <= cursor + duration)
                {
                    lineStartTime = cursor;
                    return i;
                }

                cursor += duration;
            }

            return -1;
        }

        private float CalculateAlpha(TimedTextLine line, float localLineTime)
        {
            float duration = Mathf.Max(0.01f, line.duration);
            float alpha = 1f;

            if (fadeInDuration > 0f)
            {
                alpha = Mathf.Min(alpha, Mathf.Clamp01(localLineTime / fadeInDuration));
            }

            if (fadeOutDuration > 0f)
            {
                alpha = Mathf.Min(alpha, Mathf.Clamp01((duration - localLineTime) / fadeOutDuration));
            }

            return alpha;
        }

        private float ResolveCreditsStartTime()
        {
            if (!enableCredits) return -1f;
            if (creditsStartTimeOverride >= 0f) return creditsStartTimeOverride;
            if (lines == null) return -1f;

            for (int i = 0; i < lines.Length; i++)
            {
                TimedTextLine line = lines[i];
                if (line != null && line.startCreditsHere)
                {
                    return CalculateLineStartTime(i);
                }
            }

            return CalculateTotalLineDuration();
        }

        private void UpdateCredits(float elapsed)
        {
            if (!enableCredits || creditsText == null) return;

            if (creditsStartTime >= 0f && elapsed >= creditsStartTime)
            {
                TryStartCredits(elapsed);
            }

            if (!creditsStarted) return;

            float localTime = elapsed - creditsStartTime;
            float progress = Mathf.Clamp01(localTime / creditsDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            creditsText.transform.localPosition = Vector3.Lerp(creditsStartLocalPosition, creditsEndLocalPosition, easedProgress);
            creditsText.text = creditsContent;
            SetCreditsAlpha(CalculateCreditsAlpha(localTime));
        }

        private void TryStartCredits(float elapsed)
        {
            if (!enableCredits || creditsText == null || creditsStarted) return;

            if (creditsStartTime < 0f)
            {
                creditsStartTime = elapsed;
            }

            creditsStarted = true;
            creditsText.gameObject.SetActive(true);
            creditsText.text = creditsContent;
            creditsText.transform.localPosition = creditsStartLocalPosition;
            SetCreditsAlpha(0f);
        }

        private float CalculateCreditsAlpha(float localTime)
        {
            float alpha = 1f;
            if (creditsFadeInDuration > 0f)
            {
                alpha = Mathf.Min(alpha, Mathf.Clamp01(localTime / creditsFadeInDuration));
            }

            if (creditsFadeOutDuration > 0f)
            {
                alpha = Mathf.Min(alpha, Mathf.Clamp01((creditsDuration - localTime) / creditsFadeOutDuration));
            }

            return alpha;
        }

        private void SetTextAlpha(float alpha)
        {
            if (targetText == null) return;

            Color color = baseColor;
            color.a *= Mathf.Clamp01(alpha);
            targetText.color = color;
        }

        private void SetCreditsAlpha(float alpha)
        {
            if (creditsText == null) return;

            Color color = creditsBaseColor;
            color.a *= Mathf.Clamp01(alpha);
            creditsText.color = color;
        }

        private float GetNow()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }

        private float CalculateLineStartTime(int targetIndex)
        {
            if (lines == null) return 0f;

            float time = 0f;
            for (int i = 0; i < targetIndex && i < lines.Length; i++)
            {
                TimedTextLine line = lines[i];
                if (line == null) continue;

                time += Mathf.Max(0.01f, line.duration);
            }

            return time;
        }

        private float CalculateTotalLineDuration()
        {
            if (lines == null) return 0f;

            float total = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                TimedTextLine line = lines[i];
                if (line == null) continue;

                total += Mathf.Max(0.01f, line.duration);
            }

            return total;
        }

        private bool ShouldWaitForSceneReveal()
        {
            return startAfterSceneLoaderReveal
                && SceneLoader.Instance != null
                && SceneLoader.Instance.IsLoading;
        }

        private void TryContinueAfterLastLine(float sequenceElapsed)
        {
            if (!continueAfterLastLine || continueRequested) return;

            float completionTime = enableCredits
                ? GetCreditsCompletionTime()
                : CalculateTotalLineDuration();

            if (sequenceElapsed < completionTime + continueAfterLastLineDelay) return;

            continueRequested = true;
            if (sceneController == null)
            {
                sceneController = GetComponent<CinematicSceneController>();
            }

            if (sceneController == null)
            {
                sceneController = FindFirstObjectByType<CinematicSceneController>();
            }

            if (sceneController != null)
            {
                sceneController.Continue();
            }
            else
            {
                Debug.LogWarning("[CinematicTimedTextSequence] Cannot continue after last line: no CinematicSceneController found.");
            }
        }

        private float GetCreditsCompletionTime()
        {
            float start = creditsStartTime >= 0f ? creditsStartTime : ResolveCreditsStartTime();
            if (start < 0f) return CalculateTotalLineDuration();
            return start + creditsDuration;
        }
    }
}
