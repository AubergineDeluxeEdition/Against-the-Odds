using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgainstTheOdds.Core
{
    public sealed class DisplayModeBootstrap : MonoBehaviour
    {
        private const int WindowWidth = 1920;
        private const int WindowHeight = 1080;
        private const float ReferenceOrthographicSize = 5f;

        private static DisplayModeBootstrap instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;

            GameObject gameObject = new GameObject(nameof(DisplayModeBootstrap));
            instance = gameObject.AddComponent<DisplayModeBootstrap>();
            DontDestroyOnLoad(gameObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ApplyDisplayMode();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            FitMainCamera();
        }

        private void LateUpdate()
        {
            ApplyDisplayMode();
            FitMainCamera();
        }

        private void OnDestroy()
        {
            if (instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        private static void ApplyDisplayMode()
        {
#if !UNITY_EDITOR
            Screen.SetResolution(WindowWidth, WindowHeight, FullScreenMode.Windowed);
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FitMainCamera();
        }

        private void FitMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            camera.rect = new Rect(0f, 0f, 1f, 1f);
            if (camera.orthographic)
            {
                camera.orthographicSize = ReferenceOrthographicSize;
            }
        }
    }
}
