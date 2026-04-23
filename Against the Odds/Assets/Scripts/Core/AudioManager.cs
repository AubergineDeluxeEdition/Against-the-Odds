using System.Collections;
using UnityEngine;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// Gère la musique (loop + crossfade) et les SFX one-shot.
    /// Lit les volumes depuis SettingsManager au démarrage.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources audio (à assigner dans l'inspecteur)")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Réglages par défaut")]
        [SerializeField] private float dureeCrossfadeParDefaut = 1.0f;

        // Volumes effectifs appliqués, déjà multipliés par le Master Volume
        private float volumeMusiqueEffectif = 1f;
        private float volumeSfxEffectif = 1f;

        private Coroutine coroutineFadeMusique;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            if (musicSource == null || sfxSource == null)
            {
                Debug.LogError("[AudioManager] MusicSource ou SFXSource non assignées dans l'inspecteur.");
            }
        }

        private void Start()
        {
            // SettingsManager a aussi un Awake, donc il est prêt ici
            if (SettingsManager.Instance != null)
            {
                ApplyVolumesFromSettings();
            }
        }

        /// <summary>Recalcule les volumes effectifs à partir du SettingsManager. À appeler après un changement de réglage.</summary>
        public void ApplyVolumesFromSettings()
        {
            var settings = SettingsManager.Instance;
            if (settings == null) return;

            volumeMusiqueEffectif = settings.MusicVolume * settings.MasterVolume;
            volumeSfxEffectif = settings.SFXVolume * settings.MasterVolume;

            if (musicSource != null && coroutineFadeMusique == null)
            {
                musicSource.volume = volumeMusiqueEffectif;
            }
        }

        /// <summary>Joue une musique en boucle, avec crossfade si une musique joue déjà.</summary>
        public void PlayMusic(AudioClip clip, bool fadeIn = true)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlayMusic appelé avec un clip null.");
                return;
            }

            // Si la même musique joue déjà, ne rien faire
            if (musicSource.clip == clip && musicSource.isPlaying) return;

            if (coroutineFadeMusique != null) StopCoroutine(coroutineFadeMusique);
            float duree = fadeIn ? dureeCrossfadeParDefaut : 0f;
            coroutineFadeMusique = StartCoroutine(RoutineCrossfadeMusique(clip, duree));
        }

        /// <summary>Arrête la musique, avec fondu si demandé.</summary>
        public void StopMusic(bool fadeOut = true)
        {
            if (coroutineFadeMusique != null) StopCoroutine(coroutineFadeMusique);
            float duree = fadeOut ? dureeCrossfadeParDefaut : 0f;
            coroutineFadeMusique = StartCoroutine(RoutineFadeOutMusique(duree));
        }

        /// <summary>Joue un effet sonore one-shot sans interrompre la musique ni les autres SFX.</summary>
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, volumeSfxEffectif);
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

        private IEnumerator RoutineCrossfadeMusique(AudioClip nouveauClip, float duree)
        {
            // Fade out de la musique actuelle
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

            // Fade in
            if (duree > 0f)
            {
                float temps = 0f;
                while (temps < duree)
                {
                    temps += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(0f, volumeMusiqueEffectif, temps / duree);
                    yield return null;
                }
            }

            musicSource.volume = volumeMusiqueEffectif;
            coroutineFadeMusique = null;
        }

        private IEnumerator RoutineFadeOutMusique(float duree)
        {
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
            musicSource.volume = volumeMusiqueEffectif;
            coroutineFadeMusique = null;
        }
    }
}
