using AgainstTheOdds.Core;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class CombatBossVisual : MonoBehaviour
{
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private bool hideWhenNoConfiguredSprite = true;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        ApplySprite(GetCurrentEncounter()?.bossSprite);
    }

    private void OnEnable()
    {
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        if (combatManager != null)
        {
            combatManager.OnBossSpriteChanged += ApplySprite;
            ApplySprite(GetCurrentEncounter()?.bossSprite);
        }
    }

    private void OnDisable()
    {
        if (combatManager != null)
        {
            combatManager.OnBossSpriteChanged -= ApplySprite;
        }
    }

    private void ApplySprite(Sprite sprite)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null) return;

        spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = sprite != null || !hideWhenNoConfiguredSprite;
    }

    private BossEncounterConfig GetCurrentEncounter()
    {
        if (combatManager != null && combatManager.Encounter != null)
        {
            return combatManager.Encounter;
        }

        return GetSelectedEncounter();
    }

    private static BossEncounterConfig GetSelectedEncounter()
    {
        if (GameManager.Instance == null)
        {
            return null;
        }

        return GameManager.Instance.SelectedEncounter;
    }
}
