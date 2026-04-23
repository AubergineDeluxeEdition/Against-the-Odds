using UnityEngine;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// Préférences joueur persistées via PlayerPrefs (volumes, fullscreen).
    /// Un set sur une propriété déclenche automatiquement un Save().
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // Clés PlayerPrefs
        private const string ClePrefMusicVolume = "settings.musicVolume";
        private const string ClePrefSfxVolume = "settings.sfxVolume";
        private const string ClePrefMasterVolume = "settings.masterVolume";
        private const string ClePrefFullscreen = "settings.fullscreen";

        // Valeurs par défaut
        private const float DefautMusicVolume = 0.7f;
        private const float DefautSfxVolume = 1.0f;
        private const float DefautMasterVolume = 1.0f;
        private const bool DefautFullscreen = true;

        private float musicVolume;
        private float sfxVolume;
        private float masterVolume;
        private bool fullscreen;

        public float MusicVolume
        {
            get => musicVolume;
            set { musicVolume = Mathf.Clamp01(value); Save(); NotifierAudioManager(); }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set { sfxVolume = Mathf.Clamp01(value); Save(); NotifierAudioManager(); }
        }

        public float MasterVolume
        {
            get => masterVolume;
            set { masterVolume = Mathf.Clamp01(value); Save(); NotifierAudioManager(); }
        }

        public bool Fullscreen
        {
            get => fullscreen;
            set
            {
                fullscreen = value;
                Screen.fullScreen = value;
                Save();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            Load();
        }

        public void Load()
        {
            musicVolume = PlayerPrefs.GetFloat(ClePrefMusicVolume, DefautMusicVolume);
            sfxVolume = PlayerPrefs.GetFloat(ClePrefSfxVolume, DefautSfxVolume);
            masterVolume = PlayerPrefs.GetFloat(ClePrefMasterVolume, DefautMasterVolume);
            fullscreen = PlayerPrefs.GetInt(ClePrefFullscreen, DefautFullscreen ? 1 : 0) == 1;

            Screen.fullScreen = fullscreen;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(ClePrefMusicVolume, musicVolume);
            PlayerPrefs.SetFloat(ClePrefSfxVolume, sfxVolume);
            PlayerPrefs.SetFloat(ClePrefMasterVolume, masterVolume);
            PlayerPrefs.SetInt(ClePrefFullscreen, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ResetToDefaults()
        {
            MusicVolume = DefautMusicVolume;
            SFXVolume = DefautSfxVolume;
            MasterVolume = DefautMasterVolume;
            Fullscreen = DefautFullscreen;
        }

        // Si l'AudioManager existe déjà, on lui dit de re-lire les volumes.
        private void NotifierAudioManager()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.ApplyVolumesFromSettings();
        }
    }
}
