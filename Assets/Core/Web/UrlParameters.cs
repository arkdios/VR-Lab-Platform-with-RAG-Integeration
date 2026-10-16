using UnityEngine;
using UnityEngine.Networking;

namespace PhysicsLab.Core.Web
{
    /// <summary>
    /// Reads query parameters (the part of the page URL after "?") in a WebGL build.
    /// Outside a browser the URL is empty, so every lookup fails and callers use their defaults.
    /// </summary>
    public static class UrlParameters
    {
        /// <summary>
        /// Finds a parameter by name. The value is URL-decoded.
        /// A bare name without "=" is found and returns an empty value.
        /// Splits on &, compares names, and decodes the value.
        /// </summary>
        public static bool TryGet(string name, out string value)
        {
            value = string.Empty;
            string query = ExtractQuery(Application.absoluteURL);

            foreach (string pair in query.Split('&'))
            {
                int separator = pair.IndexOf('=');
                string key = separator < 0 ? pair : pair.Substring(0, separator);
                if (key != name) continue;

                value = separator < 0
                    ? string.Empty
                    : UnityWebRequest.UnEscapeURL(pair.Substring(separator + 1)); // Turns %20 back into a space.
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cuts the text between ? and #.
        /// </summary>
        private static string ExtractQuery(string url)
        {
            int start = url.IndexOf('?');
            if (start < 0) return string.Empty;

            int end = url.IndexOf('#', start); // a "#fragment" is not part of the query
            return end < 0
                ? url.Substring(start + 1)
                : url.Substring(start + 1, end - start - 1);
        }
    }
}