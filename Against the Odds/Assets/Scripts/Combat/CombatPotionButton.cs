using AgainstTheOdds.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class CombatPotionButton : MonoBehaviour
{
    private const float ClickMoveTolerancePixels = 12f;

    [SerializeField] private CombatManager combatManager;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.8f, 0.8f, 1f);
    [SerializeField] private int iconSortingOrder = 330;
    [SerializeField] private int countSortingOrder = 331;

    private Collider2D potionCollider;
    private bool pressStartedOnPotion;
    private Vector2 pressScreenPosition;
    private bool isHovered;

    private void Awake()
    {
        potionCollider = GetComponent<Collider2D>();

        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (countText == null)
        {
            countText = GetComponentInChildren<TMP_Text>(true);
        }

        ApplySorting();
    }

    private void OnEnable()
    {
        if (combatManager == null) return;

        combatManager.OnStateChanged += Refresh;
        combatManager.OnPotionsChanged += Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        bool canUsePotion = CanUsePotion();
        UpdateVisual(canUsePotion);

        Mouse mouse = Mouse.current;
        if (mouse == null || potionCollider == null || !canUsePotion) return;

        Vector2 pointerPosition = mouse.position.ReadValue();
        bool pointerOverPotion = IsPointerOverPotion(pointerPosition);
        isHovered = pointerOverPotion;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressStartedOnPotion = pointerOverPotion;
            pressScreenPosition = pointerPosition;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            bool isClick = pressStartedOnPotion
                && pointerOverPotion
                && Vector2.Distance(pressScreenPosition, pointerPosition) <= ClickMoveTolerancePixels;

            pressStartedOnPotion = false;

            if (isClick)
            {
                combatManager.TryUsePotion();
            }
        }
    }

    private void OnDisable()
    {
        if (combatManager == null) return;

        combatManager.OnStateChanged -= Refresh;
        combatManager.OnPotionsChanged -= Refresh;
    }

    private void Refresh()
    {
        int potionCount = GameManager.Instance != null ? GameManager.Instance.PotionCount : 0;

        if (countText != null)
        {
            countText.text = "x" + potionCount;
        }

        UpdateVisual(CanUsePotion());
    }

    private bool CanUsePotion()
    {
        return combatManager != null
            && combatManager.State != null
            && GameManager.Instance != null
            && GameManager.Instance.PotionCount > 0
            && combatManager.State.PlayerHP < combatManager.State.PlayerMaxHP;
    }

    private void UpdateVisual(bool canUsePotion)
    {
        if (targetRenderer != null)
        {
            targetRenderer.color = canUsePotion
                ? (isHovered ? hoverColor : enabledColor)
                : disabledColor;
        }

        ApplySorting();
    }

    private void ApplySorting()
    {
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder = iconSortingOrder;
        }

        if (countText != null && countText.TryGetComponent(out Renderer countRenderer))
        {
            countRenderer.sortingOrder = countSortingOrder;
        }
    }

    private bool IsPointerOverPotion(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        Vector2 worldPoint = new Vector2(worldPosition.x, worldPosition.y);
        return potionCollider.OverlapPoint(worldPoint);
    }
}
