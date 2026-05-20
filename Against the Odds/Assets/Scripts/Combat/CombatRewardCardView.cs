using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class CombatRewardCardView : MonoBehaviour
{
    private const float ClickMoveTolerancePixels = 12f;
    private const float MinimumHoverScale = 2f;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private SpriteRenderer artworkRenderer;
    [SerializeField] private SpriteRenderer selectedRenderer;
    [SerializeField] private SpriteRenderer unavailableRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.86f, 0.45f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.95f, 0.72f, 1f);
    [SerializeField] private Color dimmedColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
    [SerializeField] private float hoverScale = MinimumHoverScale;
    [SerializeField] private int sortingOrder = 10020;
    [SerializeField] private int hoverSortingBoost = 2000;

    private CardDefinition card;
    private Action<CombatRewardCardView> clicked;
    private Collider2D cardCollider;
    private bool selected;
    private bool interactable;
    private bool selectionContextActive;
    private bool hovered;
    private bool externalHoverMode;
    private bool pressStartedOnCard;
    private Vector2 pressScreenPosition;
    private Vector3 restScale;

    public CardDefinition Card => card;
    public bool IsSelected => selected;

    public void ForceSortingOrder(int order, int textOrderOffset = 2)
    {
        sortingOrder = order;
        ApplySorting(textOrderOffset);
    }

    private void Awake()
    {
        cardCollider = GetComponent<Collider2D>();
        restScale = transform.localScale;
        hoverScale = Mathf.Max(hoverScale, MinimumHoverScale);
        AutoBindMissingReferences();
        ApplySorting();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hoverScale = Mathf.Max(hoverScale, MinimumHoverScale);
    }
#endif

    private void Update()
    {
        if (externalHoverMode) return;
        if (!interactable || card == null || cardCollider == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 pointerPosition = mouse.position.ReadValue();
        bool pointerOverCard = IsPointerOverCard(pointerPosition);
        if (hovered != pointerOverCard)
        {
            hovered = pointerOverCard;
            RefreshVisual();
        }

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

            if (isClick)
            {
                clicked?.Invoke(this);
            }
        }
    }

    public void Bind(CardDefinition definition, Action<CombatRewardCardView> onClicked)
    {
        card = definition;
        clicked = onClicked;
        selected = false;
        interactable = card != null;
        selectionContextActive = false;
        hovered = false;
        gameObject.SetActive(card != null);

        if (card == null) return;

        if (nameText != null) nameText.text = card.name;
        if (costText != null) costText.text = card.cost.ToString();
        if (typeText != null) typeText.text = LocalizeType(card.type);
        if (descriptionText != null) descriptionText.text = CardTextFormatter.FormatDescription(card);

        ApplyArtwork(card.artResourcePath);
        RefreshVisual();
        ApplySorting();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        RefreshVisual();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        RefreshVisual();
    }

    public void SetSelectionContext(bool hasSelection)
    {
        selectionContextActive = hasSelection;
        RefreshVisual();
    }

    public void SetExternalHoverMode(bool value)
    {
        externalHoverMode = value;
        if (!externalHoverMode) return;

        pressStartedOnCard = false;
    }

    public void SetHoveredFromOwner(bool value)
    {
        if (hovered == value) return;

        hovered = value;
        RefreshVisual();
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        if (!interactable || card == null || cardCollider == null) return false;

        Camera camera = Camera.main;
        if (camera == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        return cardCollider.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
    }

    public float GetScreenPickScore(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return float.NegativeInfinity;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        float horizontalDistance = Mathf.Abs(worldPosition.x - transform.position.x);
        float verticalDistance = Mathf.Abs(worldPosition.y - transform.position.y) * 0.25f;
        return -(horizontalDistance + verticalDistance) + sortingOrder * 0.0001f;
    }

    public float CurrentScaleMultiplier
    {
        get
        {
            if (Mathf.Approximately(restScale.x, 0f)) return 1f;
            return transform.localScale.x / restScale.x;
        }
    }

    private void RefreshVisual()
    {
        transform.localScale = restScale * (hovered ? hoverScale : 1f);
        ApplySorting();

        if (selectedRenderer != null)
        {
            selectedRenderer.gameObject.SetActive(selected);
            selectedRenderer.color = selectedColor;
        }

        if (unavailableRenderer != null)
        {
            unavailableRenderer.gameObject.SetActive(selectionContextActive && !selected && !hovered);
            unavailableRenderer.color = dimmedColor;
        }

        Color tint = GetCurrentTint();
        ApplyTint(tint);
    }

    private Color GetCurrentTint()
    {
        if (selected) return selectedColor;
        if (hovered) return hoverColor;
        if (selectionContextActive) return dimmedColor;

        return normalColor;
    }

    private void ApplyTint(Color tint)
    {
        SpriteRenderer root = GetComponent<SpriteRenderer>();
        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer == selectedRenderer || spriteRenderer == unavailableRenderer) continue;

            spriteRenderer.color = tint;
        }

        if (root != null)
        {
            root.color = tint;
        }

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            string objectName = text.name.ToLowerInvariant();
            if (objectName.Contains("count") || objectName.Contains("quantity"))
            {
                continue;
            }

            Color textColor = text.color;
            textColor.a = Mathf.Clamp01(tint.a);
            text.color = textColor;
        }
    }

    private void ApplyArtwork(string artResourcePath)
    {
        if (artworkRenderer == null) return;

        Sprite artwork = LoadArtworkSprite(artResourcePath);
        artworkRenderer.sprite = artwork;
        artworkRenderer.enabled = artwork != null;
    }

    private static Sprite LoadArtworkSprite(string artResourcePath)
    {
        if (string.IsNullOrWhiteSpace(artResourcePath)) return null;

        Sprite artwork = Resources.Load<Sprite>(artResourcePath);
        if (artwork != null) return artwork;

        Sprite[] sprites = Resources.LoadAll<Sprite>(artResourcePath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private void ApplySorting(int textOrderOffset = 2)
    {
        SpriteRenderer root = GetComponent<SpriteRenderer>();
        int sortingLayerId = root != null ? root.sortingLayerID : 0;
        int currentSortingOrder = hovered ? sortingOrder + hoverSortingBoost : sortingOrder;

        if (root != null)
        {
            root.sortingOrder = currentSortingOrder + 2;
        }

        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderer.sortingLayerID = sortingLayerId;
            if (spriteRenderer == root)
            {
                continue;
            }

            if (spriteRenderer == artworkRenderer)
            {
                spriteRenderer.sortingOrder = currentSortingOrder;
            }
            else if (spriteRenderer == selectedRenderer || spriteRenderer == unavailableRenderer)
            {
                spriteRenderer.sortingOrder = currentSortingOrder + 8;
            }
            else
            {
                spriteRenderer.sortingOrder = currentSortingOrder + 2;
            }
        }

        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            meshRenderer.sortingLayerID = sortingLayerId;
            meshRenderer.sortingOrder = currentSortingOrder + Mathf.Max(10, textOrderOffset);
        }
    }

    private bool IsPointerOverCard(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        return cardCollider.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
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

    private void AutoBindMissingReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            string objectName = text.name.ToLowerInvariant();
            if (nameText == null && objectName.Contains("name"))
            {
                nameText = text;
                continue;
            }

            if (costText == null && objectName.Contains("cost"))
            {
                costText = text;
                continue;
            }

            if (typeText == null && objectName.Contains("type"))
            {
                typeText = text;
                continue;
            }

            if (descriptionText == null && (objectName.Contains("description") || objectName.Contains("desc")))
            {
                descriptionText = text;
            }
        }

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sprite in sprites)
        {
            string objectName = sprite.name.ToLowerInvariant();
            if (artworkRenderer == null && (objectName.Contains("image") || objectName.Contains("art")))
            {
                artworkRenderer = sprite;
                continue;
            }

            if (selectedRenderer == null && (objectName.Contains("select") || objectName.Contains("highlight")))
            {
                selectedRenderer = sprite;
                continue;
            }

            if (unavailableRenderer == null && (objectName.Contains("disabled") || objectName.Contains("unavailable")))
            {
                unavailableRenderer = sprite;
            }
        }
    }
}
