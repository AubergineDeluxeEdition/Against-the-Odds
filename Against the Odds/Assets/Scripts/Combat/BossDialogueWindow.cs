using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class BossDialogueWindow : MonoBehaviour
{
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private bool closeOnClick;
    [SerializeField] private bool closeOnNextPlayerTurnEnd = true;
    [SerializeField] private bool forceSorting = true;
    [SerializeField] private int sortingOrder = 280;
    [SerializeField] private float inputCloseDelay = 0.2f;

    private int openedDuringTurn = -1;
    private float openedAtTime;

    private void Awake()
    {
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        ResolveWindowRoot();
        EnsureCloseRule();
        ApplySorting();
        Close();
    }

    private void OnEnable()
    {
        if (combatManager == null) return;

        combatManager.OnBossDialogue += Open;
        combatManager.OnPlayerTurnEnded += OnPlayerTurnEnded;
    }

    private void Start()
    {
        if (combatManager != null && !string.IsNullOrWhiteSpace(combatManager.LastBossDialogue))
        {
            Open(combatManager.LastBossDialogue);
        }
    }

    private void OnDisable()
    {
        if (combatManager == null) return;

        combatManager.OnBossDialogue -= Open;
        combatManager.OnPlayerTurnEnded -= OnPlayerTurnEnded;
    }

    private void Update()
    {
        if (!closeOnClick || !IsOpen()) return;
        if (Time.unscaledTime - openedAtTime < inputCloseDelay) return;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Close();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
        {
            Close();
        }
    }

    public void Open(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        Debug.Log($"[BossDialogueWindow] Open: {line}");

        if (windowRoot != null)
        {
            windowRoot.SetActive(true);
            EnsureRootVisible(windowRoot.transform);
        }
        else
        {
            gameObject.SetActive(true);
            EnsureRootVisible(transform);
        }

        if (dialogueText != null)
        {
            dialogueText.text = line;
        }

        ApplySorting();
        openedDuringTurn = combatManager != null ? combatManager.TurnNumber : -1;
        openedAtTime = Time.unscaledTime;
    }

    public void Close()
    {
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (windowRoot != null)
        {
            windowRoot.SetActive(false);
        }
    }

    private void OnPlayerTurnEnded(int turnNumber)
    {
        if (!closeOnNextPlayerTurnEnd || !IsOpen()) return;
        if (openedDuringTurn >= 0 && turnNumber >= openedDuringTurn)
        {
            Close();
        }
    }

    private void EnsureCloseRule()
    {
        if (closeOnClick || closeOnNextPlayerTurnEnd) return;

        closeOnNextPlayerTurnEnd = true;
    }

    private bool IsOpen()
    {
        return windowRoot != null ? windowRoot.activeSelf : gameObject.activeSelf;
    }

    private void ResolveWindowRoot()
    {
        if (windowRoot != gameObject)
        {
            return;
        }

        if (transform.childCount > 0)
        {
            windowRoot = transform.GetChild(0).gameObject;
        }
        else
        {
            windowRoot = null;
        }
    }

    private void ApplySorting()
    {
        if (!forceSorting) return;

        GameObject root = windowRoot != null ? windowRoot : gameObject;
        SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].sortingOrder = sortingOrder + i;
        }

        TMP_Text[] textRenderers = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textRenderer in textRenderers)
        {
            MeshRenderer meshRenderer = textRenderer.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = sortingOrder + spriteRenderers.Length + 1;
            }
        }
    }

    private static void EnsureRootVisible(Transform root)
    {
        if (root == null) return;

        Vector3 position = root.position;
        position.z = 0f;
        root.position = position;

        SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
            spriteRenderer.enabled = true;
        }

        TMP_Text[] textRenderers = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textRenderer in textRenderers)
        {
            textRenderer.enabled = true;
            Color color = textRenderer.color;
            color.a = 1f;
            textRenderer.color = color;
        }
    }
}
