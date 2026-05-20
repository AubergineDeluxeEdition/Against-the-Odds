using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class CombatResultButton : MonoBehaviour
{
    public enum ResultButtonAction
    {
        ContinueAfterVictory,
        EndAdventure
    }

    private const float ClickMoveTolerancePixels = 12f;

    [SerializeField] private CombatResultOverlay resultOverlay;
    [SerializeField] private ResultButtonAction action;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Color enabledColor = Color.white;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.86f, 0.55f, 1f);
    [SerializeField] private int sortingOrder = 10040;

    private Collider2D buttonCollider;
    private bool pressStartedOnButton;
    private Vector2 pressScreenPosition;

    public ResultButtonAction Action => action;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider2D>();

        if (resultOverlay == null)
        {
            resultOverlay = FindAnyObjectByType<CombatResultOverlay>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        ApplySorting();
    }

    private void Update()
    {
        bool interactable = resultOverlay != null && resultOverlay.CanUseButton(action);
        bool visible = resultOverlay != null && IsButtonVisible();
        SetVisible(visible);

        Mouse mouse = Mouse.current;
        if (!visible || mouse == null || buttonCollider == null)
        {
            UpdateVisual(interactable, false);
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        bool pointerBlockedByRewardCard = resultOverlay != null && resultOverlay.IsPointerOverRewardCard(pointerPosition);
        bool pointerOverButton = !pointerBlockedByRewardCard && IsPointerOverButton(pointerPosition);
        UpdateVisual(interactable, pointerOverButton);

        if (!interactable) return;

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
                resultOverlay.UseButton(action);
            }
        }
    }

    private bool IsButtonVisible()
    {
        if (resultOverlay == null || !resultOverlay.IsShowingResult) return false;

        return action == CombatResultButton.ResultButtonAction.ContinueAfterVictory
            ? resultOverlay.IsVictory
            : !resultOverlay.IsVictory;
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }

        if (buttonCollider != null)
        {
            buttonCollider.enabled = visible;
        }
    }

    private void UpdateVisual(bool interactable, bool hovered)
    {
        if (targetRenderer == null) return;

        targetRenderer.color = interactable
            ? hovered ? hoverColor : enabledColor
            : disabledColor;
    }

    private void ApplySorting()
    {
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder = sortingOrder;
        }

        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            meshRenderer.sortingOrder = sortingOrder + 1;
        }
    }

    public void Configure(CombatResultOverlay overlay, ResultButtonAction buttonAction)
    {
        resultOverlay = overlay;
        action = buttonAction;

        if (buttonCollider == null)
        {
            buttonCollider = GetComponent<Collider2D>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        ApplySorting();
    }

    private bool IsPointerOverButton(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        return buttonCollider.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
    }
}
