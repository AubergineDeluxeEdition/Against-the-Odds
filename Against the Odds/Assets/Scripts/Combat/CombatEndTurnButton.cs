using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class CombatEndTurnButton : MonoBehaviour
{
    private const float ClickMoveTolerancePixels = 12f;

    [SerializeField] private CombatWorldController controller;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private string playerTurnLabel = "Fin du tour";
    [SerializeField] private string discardLabel = "Defausse";
    [SerializeField] private string disabledLabel = "Tour boss";
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.65f, 1f);
    [SerializeField] private int buttonSortingOrder = 220;
    [SerializeField] private int labelSortingOrder = 221;

    private Collider2D buttonCollider;
    private bool pressStartedOnButton;
    private Vector2 pressScreenPosition;
    private bool isHovered;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider2D>();

        if (controller == null)
        {
            controller = FindAnyObjectByType<CombatWorldController>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
        }

        ApplySorting();
    }

    private void Update()
    {
        bool canEndTurn = controller != null && controller.CanEndTurn();
        UpdateVisual(canEndTurn);

        Mouse mouse = Mouse.current;
        if (mouse == null || buttonCollider == null || !canEndTurn) return;

        Vector2 pointerPosition = mouse.position.ReadValue();
        bool pointerOverButton = IsPointerOverButton(pointerPosition);
        isHovered = pointerOverButton;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressStartedOnButton = pointerOverButton;
            pressScreenPosition = pointerPosition;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            bool isClick = pressStartedOnButton
                && pointerOverButton
                && Vector2.Distance(pressScreenPosition, pointerPosition) <= ClickMoveTolerancePixels;

            pressStartedOnButton = false;

            if (isClick)
            {
                controller.EndTurn();
            }
        }
    }

    private void UpdateVisual(bool canEndTurn)
    {
        if (labelText != null)
        {
            if (controller != null && controller.IsWaitingForDiscard())
            {
                int count = controller.CardsToDiscard();
                labelText.text = count > 1 ? discardLabel + " x" + count : discardLabel;
            }
            else
            {
                labelText.text = canEndTurn ? playerTurnLabel : disabledLabel;
            }
        }

        if (targetRenderer != null)
        {
            targetRenderer.color = canEndTurn
                ? (isHovered ? hoverColor : enabledColor)
                : disabledColor;
        }

        ApplySorting();
    }

    private void ApplySorting()
    {
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder = buttonSortingOrder;
        }

        if (labelText != null && labelText.TryGetComponent(out Renderer labelRenderer))
        {
            labelRenderer.sortingOrder = labelSortingOrder;
        }
    }

    private bool IsPointerOverButton(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        Vector2 worldPoint = new Vector2(worldPosition.x, worldPosition.y);
        return buttonCollider.OverlapPoint(worldPoint);
    }
}
