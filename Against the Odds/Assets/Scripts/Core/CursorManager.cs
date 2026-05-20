using UnityEngine;

namespace AgainstTheOdds.Core
{
    public static class CursorManager
    {
        private const string CursorResourcePath = "Cursors/NORMAL";
        private static bool hasAppliedCursor;

        public static void ApplyDefaultCursor()
        {
            Texture2D cursorTexture = Resources.Load<Texture2D>(CursorResourcePath);
            if (cursorTexture == null)
            {
                Debug.LogWarning($"[CursorManager] Cursor texture not found in Resources/{CursorResourcePath}.");
                return;
            }

            Cursor.visible = true;
            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.ForceSoftware);
            hasAppliedCursor = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReapplyAfterSceneLoad()
        {
            if (!hasAppliedCursor)
            {
                ApplyDefaultCursor();
            }
        }
    }
}
