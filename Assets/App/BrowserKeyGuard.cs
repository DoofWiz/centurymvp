#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Century.App
{
    /// <summary>
    /// WebGL only: hands the keyboard to the game instead of the browser. The control scheme
    /// lives on WASD + L-Ctrl + Tab + Space — exactly the keys browsers treat as their own
    /// (Ctrl+W closes the tab mid-battle). The plugin swallows every shortcut a page is allowed
    /// to swallow, and locks the full keyboard (Ctrl+W included) while the game is fullscreen
    /// via the Keyboard Lock API. See Assets/Plugins/WebGL/BrowserKeyGuard.jslib.
    /// </summary>
    public static class BrowserKeyGuard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CenturyInstallBrowserKeyGuard();

        /// <summary>Installs the guard. Idempotent; call once at boot.</summary>
        public static void Install() => CenturyInstallBrowserKeyGuard();
#else
        /// <summary>No-op everywhere the browser is not the platform.</summary>
        public static void Install() { }
#endif
    }
}
