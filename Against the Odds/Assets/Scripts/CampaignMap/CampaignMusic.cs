using AgainstTheOdds.Core;
using UnityEngine;

namespace AgainstTheOdds.CampaignMap
{
    public class CampaignMusic : MonoBehaviour
    {
        [Header("Music")]
        [SerializeField] private AudioClip campaignMusic;
        [SerializeField] private bool fadeIn = true;

        [Header("Arrival Feedback")]
        [SerializeField] private GameObject arrivalVfxPrefab;
        [SerializeField] private Transform arrivalVfxAnchor;
        [SerializeField] private AudioClip arrivalSfx;
        [SerializeField] private float arrivalVfxLifetime = 3f;

        private void Start()
        {
            PlayCampaignMusic();
            PlayArrivalFeedback();
        }

        private void PlayCampaignMusic()
        {
            if (campaignMusic == null) return;

            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null)
            {
                Debug.LogWarning("[CampaignMusic] AudioManager absent. Launch from 00_Bootstrap to play campaign music.");
                return;
            }

            audioManager.PlayMusic(campaignMusic, fadeIn);
        }

        private void PlayArrivalFeedback()
        {
            if (arrivalSfx != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(arrivalSfx);
            }

            if (arrivalVfxPrefab == null) return;

            Vector3 spawnPosition = arrivalVfxAnchor != null ? arrivalVfxAnchor.position : transform.position;
            Quaternion spawnRotation = arrivalVfxAnchor != null ? arrivalVfxAnchor.rotation : Quaternion.identity;
            GameObject instance = Instantiate(arrivalVfxPrefab, spawnPosition, spawnRotation);

            if (arrivalVfxLifetime > 0f)
            {
                Destroy(instance, arrivalVfxLifetime);
            }
        }
    }
}
