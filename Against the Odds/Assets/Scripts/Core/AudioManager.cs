using System.Collections;
using UnityEngine;

namespace AgainstTheOdds.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Defaults")]
        [SerializeField] private float dureeCrossfadeParDefaut = 1.0f;

        private float volumeMusiqueEffectif = 1f;
        private float volumeSfxEffectif = 1f;
        private float musicDuckingMultiplier = 1f;
        private Coroutine coroutineFadeMusique;
        private Coroutine coroutineDuckingMusique;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            EnsureSources();
        }

        private void Start()
        {
            if (SettingsManager.Instance != null)
            {
                ApplyVolumesFromSettings();
            }
        }

        public void ApplyVolumesFromSettings()
        {
            SettingsManager settings = SettingsManager.Instance;
            if (settings == null) return;

            volumeMusiqueEffectif = settings.MusicVolume * settings.MasterVolume;
            volumeSfxEffectif = settings.SFXVolume * settings.MasterVolume;

            EnsureSources();
            if (musicSource != null && coroutineFadeMusique == null)
            {
                musicSource.volume = GetTargetMusicVolume();
            }
        }

        public void PlayMusic(AudioClip clip, bool fadeIn = true)
        {
            if (clip == null) return;

            EnsureSources();
            if (musicSource == null) return;

            if (musicSource.clip == clip && musicSource.isPlaying) return;

            if (coroutineFadeMusique != null)
            {
                StopCoroutine(coroutineFadeMusique);
            }

            float duree = fadeIn ? dureeCrossfadeParDefaut : 0f;
            coroutineFadeMusique = StartCoroutine(RoutineCrossfadeMusique(clip, duree));
        }

        public void StopMusic(bool fadeOut = true)
        {
            EnsureSources();
            if (musicSource == null) return;

            if (coroutineFadeMusique != null)
            {
                StopCoroutine(coroutineFadeMusique);
            }

            float duree = fadeOut ? dureeCrossfadeParDefaut : 0f;
            coroutineFadeMusique = StartCoroutine(RoutineFadeOutMusique(duree));
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;

            EnsureSources();
            if (sfxSource == null) return;

            sfxSource.PlayOneShot(clip, volumeSfxEffectif);
        }

        public void DuckMusic(float multiplier, float duration = 0.35f)
        {
            FadeMusicDucking(Mathf.Clamp01(multiplier), duration);
        }

        public void RestoreMusic(float duration = 0.35f)
        {
            FadeMusicDucking(1f, duration);
        }

        public void SetMusicVolume(float volume)
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.MusicVolume = Mathf.Clamp01(volume);
            }

            ApplyVolumesFromSettings();
        }

        public void SetSFXVolume(float volume)
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SFXVolume = Mathf.Clamp01(volume);
            }

            ApplyVolumesFromSettings();
        }

        private void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = FindOrCreateSource("MusicSource", true);
            }

            if (sfxSource == null)
            {
                sfxSource = FindOrCreateSource("SFXSource", false);
            }
        }

        private AudioSource FindOrCreateSource(string sourceName, bool loop)
        {
            Transform existing = transform.Find(sourceName);
            AudioSource source = existing != null ? existing.GetComponent<AudioSource>() : null;

            if (source == null)
            {
                GameObject sourceObject = existing != null ? existing.gameObject : new GameObject(sourceName);
                sourceObject.transform.SetParent(transform);
                source = sourceObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private IEnumerator RoutineCrossfadeMusique(AudioClip nouveauClip, float duree)
        {
            if (musicSource == null)
            {
                coroutineFadeMusique = null;
                yield break;
            }

            if (musicSource.isPlaying && duree > 0f)
            {
                float volumeDepart = musicSource.volume;
                float temps = 0f;
                while (temps < duree)
                {
                    temps += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(volumeDepart, 0f, temps / duree);
                    yield return null;
                }
            }

            musicSource.Stop();
            musicSource.clip = nouveauClip;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.Play();

            if (duree > 0f)
            {
                float temps = 0f;
                while (temps < duree)
                {
                    temps += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(0f, GetTargetMusicVolume(), temps / duree);
                    yield return null;
                }
            }

            musicSource.volume = GetTargetMusicVolume();
            coroutineFadeMusique = null;
        }

        private IEnumerator RoutineFadeOutMusique(float duree)
        {
            if (musicSource == null)
            {
                coroutineFadeMusique = null;
                yield break;
            }

            if (duree > 0f && musicSource.isPlaying)
            {
                float volumeDepart = musicSource.volume;
                float temps = 0f;
                while (temps < duree)
                {
                    temps += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(volumeDepart, 0f, temps / duree);
                    yield return null;
                }
            }

            musicSource.Stop();
            musicSource.volume = GetTargetMusicVolume();
            coroutineFadeMusique = null;
        }

        private void FadeMusicDucking(float targetMultiplier, float duration)
        {
            EnsureSources();
            if (musicSource == null)
            {
                musicDuckingMultiplier = targetMultiplier;
                return;
            }

            if (coroutineDuckingMusique != null)
            {
                StopCoroutine(coroutineDuckingMusique);
            }

            coroutineDuckingMusique = StartCoroutine(RoutineFadeMusicDucking(targetMultiplier, duration));
        }

        private IEnumerator RoutineFadeMusicDucking(float targetMultiplier, float duration)
        {
            float startMultiplier = musicDuckingMultiplier;
            float startVolume = musicSource != null ? musicSource.volume : GetTargetMusicVolume();
            float targetVolume = volumeMusiqueEffectif * targetMultiplier;

            if (duration > 0f && musicSource != null)
            {
                float temps = 0f;
                while (temps < duration)
                {
                    temps += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(temps / duration);
                    musicDuckingMultiplier = Mathf.Lerp(startMultiplier, targetMultiplier, t);
                    musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                    yield return null;
                }
            }

            musicDuckingMultiplier = targetMultiplier;
            if (musicSource != null)
            {
                musicSource.volume = GetTargetMusicVolume();
            }

            coroutineDuckingMusique = null;
        }

        private float GetTargetMusicVolume()
        {
            return volumeMusiqueEffectif * musicDuckingMultiplier;
        }
    }
}
