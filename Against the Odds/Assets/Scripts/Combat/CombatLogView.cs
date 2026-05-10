using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombatLogView : MonoBehaviour
{
    private const string PlayerColor = "#d8b36a";
    private const string DamageColor = "#c94b3d";
    private const string BossColor = "#b74b4b";
    private const string ShieldColor = "#8fa9bd";
    private const string SystemColor = "#d8d0bd";
    private const string EffectColor = "#b8a0d8";
    private const string EndColor = "#e2d27a";

    [SerializeField] private CombatManager combatManager;
    [SerializeField] private TMP_Text logText;
    [SerializeField] private int maxLines = 8;
    [SerializeField] private bool newestLineAtBottom = true;

    [Header("Card Hover Preview")]
    [SerializeField] private GameObject cardPreviewRoot;
    [SerializeField] private TMP_Text previewNameText;
    [SerializeField] private TMP_Text previewCostText;
    [SerializeField] private TMP_Text previewTypeText;
    [SerializeField] private TMP_Text previewDescriptionText;
    [SerializeField] private SpriteRenderer previewArtworkRenderer;
    [SerializeField] private Vector2 previewArtworkMaxSize;
    [SerializeField] private bool forcePreviewSorting = true;
    [SerializeField] private int previewSortingOrder = 260;
    [SerializeField] private float linkHoverPaddingPixels = 8f;

    private readonly Queue<string> lines = new Queue<string>();
    private readonly Dictionary<string, CardDefinition> cardLinks = new Dictionary<string, CardDefinition>();
    private Vector2 resolvedPreviewArtworkMaxSize;
    private int nextCardLinkId;
    private string hoveredLinkId;
    private float debugPreviewLockedUntil;

    private void Awake()
    {
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        if (logText == null)
        {
            logText = GetComponent<TMP_Text>();
        }

        if (logText != null)
        {
            logText.richText = true;
        }

        if (previewDescriptionText != null)
        {
            previewDescriptionText.richText = true;
        }

        ResolvePreviewRoot();
        ResolvePreviewArtworkSize();
        ApplyPreviewSorting();
        HideCardPreview();
        RefreshText();
    }

    private void OnEnable()
    {
        if (combatManager == null) return;

        combatManager.OnLogMessage += AddCategorizedLine;
        combatManager.OnCardPlayed += AddCardPlayed;
        combatManager.OnBossAction += AddBossAction;
        combatManager.OnCombatEnded += AddCombatEnd;
    }

    private void OnDisable()
    {
        if (combatManager == null) return;

        combatManager.OnLogMessage -= AddCategorizedLine;
        combatManager.OnCardPlayed -= AddCardPlayed;
        combatManager.OnBossAction -= AddBossAction;
        combatManager.OnCombatEnded -= AddCombatEnd;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
        {
            ShowFirstKnownCardPreview();
            debugPreviewLockedUntil = Time.unscaledTime + 2f;
            return;
        }

        if (Time.unscaledTime < debugPreviewLockedUntil) return;

        UpdateCardHover();
    }

    public void AddLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        lines.Enqueue(line);
        while (lines.Count > maxLines)
        {
            lines.Dequeue();
        }

        RefreshText();
    }

    private void AddCategorizedLine(string message, string category)
    {
        if (category == "boss_dialogue") return;
        if (category == "player" && message.StartsWith("Vous jouez")) return;

        AddLine(ColorizeWithCardLink(message, GetColorForMessage(message, category)));
    }

    private void AddCardPlayed(CardDefinition card)
    {
        if (card == null) return;

        string linkId = "card_" + nextCardLinkId++;
        cardLinks[linkId] = card;
        string cardName = EscapeRichText(card.name);
        AddLine($"<color={PlayerColor}>Vous jouez <link=\"{linkId}\"><u>{cardName}</u></link></color>");
    }

    private void AddBossAction(string action)
    {
        AddLine(Colorize("Action: " + action, ShieldColor));
    }

    private void AddCombatEnd(bool victory)
    {
        AddLine(Colorize(victory ? "Victoire" : "Defaite", EndColor));
    }

    private void RefreshText()
    {
        if (logText == null) return;

        string[] entries = lines.ToArray();
        if (!newestLineAtBottom)
        {
            System.Array.Reverse(entries);
        }

        logText.text = string.Join("\n", entries);
        logText.ForceMeshUpdate();
    }

    private void UpdateCardHover()
    {
        if (logText == null || cardPreviewRoot == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            HideCardPreview();
            return;
        }

        Camera camera = logText.canvas != null && logText.canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : Camera.main;

        if (!TryFindHoveredCardLink(mouse.position.ReadValue(), camera, out string linkId))
        {
            hoveredLinkId = null;
            HideCardPreview();
            return;
        }

        if (hoveredLinkId == linkId && cardPreviewRoot.activeSelf) return;

        hoveredLinkId = linkId;
        if (cardLinks.TryGetValue(linkId, out CardDefinition card))
        {
            ShowCardPreview(card);
        }
        else
        {
            HideCardPreview();
        }
    }

    private bool TryFindHoveredCardLink(Vector2 screenPosition, Camera camera, out string linkId)
    {
        linkId = null;
        logText.ForceMeshUpdate();

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(logText, screenPosition, camera);
        if (linkIndex >= 0 && linkIndex < logText.textInfo.linkCount)
        {
            linkId = logText.textInfo.linkInfo[linkIndex].GetLinkID();
            return cardLinks.ContainsKey(linkId);
        }

        for (int i = 0; i < logText.textInfo.linkCount; i++)
        {
            TMP_LinkInfo linkInfo = logText.textInfo.linkInfo[i];
            if (!IsPointerOverLink(linkInfo, screenPosition, camera))
            {
                continue;
            }

            string candidateId = linkInfo.GetLinkID();
            if (!cardLinks.ContainsKey(candidateId))
            {
                continue;
            }

            linkId = candidateId;
            return true;
        }

        return false;
    }

    private bool IsPointerOverLink(TMP_LinkInfo linkInfo, Vector2 screenPosition, Camera camera)
    {
        int firstCharacter = linkInfo.linkTextfirstCharacterIndex;
        int lastCharacter = firstCharacter + linkInfo.linkTextLength;
        for (int i = firstCharacter; i < lastCharacter && i < logText.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = logText.textInfo.characterInfo[i];
            if (!character.isVisible) continue;

            Vector3 bottomLeft = logText.transform.TransformPoint(character.bottomLeft);
            Vector3 topRight = logText.transform.TransformPoint(character.topRight);
            Vector2 screenBottomLeft = RectTransformUtility.WorldToScreenPoint(camera, bottomLeft);
            Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(camera, topRight);

            Rect characterRect = Rect.MinMaxRect(
                Mathf.Min(screenBottomLeft.x, screenTopRight.x) - linkHoverPaddingPixels,
                Mathf.Min(screenBottomLeft.y, screenTopRight.y) - linkHoverPaddingPixels,
                Mathf.Max(screenBottomLeft.x, screenTopRight.x) + linkHoverPaddingPixels,
                Mathf.Max(screenBottomLeft.y, screenTopRight.y) + linkHoverPaddingPixels);

            if (characterRect.Contains(screenPosition))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowCardPreview(CardDefinition card)
    {
        cardPreviewRoot.SetActive(true);
        EnsurePreviewVisible();

        SetPreviewText(previewNameText, card.name);
        CombatState state = combatManager != null ? combatManager.State : null;
        SetPreviewText(previewCostText, new CardInstance(card).GetEffectiveCost(state).ToString());
        SetPreviewText(previewTypeText, LocalizeType(card.type));
        SetPreviewText(previewDescriptionText, CardTextFormatter.FormatDescription(new CardInstance(card), state));

        if (previewArtworkRenderer != null)
        {
            Sprite artwork = LoadArtworkSprite(card.artResourcePath);
            previewArtworkRenderer.sprite = artwork;
            previewArtworkRenderer.enabled = artwork != null;
            previewArtworkRenderer.color = Color.white;
            FitPreviewArtwork(artwork);
        }

        ApplyPreviewSorting();
    }

    private void HideCardPreview()
    {
        if (Time.unscaledTime < debugPreviewLockedUntil) return;

        if (cardPreviewRoot != null)
        {
            cardPreviewRoot.SetActive(false);
        }
    }

    private static Sprite LoadArtworkSprite(string artResourcePath)
    {
        if (string.IsNullOrWhiteSpace(artResourcePath)) return null;

        Sprite artwork = Resources.Load<Sprite>(artResourcePath);
        if (artwork != null) return artwork;

        Sprite[] sprites = Resources.LoadAll<Sprite>(artResourcePath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private CardDefinition FindKnownCardInText(string text)
    {
        CombatState state = combatManager != null ? combatManager.State : null;
        if (state == null || string.IsNullOrWhiteSpace(text)) return null;

        CardDefinition card = FindCardContainedInText(state.Hand, text);
        if (card != null) return card;

        card = FindCardContainedInText(state.DrawPile, text);
        if (card != null) return card;

        card = FindCardContainedInText(state.DiscardPile, text);
        if (card != null) return card;

        card = FindCardContainedInText(state.ExhaustedPile, text);
        if (card != null) return card;

        return state.ActiveTerrain != null && text.Contains(state.ActiveTerrain.name)
            ? state.ActiveTerrain
            : null;
    }

    private void ShowFirstKnownCardPreview()
    {
        CombatState state = combatManager != null ? combatManager.State : null;
        CardDefinition card = null;

        if (state != null)
        {
            card = FirstCard(state.Hand)
                ?? FirstCard(state.DrawPile)
                ?? FirstCard(state.DiscardPile)
                ?? FirstCard(state.ExhaustedPile)
                ?? state.ActiveTerrain;
        }

        if (card == null)
        {
            Debug.LogWarning("[CombatLogView] No card available for preview debug.");
            return;
        }

        Debug.Log($"[CombatLogView] Debug preview: {card.name}");
        ShowCardPreview(card);
    }

    private static CardDefinition FirstCard(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0) return null;
        return cards[0]?.Definition;
    }

    private static CardDefinition FindCardContainedInText(IReadOnlyList<CardInstance> cards, string text)
    {
        if (cards == null) return null;

        for (int i = 0; i < cards.Count; i++)
        {
            CardDefinition definition = cards[i]?.Definition;
            if (definition != null && text.Contains(definition.name))
            {
                return definition;
            }
        }

        return null;
    }

    private void FitPreviewArtwork(Sprite artwork)
    {
        if (previewArtworkRenderer == null) return;

        previewArtworkRenderer.transform.localScale = Vector3.one;
        if (artwork == null) return;

        Vector2 spriteSize = artwork.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        Vector2 targetSize = resolvedPreviewArtworkMaxSize;
        if (targetSize.x <= 0f || targetSize.y <= 0f)
        {
            targetSize = spriteSize;
        }

        float scaleX = targetSize.x / spriteSize.x;
        float scaleY = targetSize.y / spriteSize.y;
        float scale = Mathf.Min(scaleX, scaleY);
        previewArtworkRenderer.transform.localScale = Vector3.one * scale;
    }

    private void ResolvePreviewArtworkSize()
    {
        resolvedPreviewArtworkMaxSize = previewArtworkMaxSize;
        if (resolvedPreviewArtworkMaxSize.x > 0f && resolvedPreviewArtworkMaxSize.y > 0f) return;
        if (previewArtworkRenderer == null || previewArtworkRenderer.sprite == null) return;

        Vector2 spriteSize = previewArtworkRenderer.sprite.bounds.size;
        Vector3 localScale = previewArtworkRenderer.transform.localScale;
        resolvedPreviewArtworkMaxSize = new Vector2(
            Mathf.Abs(spriteSize.x * localScale.x),
            Mathf.Abs(spriteSize.y * localScale.y));
    }

    private void ApplyPreviewSorting()
    {
        if (!forcePreviewSorting || cardPreviewRoot == null) return;

        SpriteRenderer[] spriteRenderers = cardPreviewRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].sortingOrder = previewSortingOrder + i;
        }

        TMP_Text[] textRenderers = cardPreviewRoot.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textRenderer in textRenderers)
        {
            ApplyTextSorting(textRenderer, previewSortingOrder + spriteRenderers.Length + 10);
        }

        ApplyTextSorting(previewNameText, previewSortingOrder + spriteRenderers.Length + 11);
        ApplyTextSorting(previewCostText, previewSortingOrder + spriteRenderers.Length + 12);
        ApplyTextSorting(previewTypeText, previewSortingOrder + spriteRenderers.Length + 13);
        ApplyTextSorting(previewDescriptionText, previewSortingOrder + spriteRenderers.Length + 20);
    }

    private void EnsurePreviewVisible()
    {
        if (cardPreviewRoot == null) return;

        Transform previewTransform = cardPreviewRoot.transform;
        Vector3 position = previewTransform.position;
        position.z = 0f;
        previewTransform.position = position;

        SpriteRenderer[] spriteRenderers = cardPreviewRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            ForceWorldZ(spriteRenderer.transform, 0f);
            spriteRenderer.enabled = true;
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }

        TMP_Text[] textRenderers = cardPreviewRoot.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textRenderer in textRenderers)
        {
            ForceWorldZ(textRenderer.transform, 0f);
            textRenderer.enabled = true;
            Color color = textRenderer.color;
            color.a = 1f;
            textRenderer.color = color;

            MeshRenderer meshRenderer = textRenderer.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
        }
    }

    private static void SetPreviewText(TMP_Text textRenderer, string value)
    {
        if (textRenderer == null) return;

        textRenderer.gameObject.SetActive(true);
        textRenderer.enabled = true;
        textRenderer.text = value ?? string.Empty;

        Color color = textRenderer.color;
        color.a = 1f;
        textRenderer.color = color;

        MeshRenderer meshRenderer = textRenderer.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
        }

        textRenderer.ForceMeshUpdate();
    }

    private static void ApplyTextSorting(TMP_Text textRenderer, int sortingOrder)
    {
        if (textRenderer == null) return;

        MeshRenderer meshRenderer = textRenderer.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = sortingOrder;
        }
    }

    private static void ForceWorldZ(Transform target, float z)
    {
        if (target == null) return;

        Vector3 position = target.position;
        position.z = z;
        target.position = position;
    }

    private void ResolvePreviewRoot()
    {
        Transform commonRoot = null;
        MergePreviewRoot(previewNameText != null ? previewNameText.transform : null, ref commonRoot);
        MergePreviewRoot(previewCostText != null ? previewCostText.transform : null, ref commonRoot);
        MergePreviewRoot(previewTypeText != null ? previewTypeText.transform : null, ref commonRoot);
        MergePreviewRoot(previewDescriptionText != null ? previewDescriptionText.transform : null, ref commonRoot);
        MergePreviewRoot(previewArtworkRenderer != null ? previewArtworkRenderer.transform : null, ref commonRoot);

        if (commonRoot == null) return;

        if (cardPreviewRoot == null || !IsAncestor(cardPreviewRoot.transform, commonRoot))
        {
            cardPreviewRoot = commonRoot.gameObject;
        }
    }

    private static void MergePreviewRoot(Transform candidate, ref Transform commonRoot)
    {
        if (candidate == null) return;

        commonRoot = commonRoot == null
            ? candidate
            : FindCommonAncestor(commonRoot, candidate);
    }

    private static Transform FindCommonAncestor(Transform first, Transform second)
    {
        Transform current = first;
        while (current != null)
        {
            if (IsAncestor(current, second))
            {
                return current;
            }

            current = current.parent;
        }

        return first;
    }

    private static bool IsAncestor(Transform possibleAncestor, Transform child)
    {
        Transform current = child;
        while (current != null)
        {
            if (current == possibleAncestor)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static string GetColorForMessage(string message, string category)
    {
        if (category == "boss") return BossColor;
        if (category == "player") return PlayerColor;
        if (category == "system") return SystemColor;

        string lower = message.ToLowerInvariant();
        if (lower.Contains("degat") || lower.Contains("attaque") || lower.Contains("brulure")) return DamageColor;
        if (lower.Contains("shield") || lower.Contains("bouclier") || lower.Contains("blocage") || lower.Contains("bloque")) return ShieldColor;
        if (lower.Contains("mana") || lower.Contains("pioche") || lower.Contains("defausse")) return EffectColor;
        return SystemColor;
    }

    private static string Colorize(string text, string color)
    {
        return $"<color={color}>{EscapeRichText(text)}</color>";
    }

    private string ColorizeWithCardLink(string text, string color)
    {
        if (TryCreateCardLinkForMessage(text, out string linkedText))
        {
            return $"<color={color}>{linkedText}</color>";
        }

        return Colorize(text, color);
    }

    private bool TryCreateCardLinkForMessage(string message, out string linkedText)
    {
        linkedText = null;
        if (string.IsNullOrWhiteSpace(message)) return false;

        int separatorIndex = message.LastIndexOf(':');
        if (separatorIndex < 0 || separatorIndex >= message.Length - 1) return false;

        string prefix = message.Substring(0, separatorIndex + 1);
        string cardName = message.Substring(separatorIndex + 1).Trim();
        if (string.IsNullOrWhiteSpace(cardName)) return false;

        CardDefinition card = FindKnownCardByName(cardName);
        if (card == null) return false;

        string linkId = "card_" + nextCardLinkId++;
        cardLinks[linkId] = card;
        linkedText = $"{EscapeRichText(prefix)} <link=\"{linkId}\"><u>{EscapeRichText(card.name)}</u></link>";
        return true;
    }

    private CardDefinition FindKnownCardByName(string cardName)
    {
        CombatState state = combatManager != null ? combatManager.State : null;
        if (state == null) return null;

        CardDefinition card = FindCardByName(state.Hand, cardName);
        if (card != null) return card;

        card = FindCardByName(state.DrawPile, cardName);
        if (card != null) return card;

        card = FindCardByName(state.DiscardPile, cardName);
        if (card != null) return card;

        card = FindCardByName(state.ExhaustedPile, cardName);
        if (card != null) return card;

        return state.ActiveTerrain != null && state.ActiveTerrain.name == cardName
            ? state.ActiveTerrain
            : null;
    }

    private static CardDefinition FindCardByName(IReadOnlyList<CardInstance> cards, string cardName)
    {
        if (cards == null) return null;

        for (int i = 0; i < cards.Count; i++)
        {
            CardDefinition definition = cards[i]?.Definition;
            if (definition != null && definition.name == cardName)
            {
                return definition;
            }
        }

        return null;
    }

    private static string EscapeRichText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        StringBuilder builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            switch (character)
            {
                case '<':
                    builder.Append(' ');
                    break;
                case '>':
                    builder.Append(' ');
                    break;
                case '&':
                    builder.Append("and");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
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
