using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombatWorldController : MonoBehaviour
{
    private enum FillAxis { Horizontal, Vertical }
    private enum FillAnchor { NegativeSide, PositiveSide }

    [Header("Logic")]
    [SerializeField] private CombatManager combatManager;

    [Header("Boss")]
    [SerializeField] private SpriteRenderer bossSpriteRenderer;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text bossHpText;
    [SerializeField] private SpriteRenderer bossHpFill;
    [SerializeField] private SpriteRenderer bossHpInvulnerableOverlay;
    [SerializeField] private Color bossInvulnerableHpTint = new Color(1f, 0.76f, 0.18f, 0.55f);
    [SerializeField] private FillAxis bossHpAxis = FillAxis.Horizontal;
    [SerializeField] private FillAnchor bossHpAnchor = FillAnchor.NegativeSide;
    [SerializeField] private TMP_Text bossShieldText;
    [SerializeField] private SpriteRenderer bossShieldFill;
    [SerializeField] private GameObject bossInvulnerableIcon;
    [SerializeField] private GameObject bossVulnerableIcon;
    [SerializeField] private GameObject bossStunnedIcon;

    [Header("Player")]
    [SerializeField] private TMP_Text playerHpText;
    [SerializeField] private SpriteRenderer playerHpFill;
    [SerializeField] private FillAxis playerHpAxis = FillAxis.Vertical;
    [SerializeField] private FillAnchor playerHpAnchor = FillAnchor.NegativeSide;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private SpriteRenderer manaFill;
    [SerializeField] private FillAxis manaFillAxis = FillAxis.Vertical;
    [SerializeField] private FillAnchor manaFillAnchor = FillAnchor.NegativeSide;
    [SerializeField] private bool useShaderFillForMana = true;
    [SerializeField] private TMP_Text playerShieldText;
    [SerializeField] private SpriteRenderer playerShieldIcon;

    [Header("Render Order")]
    [SerializeField] private int playerHudSortingOrder = 320;

    [Header("Cards")]
    [SerializeField] private CombatWorldHandView handView;
    [SerializeField] private Transform handRoot;
    [SerializeField] private CombatWorldCardView cardPrefab;
    [SerializeField] private float cardSpacing = 1.75f;

    [Header("Optional Feedback")]
    [SerializeField] private TMP_Text combatLogText;
    [SerializeField] private TMP_Text endTurnHintText;
    [SerializeField] private TMP_Text bossDialogueText;
    [SerializeField] private TMP_Text bossActionText;

    [Header("End Feedback")]
    [SerializeField] private float bossDeathDisappearDuration = 1.15f;
    [SerializeField] private float bossDeathFlickerInterval = 0.08f;
    [SerializeField] private float bossDeathShrinkMultiplier = 0.08f;

    private readonly List<CombatWorldCardView> cardViews = new List<CombatWorldCardView>();
    private Vector3 bossHpInitialScale;
    private Vector3 bossHpInitialPosition;
    private Vector3 bossVisualInitialScale = Vector3.one;
    private Vector3 bossShieldInitialScale;
    private Vector3 bossShieldInitialPosition;
    private Vector3 playerHpInitialScale;
    private Vector3 playerHpInitialPosition;
    private Vector3 manaFillInitialScale;
    private Vector3 manaFillInitialPosition;
    private bool combatLogHandledByDedicatedView;
    private Coroutine bossDeathRoutine;

    private void Awake()
    {
        if (combatManager == null)
        {
            combatManager = GetComponent<CombatManager>();
        }

        if (bossHpFill != null)
        {
            bossHpInitialScale = bossHpFill.transform.localScale;
            bossHpInitialPosition = bossHpFill.transform.localPosition;
        }

        if (bossHpInvulnerableOverlay != null)
        {
            bossHpInvulnerableOverlay.gameObject.SetActive(false);
            bossHpInvulnerableOverlay.color = bossInvulnerableHpTint;
        }

        if (bossSpriteRenderer != null)
        {
            bossVisualInitialScale = bossSpriteRenderer.transform.localScale;
            bossSpriteRenderer.sprite = null;
            bossSpriteRenderer.enabled = false;
        }

        if (bossShieldFill != null)
        {
            bossShieldInitialScale = bossShieldFill.transform.localScale;
            bossShieldInitialPosition = bossShieldFill.transform.localPosition;
        }

        if (playerHpFill != null)
        {
            playerHpInitialScale = playerHpFill.transform.localScale;
            playerHpInitialPosition = playerHpFill.transform.localPosition;
        }

        if (manaFill != null)
        {
            EnsureManaUsesSpriteFill();
            manaFillInitialScale = manaFill.transform.localScale;
            manaFillInitialPosition = manaFill.transform.localPosition;
        }

        ApplyPlayerHudSorting();

        combatLogHandledByDedicatedView = combatLogText != null
            && combatLogText.GetComponentInParent<CombatLogView>() != null;
    }

    private void OnEnable()
    {
        if (combatManager == null) return;

        combatManager.OnStateChanged += Refresh;
        combatManager.OnCombatEnded += OnCombatEnded;
        combatManager.OnLogMessage += OnLogMessage;
        combatManager.OnDiscardRequested += OnDiscardRequested;
        combatManager.OnBossDialogue += OnBossDialogue;
        combatManager.OnBossAction += OnBossAction;
        combatManager.OnBossVisualChanged += OnBossVisualChanged;
    }

    private void Start()
    {
        if (combatManager == null)
        {
            Debug.LogError("[CombatWorldController] CombatManager is missing.");
            return;
        }

        combatManager.StartCombat();
        Refresh();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            EndTurn();
        }
    }

    private void OnDisable()
    {
        if (combatManager == null) return;

        combatManager.OnStateChanged -= Refresh;
        combatManager.OnCombatEnded -= OnCombatEnded;
        combatManager.OnLogMessage -= OnLogMessage;
        combatManager.OnDiscardRequested -= OnDiscardRequested;
        combatManager.OnBossDialogue -= OnBossDialogue;
        combatManager.OnBossAction -= OnBossAction;
        combatManager.OnBossVisualChanged -= OnBossVisualChanged;
    }

    public void EndTurn()
    {
        if (!CanEndTurn()) return;

        combatManager.EndTurn();
    }

    public bool CanEndTurn()
    {
        return combatManager != null
            && combatManager.State != null
            && combatManager.CurrentPhase == TurnManager.TurnPhase.PlayerTurn
            && !combatManager.State.WaitingForDiscard;
    }

    public bool IsWaitingForDiscard()
    {
        return combatManager != null
            && combatManager.State != null
            && combatManager.State.WaitingForDiscard;
    }

    public int CardsToDiscard()
    {
        return IsWaitingForDiscard() ? combatManager.State.CardsToDiscard : 0;
    }

    public bool IsPlayerTurn()
    {
        return combatManager != null
            && combatManager.State != null
            && combatManager.CurrentPhase == TurnManager.TurnPhase.PlayerTurn;
    }

    public void TryPlayCard(CardInstance card)
    {
        if (card == null || combatManager == null || combatManager.State == null) return;

        if (combatManager.State.WaitingForDiscard)
        {
            combatManager.ResolveDiscard(card);
            return;
        }

        combatManager.TryPlayCard(card);
    }

    private void Refresh()
    {
        if (combatManager == null || combatManager.State == null) return;

        CombatState state = combatManager.State;

        SetAnchoredBar(bossHpFill, bossHpInitialScale, bossHpInitialPosition, state.EnemyHP, state.EnemyMaxHP, bossHpAxis, bossHpAnchor);
        SetAnchoredBar(playerHpFill, playerHpInitialScale, playerHpInitialPosition, state.PlayerHP, state.PlayerMaxHP, playerHpAxis, playerHpAnchor);
        SetAnchoredBar(bossShieldFill, bossShieldInitialScale, bossShieldInitialPosition, state.EnemyShield, state.EnemyMaxHP, bossHpAxis, bossHpAnchor);
        SetAnchoredBar(manaFill, manaFillInitialScale, manaFillInitialPosition, state.CurrentMana, state.MaxMana, manaFillAxis, manaFillAnchor);
        ApplyPlayerHudSorting();

        if (bossHpText != null) bossHpText.text = state.EnemyHP + " / " + state.EnemyMaxHP;
        if (bossShieldText != null) bossShieldText.text = state.EnemyShield > 0 ? state.EnemyShield.ToString() : string.Empty;
        if (playerHpText != null) playerHpText.text = state.PlayerHP.ToString();
        if (manaText != null) manaText.text = state.CurrentMana.ToString();
        if (playerShieldText != null) playerShieldText.text = state.PlayerBlock > 0 ? state.PlayerBlock.ToString() : string.Empty;
        if (playerShieldIcon != null) playerShieldIcon.gameObject.SetActive(state.PlayerBlock > 0);
        if (bossShieldFill != null) bossShieldFill.gameObject.SetActive(state.EnemyShield > 0);
        if (bossInvulnerableIcon != null) bossInvulnerableIcon.SetActive(state.EnemyInvulnerableTurns > 0);
        if (bossHpInvulnerableOverlay != null)
        {
            bool bossIsInvulnerable = state.EnemyInvulnerableTurns > 0;
            bossHpInvulnerableOverlay.gameObject.SetActive(bossIsInvulnerable);
            bossHpInvulnerableOverlay.color = bossInvulnerableHpTint;
            SetAnchoredBar(bossHpInvulnerableOverlay, bossHpInitialScale, bossHpInitialPosition, state.EnemyHP, state.EnemyMaxHP, bossHpAxis, bossHpAnchor);
        }
        if (bossVulnerableIcon != null) bossVulnerableIcon.SetActive(state.EnemyVulnerableTurns > 0);
        if (bossStunnedIcon != null) bossStunnedIcon.SetActive(state.EnemyStunnedTurns > 0);

        if (bossNameText != null)
        {
            bossNameText.text = combatManager.BossDisplayName;
        }

        if (endTurnHintText != null)
        {
            endTurnHintText.text = state.WaitingForDiscard
                ? "Choisis " + state.CardsToDiscard + " carte(s) a defausser"
                : combatManager.CurrentPhase == TurnManager.TurnPhase.PlayerTurn
                ? "Space: end turn"
                : "Boss turn";
        }

        RebuildHand(state);
        ApplyPlayerHudSorting();
    }

    private void RebuildHand(CombatState state)
    {
        if (combatManager != null && combatManager.CombatEnded)
        {
            ClearHand();
            return;
        }

        if (handView != null)
        {
            handView.Rebuild(state.Hand, this, state);
            return;
        }

        if (handRoot == null || cardPrefab == null) return;

        for (int i = 0; i < cardViews.Count; i++)
        {
            if (cardViews[i] != null)
            {
                Destroy(cardViews[i].gameObject);
            }
        }
        cardViews.Clear();

        float startX = -((state.Hand.Count - 1) * cardSpacing) * 0.5f;
        for (int i = 0; i < state.Hand.Count; i++)
        {
            CombatWorldCardView view = Instantiate(cardPrefab, handRoot);
            view.transform.localPosition = new Vector3(startX + i * cardSpacing, 0f, 0f);
            view.Bind(state.Hand[i], this, state);
            cardViews.Add(view);
        }
    }

    private void ClearHand()
    {
        if (handView != null)
        {
            handView.Clear();
        }

        for (int i = 0; i < cardViews.Count; i++)
        {
            if (cardViews[i] != null)
            {
                Destroy(cardViews[i].gameObject);
            }
        }

        cardViews.Clear();
    }

    private void ApplyPlayerHudSorting()
    {
        ApplyHudGroupSorting(playerHpFill, playerHpText, playerHudSortingOrder);
        ApplyHudGroupSorting(manaFill, manaText, playerHudSortingOrder);
        ApplyStatusIconSorting(playerShieldIcon, playerShieldText, playerHudSortingOrder + 10);
    }

    private static void ApplyHudGroupSorting(SpriteRenderer fillRenderer, TMP_Text text, int baseSortingOrder)
    {
        Transform root = FindVisualGroupRoot(fillRenderer != null ? fillRenderer.transform : null);
        if (root != null)
        {
            root.gameObject.SetActive(true);
            Vector3 rootPosition = root.position;
            rootPosition.z = 0f;
            root.position = rootPosition;

            foreach (SpriteRenderer spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.enabled = true;
                spriteRenderer.sortingOrder = baseSortingOrder;
            }

            foreach (TMP_Text childText in root.GetComponentsInChildren<TMP_Text>(true))
            {
                childText.enabled = true;
                if (childText.TryGetComponent(out Renderer childTextRenderer))
                {
                    childTextRenderer.enabled = true;
                    childTextRenderer.sortingOrder = baseSortingOrder + 2;
                }
            }
        }

        if (fillRenderer != null)
        {
            fillRenderer.enabled = true;
            fillRenderer.sortingOrder = baseSortingOrder + 1;
        }

        if (text != null && text.TryGetComponent(out Renderer textRenderer))
        {
            text.enabled = true;
            textRenderer.enabled = true;
            textRenderer.sortingOrder = baseSortingOrder + 2;
        }
    }

    private static void ApplyStatusIconSorting(SpriteRenderer iconRenderer, TMP_Text text, int baseSortingOrder)
    {
        if (iconRenderer != null)
        {
            iconRenderer.sortingOrder = baseSortingOrder;
        }

        if (text != null && text.TryGetComponent(out Renderer textRenderer))
        {
            textRenderer.sortingOrder = baseSortingOrder + 1;
        }
    }

    private static Transform FindVisualGroupRoot(Transform start)
    {
        if (start == null) return null;

        Transform current = start.parent;
        while (current != null)
        {
            if (current.GetComponent<SpriteRenderer>() != null)
            {
                return current;
            }

            current = current.parent;
        }

        return start;
    }

    private static void SetAnchoredBar(
        SpriteRenderer fillRenderer,
        Vector3 initialScale,
        Vector3 initialPosition,
        int current,
        int max,
        FillAxis axis,
        FillAnchor anchor)
    {
        if (fillRenderer == null || max <= 0) return;

        float ratio = Mathf.Clamp01((float)current / max);
        Transform fill = fillRenderer.transform;
        CombatSpriteFill spriteFill = fill.GetComponent<CombatSpriteFill>();
        if (spriteFill != null)
        {
            fill.localScale = initialScale;
            fill.localPosition = initialPosition;
            spriteFill.SetFill(ratio);
            return;
        }

        Vector2 localSize = GetFillLocalSize(fillRenderer);
        float direction = anchor == FillAnchor.NegativeSide ? -1f : 1f;

        if (axis == FillAxis.Horizontal)
        {
            float lostWidth = localSize.x * initialScale.x * (1f - ratio);
            fill.localScale = new Vector3(initialScale.x * ratio, initialScale.y, initialScale.z);
            fill.localPosition = new Vector3(
                initialPosition.x + direction * lostWidth * 0.5f,
                initialPosition.y,
                initialPosition.z);
            return;
        }

        fill.localScale = new Vector3(initialScale.x, initialScale.y * ratio, initialScale.z);
        float lostHeight = localSize.y * initialScale.y * (1f - ratio);
        fill.localPosition = new Vector3(
            initialPosition.x,
            initialPosition.y + direction * lostHeight * 0.5f,
            initialPosition.z);
    }

    private static Vector2 GetFillLocalSize(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer.sprite.bounds.size;
        }

        return Vector2.one;
    }

    private void EnsureManaUsesSpriteFill()
    {
        if (!useShaderFillForMana || manaFill == null) return;
        if (manaFill.GetComponent<CombatSpriteFill>() != null) return;

        manaFill.gameObject.AddComponent<CombatSpriteFill>();
    }

    private void OnCombatEnded(bool victory)
    {
        ClearHand();
        if (victory)
        {
            PlayBossDeathDisappearance();
        }

        if (combatLogHandledByDedicatedView) return;

        if (combatLogText != null)
        {
            combatLogText.text = victory ? "Victoire" : "Defaite";
        }
    }

    private void OnLogMessage(string message, string category)
    {
        if (combatLogHandledByDedicatedView) return;

        if (combatLogText != null)
        {
            combatLogText.text = message;
        }
    }

    private void OnDiscardRequested(int count)
    {
        if (combatLogHandledByDedicatedView) return;

        if (combatLogText != null)
        {
            combatLogText.text = "Choisis " + count + " carte(s) a defausser";
        }
    }

    private void OnBossDialogue(string line)
    {
        if (bossDialogueText != null)
        {
            bossDialogueText.text = line;
        }
    }

    private void OnBossAction(string action)
    {
        if (bossActionText != null)
        {
            bossActionText.text = action;
        }
    }

    private void OnBossVisualChanged(Sprite sprite, float scale)
    {
        if (bossSpriteRenderer == null) return;

        if (bossDeathRoutine != null)
        {
            StopCoroutine(bossDeathRoutine);
            bossDeathRoutine = null;
        }

        bossSpriteRenderer.sprite = sprite;
        bossSpriteRenderer.enabled = sprite != null;
        bossSpriteRenderer.transform.localScale = bossVisualInitialScale * Mathf.Max(0.01f, scale);
        bossSpriteRenderer.color = Color.white;
    }

    private void PlayBossDeathDisappearance()
    {
        if (bossSpriteRenderer == null || bossSpriteRenderer.sprite == null) return;

        if (bossDeathRoutine != null)
        {
            StopCoroutine(bossDeathRoutine);
        }

        bossDeathRoutine = StartCoroutine(BossDeathDisappearanceRoutine());
    }

    private IEnumerator BossDeathDisappearanceRoutine()
    {
        Color startColor = bossSpriteRenderer.color;
        Vector3 startScale = bossSpriteRenderer.transform.localScale;
        Vector3 endScale = startScale * Mathf.Max(0.01f, bossDeathShrinkMultiplier);
        float duration = Mathf.Max(0.01f, bossDeathDisappearDuration);
        float flicker = Mathf.Max(0.01f, bossDeathFlickerInterval);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            bool visibleFrame = Mathf.FloorToInt(elapsed / flicker) % 2 == 0;

            bossSpriteRenderer.enabled = visibleFrame;
            bossSpriteRenderer.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            bossSpriteRenderer.color = color;
            yield return null;
        }

        bossSpriteRenderer.enabled = false;
        bossSpriteRenderer.color = startColor;
        bossSpriteRenderer.transform.localScale = startScale;
        bossDeathRoutine = null;
    }
}
