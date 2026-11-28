using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Hides and shows the Lab assistant panel, so the student can see the bench while working.
    /// Put this on the StudentScreen object. Three things do the same job: the H key (it works while the
    /// mouse is locked, when no button can be clicked), the Hide button on the panel, and the Assistant tab
    /// that shows at the right edge while the panel is hidden.
    ///
    /// The panel is hidden with a CanvasGroup and is not switched off. ChatPanelPresenter lives on the panel and
    /// only listens for replies while the panel is active, so a reply that arrives while it is hidden would be lost.
    /// A student who asks a question and then hides the panel still finds the answer when it comes back.
    /// </summary>
    public class AssistantPanelToggle : MonoBehaviour
    {
        [Tooltip("The CanvasGroup on the ChatPanel object.")]
        [SerializeField] private CanvasGroup panelGroup;
        [Tooltip("The Hide button on the panel.")]
        [SerializeField] private Button hideButton;
        [Tooltip("The Assistant tab at the right edge. It shows only while the panel is hidden.")]
        [SerializeField] private Button showTab;
        [Tooltip("The key that hides or shows the panel on the desktop.")]
        [SerializeField] private Key toggleKey = Key.H;

        private bool _visible = true;   // the panel starts open

        private void Awake()
        {
            hideButton.onClick.AddListener(Hide);
            showTab.onClick.AddListener(Show);
        }

        // The student screen is switched off and on by the role screens. Match the saved state each time it opens.
        private void OnEnable() => Apply();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) SetVisible(!_visible);
        }

        private void SetVisible(bool visible)
        {
            if (visible == _visible) return;

            _visible = visible;
            Apply();
        }

        private void Hide() => SetVisible(false);

        private void Show() => SetVisible(true);

        private void Apply()
        {
            panelGroup.alpha = _visible ? 1f : 0f;
            panelGroup.interactable = _visible;
            panelGroup.blocksRaycasts = _visible;   // a hidden panel must not take clicks or pointer rays
            showTab.gameObject.SetActive(!_visible);
        }
    }
}
