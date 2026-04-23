using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using AgainstTheOdds.Core;

namespace AgainstTheOdds.CampaignMap
{
    /// <summary>
    /// Contrôleur de la Main Camera de la scène 03_CampaignMap.
    /// Scroll vertical (molette / drag clic gauche / flèches) avec smoothing SmoothDamp et clamp,
    /// plus FocusOnPin() pour centrer la caméra sur un pin donné (appelé au Start sur le boss courant).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CampaignCameraController : MonoBehaviour
    {
        [Header("Bornes verticales (Y monde)")]
        [Tooltip("Y minimum (centre de la carte du bas).")]
        [SerializeField] private float minY = 0f;
        [Tooltip("Y maximum (centre de la carte du haut, boss final).")]
        [SerializeField] private float maxY = 40f;

        [Header("Vitesses d'input")]
        [Tooltip("Unités monde ajoutées par cran de molette (un cran = une \"notch\" standard).")]
        [SerializeField] private float scrollSpeed = 2f;
        [Tooltip("Multiplicateur appliqué au delta souris (pixels/frame) pendant le drag. Input System : rester petit (~0.05).")]
        [SerializeField] private float dragSpeed = 0.05f;
        [Tooltip("Unités monde par seconde quand une flèche haut/bas est maintenue.")]
        [SerializeField] private float keyboardSpeed = 6f;

        [Header("Smoothing")]
        [Tooltip("Temps (secondes) que met SmoothDamp pour rattraper la cible. Plus petit = plus réactif, plus grand = plus mou.")]
        [SerializeField] private float smoothFactor = 0.15f;

        [Header("Focus automatique")]
        [Tooltip("Durée (secondes) de l'animation FocusOnPin.")]
        [SerializeField] private float focusDuration = 0.8f;

        // Cible de Y vers laquelle la caméra tend en permanence (mise à jour par les inputs).
        private float targetY;
        // Vitesse courante utilisée par Mathf.SmoothDamp.
        private float currentVelocity;
        // Désactivé pendant FocusOnPin pour éviter que l'utilisateur "lutte" contre l'animation.
        private bool userInputEnabled = true;
        private Coroutine focusRoutine;

        private void Start()
        {
            var cam = GetComponent<Camera>();
            if (!cam.orthographic)
            {
                Debug.LogWarning("[CampaignCameraController] La caméra n'était pas orthographique — forcée en ortho.");
                cam.orthographic = true;
            }

            // On part de la position actuelle (clampée), puis on focus sur le pin courant si possible.
            targetY = Mathf.Clamp(transform.position.y, minY, maxY);
            ApplyY(targetY);

            FocusSurBossCourant();
        }

        private void Update()
        {
            if (userInputEnabled)
            {
                LireInputs();
            }

            // Clamp permanent (utile si on retouche min/max en Play Mode).
            targetY = Mathf.Clamp(targetY, minY, maxY);

            // SmoothDamp : fluide quel que soit le frame rate, pas saccadé comme un Lerp naïf.
            float nouveauY = Mathf.SmoothDamp(transform.position.y, targetY, ref currentVelocity, smoothFactor);
            ApplyY(nouveauY);
        }

        private void LireInputs()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            // 1) Molette — input principal.
            //    L'Input System renvoie le scroll en "pixels" (typiquement 120 par cran sous Windows).
            //    On normalise en /120 pour que scrollSpeed reste exprimé en "unités par cran".
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    targetY += (scroll / 120f) * scrollSpeed;
                }

                // 2) Drag clic gauche maintenu — convention "grab the map" : la carte suit le curseur,
                //    donc la caméra va dans le sens opposé au mouvement souris.
                //    mouse.delta renvoie des pixels/frame (bien plus gros que l'ancien GetAxis), d'où dragSpeed ~0.05.
                if (mouse.leftButton.isPressed)
                {
                    float deltaY = mouse.delta.ReadValue().y;
                    if (Mathf.Abs(deltaY) > 0.001f)
                    {
                        targetY -= deltaY * dragSpeed;
                    }
                }
            }

            // 3) Flèches haut/bas — fallback clavier.
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed)
                {
                    targetY += keyboardSpeed * Time.deltaTime;
                }
                else if (keyboard.downArrowKey.isPressed)
                {
                    targetY -= keyboardSpeed * Time.deltaTime;
                }
            }
        }

        private void ApplyY(float y)
        {
            var p = transform.position;
            p.y = y;
            transform.position = p;
        }

        /// <summary>
        /// Anime la caméra vers la position Y du Transform donné. Désactive le scroll utilisateur
        /// pendant l'animation puis le réactive à la fin. Appelable à tout moment : toute animation
        /// déjà en cours est interrompue et remplacée.
        /// </summary>
        public void FocusOnPin(Transform pinTransform)
        {
            if (pinTransform == null)
            {
                Debug.LogWarning("[CampaignCameraController] FocusOnPin appelé avec un Transform null — ignoré.");
                return;
            }

            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
            }
            focusRoutine = StartCoroutine(FocusCoroutine(pinTransform.position.y));
        }

        private IEnumerator FocusCoroutine(float cibleY)
        {
            userInputEnabled = false;
            currentVelocity = 0f;

            float departY = transform.position.y;
            float clampCibleY = Mathf.Clamp(cibleY, minY, maxY);

            // Cas trivial : on est déjà au bon endroit, rien à animer.
            if (Mathf.Approximately(departY, clampCibleY) || focusDuration <= 0f)
            {
                ApplyY(clampCibleY);
            }
            else
            {
                float t = 0f;
                while (t < focusDuration)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / focusDuration);
                    // SmoothStep = ease-in/ease-out, plus agréable qu'un Lerp linéaire.
                    float y = Mathf.Lerp(departY, clampCibleY, Mathf.SmoothStep(0f, 1f, k));
                    ApplyY(y);
                    yield return null;
                }
            }

            // Aligne la cible du SmoothDamp sur la position finale pour que la reprise user soit sans à-coup.
            ApplyY(clampCibleY);
            targetY = clampCibleY;
            userInputEnabled = true;
            focusRoutine = null;
        }

        private void FocusSurBossCourant()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[CampaignCameraController] GameManager.Instance absent — démarrage hors 00_Bootstrap ? Focus auto skippé.");
                return;
            }

            int index = GameManager.Instance.CurrentBossIndex;
            if (index < 0 || index >= GameManager.NombreBossCampagne)
            {
                Debug.LogWarning($"[CampaignCameraController] CurrentBossIndex hors range ({index}). Focus auto skippé.");
                return;
            }

            var pin = CampaignPinRegistry.GetPin(index);
            if (pin == null)
            {
                Debug.LogWarning($"[CampaignCameraController] Aucun BossPin trouvé pour l'index {index}. Vérifie que tous les pins ont un BossPin avec le bon bossIndex.");
                return;
            }

            FocusOnPin(pin.transform);
        }

#if UNITY_EDITOR
        // Visualise les bornes min/max dans la Scene view quand la caméra est sélectionnée.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = transform.position;
            Gizmos.DrawLine(new Vector3(p.x - 5f, minY, p.z), new Vector3(p.x + 5f, minY, p.z));
            Gizmos.DrawLine(new Vector3(p.x - 5f, maxY, p.z), new Vector3(p.x + 5f, maxY, p.z));
        }
#endif
    }
}
