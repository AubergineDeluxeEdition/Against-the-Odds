using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class CombatWorldCardView : MonoBehaviour
{
    private const float ClickMoveTolerancePixels = 12f;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private SpriteRenderer artworkRenderer;
    [SerializeField] private SpriteRenderer highlightRenderer;
    [SerializeField] private Color playableColor = Color.white;
    [SerializeField] private Color unplayableColor = new Color(1f, 1f, 1f, 0.45f);

    private CardInstance card;
    private CombatWorldController controller;
    private Vector3 restLocalPosition;
    private Vector3 restLocalScale;
    private int restSortingOrder;
    private float hoverLift;
    private float hoverScale = 1f;
    private int hoverSortingBoost;
    private bool isHovered;
    private bool pressStartedOnCard;
    private Vector2 pressScreenPosition;
    private Collider2D cardCollider;
    private bool previewOnly;

    private void Awake()
    {
        cardCollider = GetComponent<Collider2D>();
        AutoAssignMissingReferences();
    }

    private void AutoAssignMissingReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            string objectName = text.gameObject.name.ToLowerInvariant();

            if (costText == null && (objectName.Contains("cost") || objectName.Contains("mana")))
            {
                costText = text;
                continue;
            }

            if (descriptionText == null && (objectName.Contains("description") || objectName.Contains("desc") || objectName.Contains("effect")))
            {
                descriptionText = text;
                continue;
            }

            if (typeText == null && objectName.Contains("type"))
            {
                typeText = text;
                continue;
            }

            if (nameText == null && (objectName.Contains("name") || objectName.Contains("title")))
            {
                nameText = text;
            }
        }

        if (nameText == null)
        {
            foreach (TMP_Text text in texts)
            {
                if (text != costText && text != descriptionText && text != typeText)
                {
                    nameText = text;
                    break;
                }
            }
        }
    }

    public void Bind(CardInstance cardInstance, CombatWorldController owner, CombatState state)
    {
        previewOnly = false;
        card = cardInstance;
        controller = owner;

        if (card == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplyCardData(card, state, true);
    }

    public void BindPreview(CardDefinition definition, CombatState state)
    {
        previewOnly = true;
        controller = null;
        card = definition != null ? new CardInstance(definition) : null;

        if (card == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplyCardData(card, state, true);
    }

    private void ApplyCardData(CardInstance cardInstance, CombatState state, bool canInteractFallback)
    {
        int effectiveCost = cardInstance.GetEffectiveCost(state);
        bool canPay = state != null && state.CurrentMana >= effectiveCost;
        bool canInteract = state != null ? state.WaitingForDiscard || canPay : canInteractFallback;

        if (nameText != null) nameText.text = cardInstance.Definition.name;
        if (costText != null) costText.text = effectiveCost.ToString();
        if (typeText != null) typeText.text = LocalizeType(cardInstance.Definition.type);
        if (descriptionText != null)
        {
            descriptionText.richText = true;
            descriptionText.text = CardTextFormatter.FormatDescription(cardInstance, state);
        }

        ApplyArtwork(cardInstance.Definition.artResourcePath);
        if (highlightRenderer != null) highlightRenderer.color = canInteract ? playableColor : unplayableColor;
    }

    private void ApplyArtwork(string artResourcePath)
    {
        if (artworkRenderer == null) return;

        if (string.IsNullOrWhiteSpace(artResourcePath))
        {
            Debug.LogWarning($"[CombatWorldCardView] Missing artResourcePath for card '{card.Definition.id}'.");
            return;
        }

        Sprite artwork = LoadArtworkSprite(artResourcePath);
        if (artwork == null)
        {
            Debug.LogWarning($"[CombatWorldCardView] Missing card artwork at Resources/{artResourcePath}.");
            return;
        }

        artworkRenderer.color = Color.white;
        artworkRenderer.sprite = artwork;
    }

    private static Sprite LoadArtworkSprite(string artResourcePath)
    {
        Sprite artwork = Resources.Load<Sprite>(artResourcePath);
        if (artwork != null)
        {
            return artwork;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(artResourcePath);
        if (sprites != null && sprites.Length > 0)
        {
            return sprites[0];
        }

        return null;
    }

    public void SetRestState(
        Vector3 localPosition,
        Vector3 localScale,
        int sortingOrder,
        float hoverLiftAmount,
        float hoverScaleMultiplier,
        int hoverSortingOrderBoost)
    {
        restLocalPosition = localPosition;
        restLocalScale = localScale;
        restSortingOrder = sortingOrder;
        hoverLift = hoverLiftAmount;
        hoverScale = hoverScaleMultiplier;
        hoverSortingBoost = hoverSortingOrderBoost;

        RestoreVisualState();
    }

    public void ForceSortingOrder(int sortingOrder)
    {
        restSortingOrder = sortingOrder;
        SetSortingOrder(sortingOrder);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || cardCollider == null || card == null || previewOnly) return;

        Vector2 pointerPosition = mouse.position.ReadValue();
        bool pointerOverCard = IsPointerOverTopmostCard(pointerPosition);

        SetHovered(pointerOverCard);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressStartedOnCard = pointerOverCard;
            pressScreenPosition = pointerPosition;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            bool isClick = pressStartedOnCard
                && pointerOverCard
                && Vector2.Distance(pressScreenPosition, pointerPosition) <= ClickMoveTolerancePixels;

            pressStartedOnCard = false;

            if (isClick && controller != null)
            {
                controller.TryPlayCard(card);
            }
        }
    }

    private void SetHovered(bool hovered)
    {
        if (isHovered == hovered) return;

        isHovered = hovered;
        if (isHovered)
        {
            transform.localPosition = restLocalPosition + Vector3.up * hoverLift;
            transform.localScale = restLocalScale * hoverScale;
            SetSortingOrder(restSortingOrder + hoverSortingBoost);
        }
        else
        {
            RestoreVisualState();
        }
    }

    private void RestoreVisualState()
    {
        if (isHovered) return;

        transform.localPosition = restLocalPosition;
        transform.localScale = restLocalScale;
        SetSortingOrder(restSortingOrder);
    }

    private void SetSortingOrder(int sortingOrder)
    {
        SpriteRenderer rootSpriteRenderer = GetComponent<SpriteRenderer>();
        if (rootSpriteRenderer != null)
        {
            rootSpriteRenderer.sortingOrder = sortingOrder;
        }

        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer != rootSpriteRenderer)
            {
                spriteRenderer.sortingOrder = sortingOrder + 1;
            }
        }

        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            meshRenderer.sortingOrder = sortingOrder + 2;
        }
    }

    private bool IsPointerOverTopmostCard(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        Vector2 worldPoint = new Vector2(worldPosition.x, worldPosition.y);
        if (!cardCollider.OverlapPoint(worldPoint)) return false;

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
        int ownSortingOrder = GetSortingOrder();

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit == cardCollider) continue;

            CombatWorldCardView otherCard = hit.GetComponentInParent<CombatWorldCardView>();
            if (otherCard != null && otherCard.GetSortingOrder() > ownSortingOrder)
            {
                return false;
            }
        }

        return true;
    }

    private int GetSortingOrder()
    {
        SpriteRenderer rootSpriteRenderer = GetComponent<SpriteRenderer>();
        return rootSpriteRenderer != null ? rootSpriteRenderer.sortingOrder : restSortingOrder;
    }

    private static string LocalizeType(string type)
    {
        switch (type)
        {
            case "attack": return "Sort";
            case "defense": return "Defense";
            case "utility": return "Rituel";
            case "terrain": return "Terrain";
            default: return type;
        }
    }
}
