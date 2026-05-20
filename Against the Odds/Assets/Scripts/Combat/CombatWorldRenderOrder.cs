using TMPro;
using UnityEngine;

public class CombatWorldRenderOrder : MonoBehaviour
{
    [SerializeField] private int baseSortingOrder = 150;
    [SerializeField] private bool forceWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    private void Awake()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Apply();
    }
#endif

    private void Apply()
    {
        if (forceWorldZ)
        {
            Vector3 position = transform.position;
            position.z = worldZ;
            transform.position = position;
        }

        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer.GetComponentInParent<CombatWorldCardView>() != null)
            {
                continue;
            }

            spriteRenderer.sortingOrder = baseSortingOrder;
        }

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<CombatWorldCardView>() != null)
            {
                continue;
            }

            Renderer textRenderer = text.GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = baseSortingOrder + 1;
            }
        }
    }
}
