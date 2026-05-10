using AgainstTheOdds.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Plays a configurable sound when the object is clicked.
/// Works with UI EventSystem clicks and with world-space Collider2D objects.
/// </summary>
public class ClickSfx : MonoBehaviour, IPointerClickHandler
{
    private const float ClickMoveTolerancePixels = 12f;

    [SerializeField] private AudioClip clickSfx;
    [SerializeField] private AudioSource fallbackAudioSource;
    [SerializeField] private bool useCollider2DClick = true;
    [SerializeField] private bool playWhenDisabled;

    private Collider2D targetCollider;
    private bool pressStartedOnCollider;
    private Vector2 pressScreenPosition;
    private float lastPlayTime;

    private void Awake()
    {
        targetCollider = GetComponent<Collider2D>();
        if (fallbackAudioSource == null)
        {
            fallbackAudioSource = GetComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (!useCollider2DClick || targetCollider == null) return;
        if (!playWhenDisabled && (!isActiveAndEnabled || !targetCollider.enabled)) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 pointerPosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressStartedOnCollider = IsPointerOverCollider(pointerPosition);
            pressScreenPosition = pointerPosition;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            bool isClick = pressStartedOnCollider
                && Vector2.Distance(pressScreenPosition, pointerPosition) <= ClickMoveTolerancePixels
                && IsPointerOverCollider(pointerPosition);

            pressStartedOnCollider = false;

            if (isClick)
            {
                Play();
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Play();
    }

    public void Play()
    {
        if (clickSfx == null) return;

        // Prevent double fire when an object has both UI raycast and Collider2D click paths.
        if (Time.unscaledTime - lastPlayTime < 0.05f) return;
        lastPlayTime = Time.unscaledTime;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clickSfx);
            return;
        }

        if (fallbackAudioSource != null)
        {
            fallbackAudioSource.PlayOneShot(clickSfx);
        }
    }

    private bool IsPointerOverCollider(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        return targetCollider.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
    }
}
