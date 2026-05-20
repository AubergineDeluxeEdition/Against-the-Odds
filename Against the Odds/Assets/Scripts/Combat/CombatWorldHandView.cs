using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private const int SortingStride = 20;

    private readonly List<CombatWorldCardView> cardViews = new List<CombatWorldCardView>();
    private CombatWorldCardView hoveredCard;
    private CombatWorldCardView pressedCard;

    private void Awake()
    {
        HideSceneTemplateCard();
    }

    public void Rebuild(IReadOnlyList<CardInstance> cards, CombatWorldController controller, CombatState state)
    {
        HideSceneTemplateCard();
        hoveredCard = null;
        pressedCard = null;
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
            ForceWorldZ(view.transform, cardWorldZ);
            int restSortingOrder = baseCardSortingOrder + i * SortingStride;
            int safeHoverBoost = Mathf.Max(hoverSortingBoost, cards.Count * SortingStride + 200);

            view.Bind(cards[i], controller, state);
            view.SetWorldDepthFromHand(cardWorldZ);
            view.SetRestState(view.transform.localPosition, view.transform.localScale, restSortingOrder, hoverLift, hoverScale, safeHoverBoost);

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

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            SetHoveredCard(null);
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        CombatWorldCardView bestCard = FindBestCardAt(pointerPosition);
        SetHoveredCard(bestCard);
        RefreshCardVisualOrder();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressedCard = bestCard;
            if (pressedCard != null)
            {
                pressedCard.PressFromHand(pointerPosition);
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (pressedCard != null)
            {
                pressedCard.ReleaseFromHand(pointerPosition);
            }

            pressedCard = null;
        }
    }

    private CombatWorldCardView FindBestCardAt(Vector2 screenPosition)
    {
        CombatWorldCardView bestCard = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < cardViews.Count; i++)
        {
            CombatWorldCardView cardView = cardViews[i];
            if (cardView == null || !cardView.ContainsScreenPoint(screenPosition))
            {
                continue;
            }

            float score = cardView.GetHandPickScore(screenPosition);
            if (score > bestScore)
            {
                bestScore = score;
                bestCard = cardView;
            }
        }

        return bestCard;
    }

    public bool IsPointerOverAnyCard(Vector2 screenPosition)
    {
        return FindBestCardAt(screenPosition) != null;
    }

    private void SetHoveredCard(CombatWorldCardView cardView)
    {
        if (hoveredCard == cardView) return;

        if (hoveredCard != null)
        {
            hoveredCard.SetHoveredFromHand(false);
        }

        hoveredCard = cardView;

        if (hoveredCard != null)
        {
            hoveredCard.SetHoveredFromHand(true);
        }
    }

    private void RefreshCardVisualOrder()
    {
        for (int i = 0; i < cardViews.Count; i++)
        {
            CombatWorldCardView cardView = cardViews[i];
            if (cardView == null) continue;

            bool isFocused = cardView == hoveredCard;
            cardView.SetWorldDepthFromHand(isFocused ? cardWorldZ - 1f : cardWorldZ);
            cardView.SetHoveredFromHand(isFocused);
        }
    }

    private void HideSceneTemplateCard()
    {
        if (cardPrefab == null) return;

        Transform prefabTransform = cardPrefab.transform;
        if (prefabTransform == transform || prefabTransform.IsChildOf(transform))
        {
            cardPrefab.gameObject.SetActive(false);
        }
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

        return new Vector3(x, y, 0f);
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
