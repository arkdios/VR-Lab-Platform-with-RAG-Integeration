using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PhysicsLab.Core.Web
{
    /// <summary>
    /// Calls browser features that Unity WebGL cannot reach by itself. Outside a WebGL build
    /// it falls back to files and strings that work in the Editor, so the same code can be tested.
    /// Since the Editor cannot run a .jslib. The #else branch lets you test CSV export in the Editor.
    /// </summary>
    public static class BrowserBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DownloadTextFile(string fileName, string mimeType, string content);

        [DllImport("__Internal")]
        private static extern string GetUserAgentString();
#endif

        public static string UserAgent
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return GetUserAgentString();
#else
                return Application.platform.ToString();
#endif
            }
        }

        /// <summary>
        /// Gives the user a file.
        /// </summary>
        public static void SaveTextFile(string fileName, string mimeType, string content)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DownloadTextFile(fileName, mimeType, content);
#else
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, content);
            Debug.Log($"[BrowserBridge] Saved {path}");
#endif
        }
    }
}