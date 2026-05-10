using AgainstTheOdds.Core;
using UnityEngine;

namespace AgainstTheOdds.CampaignMap
{
    public class CampaignRegionVisibility : MonoBehaviour
    {
        [Tooltip("First boss index that makes this region visible.")]
        [SerializeField] private int unlockAtBossIndex = 0;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Collider2D[] colliders;

        private bool lastVisibleState;
        private bool initialized;

        private void Reset()
        {
            CacheChildren();
        }

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0 || colliders == null || colliders.Length == 0)
            {
                CacheChildren();
            }
        }

        private void OnEnable()
        {
            ApplyVisibility(force: true);
        }

        private void Update()
        {
            ApplyVisibility(force: false);
        }

        private void CacheChildren()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void ApplyVisibility(bool force)
        {
            int currentBossIndex = GameManager.Instance != null ? GameManager.Instance.CurrentBossIndex : 0;
            bool visible = currentBossIndex >= unlockAtBossIndex;

            if (initialized && !force && visible == lastVisibleState) return;

            initialized = true;
            lastVisibleState = visible;

            if (renderers != null)
            {
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null && !BelongsToBossPin(renderer.transform))
                    {
                        renderer.enabled = visible;
                    }
                }
            }

            if (colliders != null)
            {
                foreach (Collider2D collider2d in colliders)
                {
                    if (collider2d != null && !BelongsToBossPin(collider2d.transform))
                    {
                        collider2d.enabled = visible;
                    }
                }
            }
        }

        private static bool BelongsToBossPin(Transform target)
        {
            return target != null && target.GetComponentInParent<BossPin>(true) != null;
        }
    }
}
