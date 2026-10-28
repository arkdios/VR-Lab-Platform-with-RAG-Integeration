using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Draws one line of help at the bottom of the desktop view, so a new user knows how
    /// the mouse works. It reads the cursor state only. It changes nothing.
    /// </summary>
    public class DesktopHint : MonoBehaviour
    {
        private const string LockedText =
            "Look is ON. Hold left click on the ring or scale to drag. Tab or Esc frees the mouse for buttons.";
        private const string FreeText =
            "Mouse is free (buttons work). Press Tab or click the scene to look around and drag.";

        private GUIStyle _style; // built once, on first draw

        // OnGUI draws plain text on the screen. It needs no Canvas.
        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                _style.normal.textColor = Color.white;
            }

            string text = Cursor.lockState == CursorLockMode.Locked ? LockedText : FreeText;
            Rect area = new Rect(0f, Screen.height - 56f, Screen.width, 48f);

            GUI.Box(area, GUIContent.none); // dark box so the text is readable on any scene colour
            GUI.Label(area, text, _style);
        }
    }
}