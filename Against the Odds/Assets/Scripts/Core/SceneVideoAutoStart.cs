using System.Collections;
using UnityEngine;

namespace AgainstTheOdds.Core
{
    public sealed class SceneVideoAutoStart : MonoBehaviour
    {
        private static SceneVideoAutoStart instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;

            GameObject gameObject = new GameObject(nameof(SceneVideoAutoStart));
            instance = gameObject.AddComponent<SceneVideoAutoStart>();
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            yield return SceneVideoPreloader.PrepareSceneVideos();
        }
    }
}
