using System.Collections;
using System.Collections.Generic;
using AgainstTheOdds.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public class CombatResultOverlay : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private string campaignSceneName = "03_CampaignMap";
    [SerializeField] private string defeatSceneName = "01_MainMenu";

    [Header("Roots")]
    [SerializeField] private GameObject backdropRoot;
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject potionRewardRoot;
    [SerializeField] private GameObject cardRewardsRoot;
    [SerializeField] private GameObject continueButtonRoot;
    [SerializeField] private GameObject defeatButtonRoot;
    [SerializeField] private GameObject continueButtonTextRoot;
    [SerializeField] private GameObject defeatButtonTextRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [FormerlySerializedAs("defeatText")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text potionRewardText;
    [SerializeField] private string victoryTitle = "Victoire";
    [SerializeField] private string defeatTitle = "Defaite";

    [Header("Rewards")]
    [Tooltip("Drop your card GameObjects here. The script will add/use CombatRewardCardView automatically.")]
    [SerializeField] private GameObject[] rewardCardObjects;
    [SerializeField] private CombatRewardCardView[] rewardCards;
    [SerializeField] private bool allowContinueWithoutAllRewardPicks;

    [Header("Render Order")]
    [SerializeField] private bool forceSorting = true;
    [SerializeField] private int sortingOrder = 10000;

    [Header("Timing & Feedback")]
    [SerializeField] private float resultRevealDelay = 1.6f;
    [SerializeField] private float overlayAnimationDuration = 0.35f;
    [SerializeField] private Vector3 overlayHiddenScale = new Vector3(0.94f, 0.94f, 1f);
    [SerializeField, Range(0f, 1f)] private float resultMusicVolumeMultiplier = 0.35f;
    [SerializeField] private float resultMusicDuckDuration = 0.45f;

    private readonly List<CombatRewardCardView> selectedCards = new List<CombatRewardCardView>();
    private bool showingResult;
    private bool victory;
    private int requiredPickCount;
    private bool victoryCardsStepActive;
    private bool victoryLoreStepShown;
    private string pendingVictoryLore;
    private Coroutine showRoutine;
    private Coroutine animationRoutine;
    private Vector3 overlayInitialScale = Vector3.one;

    public bool IsShowingResult => showingResult;
    public bool IsVictory => showingResult && victory;

    private void Awake()
    {
        if (combatManager == null)
        {
            combatManager = FindAnyObjectByType<CombatManager>();
        }

        AutoBindMissingReferences();
        Transform animatedRoot = GetAnimatedOverlayTransform();
        if (animatedRoot != null)
        {
            overlayInitialScale = animatedRoot.localScale;
        }
        Hide();
    }

    private void OnEnable()
    {
        if (combatManager == null) return;

        combatManager.OnCombatEnded += Show;
    }

    private void OnDisable()
    {
        if (combatManager == null) return;

        combatManager.OnCombatEnded -= Show;
    }

    private void LateUpdate()
    {
        if (showingResult)
        {
            UpdateResultButtonVisibility();
        }
    }

    public bool CanUseButton(CombatResultButton.ResultButtonAction action)
    {
        if (!showingResult) return false;

        switch (action)
        {
            case CombatResultButton.ResultButtonAction.ContinueAfterVictory:
                return victory && CanContinueVictoryFlow();
            case CombatResultButton.ResultButtonAction.EndAdventure:
                return !victory;
            default:
                return false;
        }
    }

    public bool IsPointerOverRewardCard(Vector2 screenPosition)
    {
        if (!showingResult || !victory || rewardCards == null) return false;

        foreach (CombatRewardCardView rewardCard in rewardCards)
        {
            if (rewardCard != null && rewardCard.ContainsScreenPoint(screenPosition))
            {
                return true;
            }
        }

        return false;
    }

    public void UseButton(CombatResultButton.ResultButtonAction action)
    {
        if (!CanUseButton(action)) return;

        switch (action)
        {
            case CombatResultButton.ResultButtonAction.ContinueAfterVictory:
                ContinueAfterVictory();
                break;
            case CombatResultButton.ResultButtonAction.EndAdventure:
                PlayDefeatButtonSfx();
                EndAdventure();
                break;
        }
    }

    private void Show(bool didWin)
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowAfterDelay(didWin));
    }

    private IEnumerator ShowAfterDelay(bool didWin)
    {
        if (resultRevealDelay > 0f)
        {
            yield return new WaitForSeconds(resultRevealDelay);
        }

        ShowImmediate(didWin);
        showRoutine = null;
    }

    private void ShowImmediate(bool didWin)
    {
        showingResult = true;
        victory = didWin;
        selectedCards.Clear();
        RefreshResultButtonReferences();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.DuckMusic(resultMusicVolumeMultiplier, resultMusicDuckDuration);
            AudioClip resultClip = victory ? combatManager?.VictorySfx : combatManager?.DefeatSfx;
            AudioManager.Instance.PlaySFX(resultClip);
        }

        SetOverlayVisible(true);

        SetActive(potionRewardRoot, victory);
        SetActive(cardRewardsRoot, false);
        UpdateResultButtonVisibility();

        if (titleText != null)
        {
            titleText.text = victory ? victoryTitle : defeatTitle;
        }

        if (resultText != null)
        {
            resultText.gameObject.SetActive(!victory);
            resultText.text = victory ? string.Empty : GetDefeatBodyText();
        }

        if (victory)
        {
            ShowVictoryRewards();
        }
        else
        {
            HideRewardCards();
        }

        ApplySorting();
        PlayOverlayAnimation();
    }

    private void Hide()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        showingResult = false;
        selectedCards.Clear();
        RefreshResultButtonReferences();

        SetActive(potionRewardRoot, false);
        SetActive(cardRewardsRoot, false);
        SetResultButtonGroupActive(continueButtonRoot, continueButtonTextRoot, false);
        SetResultButtonGroupActive(defeatButtonRoot, defeatButtonTextRoot, false);
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
            resultText.text = string.Empty;
        }
        HideRewardCards();
        SetOverlayVisible(false);

        Transform animatedRoot = GetAnimatedOverlayTransform();
        if (animatedRoot != null)
        {
            animatedRoot.localScale = overlayInitialScale;
        }
    }

    private void ShowVictoryRewards()
    {
        int potionReward = combatManager != null ? combatManager.PotionRewardCount : 0;
        requiredPickCount = combatManager != null ? combatManager.RewardPickCount : 0;
        pendingVictoryLore = combatManager != null ? combatManager.VictoryResultText : string.Empty;
        victoryLoreStepShown = false;

        if (potionRewardText != null)
        {
            potionRewardText.text = potionReward > 0 ? "+" + potionReward + " potion(s)" : "Aucune potion";
        }

        List<CardDefinition> choices = combatManager != null
            ? combatManager.CreateRewardChoices()
            : new List<CardDefinition>();
        bool hasCards = choices.Count > 0 && rewardCards.Length > 0 && requiredPickCount > 0;

        SetVisualActive(cardRewardsRoot, hasCards);
        NormalizeRewardRootDepth();

        for (int i = 0; i < rewardCards.Length; i++)
        {
            CardDefinition card = i < choices.Count ? choices[i] : null;
            if (rewardCards[i] != null)
            {
                rewardCards[i].Bind(card, ToggleRewardCard);
                rewardCards[i].SetInteractable(card != null);
                NormalizeRewardCardDepth(rewardCards[i]);
            }
        }

        int visibleRewardCards = rewardCards != null ? rewardCards.Length : 0;
        requiredPickCount = Mathf.Min(requiredPickCount, choices.Count, visibleRewardCards);
        victoryCardsStepActive = hasCards && requiredPickCount > 0;
        if (!victoryCardsStepActive)
        {
            HideRewardCards();
            ShowVictoryLoreIfAny();
        }
        else if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
            resultText.text = string.Empty;
        }

        RefreshSelectionState();
        ApplySorting();
    }

    private string GetDefeatBodyText()
    {
        string configuredText = combatManager != null ? combatManager.DefeatResultText : string.Empty;
        return string.IsNullOrWhiteSpace(configuredText)
            ? "L'aventure s'arrete ici."
            : configuredText;
    }

    private void ToggleRewardCard(CombatRewardCardView cardView)
    {
        if (cardView == null || cardView.Card == null) return;

        if (selectedCards.Contains(cardView))
        {
            selectedCards.Remove(cardView);
            cardView.SetSelected(false);
            RefreshSelectionState();
            return;
        }

        if (selectedCards.Count >= requiredPickCount && requiredPickCount > 0)
        {
            CombatRewardCardView oldestSelection = selectedCards[0];
            selectedCards.RemoveAt(0);
            if (oldestSelection != null)
            {
                oldestSelection.SetSelected(false);
            }
        }

        selectedCards.Add(cardView);
        cardView.SetSelected(true);
        RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        bool hasSelection = selectedCards.Count > 0;
        foreach (CombatRewardCardView card in rewardCards)
        {
            if (card == null || card.Card == null) continue;

            card.SetInteractable(true);
            card.SetSelectionContext(hasSelection);
        }
    }

    private bool HasValidRewardSelection()
    {
        if (!victory) return false;
        if (allowContinueWithoutAllRewardPicks) return true;

        return selectedCards.Count >= requiredPickCount;
    }

    private bool CanContinueVictoryFlow()
    {
        if (victoryCardsStepActive)
        {
            return HasValidRewardSelection();
        }

        return true;
    }

    private void ContinueAfterVictory()
    {
        if (victoryCardsStepActive)
        {
            victoryCardsStepActive = false;
            SetVisualActive(cardRewardsRoot, false);
            HideRewardCards();

            if (ShowVictoryLoreIfAny())
            {
                ApplySorting();
                return;
            }
        }

        BossEncounterConfig encounter = combatManager != null ? combatManager.Encounter : null;
        bool campaignComplete = false;
        if (GameManager.Instance != null)
        {
            var ids = new List<string>();
            foreach (CombatRewardCardView cardView in selectedCards)
            {
                if (cardView?.Card != null)
                {
                    ids.Add(cardView.Card.id);
                }
            }

            GameManager.Instance.AddRewardCards(ids);
            campaignComplete = GameManager.Instance.AdvanceToNextBoss();
        }

        AudioManager.Instance?.RestoreMusic(resultMusicDuckDuration);

        string nextScene = campaignSceneName;
        if (campaignComplete)
        {
            nextScene = !string.IsNullOrWhiteSpace(encounter?.finalVictorySceneName)
                ? encounter.finalVictorySceneName
                : defeatSceneName;
            GameManager.Instance?.SetPendingCinematicNextScene(defeatSceneName);
        }
        else if (!string.IsNullOrWhiteSpace(encounter?.postVictorySceneName))
        {
            nextScene = encounter.postVictorySceneName;
            GameManager.Instance?.SetPendingCinematicNextScene(campaignSceneName);
        }

        LoadScene(nextScene);
    }

    private bool ShowVictoryLoreIfAny()
    {
        if (victoryLoreStepShown || string.IsNullOrWhiteSpace(pendingVictoryLore))
        {
            return false;
        }

        victoryLoreStepShown = true;
        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = pendingVictoryLore;
        }

        return true;
    }

    private void EndAdventure()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndRun();
        }

        AudioManager.Instance?.RestoreMusic(resultMusicDuckDuration);

        BossEncounterConfig encounter = combatManager != null ? combatManager.Encounter : null;
        string nextScene = !string.IsNullOrWhiteSpace(encounter?.postDefeatSceneName)
            ? encounter.postDefeatSceneName
            : defeatSceneName;

        if (!string.IsNullOrWhiteSpace(encounter?.postDefeatSceneName))
        {
            GameManager.Instance?.SetPendingCinematicNextScene(defeatSceneName);
        }

        LoadScene(nextScene);
    }

    private static void LoadScene(string sceneName)
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private void PlayDefeatButtonSfx()
    {
        AudioClip clip = combatManager != null ? combatManager.DefeatButtonSfx : null;
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }

    private void HideRewardCards()
    {
        foreach (CombatRewardCardView card in rewardCards)
        {
            if (card != null)
            {
                card.gameObject.SetActive(false);
            }
        }
    }

    private void NormalizeRewardRootDepth()
    {
        if (cardRewardsRoot == null) return;

        Transform root = cardRewardsRoot.transform;
        Vector3 position = root.localPosition;
        position.z = 0f;
        root.localPosition = position;
    }

    private static void NormalizeRewardCardDepth(CombatRewardCardView card)
    {
        if (card == null) return;

        foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
        {
            Vector3 position = child.localPosition;
            position.z = 0f;
            child.localPosition = position;
        }
    }

    private void AutoBindMissingReferences()
    {
        if (backdropRoot == null)
        {
            backdropRoot = FindChildByName(transform.root, "resultbackdrop");
        }

        if (overlayRoot == null)
        {
            overlayRoot = FindChildByName(transform.root, "resultpanel");
        }

        if (continueButtonRoot == null)
        {
            continueButtonRoot = FindChildByName(transform.root, "btncontinue");
        }

        if (defeatButtonRoot == null)
        {
            defeatButtonRoot = FindChildByName(transform.root, "btndefeat");
        }

        if (continueButtonTextRoot == null)
        {
            continueButtonTextRoot = FindChildByName(transform.root, "btncontinuetxt");
        }

        if (defeatButtonTextRoot == null)
        {
            defeatButtonTextRoot = FindChildByName(transform.root, "btndefeattxt");
        }

        ConfigureResultButton(continueButtonRoot, CombatResultButton.ResultButtonAction.ContinueAfterVictory);
        ConfigureResultButton(defeatButtonRoot, CombatResultButton.ResultButtonAction.EndAdventure);

        if (rewardCardObjects != null && rewardCardObjects.Length > 0)
        {
            rewardCards = new CombatRewardCardView[rewardCardObjects.Length];
            for (int i = 0; i < rewardCardObjects.Length; i++)
            {
                if (rewardCardObjects[i] == null) continue;

                if (!rewardCardObjects[i].TryGetComponent(out CombatRewardCardView rewardCard))
                {
                    rewardCard = rewardCardObjects[i].AddComponent<CombatRewardCardView>();
                }

                rewardCards[i] = rewardCard;
            }
        }

        if (rewardCards == null || rewardCards.Length == 0)
        {
            rewardCards = GetComponentsInChildren<CombatRewardCardView>(true);
        }

        if (rewardCards == null)
        {
            rewardCards = new CombatRewardCardView[0];
        }
    }

    private void ApplySorting()
    {
        if (!forceSorting) return;

        int index = 0;
        if (backdropRoot != null)
        {
            foreach (SpriteRenderer spriteRenderer in backdropRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.sortingOrder = sortingOrder + index;
                index++;
            }
        }

        GameObject root = overlayRoot != null ? overlayRoot : gameObject;
        index = ApplySortingForRoot(root, index);
        index = ApplySortingForRoot(potionRewardRoot, index);
        index = ApplySortingForRoot(cardRewardsRoot, index);
        index = ApplySortingForRoot(continueButtonRoot, index);
        index = ApplySortingForRoot(continueButtonTextRoot, index);
        index = ApplySortingForRoot(defeatButtonRoot, index);
        ApplySortingForRoot(defeatButtonTextRoot, index);
    }

    private void UpdateResultButtonVisibility()
    {
        RefreshResultButtonReferences();
        SetResultButtonGroupActive(continueButtonRoot, continueButtonTextRoot, showingResult && victory);
        SetResultButtonGroupActive(defeatButtonRoot, defeatButtonTextRoot, showingResult && !victory);
    }

    private void RefreshResultButtonReferences()
    {
        continueButtonRoot = FindChildByName(transform.root, "btncontinue");
        defeatButtonRoot = FindChildByName(transform.root, "btndefeat");
        continueButtonTextRoot = FindChildByName(transform.root, "btncontinuetxt");
        defeatButtonTextRoot = FindChildByName(transform.root, "btndefeattxt");
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private void SetOverlayVisible(bool visible)
    {
        SetVisualActive(backdropRoot, visible);

        if (overlayRoot != null && overlayRoot != gameObject)
        {
            SetVisualActive(overlayRoot, visible);
            return;
        }

        GameObject root = overlayRoot != null ? overlayRoot : gameObject;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }

        foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = visible;
        }
    }

    private void PlayOverlayAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(AnimateOverlayAppearance());
    }

    private IEnumerator AnimateOverlayAppearance()
    {
        Transform animatedRoot = GetAnimatedOverlayTransform();
        if (animatedRoot != null)
        {
            animatedRoot.localScale = Vector3.Scale(overlayInitialScale, overlayHiddenScale);
        }

        SpriteRenderer[] sprites = GetFadeSprites();
        TMP_Text[] texts = GetFadeTexts();
        Color[] spriteColors = CaptureSpriteColors(sprites);
        Color[] textColors = CaptureTextColors(texts);
        SetFadeAlpha(sprites, texts, spriteColors, textColors, 0f);

        float duration = Mathf.Max(0.01f, overlayAnimationDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (animatedRoot != null)
            {
                animatedRoot.localScale = Vector3.Lerp(Vector3.Scale(overlayInitialScale, overlayHiddenScale), overlayInitialScale, t);
            }

            SetFadeAlpha(sprites, texts, spriteColors, textColors, t);
            yield return null;
        }

        if (animatedRoot != null)
        {
            animatedRoot.localScale = overlayInitialScale;
        }

        SetFadeAlpha(sprites, texts, spriteColors, textColors, 1f);
        animationRoutine = null;
    }

    private Transform GetAnimatedOverlayTransform()
    {
        GameObject root = overlayRoot != null ? overlayRoot : gameObject;
        return root != null ? root.transform : null;
    }

    private SpriteRenderer[] GetFadeSprites()
    {
        var sprites = new List<SpriteRenderer>();
        AddSprites(backdropRoot, sprites);
        AddSprites(overlayRoot != null ? overlayRoot : gameObject, sprites);
        AddSprites(potionRewardRoot, sprites);
        AddSprites(cardRewardsRoot, sprites);
        AddSprites(continueButtonRoot, sprites);
        AddSprites(continueButtonTextRoot, sprites);
        AddSprites(defeatButtonRoot, sprites);
        AddSprites(defeatButtonTextRoot, sprites);
        return sprites.ToArray();
    }

    private TMP_Text[] GetFadeTexts()
    {
        var texts = new List<TMP_Text>();
        AddTexts(overlayRoot != null ? overlayRoot : gameObject, texts);
        AddTexts(potionRewardRoot, texts);
        AddTexts(cardRewardsRoot, texts);
        AddTexts(continueButtonRoot, texts);
        AddTexts(continueButtonTextRoot, texts);
        AddTexts(defeatButtonRoot, texts);
        AddTexts(defeatButtonTextRoot, texts);
        return texts.ToArray();
    }

    private static void AddSprites(GameObject root, List<SpriteRenderer> sprites)
    {
        if (root == null || !root.activeInHierarchy) return;
        sprites.AddRange(root.GetComponentsInChildren<SpriteRenderer>(true));
    }

    private static void AddTexts(GameObject root, List<TMP_Text> texts)
    {
        if (root == null || !root.activeInHierarchy) return;
        texts.AddRange(root.GetComponentsInChildren<TMP_Text>(true));
    }

    private static Color[] CaptureSpriteColors(SpriteRenderer[] sprites)
    {
        var colors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            colors[i] = sprites[i] != null ? sprites[i].color : Color.white;
        }

        return colors;
    }

    private static Color[] CaptureTextColors(TMP_Text[] texts)
    {
        var colors = new Color[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            colors[i] = texts[i] != null ? texts[i].color : Color.white;
        }

        return colors;
    }

    private static void SetFadeAlpha(SpriteRenderer[] sprites, TMP_Text[] texts, Color[] spriteColors, Color[] textColors, float alphaRatio)
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            Color color = spriteColors[i];
            color.a *= alphaRatio;
            sprites[i].color = color;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            Color color = textColors[i];
            color.a *= alphaRatio;
            texts[i].color = color;
        }
    }

    private static void SetResultButtonGroupActive(GameObject buttonRoot, GameObject textRoot, bool active)
    {
        SetVisualActive(buttonRoot, active);
        SetVisualActive(textRoot, active);
    }

    private static void SetVisualActive(GameObject target, bool active)
    {
        if (target == null) return;

        target.SetActive(active);

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = active;
        }

        foreach (Collider2D collider in target.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = active;
        }
    }

    private int ApplySortingForRoot(GameObject root, int startIndex)
    {
        if (root == null) return startIndex;

        CombatRewardCardView rewardCard = root.GetComponent<CombatRewardCardView>();
        if (rewardCard != null)
        {
            rewardCard.ForceSortingOrder(sortingOrder + startIndex * 10, 30);
            return startIndex + 1;
        }

        int index = startIndex;
        SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i].GetComponentInParent<CombatRewardCardView>() != null) continue;
            spriteRenderers[i].sortingOrder = sortingOrder + index;
            index++;
        }

        TMP_Text[] textRenderers = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textRenderer in textRenderers)
        {
            if (textRenderer.GetComponentInParent<CombatRewardCardView>() != null) continue;
            if (textRenderer.TryGetComponent(out Renderer renderer))
            {
                renderer.sortingOrder = sortingOrder + index + 10;
                index++;
            }
        }

        return index;
    }

    private static GameObject FindChildByName(Transform root, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        GameObject found = FindChildByNameInRoot(root, objectName);
        if (found != null)
        {
            return found;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            found = FindChildByNameInRoot(rootObjects[i].transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindChildByNameInRoot(Transform root, string objectName)
    {
        if (root == null) return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void ConfigureResultButton(GameObject buttonRoot, CombatResultButton.ResultButtonAction action)
    {
        if (buttonRoot == null) return;

        if (buttonRoot.GetComponent<Collider2D>() == null)
        {
            buttonRoot.AddComponent<BoxCollider2D>();
        }

        CombatResultButton button = buttonRoot.GetComponent<CombatResultButton>();
        if (button == null)
        {
            button = buttonRoot.AddComponent<CombatResultButton>();
        }

        button.Configure(this, action);
    }
}
