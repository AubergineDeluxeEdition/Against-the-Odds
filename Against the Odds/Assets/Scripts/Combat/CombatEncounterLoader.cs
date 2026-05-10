using AgainstTheOdds.Core;
using UnityEngine;

public class CombatEncounterLoader : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] private CombatManager combatManager;

    private void Awake()
    {
        BossEncounterConfig encounter = GetEncounter();
        if (encounter == null)
        {
            Debug.LogError("[CombatEncounterLoader] No encounter selected. Load this scene from a campaign pin.");
            return;
        }

        ApplyEncounter(encounter);
    }

    private BossEncounterConfig GetEncounter()
    {
        if (GameManager.Instance != null && GameManager.Instance.SelectedEncounter != null)
        {
            return GameManager.Instance.SelectedEncounter;
        }
        return null;
    }

    private void ApplyEncounter(BossEncounterConfig encounter)
    {
        if (combatManager != null)
        {
            combatManager.Configure(encounter);
        }

        if (AudioManager.Instance != null && encounter.combatMusic != null)
        {
            AudioManager.Instance.PlayMusic(encounter.combatMusic, encounter.fadeInCombatMusic);
        }
    }
}
