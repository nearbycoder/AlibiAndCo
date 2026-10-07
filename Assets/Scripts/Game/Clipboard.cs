using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// The system clipboard: Unity's on the desktop, the page's in a browser (Plugins/WebGL/Clipboard.jslib).
    /// Automated runs keep the text to themselves unless -alibiClipboardCheck asks for the real thing,
    /// so a test never overwrites what's on the desktop's clipboard.
    /// </summary>
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void AlibiCopyText(string text);
#endif
        /// <summary>The last text copied (for the self-tests).</summary>
        public static string Last { get; private set; }
        /// <summary>Automation only: don't touch the system clipboard.</summary>
        public static bool Private;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Last = null; Private = false; }   // see Art.ResetStatics

        public static void Copy(string text)
        {
            Last = text;
            Debug.Log("[Share] copied: " + text);
            if (Private) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            AlibiCopyText(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
