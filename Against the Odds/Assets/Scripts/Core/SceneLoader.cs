using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// Charge les scènes en async avec un fondu au noir via un CanvasGroup full-screen.
    /// Expose aussi FadeToBlack/FadeFromBlack pour des transitions autonomes (cinématiques, game over, etc.).
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Canvas de fondu (à assigner dans l'inspecteur)")]
        [SerializeField] private CanvasGroup fadeCanvasGroup;

        [Header("Réglages par défaut")]
        [SerializeField] private float dureeFonduParDefaut = 0.5f;

        public bool IsLoading { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            if (fadeCanvasGroup == null)
            {
                Debug.LogError("[SceneLoader] FadeCanvasGroup non assigné dans l'inspecteur.");
                return;
            }

            // Démarrage sur un écran transparent, non-bloquant
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        /// <summary>Charge une scène avec fondu au noir à l'entrée et à la sortie.</summary>
        public void LoadScene(string sceneName, float fadeDuration = -1f)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Chargement déjà en cours, requête vers '{sceneName}' ignorée.");
                return;
            }

            if (fadeDuration < 0f) fadeDuration = dureeFonduParDefaut;
            StartCoroutine(RoutineChargementScene(sceneName, fadeDuration));
        }

        /// <summary>Anime l'alpha de 0 → 1 (écran qui devient noir).</summary>
        public void FadeToBlack(float duration = -1f)
        {
            if (duration < 0f) duration = dureeFonduParDefaut;
            StartCoroutine(RoutineFade(fadeCanvasGroup.alpha, 1f, duration));
        }

        /// <summary>Anime l'alpha de 1 → 0 (l'écran redevient visible).</summary>
        public void FadeFromBlack(float duration = -1f)
        {
            if (duration < 0f) duration = dureeFonduParDefaut;
            StartCoroutine(RoutineFade(fadeCanvasGroup.alpha, 0f, duration));
        }

        private IEnumerator RoutineChargementScene(string sceneName, float dureeFade)
        {
            IsLoading = true;

            yield return RoutineFade(fadeCanvasGroup.alpha, 1f, dureeFade);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] Impossible de charger '{sceneName}'. Scène absente des Build Settings ?");
                yield return RoutineFade(1f, 0f, dureeFade);
                IsLoading = false;
                yield break;
            }

            op.allowSceneActivation = false;

            // 0.9 = scène prête à être activée (Unity réserve 0.9–1.0 pour l'activation)
            while (op.progress < 0.9f) yield return null;

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            yield return SceneVideoPreloader.PrepareSceneVideos();

            yield return RoutineFade(1f, 0f, dureeFade);

            IsLoading = false;
        }

        private IEnumerator RoutineFade(float alphaDepart, float alphaArrivee, float duree)
        {
            if (fadeCanvasGroup == null) yield break;

            // Pendant le fondu, on bloque les inputs dès qu'il y a de l'opacité
            fadeCanvasGroup.blocksRaycasts = true;

            if (duree <= 0f)
            {
                fadeCanvasGroup.alpha = alphaArrivee;
            }
            else
            {
                float temps = 0f;
                fadeCanvasGroup.alpha = alphaDepart;

                while (temps < duree)
                {
                    // unscaledDeltaTime pour rester insensible à Time.timeScale (pause, ralenti…)
                    temps += Time.unscaledDeltaTime;
                    fadeCanvasGroup.alpha = Mathf.Lerp(alphaDepart, alphaArrivee, temps / duree);
                    yield return null;
                }

                fadeCanvasGroup.alpha = alphaArrivee;
            }

            // On ne bloque les raycasts que si l'écran est effectivement noir
            fadeCanvasGroup.blocksRaycasts = alphaArrivee > 0.01f;
        }
    }
}
