using System;
using TMPro;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>One page of a guide board: a title and a block of text. Rich text tags such as colours work.</summary>
    [Serializable]
    public struct GuidePage
    {
        public string title;
        [TextArea(3, 12)] public string body;
    }

    /// <summary>
    /// A sign in the room with several pages and two buttons, Previous and Next.
    /// One board holds a whole guide, so the room does not need a sign for every topic.
    /// It only changes text when a button is pressed, so it costs no work in between.
    /// The Editor builder (LabBoardBuilder) makes the board and fills in the pages.
    /// </summary>
    public class GuideBoard : MonoBehaviour
    {
        [SerializeField] private GuidePage[] pages;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private BoardButton previousButton;
        [SerializeField] private BoardButton nextButton;

        private int _pageIndex;

        public int PageCount => pages == null ? 0 : pages.Length;

        private void OnEnable()
        {
            previousButton.Pressed += ShowPrevious;
            nextButton.Pressed += ShowNext;
            ShowPage(_pageIndex);
        }

        private void OnDisable()
        {
            previousButton.Pressed -= ShowPrevious;
            nextButton.Pressed -= ShowNext;
        }

        private void ShowPrevious() => ShowPage(_pageIndex - 1);

        private void ShowNext() => ShowPage(_pageIndex + 1);

        /// <summary>Shows a page. A number outside the list is moved to the nearest page.</summary>
        public void ShowPage(int index)
        {
            if (PageCount == 0) return;

            _pageIndex = Mathf.Clamp(index, 0, PageCount - 1);
            GuidePage page = pages[_pageIndex];
            titleLabel.text = page.title;
            bodyLabel.text = page.body;
            pageLabel.text = $"Page {_pageIndex + 1} of {PageCount}";

            // The buttons grey out at the first and the last page.
            previousButton.Interactable = _pageIndex > 0;
            nextButton.Interactable = _pageIndex < PageCount - 1;
        }
    }
}
