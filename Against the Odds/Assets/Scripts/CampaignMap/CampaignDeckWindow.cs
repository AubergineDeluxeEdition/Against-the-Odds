using System.Collections.Generic;
using System.Linq;
using AgainstTheOdds.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AgainstTheOdds.CampaignMap
{
    public class CampaignDeckWindow : MonoBehaviour
    {
        [Header("Window")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private bool hideOnAwake = true;

        [Header("Card Database")]
        [SerializeField] private string fallbackCardDatabaseResourcePath = "Data/Deck";

        [Header("Slots")]
        [Tooltip("Drop the card GameObjects displayed in the deck window here.")]
        [SerializeField] private GameObject[] cardSlotObjects;
        [SerializeField] private CombatRewardCardView[] cardSlots;
        [Tooltip("Optional quantity labels matching the card slot order.")]
        [SerializeField] private TMP_Text[] countTexts;
        [SerializeField] private string countFormat = "x{0}";

        [Header("Dynamic Rows")]
        [SerializeField] private Transform rowsRoot;
        [SerializeField] private GameObject rowTemplate;
        [SerializeField] private int cardsPerRow = 3;
        [SerializeField] private float rowVerticalSpacing = 1.8f;
        [SerializeField] private bool cloneRowsFromTemplate = true;

        [Header("Pagination")]
        [SerializeField] private int rowsPerPage = 2;
        [SerializeField] private GameObject previousPageArrow;
        [SerializeField] private GameObject nextPageArrow;
        [SerializeField] private bool autoFindPageArrows = true;
        [SerializeField] private bool anchorPageArrowsToCamera = true;
        [SerializeField] private Vector2 previousArrowViewportPosition = new Vector2(0.08f, 0.5f);
        [SerializeField] private Vector2 nextArrowViewportPosition = new Vector2(0.92f, 0.5f);
        [SerializeField] private float arrowClickMoveTolerancePixels = 12f;

        [Header("Render Order")]
        [SerializeField] private bool forceRenderOrder = true;
        [SerializeField] private int panelSortingOrder = 12000;
        [SerializeField] private int cardSortingOrder = 12100;
        [SerializeField] private int textSortingOrder = 12300;
        [SerializeField] private int arrowSortingOrder = 12450;
        [SerializeField] private int focusedCardTextSortingOrder = 30000;

        private DeckDefinition cardDatabase;
        private readonly List<GameObject> generatedRows = new List<GameObject>();
        private readonly List<GameObject> detectedWindowParts = new List<GameObject>();
        private bool closedAtStartup;
        private bool isOpen;
        private int currentPageIndex;
        private int totalPageCount = 1;
        private Collider2D previousPageCollider;
        private Collider2D nextPageCollider;
        private PageArrow pressedArrow;
        private PageArrow hoveredArrow;
        private Vector2 arrowPressScreenPosition;
        private CombatRewardCardView hoveredSlot;
        private readonly Dictionary<GameObject, Vector3> arrowBaseScales = new Dictionary<GameObject, Vector3>();
        private readonly Dictionary<GameObject, Color> arrowBaseColors = new Dictionary<GameObject, Color>();
        private readonly Dictionary<TMP_Text, Vector3> countTextBaseScales = new Dictionary<TMP_Text, Vector3>();
        private readonly Dictionary<TMP_Text, Vector3> countTextBasePositions = new Dictionary<TMP_Text, Vector3>();

        private enum PageArrow
        {
            None,
            Previous,
            Next
        }

        private void Awake()
        {
            if (windowRoot == null)
            {
                windowRoot = gameObject;
            }

            AutoBindSlots();

            if (hideOnAwake)
            {
                Close();
                closedAtStartup = true;
            }
        }

        private void Start()
        {
            if (hideOnAwake && !closedAtStartup)
            {
                Close();
                closedAtStartup = true;
            }
        }

        public void Open()
        {
            isOpen = true;
            SetWindowVisible(true);
            Rebuild();
            ApplyRenderOrder();
        }

        public void Close()
        {
            isOpen = false;
            HideSlots();
            SetWindowVisible(false);
            SetArrowVisibility();
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
                return;
            }

            Open();
        }

        public void Rebuild()
        {
            AutoBindSlots();

            cardDatabase = LoadCardDatabase();
            if (cardDatabase?.cards == null)
            {
                HideSlots();
                return;
            }

            Dictionary<string, int> counts = BuildCurrentDeckCounts();
            var cardById = cardDatabase.cards
                .Where(card => card != null && !string.IsNullOrWhiteSpace(card.id))
                .GroupBy(card => card.id)
                .ToDictionary(group => group.Key, group => group.First());
            var cardsToShow = counts.Keys
                .Where(cardId => cardById.ContainsKey(cardId))
                .Select(cardId => cardById[cardId])
                .ToList();

            int cardsPerPage = GetCardsPerPage();
            totalPageCount = Mathf.Max(1, Mathf.CeilToInt((float)cardsToShow.Count / cardsPerPage));
            currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPageCount - 1);

            EnsureEnoughSlots(Mathf.Min(cardsToShow.Count, cardsPerPage));
            var pageCards = cardsToShow
                .Skip(currentPageIndex * cardsPerPage)
                .Take(cardsPerPage)
                .ToList();

            for (int i = 0; i < cardSlots.Length; i++)
            {
                CombatRewardCardView slot = cardSlots[i];
                CardDefinition card = i < pageCards.Count ? pageCards[i] : null;

                if (slot != null)
                {
                    slot.Bind(card, null);
                    slot.SetInteractable(card != null);
                    slot.SetSelectionContext(false);
                    slot.SetExternalHoverMode(true);
                    slot.SetHoveredFromOwner(false);
                    int slotSortingOrder = cardSortingOrder + i * 10;
                    slot.ForceSortingOrder(slotSortingOrder, Mathf.Max(2, textSortingOrder - slotSortingOrder));
                }

                if (i < countTexts.Length && countTexts[i] != null)
                {
                    bool hasCard = card != null;
                    countTexts[i].gameObject.SetActive(hasCard);
                    countTexts[i].text = hasCard ? string.Format(countFormat, counts[card.id]) : string.Empty;
                    CacheCountTextBaseScale(countTexts[i]);
                }
            }

            SetArrowVisibility();
            ApplyRenderOrder();
        }

        public void NextPage()
        {
            if (!isOpen || currentPageIndex >= totalPageCount - 1) return;

            currentPageIndex++;
            Rebuild();
        }

        public void PreviousPage()
        {
            if (!isOpen || currentPageIndex <= 0) return;

            currentPageIndex--;
            Rebuild();
        }

        private void Update()
        {
            if (!isOpen) return;

            ReadCardHover();
            ReadArrowHover();
            ReadArrowClicks();
        }

        private Dictionary<string, int> BuildCurrentDeckCounts()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[CampaignDeckWindow] GameManager absent. Impossible d'afficher le deck courant du joueur.");
                return new Dictionary<string, int>();
            }

            IEnumerable<string> cardIds = GameManager.Instance.BuildRunDeckCardIds();

            var counts = new Dictionary<string, int>();
            foreach (string cardId in cardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId)) continue;

                if (!counts.ContainsKey(cardId))
                {
                    counts[cardId] = 0;
                }

                counts[cardId]++;
            }

            return counts;
        }

        private DeckDefinition LoadCardDatabase()
        {
            string resourcePath = GameManager.Instance != null && !string.IsNullOrWhiteSpace(GameManager.Instance.CardDatabaseResourcePath)
                ? GameManager.Instance.CardDatabaseResourcePath
                : fallbackCardDatabaseResourcePath;

            return DeckLoader.Load(resourcePath);
        }

        private void AutoBindSlots()
        {
            detectedWindowParts.Clear();

            if (cloneRowsFromTemplate && rowTemplate != null)
            {
                if (rowsRoot == null)
                {
                    rowsRoot = rowTemplate.transform.parent;
                }

                rowTemplate.SetActive(true);
            }

            if (cardSlotObjects != null && cardSlotObjects.Length > 0)
            {
                cardSlots = new CombatRewardCardView[cardSlotObjects.Length];
                for (int i = 0; i < cardSlotObjects.Length; i++)
                {
                    if (cardSlotObjects[i] == null) continue;

                    if (!cardSlotObjects[i].TryGetComponent(out CombatRewardCardView slot))
                    {
                        slot = cardSlotObjects[i].AddComponent<CombatRewardCardView>();
                    }

                    cardSlots[i] = slot;
                }
            }

            DetectRowTemplateFromSerializedSlots();

            if (cardSlots == null || cardSlots.Length == 0)
            {
                Transform searchRoot = GetSlotSearchRoot();
                cardSlots = searchRoot.GetComponentsInChildren<CombatRewardCardView>(true);
            }
            else if (rowTemplate != null || rowsRoot != null)
            {
                RefreshSlotsFromRows();
            }

            if (cardSlots == null)
            {
                cardSlots = new CombatRewardCardView[0];
            }

            if (countTexts == null || countTexts.Length == 0)
            {
                Transform searchRoot = GetSlotSearchRoot();
                countTexts = searchRoot.GetComponentsInChildren<TMP_Text>(true)
                    .Where(text => text.name.ToLowerInvariant().Contains("count") || text.name.ToLowerInvariant().Contains("quantity"))
                    .ToArray();
            }

            AutoBindArrows();
            DetectWindowParts();
        }

        private void EnsureEnoughSlots(int cardCount)
        {
            int effectiveCardsPerRow = GetTemplateSlotCount();
            if (!cloneRowsFromTemplate || rowTemplate == null || effectiveCardsPerRow <= 0)
            {
                return;
            }

            int neededRows = Mathf.CeilToInt((float)Mathf.Max(0, cardCount) / effectiveCardsPerRow);
            neededRows = Mathf.Max(1, neededRows);
            int currentRows = 1 + generatedRows.Count;

            for (int i = currentRows; i < neededRows; i++)
            {
                GameObject row = Instantiate(rowTemplate, rowsRoot != null ? rowsRoot : rowTemplate.transform.parent);
                row.name = rowTemplate.name + "_" + (i + 1);
                row.transform.localPosition = rowTemplate.transform.localPosition + Vector3.down * rowVerticalSpacing * i;
                row.transform.localRotation = rowTemplate.transform.localRotation;
                row.transform.localScale = rowTemplate.transform.localScale;
                row.SetActive(true);
                generatedRows.Add(row);
            }

            for (int i = 0; i < generatedRows.Count; i++)
            {
                generatedRows[i].SetActive(i + 1 < neededRows);
            }

            RefreshSlotsFromRows();
        }

        private int GetCardsPerPage()
        {
            int effectiveCardsPerRow = GetTemplateSlotCount();
            return Mathf.Max(1, effectiveCardsPerRow * Mathf.Max(1, rowsPerPage));
        }

        private void RefreshSlotsFromRows()
        {
            Transform root = GetSlotSearchRoot();
            cardSlots = root.GetComponentsInChildren<CombatRewardCardView>(true);
            countTexts = root.GetComponentsInChildren<TMP_Text>(true)
                .Where(text => text.name.ToLowerInvariant().Contains("count") || text.name.ToLowerInvariant().Contains("quantity"))
                .ToArray();
        }

        private Transform GetSlotSearchRoot()
        {
            if (rowsRoot != null) return rowsRoot;
            if (rowTemplate != null) return rowTemplate.transform.parent != null ? rowTemplate.transform.parent : rowTemplate.transform;
            if (windowRoot != null && windowRoot.GetComponentsInChildren<CombatRewardCardView>(true).Length > 0) return windowRoot.transform;
            if (windowRoot != null && windowRoot.transform.parent != null) return windowRoot.transform.parent;
            if (windowRoot != null) return windowRoot.transform;

            return transform;
        }

        private void HideSlots()
        {
            hoveredSlot = null;
            foreach (CombatRewardCardView slot in cardSlots)
            {
                if (slot != null)
                {
                    slot.SetHoveredFromOwner(false);
                    slot.gameObject.SetActive(false);
                }
            }

            foreach (TMP_Text countText in countTexts)
            {
                if (countText != null)
                {
                    countText.gameObject.SetActive(false);
                }
            }

            foreach (GameObject part in detectedWindowParts)
            {
                if (part != null && part != windowRoot)
                {
                    part.SetActive(false);
                }
            }
        }

        private void SetWindowVisible(bool visible)
        {
            if (windowRoot != null)
            {
                windowRoot.SetActive(visible);
            }

            foreach (GameObject part in detectedWindowParts)
            {
                if (part != null && part != windowRoot)
                {
                    part.SetActive(visible);
                }
            }

            if (visible)
            {
                SetArrowVisibility();
            }
            else
            {
                SetActive(previousPageArrow, false);
                SetActive(nextPageArrow, false);
            }
        }

        private void DetectRowTemplateFromSerializedSlots()
        {
            if (rowTemplate != null || cardSlots == null || cardSlots.Length == 0 || cardSlots[0] == null)
            {
                return;
            }

            Transform firstSlotParent = cardSlots[0].transform.parent;
            if (firstSlotParent == null)
            {
                return;
            }

            rowTemplate = firstSlotParent.gameObject;
            rowsRoot = firstSlotParent.parent;
        }

        private int GetTemplateSlotCount()
        {
            if (rowTemplate == null)
            {
                return Mathf.Max(1, cardsPerRow);
            }

            int templateSlotCount = rowTemplate.GetComponentsInChildren<CombatRewardCardView>(true).Length;
            return Mathf.Max(1, templateSlotCount > 0 ? templateSlotCount : cardsPerRow);
        }

        private void DetectWindowParts()
        {
            AddDetectedPart(windowRoot);
            AddDetectedPart(rowTemplate);
            AddDetectedPart(previousPageArrow);
            AddDetectedPart(nextPageArrow);
            AddSiblingWindowParts();

            Transform searchRoot = GetSlotSearchRoot();
            if (searchRoot != null
                && searchRoot != transform
                && searchRoot != transform.parent
                && searchRoot != rowsRoot
                && (windowRoot == null || searchRoot != windowRoot.transform.parent))
            {
                AddDetectedPart(searchRoot.gameObject);
            }
        }

        private void AddDetectedPart(GameObject part)
        {
            if (part == null || detectedWindowParts.Contains(part))
            {
                return;
            }

            detectedWindowParts.Add(part);
        }

        private void AddSiblingWindowParts()
        {
            if (windowRoot == null || windowRoot.transform.parent == null)
            {
                return;
            }

            foreach (Transform sibling in windowRoot.transform.parent)
            {
                string siblingName = sibling.name.ToLowerInvariant();
                if (sibling == windowRoot.transform
                    || siblingName.Contains("panel")
                    || siblingName.Contains("row")
                    || siblingName.Contains("cards")
                    || siblingName.Contains("backbg"))
                {
                    AddDetectedPart(sibling.gameObject);
                }
            }
        }

        private void ApplyRenderOrder()
        {
            if (!forceRenderOrder)
            {
                return;
            }

            ApplyRootRenderOrder(windowRoot, panelSortingOrder);
            ApplyRootRenderOrder(rowTemplate, cardSortingOrder);

            foreach (GameObject part in detectedWindowParts)
            {
                int sortingOrder = part == rowTemplate ? cardSortingOrder : panelSortingOrder;
                ApplyRootRenderOrder(part, sortingOrder);
            }

            foreach (GameObject generatedRow in generatedRows)
            {
                ApplyRootRenderOrder(generatedRow, cardSortingOrder);
            }

            ApplyRootRenderOrder(previousPageArrow, arrowSortingOrder);
            ApplyRootRenderOrder(nextPageArrow, arrowSortingOrder);

            foreach (CombatRewardCardView slot in cardSlots)
            {
                if (slot != null)
                {
                    int slotIndex = System.Array.IndexOf(cardSlots, slot);
                    int slotSortingOrder = cardSortingOrder + Mathf.Max(0, slotIndex) * 10;
                    slot.ForceSortingOrder(slotSortingOrder, Mathf.Max(2, textSortingOrder - slotSortingOrder));
                    ApplyTextRenderOrder(slot.gameObject, textSortingOrder + Mathf.Max(0, slotIndex) * 10);
                }
            }

            ApplyCountTextVisuals();
        }

        private void LateUpdate()
        {
            if (!isOpen) return;

            ApplyDeckCardTextVisuals();
            ApplyCountTextVisuals();
        }

        private void ApplyRootRenderOrder(GameObject root, int spriteOrder)
        {
            if (root == null)
            {
                return;
            }

            if (root.GetComponent<CombatRewardCardView>() != null)
            {
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Vector3 localPosition = child.localPosition;
                if (!Mathf.Approximately(localPosition.z, 0f))
                {
                    child.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
                }
            }

            foreach (SpriteRenderer spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.sortingOrder = spriteRenderer.transform == root.transform ? spriteOrder : spriteOrder + 1;
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                ApplyTextRendererOrder(text, textSortingOrder);
            }
        }

        private void ApplyTextRenderOrder(GameObject root, int order)
        {
            if (root == null) return;

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                ApplyTextRendererOrder(text, order);
            }
        }

        private static void ApplyTextRendererOrder(TMP_Text text, int order)
        {
            if (text == null) return;

            Renderer textRenderer = text.GetComponent<Renderer>();
            if (textRenderer == null) return;

            textRenderer.sortingLayerID = 0;
            textRenderer.sortingOrder = order;
        }

        private void AutoBindArrows()
        {
            if (!autoFindPageArrows)
            {
                CacheArrowColliders();
                return;
            }

            Transform root = windowRoot != null ? windowRoot.transform.root : transform.root;
            if (previousPageArrow == null)
            {
                previousPageArrow = FindArrow(root, true);
            }

            if (nextPageArrow == null)
            {
                nextPageArrow = FindArrow(root, false);
            }

            if (previousPageArrow == null)
            {
                previousPageArrow = FindArrowInActiveScene(true);
            }

            if (nextPageArrow == null)
            {
                nextPageArrow = FindArrowInActiveScene(false);
            }

            CacheArrowColliders();
        }

        private void CacheArrowColliders()
        {
            previousPageCollider = EnsureArrowCollider(previousPageArrow);
            nextPageCollider = EnsureArrowCollider(nextPageArrow);
        }

        private static GameObject FindArrow(Transform root, bool previous)
        {
            if (root == null) return null;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string objectName = child.name.ToLowerInvariant();
                bool looksLikeArrow = objectName.Contains("arrow") || objectName.Contains("fleche") || objectName.Contains("page");
                if (!looksLikeArrow) continue;

                bool isPrevious = objectName.Contains("left") || objectName.Contains("prev") || objectName.Contains("previous") || objectName.Contains("gauche");
                bool isNext = objectName.Contains("right") || objectName.Contains("next") || objectName.Contains("droite");

                if (previous && isPrevious) return child.gameObject;
                if (!previous && isNext) return child.gameObject;
            }

            return null;
        }

        private static GameObject FindArrowInActiveScene(bool previous)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject rootObject in activeScene.GetRootGameObjects())
            {
                GameObject arrow = FindArrow(rootObject.transform, previous);
                if (arrow != null)
                {
                    return arrow;
                }
            }

            return null;
        }

        private static Collider2D EnsureArrowCollider(GameObject arrow)
        {
            if (arrow == null) return null;

            Collider2D collider = arrow.GetComponentInChildren<Collider2D>(true);
            if (collider != null) return collider;

            SpriteRenderer spriteRenderer = arrow.GetComponentInChildren<SpriteRenderer>(true);
            BoxCollider2D boxCollider = arrow.AddComponent<BoxCollider2D>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                boxCollider.size = spriteRenderer.sprite.bounds.size;
                boxCollider.offset = spriteRenderer.sprite.bounds.center;
            }

            return boxCollider;
        }

        private void SetArrowVisibility()
        {
            bool showPrevious = isOpen && totalPageCount > 1 && currentPageIndex > 0;
            bool showNext = isOpen && totalPageCount > 1 && currentPageIndex < totalPageCount - 1;

            SetActive(previousPageArrow, showPrevious);
            SetActive(nextPageArrow, showNext);
            PositionPageArrows();
            if (!showPrevious && hoveredArrow == PageArrow.Previous)
            {
                SetHoveredArrow(PageArrow.None);
            }

            if (!showNext && hoveredArrow == PageArrow.Next)
            {
                SetHoveredArrow(PageArrow.None);
            }
        }

        private void PositionPageArrows()
        {
            if (!anchorPageArrowsToCamera || !isOpen) return;

            PositionArrowInViewport(previousPageArrow, previousArrowViewportPosition);
            PositionArrowInViewport(nextPageArrow, nextArrowViewportPosition);
        }

        private static void PositionArrowInViewport(GameObject arrow, Vector2 viewportPosition)
        {
            if (arrow == null || !arrow.activeSelf) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 currentPosition = arrow.transform.position;
            float distanceFromCamera = Mathf.Abs(currentPosition.z - camera.transform.position.z);
            Vector3 worldPosition = camera.ViewportToWorldPoint(new Vector3(viewportPosition.x, viewportPosition.y, distanceFromCamera));
            arrow.transform.position = new Vector3(worldPosition.x, worldPosition.y, currentPosition.z);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void ReadArrowClicks()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pointerPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedArrow = GetArrowUnderPointer(pointerPosition);
                arrowPressScreenPosition = pointerPosition;
            }

            if (!mouse.leftButton.wasReleasedThisFrame) return;

            PageArrow releasedArrow = GetArrowUnderPointer(pointerPosition);
            bool isClick = pressedArrow != PageArrow.None
                && pressedArrow == releasedArrow
                && Vector2.Distance(arrowPressScreenPosition, pointerPosition) <= arrowClickMoveTolerancePixels;

            pressedArrow = PageArrow.None;

            if (!isClick) return;

            if (releasedArrow == PageArrow.Previous)
            {
                PreviousPage();
            }
            else if (releasedArrow == PageArrow.Next)
            {
                NextPage();
            }
        }

        private void ReadArrowHover()
        {
            Mouse mouse = Mouse.current;
            SetHoveredArrow(mouse != null ? GetArrowUnderPointer(mouse.position.ReadValue()) : PageArrow.None);
        }

        private void ReadCardHover()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                SetHoveredSlot(null);
                return;
            }

            CombatRewardCardView bestSlot = FindBestSlotAt(mouse.position.ReadValue());
            SetHoveredSlot(bestSlot);
            ApplyCountTextVisuals();
        }

        private CombatRewardCardView FindBestSlotAt(Vector2 screenPosition)
        {
            CombatRewardCardView bestSlot = null;
            float bestScore = float.NegativeInfinity;

            foreach (CombatRewardCardView slot in cardSlots)
            {
                if (slot == null || !slot.ContainsScreenPoint(screenPosition))
                {
                    continue;
                }

                float score = slot.GetScreenPickScore(screenPosition);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                }
            }

            return bestSlot;
        }

        private void SetHoveredSlot(CombatRewardCardView slot)
        {
            if (hoveredSlot == slot) return;

            if (hoveredSlot != null)
            {
                hoveredSlot.SetHoveredFromOwner(false);
            }

            hoveredSlot = slot;

            if (hoveredSlot != null)
            {
                hoveredSlot.SetHoveredFromOwner(true);
            }
        }

        private void CacheCountTextBaseScale(TMP_Text countText)
        {
            if (countText == null) return;

            if (!countTextBaseScales.ContainsKey(countText))
            {
                countTextBaseScales[countText] = countText.transform.localScale;
            }

            if (!countTextBasePositions.ContainsKey(countText))
            {
                countTextBasePositions[countText] = countText.transform.localPosition;
            }
        }

        private void ApplyCountTextVisuals()
        {
            for (int i = 0; i < countTexts.Length; i++)
            {
                TMP_Text countText = countTexts[i];
                if (countText == null) continue;

                CacheCountTextBaseScale(countText);
                Vector3 baseScale = countTextBaseScales[countText];
                Vector3 basePosition = countTextBasePositions[countText];
                float parentScale = 1f;
                if (i < cardSlots.Length && cardSlots[i] != null)
                {
                    parentScale = Mathf.Max(0.001f, cardSlots[i].CurrentScaleMultiplier);
                }

                countText.transform.localScale = baseScale / parentScale;
                countText.transform.localPosition = basePosition / parentScale;
                ApplyTextRendererOrder(countText, textSortingOrder + 500 + i);
            }
        }

        private void ApplyDeckCardTextVisuals()
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                CombatRewardCardView slot = cardSlots[i];
                if (slot == null) continue;

                int order = slot == hoveredSlot
                    ? focusedCardTextSortingOrder + i
                    : textSortingOrder + i * 10;

                foreach (TMP_Text text in slot.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (IsCountText(text)) continue;

                    text.richText = true;
                    ApplyTextRendererOrder(text, order);
                }
            }
        }

        private static bool IsCountText(TMP_Text text)
        {
            if (text == null) return false;

            string objectName = text.name.ToLowerInvariant();
            return objectName.Contains("count") || objectName.Contains("quantity");
        }

        private PageArrow GetArrowUnderPointer(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (camera == null) return PageArrow.None;

            Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
            Vector2 worldPoint = new Vector2(worldPosition.x, worldPosition.y);

            if (previousPageCollider != null
                && previousPageCollider.isActiveAndEnabled
                && previousPageCollider.OverlapPoint(worldPoint))
            {
                return PageArrow.Previous;
            }

            if (nextPageCollider != null
                && nextPageCollider.isActiveAndEnabled
                && nextPageCollider.OverlapPoint(worldPoint))
            {
                return PageArrow.Next;
            }

            return PageArrow.None;
        }

        private void SetHoveredArrow(PageArrow arrow)
        {
            if (hoveredArrow == arrow) return;

            ApplyArrowHoverVisual(previousPageArrow, false);
            ApplyArrowHoverVisual(nextPageArrow, false);
            hoveredArrow = arrow;

            if (hoveredArrow == PageArrow.Previous)
            {
                ApplyArrowHoverVisual(previousPageArrow, true);
            }
            else if (hoveredArrow == PageArrow.Next)
            {
                ApplyArrowHoverVisual(nextPageArrow, true);
            }
        }

        private void ApplyArrowHoverVisual(GameObject arrow, bool hovered)
        {
            if (arrow == null) return;

            if (!arrowBaseScales.ContainsKey(arrow))
            {
                arrowBaseScales[arrow] = arrow.transform.localScale;
            }

            arrow.transform.localScale = arrowBaseScales[arrow] * (hovered ? 1.12f : 1f);

            foreach (SpriteRenderer spriteRenderer in arrow.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!arrowBaseColors.ContainsKey(spriteRenderer.gameObject))
                {
                    arrowBaseColors[spriteRenderer.gameObject] = spriteRenderer.color;
                }

                Color baseColor = arrowBaseColors[spriteRenderer.gameObject];
                spriteRenderer.color = hovered ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
                spriteRenderer.sortingOrder = arrowSortingOrder;
            }
        }
    }
}
