using UnityEngine;

public class CombatSpriteFill : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private bool fillFromBottom = true;

    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int FillFromBottomId = Shader.PropertyToID("_FillFromBottom");

    private SpriteRenderer spriteRenderer;
    private Material runtimeMaterial;
    private float fillAmount = 1f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureMaterial();
        Apply();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    public void SetFill(float amount)
    {
        fillAmount = Mathf.Clamp01(amount);
        EnsureMaterial();
        Apply();
    }

    private void EnsureMaterial()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = targetRenderer != null ? targetRenderer : GetComponent<SpriteRenderer>();
        }

        if (runtimeMaterial != null) return;
        if (spriteRenderer == null)
        {
            Debug.LogError("[CombatSpriteFill] No SpriteRenderer assigned or found.");
            return;
        }

        Shader shader = Shader.Find("AgainstTheOdds/SpriteVerticalFill");
        if (shader == null)
        {
            Debug.LogError("[CombatSpriteFill] Shader AgainstTheOdds/SpriteVerticalFill not found.");
            return;
        }

        runtimeMaterial = new Material(shader);
        spriteRenderer.material = runtimeMaterial;
    }

    private void Apply()
    {
        if (runtimeMaterial == null) return;

        runtimeMaterial.SetFloat(FillAmountId, fillAmount);
        runtimeMaterial.SetFloat(FillFromBottomId, fillFromBottom ? 1f : 0f);
    }
}
