using System.Collections.Generic;
using UnityEngine;

public class CombatWorldHandView : MonoBehaviour
{
    [Header("Cards")]
    [SerializeField] private CombatWorldCardView cardPrefab;

    [Header("Zone")]
    [SerializeField] private float zoneWidth = 8f;
    [SerializeField] private float preferredSpacing = 1.7f;
    [SerializeField] private float minSpacing = 0.75f;
    [SerializeField] private float cardArcHeight = 0.25f;
    [SerializeField] private float cardWorldZ = 0f;
    [SerializeField] private int baseCardSortingOrder = 230;

    [Header("Hover")]
    [SerializeField] private float hoverLift = 0.55f;
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private int hoverSortingBoost = 50;

    private readonly List<CombatWorldCardView> cardViews = new List<CombatWorldCardView>();

    public void Rebuild(IReadOnlyList<CardInstance> cards, CombatWorldController controller, CombatState state)
    {
        Clear();

        if (cardPrefab == null || cards == null || cards.Count == 0)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("[CombatWorldHandView] Card prefab is missing.");
            }

            if (cards == null || cards.Count == 0)
            {
                Debug.LogWarning("[CombatWorldHandView] No cards to display.");
            }

            return;
        }

        float spacing = GetSpacing(cards.Count);
        float startX = -spacing * (cards.Count - 1) * 0.5f;

        for (int i = 0; i < cards.Count; i++)
        {
            CombatWorldCardView view = Instantiate(cardPrefab, transform);
            view.gameObject.SetActive(true);

            Vector3 localPosition = GetCardPosition(i, cards.Count, startX, spacing);

            view.transform.localPosition = localPosition;
            view.transform.localRotation = Quaternion.identity;
            ForceWorldZ(view.transform, cardWorldZ - i * 0.02f);
            view.Bind(cards[i], controller, state);
            view.SetRestState(view.transform.localPosition, view.transform.localScale, baseCardSortingOrder + i, hoverLift, hoverScale, hoverSortingBoost);

            cardViews.Add(view);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < cardViews.Count; i++)
        {
            if (cardViews[i] != null)
            {
                Destroy(cardViews[i].gameObject);
            }
        }

        cardViews.Clear();
    }

    private float GetSpacing(int count)
    {
        if (count <= 1) return 0f;

        float spacingThatFits = zoneWidth / (count - 1);
        return Mathf.Max(minSpacing, Mathf.Min(preferredSpacing, spacingThatFits));
    }

    private Vector3 GetCardPosition(int index, int count, float startX, float spacing)
    {
        float x = startX + spacing * index;
        float normalized = count <= 1 ? 0f : Mathf.InverseLerp(0, count - 1, index) * 2f - 1f;
        float y = -Mathf.Abs(normalized) * cardArcHeight;

        return new Vector3(x, y, -index * 0.02f);
    }

    private static void ForceWorldZ(Transform target, float worldZ)
    {
        Vector3 position = target.position;
        position.z = worldZ;
        target.position = position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
        Vector3 left = transform.position + Vector3.left * zoneWidth * 0.5f;
        Vector3 right = transform.position + Vector3.right * zoneWidth * 0.5f;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireCube(transform.position, new Vector3(zoneWidth, 2f, 0.1f));
    }
#endif
}
