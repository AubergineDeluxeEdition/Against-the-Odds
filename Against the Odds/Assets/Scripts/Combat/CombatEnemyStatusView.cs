using TMPro;
using UnityEngine;

public class CombatEnemyStatusView : MonoBehaviour
{
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private string statusId = "burn";
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private int sortingOrder = 160;

    private void Awake()
    {
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }
    }

    private void OnEnable()
    {
        if (combatManager != null)
        {
            combatManager.OnStateChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (combatManager != null)
        {
            combatManager.OnStateChanged -= Refresh;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        int stacks = GetStacks();
        bool visible = stacks > 0;

        if (visualRoot != null && visualRoot != gameObject)
        {
            visualRoot.SetActive(visible);
        }

        if (iconRenderer != null)
        {
            iconRenderer.enabled = visible;
        }

        Renderer countRenderer = countText != null ? countText.GetComponent<Renderer>() : null;
        if (countRenderer != null)
        {
            countRenderer.enabled = visible;
        }

        if (!visible) return;

        if (countText != null)
        {
            countText.text = stacks.ToString();
        }

        ApplySorting();
    }

    private int GetStacks()
    {
        if (combatManager == null || combatManager.State == null) return 0;

        return combatManager.State.EnemyStatuses.TryGetValue(statusId, out int stacks)
            ? stacks
            : 0;
    }

    private void ApplySorting()
    {
        if (iconRenderer != null)
        {
            iconRenderer.sortingOrder = sortingOrder;
        }

        if (countText != null && countText.TryGetComponent(out Renderer textRenderer))
        {
            textRenderer.sortingOrder = sortingOrder + 1;
        }
    }
}
