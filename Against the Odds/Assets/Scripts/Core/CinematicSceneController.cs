using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        [SerializeField] private bool skipOnAnyKeyboardKey = true;
        [SerializeField] private bool skipOnMouseClick = true;
        [SerializeField] private bool skipOnTouch = true;

        [Header("Auto Advance")]
        [Tooltip("0 disables auto advance.")]
        [SerializeField, Min(0f)] private float autoAdvanceAfterSeconds;

        private float startTime;
        private bool isLeaving;

        private void OnEnable()
        {
            startTime = Time.unscaledTime;
            isLeaving = false;
            ApplyMusic();
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
