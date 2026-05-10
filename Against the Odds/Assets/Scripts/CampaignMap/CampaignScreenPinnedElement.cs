using TMPro;
using UnityEngine;

namespace AgainstTheOdds.CampaignMap
{
    public class CampaignScreenPinnedElement : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool keepInitialScreenPosition = true;
        [SerializeField] private Vector2 viewportPosition = new Vector2(0.5f, 0.5f);
        [SerializeField] private float worldDepthFromCamera = 10f;

        [Header("Render Order")]
        [SerializeField] private bool forceRenderOrder = true;
        [SerializeField] private int spriteSortingOrder = 11000;
        [SerializeField] private int textSortingOrder = 11200;

        private float capturedDepth;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null) return;

            Vector3 screenPosition = targetCamera.WorldToScreenPoint(transform.position);
            capturedDepth = Mathf.Abs(screenPosition.z) > 0.001f ? screenPosition.z : worldDepthFromCamera;

            if (keepInitialScreenPosition)
            {
                viewportPosition = targetCamera.WorldToViewportPoint(transform.position);
            }

            ApplyRenderOrder();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            Vector3 worldPosition = targetCamera.ViewportToWorldPoint(new Vector3(
                viewportPosition.x,
                viewportPosition.y,
                capturedDepth));

            transform.position = worldPosition;
            ApplyRenderOrder();
        }

        private void ApplyRenderOrder()
        {
            if (!forceRenderOrder)
            {
                return;
            }

            bool isDeckButton = IsDeckButtonRoot();
            int effectiveSpriteOrder = isDeckButton ? Mathf.Max(spriteSortingOrder, 12400) : spriteSortingOrder;
            int effectiveTextOrder = isDeckButton ? Mathf.Max(textSortingOrder, effectiveSpriteOrder + 200) : textSortingOrder;

            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (ShouldSkipRenderOrder(spriteRenderer.transform))
                {
                    continue;
                }

                spriteRenderer.sortingOrder = effectiveSpriteOrder;
            }

            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (ShouldSkipRenderOrder(text.transform))
                {
                    continue;
                }

                Renderer textRenderer = text.GetComponent<Renderer>();
                if (textRenderer != null)
                {
                    textRenderer.sortingLayerID = 0;
                    textRenderer.sortingOrder = effectiveTextOrder;
                }
            }

            ApplySiblingDeckTextOrder(effectiveTextOrder);
        }

        private bool IsDeckButtonRoot()
        {
            string objectName = name.ToLowerInvariant();
            return objectName.Contains("deck") && !objectName.Contains("panel");
        }

        private bool ShouldSkipRenderOrder(Transform target)
        {
            Transform current = target;
            while (current != null && current != transform)
            {
                string objectName = current.name.ToLowerInvariant();
                if (objectName.Contains("deckpanel")
                    || objectName.Contains("panel")
                    || objectName.Contains("row")
                    || objectName.Contains("cards")
                    || objectName.Contains("backbg"))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void ApplySiblingDeckTextOrder(int effectiveTextOrder)
        {
            if (!IsDeckButtonRoot() || transform.parent == null)
            {
                return;
            }

            foreach (Transform sibling in transform.parent)
            {
                if (!sibling.name.ToLowerInvariant().Contains("deck")) continue;

                foreach (TMP_Text text in sibling.GetComponentsInChildren<TMP_Text>(true))
                {
                    Renderer textRenderer = text.GetComponent<Renderer>();
                    if (textRenderer == null) continue;

                    textRenderer.sortingLayerID = 0;
                    textRenderer.sortingOrder = effectiveTextOrder + 50;
                }
            }
        }
    }
}
