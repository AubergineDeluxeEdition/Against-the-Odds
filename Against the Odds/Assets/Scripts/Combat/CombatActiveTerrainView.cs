using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]
public class CombatActiveTerrainView : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private CombatManager combatManager;

    [Header("Terrain Slot")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Collider2D hoverCollider;
    [SerializeField] private GameObject activeRoot;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private bool clipIconToCircle = true;
    [SerializeField] private SpriteMask circleMask;
    [SerializeField] private Sprite circleMaskSprite;
    [SerializeField] private bool fitIconToParentBounds = true;
    [SerializeField, Min(0f)] private float iconMargin = 0.08f;
    [SerializeField] private Vector2 iconMaxSize = new Vector2(1f, 1f);
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.65f, 1f);
    [SerializeField] private int iconSortingOrder = 340;

    [Header("Hover Preview")]
    [SerializeField] private CombatWorldCardView previewCardView;
    [SerializeField] private GameObject previewRoot;
    [SerializeField] private int previewSortingOrder = 620;

    private CardDefinition currentTerrain;
    private Vector3 iconBaseScale = Vector3.one;
    private bool previewVisible;
    private static Sprite generatedCircleMaskSprite;

    private void Awake()
    {
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        if (iconRenderer == null)
        {
            iconRenderer = GetComponent<SpriteRenderer>();
        }

        if (hoverCollider == null)
        {
            hoverCollider = GetComponent<Collider2D>();
        }

        if (previewRoot == null && previewCardView != null)
        {
            previewRoot = previewCardView.gameObject;
        }

        if (iconRenderer != null)
        {
            iconBaseScale = iconRenderer.transform.localScale;
        }

        SetupCircleMask();
        ApplySorting();
        SetPreviewVisible(false);
    }

    private void OnEnable()
    {
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        if (combatManager != null)
        {
            combatManager.OnStateChanged += Refresh;
            combatManager.OnCombatEnded += HideOnCombatEnd;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (currentTerrain == null)
        {
            SetPreviewVisible(false);
            return;
        }

        bool isHovered = IsPointerOverSlot();
        if (iconRenderer != null)
        {
            iconRenderer.color = isHovered ? hoverColor : normalColor;
        }

        SetPreviewVisible(isHovered);
    }

    private void OnDisable()
    {
        if (combatManager != null)
        {
            combatManager.OnStateChanged -= Refresh;
            combatManager.OnCombatEnded -= HideOnCombatEnd;
        }
    }

    private void Refresh()
    {
        CombatState state = combatManager != null ? combatManager.State : null;
        CardDefinition terrain = state != null ? state.ActiveTerrain : null;
        bool hasTerrain = terrain != null;

        currentTerrain = terrain;

        if (activeRoot != null)
        {
            activeRoot.SetActive(hasTerrain);
        }

        if (emptyRoot != null)
        {
            emptyRoot.SetActive(!hasTerrain);
        }

        if (iconRenderer != null)
        {
            iconRenderer.enabled = hasTerrain;
            iconRenderer.color = normalColor;

            if (hasTerrain)
            {
                iconRenderer.sprite = LoadArtworkSprite(terrain.artResourcePath);
                FitIconToSlot();
                SetupCircleMask();
            }
            else
            {
                iconRenderer.sprite = null;
            }
        }

        if (previewCardView != null)
        {
            previewCardView.BindPreview(terrain, state);
            previewCardView.ForceSortingOrder(previewSortingOrder);
            ApplySortingToRoot(previewCardView.gameObject, previewSortingOrder);
        }

        ApplySorting();
        SetPreviewVisible(false);
    }

    private void HideOnCombatEnd(bool _)
    {
        SetPreviewVisible(false);
    }

    private bool IsPointerOverSlot()
    {
        Mouse mouse = Mouse.current;
        Camera camera = Camera.main;
        if (mouse == null || camera == null || hoverCollider == null) return false;

        Vector3 worldPosition = camera.ScreenToWorldPoint(mouse.position.ReadValue());
        return hoverCollider.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
    }

    private void SetPreviewVisible(bool visible)
    {
        previewVisible = visible;
        if (previewRoot != null)
        {
            previewRoot.SetActive(visible && currentTerrain != null);
        }
    }

    private void FitIconToSlot()
    {
        if (iconRenderer == null || iconRenderer.sprite == null) return;
        Vector2 slotSize = GetSlotSize();
        if (slotSize.x <= 0f || slotSize.y <= 0f) return;

        Vector2 spriteSize = iconRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        float scale = Mathf.Min(slotSize.x / spriteSize.x, slotSize.y / spriteSize.y);
        iconRenderer.transform.localScale = iconBaseScale * scale;

        ApplyMaskTransform(slotSize);
    }

    private void SetupCircleMask()
    {
        if (!clipIconToCircle || iconRenderer == null) return;

        if (circleMask == null)
        {
            GameObject maskObject = new GameObject(name + "_CircleMask");
            maskObject.transform.SetParent(transform.parent, false);
            circleMask = maskObject.AddComponent<SpriteMask>();
        }

        if (circleMaskSprite == null)
        {
            circleMaskSprite = GetGeneratedCircleMaskSprite();
        }

        circleMask.sprite = circleMaskSprite;
        circleMask.isCustomRangeActive = true;
        circleMask.frontSortingLayerID = iconRenderer.sortingLayerID;
        circleMask.backSortingLayerID = iconRenderer.sortingLayerID;
        circleMask.frontSortingOrder = iconSortingOrder + 1;
        circleMask.backSortingOrder = iconSortingOrder - 1;
        ApplyMaskTransform(GetSlotSize());
        iconRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
    }

    private Vector2 GetSlotSize()
    {
        if (fitIconToParentBounds)
        {
            Transform parent = iconRenderer != null ? iconRenderer.transform.parent : transform.parent;
            if (parent != null)
            {
                SpriteRenderer parentRenderer = parent.GetComponent<SpriteRenderer>();
                if (parentRenderer != null && parentRenderer.bounds.size.x > 0f && parentRenderer.bounds.size.y > 0f)
                {
                    Vector3 size = parentRenderer.bounds.size;
                    return ApplyMargin(new Vector2(size.x, size.y));
                }

                Collider2D parentCollider = parent.GetComponent<Collider2D>();
                if (parentCollider != null && parentCollider.bounds.size.x > 0f && parentCollider.bounds.size.y > 0f)
                {
                    Vector3 size = parentCollider.bounds.size;
                    return ApplyMargin(new Vector2(size.x, size.y));
                }
            }

            if (hoverCollider != null && hoverCollider.bounds.size.x > 0f && hoverCollider.bounds.size.y > 0f)
            {
                Vector3 size = hoverCollider.bounds.size;
                return ApplyMargin(new Vector2(size.x, size.y));
            }
        }

        return ApplyMargin(iconMaxSize);
    }

    private Vector2 ApplyMargin(Vector2 size)
    {
        float margin = Mathf.Max(0f, iconMargin);
        return new Vector2(
            Mathf.Max(0.01f, size.x - margin * 2f),
            Mathf.Max(0.01f, size.y - margin * 2f));
    }

    private void ApplyMaskTransform(Vector2 slotSize)
    {
        if (circleMask == null || iconRenderer == null) return;

        circleMask.transform.position = iconRenderer.transform.position;
        circleMask.transform.rotation = iconRenderer.transform.rotation;
        circleMask.transform.localScale = new Vector3(slotSize.x, slotSize.y, 1f);
    }

    private void ApplySorting()
    {
        if (iconRenderer != null)
        {
            iconRenderer.sortingOrder = iconSortingOrder;
            if (!clipIconToCircle)
            {
                iconRenderer.maskInteraction = SpriteMaskInteraction.None;
            }
        }

        if (activeRoot != null)
        {
            ApplySortingToRoot(activeRoot, iconSortingOrder);
        }

        if (emptyRoot != null)
        {
            ApplySortingToRoot(emptyRoot, iconSortingOrder);
        }

        if (previewRoot != null)
        {
            ApplySortingToRoot(previewRoot, previewSortingOrder);
        }
    }

    private static void ApplySortingToRoot(GameObject root, int sortingOrder)
    {
        if (root == null) return;

        foreach (SpriteRenderer spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderer.sortingOrder = sortingOrder;
        }

        foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            meshRenderer.sortingOrder = sortingOrder + 10;
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.TryGetComponent(out Renderer textRenderer))
            {
                textRenderer.sortingOrder = sortingOrder + 30;
            }
        }
    }

    private static Sprite GetGeneratedCircleMaskSprite()
    {
        if (generatedCircleMaskSprite != null)
        {
            return generatedCircleMaskSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Generated Terrain Circle Mask";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = (size - 2) * 0.5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedCircleMaskSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);

        generatedCircleMaskSprite.name = "GeneratedTerrainCircleMaskSprite";
        return generatedCircleMaskSprite;
    }

    private static Sprite LoadArtworkSprite(string artResourcePath)
    {
        if (string.IsNullOrWhiteSpace(artResourcePath)) return null;

        Sprite artwork = Resources.Load<Sprite>(artResourcePath);
        if (artwork != null)
        {
            return artwork;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(artResourcePath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }
}
