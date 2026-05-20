using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace AgainstTheOdds.Core
{
    public static class SceneVideoPreloader
    {
        private const float DefaultTimeout = 5f;

        public static IEnumerator PrepareSceneVideos(float timeout = DefaultTimeout)
        {
            VideoPlayer[] players = Object.FindObjectsByType<VideoPlayer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (players == null || players.Length == 0)
            {
                yield return PrepareCinematicBackgrounds(timeout);
                yield break;
            }

            int pendingPlayers = 0;

            foreach (VideoPlayer player in players)
            {
                if (player == null || !player.gameObject.scene.isLoaded) continue;
                if (player.GetComponent<CinematicBackgroundPlayer>() != null) continue;
                if (player.clip == null && string.IsNullOrWhiteSpace(player.url)) continue;

                pendingPlayers++;
                player.playOnAwake = false;
                player.waitForFirstFrame = true;
                ClearTargetTexture(player);

                if (player.isPlaying)
                {
                    player.Stop();
                }

                player.Prepare();
            }

            if (pendingPlayers == 0)
            {
                yield return PrepareCinematicBackgrounds(timeout);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < timeout && !AllPrepared(players))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            foreach (VideoPlayer player in players)
            {
                if (player == null || !player.gameObject.scene.isLoaded) continue;
                if (player.GetComponent<CinematicBackgroundPlayer>() != null) continue;
                if (player.clip == null && string.IsNullOrWhiteSpace(player.url)) continue;

                player.Play();
            }

            // Give Unity one rendered frame before the fade reveals the scene.
            yield return null;
            yield return new WaitForEndOfFrame();

            yield return PrepareCinematicBackgrounds(timeout);
        }

        private static IEnumerator PrepareCinematicBackgrounds(float timeout)
        {
            CinematicBackgroundPlayer[] backgrounds = Object.FindObjectsByType<CinematicBackgroundPlayer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (backgrounds == null || backgrounds.Length == 0)
            {
                yield break;
            }

            foreach (CinematicBackgroundPlayer background in backgrounds)
            {
                if (background == null || !background.gameObject.scene.isLoaded) continue;

                CoroutineTimeout timeoutGuard = new CoroutineTimeout(timeout);
                IEnumerator prepare = background.PrepareForSceneReveal();
                while (!timeoutGuard.Expired && prepare.MoveNext())
                {
                    yield return prepare.Current;
                }
            }

            yield return null;
            yield return new WaitForEndOfFrame();
        }

        private static bool AllPrepared(VideoPlayer[] players)
        {
            foreach (VideoPlayer player in players)
            {
                if (player == null || !player.gameObject.scene.isLoaded) continue;
                if (player.GetComponent<CinematicBackgroundPlayer>() != null) continue;
                if (player.clip == null && string.IsNullOrWhiteSpace(player.url)) continue;
                if (!player.isPrepared) return false;
            }

            return true;
        }

        private static void ClearTargetTexture(VideoPlayer player)
        {
            if (player == null || player.targetTexture == null) return;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = player.targetTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        private readonly struct CoroutineTimeout
        {
            private readonly float startTime;
            private readonly float duration;

            public CoroutineTimeout(float duration)
            {
                startTime = Time.unscaledTime;
                this.duration = Mathf.Max(0.01f, duration);
            }

            public bool Expired => Time.unscaledTime - startTime >= duration;
        }
    }
}
