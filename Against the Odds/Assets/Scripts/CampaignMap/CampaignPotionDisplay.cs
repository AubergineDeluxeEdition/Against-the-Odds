using AgainstTheOdds.Core;
using TMPro;
using UnityEngine;

namespace AgainstTheOdds.CampaignMap
{
    public class CampaignPotionDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text countText;
        [SerializeField] private string countFormat = "x{0}";
        [SerializeField] private int textSortingOrder = 11200;

        private int lastPotionCount = int.MinValue;

        private void Awake()
        {
            if (countText == null)
            {
                countText = GetComponentInChildren<TMP_Text>(true);
            }

            Refresh(true);
        }

        private void OnEnable()
        {
            Refresh(true);
        }

        private void Update()
        {
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            int potionCount = GameManager.Instance != null ? GameManager.Instance.PotionCount : 0;
            if (!force && potionCount == lastPotionCount)
            {
                ApplySorting();
                return;
            }

            lastPotionCount = potionCount;

            if (countText != null)
            {
                countText.text = string.Format(countFormat, potionCount);
            }

            ApplySorting();
        }

        private void ApplySorting()
        {
            if (countText == null)
            {
                return;
            }

            Renderer textRenderer = countText.GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = textSortingOrder;
            }
        }
    }
}
