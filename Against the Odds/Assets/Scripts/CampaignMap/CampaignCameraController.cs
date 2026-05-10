using System.Collections;
using System.Collections.Generic;
using AgainstTheOdds.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AgainstTheOdds.CampaignMap
{
    [RequireComponent(typeof(Camera))]
    public class CampaignCameraController : MonoBehaviour
    {
        [Header("Bounds")]
        [SerializeField] private float minX = 0f;
        [SerializeField] private float maxX = 0f;
        [SerializeField] private float minY = 0f;
        [SerializeField] private float maxY = 40f;
        [SerializeField] private bool lockXWhenBoundsAreZero = true;

        [Header("Manual Movement")]
        [SerializeField] private float smoothTime = 0.12f;

        [Header("Pin Navigation")]
        [SerializeField] private bool enablePinNavigation = true;
        [SerializeField] private float focusSmoothTime = 0.28f;
        [SerializeField] private float focusArrivalDistance = 0.02f;
        [SerializeField] private float focusMaxDuration = 2f;
        [SerializeField] private float directionalConeDegrees = 65f;
        [SerializeField] private float navigationCooldown = 0.06f;
        [SerializeField] private Vector2 pinFocusOffset = Vector2.zero;
        [SerializeField] private bool focusCurrentBossOnStart = true;

        private Camera campaignCamera;
        private Vector2 targetPosition;
        private Vector2 smoothVelocity;
        private bool userInputEnabled = true;
        private bool isFocusing;
        private float nextNavigationTime;
        private Coroutine focusRoutine;
        private BossPin focusedPin;

        private void Awake()
        {
            campaignCamera = GetComponent<Camera>();
            if (!campaignCamera.orthographic)
            {
                Debug.LogWarning("[CampaignCameraController] Campaign camera forced to orthographic.");
                campaignCamera.orthographic = true;
            }
        }

        private void Start()
        {
            targetPosition = ClampPosition(transform.position);
            ApplyPosition(targetPosition);

            if (focusCurrentBossOnStart)
            {
                FocusCurrentBoss();
            }
        }

        private void Update()
        {
            if (userInputEnabled)
            {
                ReadInputs();
            }

            targetPosition = ClampPosition(targetPosition);
            float activeSmoothTime = isFocusing ? focusSmoothTime : smoothTime;
            Vector2 currentPosition = transform.position;
            Vector2 newPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref smoothVelocity, activeSmoothTime);
            ApplyPosition(newPosition);
        }

        public void FocusOnPin(Transform pinTransform)
        {
            if (pinTransform == null)
            {
                Debug.LogWarning("[CampaignCameraController] FocusOnPin called with null transform.");
                return;
            }

            focusedPin = pinTransform.GetComponent<BossPin>();
            FocusOnWorldPosition(pinTransform.position);
        }

        public void FocusOnBossIndex(int bossIndex)
        {
            BossPin pin = CampaignPinRegistry.GetPin(bossIndex);
            if (pin == null)
            {
                Debug.LogWarning($"[CampaignCameraController] No BossPin found for index {bossIndex}.");
                return;
            }

            focusedPin = pin;
            FocusOnWorldPosition(pin.transform.position);
        }

        private void ReadInputs()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (mouse != null)
            {
                ReadMouse(mouse);
            }

            if (keyboard != null)
            {
                ReadKeyboard(keyboard);
            }
        }

        private void ReadMouse(Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (!enablePinNavigation || Mathf.Abs(scroll) <= 0.01f || Time.unscaledTime < nextNavigationTime)
            {
                return;
            }

            Vector2 direction = scroll > 0f ? Vector2.up : Vector2.down;
            int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scroll) / 120f));
            bool moved = false;
            for (int i = 0; i < steps; i++)
            {
                moved |= FocusPinInDirection(direction);
            }

            if (moved)
            {
                nextNavigationTime = Time.unscaledTime + navigationCooldown;
            }
        }

        private void ReadKeyboard(Keyboard keyboard)
        {
            Vector2 direction = Vector2.zero;

            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) direction.y += 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) direction.y -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction.x += 1f;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction.x -= 1f;

            if (direction == Vector2.zero || !enablePinNavigation || Time.unscaledTime < nextNavigationTime)
            {
                return;
            }

            if (IsNavigationPressed(keyboard) && FocusPinInDirection(direction.normalized))
            {
                nextNavigationTime = Time.unscaledTime + navigationCooldown;
            }
        }

        private bool IsNavigationPressed(Keyboard keyboard)
        {
            return keyboard.upArrowKey.wasPressedThisFrame
                || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame
                || keyboard.rightArrowKey.wasPressedThisFrame
                || keyboard.wKey.wasPressedThisFrame
                || keyboard.aKey.wasPressedThisFrame
                || keyboard.sKey.wasPressedThisFrame
                || keyboard.dKey.wasPressedThisFrame;
        }

        private bool FocusPinInDirection(Vector2 direction)
        {
            IReadOnlyList<BossPin> pins = CampaignPinRegistry.GetAllPins();
            if (pins.Count == 0)
            {
                return false;
            }

            if (focusedPin == null)
            {
                focusedPin = FindNearestPin(pins, transform.position);
            }
            Vector2 origin = focusedPin != null ? (Vector2)focusedPin.transform.position : (Vector2)transform.position;
            BossPin bestPin = null;
            float bestScore = float.PositiveInfinity;
            float minDot = Mathf.Cos(directionalConeDegrees * Mathf.Deg2Rad);

            foreach (BossPin pin in pins)
            {
                if (pin == null || pin == focusedPin) continue;

                Vector2 toPin = (Vector2)pin.transform.position - origin;
                float distance = toPin.magnitude;
                if (distance <= 0.001f) continue;

                Vector2 toPinDirection = toPin / distance;
                float dot = Vector2.Dot(direction, toPinDirection);
                if (dot < minDot) continue;

                float alignmentPenalty = (1f - dot) * 20f;
                float score = distance + alignmentPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPin = pin;
                }
            }

            if (bestPin == null)
            {
                return false;
            }

            focusedPin = bestPin;
            FocusOnWorldPosition(bestPin.transform.position);
            return true;
        }

        private BossPin FindNearestPin(IReadOnlyList<BossPin> pins, Vector2 position)
        {
            BossPin nearestPin = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (BossPin pin in pins)
            {
                if (pin == null) continue;

                float distance = Vector2.Distance(position, pin.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPin = pin;
                }
            }

            return nearestPin;
        }

        private void FocusCurrentBoss()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[CampaignCameraController] GameManager missing. Start the game from 00_Bootstrap for campaign focus.");
                return;
            }

            FocusOnBossIndex(GameManager.Instance.CurrentBossIndex);
        }

        private void FocusOnWorldPosition(Vector2 worldPosition)
        {
            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
            }

            focusRoutine = StartCoroutine(FocusCoroutine(worldPosition + pinFocusOffset));
        }

        private IEnumerator FocusCoroutine(Vector2 worldPosition)
        {
            userInputEnabled = true;
            isFocusing = true;
            smoothVelocity = Vector2.zero;

            Vector2 end = ClampPosition(worldPosition);
            targetPosition = end;
            float elapsed = 0f;

            if (focusSmoothTime <= 0f)
            {
                ApplyPosition(end);
            }
            else
            {
                while (Vector2.Distance(transform.position, end) > focusArrivalDistance && elapsed < focusMaxDuration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            ApplyPosition(end);
            targetPosition = end;
            isFocusing = false;
            userInputEnabled = true;
            focusRoutine = null;
        }

        private Vector2 ClampPosition(Vector2 position)
        {
            bool lockX = lockXWhenBoundsAreZero && Mathf.Approximately(minX, maxX);
            float x = lockX ? transform.position.x : Mathf.Clamp(position.x, minX, maxX);
            float y = Mathf.Clamp(position.y, minY, maxY);
            return new Vector2(x, y);
        }

        private void ApplyPosition(Vector2 position)
        {
            Vector3 current = transform.position;
            transform.position = new Vector3(position.x, position.y, current.z);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = transform.position;
            Gizmos.DrawLine(new Vector3(p.x - 5f, minY, p.z), new Vector3(p.x + 5f, minY, p.z));
            Gizmos.DrawLine(new Vector3(p.x - 5f, maxY, p.z), new Vector3(p.x + 5f, maxY, p.z));
            Gizmos.DrawLine(new Vector3(minX, p.y - 5f, p.z), new Vector3(minX, p.y + 5f, p.z));
            Gizmos.DrawLine(new Vector3(maxX, p.y - 5f, p.z), new Vector3(maxX, p.y + 5f, p.z));
        }
#endif
    }
}
