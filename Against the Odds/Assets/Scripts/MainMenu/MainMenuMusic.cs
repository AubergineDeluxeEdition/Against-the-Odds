using UnityEngine;
using AgainstTheOdds.Core;

namespace AgainstTheOdds.MainMenu
{
    /// <summary>
    /// Lance la musique de menu à l'entrée dans la scène 01_MainMenu.
    /// À attacher sur un GameObject de la scène MainMenu (par exemple "MenuAmbience").
    /// </summary>
    public class MainMenuMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip musiqueMenu;
        [SerializeField] private bool fadeIn = true;

        private void Start()
        {
            if (AudioManager.Instance == null)
            {
                Debug.LogWarning("[MainMenuMusic] AudioManager absent — la scène a-t-elle été lancée depuis 00_Bootstrap ?");
                return;
            }

            AudioManager.Instance.PlayMusic(musiqueMenu, fadeIn);
        }
    }
}
