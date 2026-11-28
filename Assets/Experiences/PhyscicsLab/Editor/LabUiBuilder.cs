#if UNITY_EDITOR
using System.Text;
using PhysicsLab.Core.Rigs;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Build student screen (timer, Finish, bigger assistant). Also a step of Lab > 0.
    /// Changes the student screen (the object StudentScreen under Lab UI):
    ///  1. Left panel (HUDPanel): adds a timer line on top and a Finish button at the bottom, and makes the panel a little wider and taller.
    ///  2. Right panel (ChatPanel, the assistant): makes it much bigger, with a title, a 2 x 2 grid of question buttons,
    ///     a status line and a big answer area that scrolls when the answer is long (mouse wheel, drag or the bar).
    ///     The old panel was 640 x 370 and its buttons were 100 x 100.
    ///     It also gets a Hide button (and the H key), and an Assistant tab at the right edge that brings the panel back.
    ///  3. Makes the notice that opens when the student presses Finish (FinishPanel, a child of Lab UI).
    ///  4. Adds the LabSession object with LabTimer and LabFinishController, and gives them their links.
    ///  5. Makes the headset copy of the screen a little bigger and farther away, so the bigger panels fit.
    /// New text and buttons are copies of the ones that are already there, so they look the same.
    /// It finds every part by name. It does not delete the old parts. Safe to run twice.
    /// </summary>
    public static class LabUiBuilder
    {
        // Names of the parts that already exist in the scene.
        private const string HudPanelName = "HUDPanel";
        private const string StepLabelName = "StepLabel";
        private const string ReadingLabelName = "ReadingLabel";
        private const string CountLabelName = "MeasurementCountLabel";
        private const string NextButtonName = "NextStepButton";
        private const string RecordButtonName = "RecordButton";
        private const string ExportButtonName = "ExportButton";
        private const string ChatPanelName = "ChatPanel";
        private const string QuestionGridName = "QuestionGrid";
        private const string StatusLabelName = "StatusLabel";
        private const string AnswerLabelName = "AnswerLabel";
        private const string LabUiName = "Lab UI";

        // Names of the parts this builder makes.
        private const string TimerLabelName = "TimerLabel";
        private const string FinishButtonName = "FinishButton";
        private const string ChatTitleName = "ChatTitle";
        private const string AnswerBackgroundName = "AnswerBackground";
        private const string NoticeName = "FinishPanel";
        private const string SessionName = "LabSession";
        private const string HideButtonName = "HideAssistantButton";
        private const string ShowTabName = "AssistantTab";
        private const string AnswerScrollName = "AnswerScroll";
        private const string ViewportName = "Viewport";
        private const string ScrollbarName = "AnswerScrollbar";
        private const string SlidingAreaName = "SlidingArea";
        private const string HandleName = "Handle";

        // Left panel. Heights are in canvas pixels (the canvas is 1920 x 1080 on the desktop).
        private const float HudWidth = 380f;
        private const float HudTimerHeight = 48f;
        private const float HudStepHeight = 70f;
        private const float HudLabelHeight = 40f;
        private const float HudButtonHeight = 56f;

        // Right panel, the assistant.
        private const float ChatWidth = 760f;
        private const float ChatHeight = 720f;
        private const float ChatSide = 24f;
        private const float ChatGap = 12f;
        private const float ChatButtonHeight = 88f;

        // Hide button (in the panel header) and Assistant tab (at the right edge of the screen).
        private const float HideButtonWidth = 170f;
        private const float HideButtonHeight = 48f;
        private const float HideButtonTop = 14f;   // the same top gap as the ChatTitle row
        private const float TabWidth = 210f;
        private const float TabHeight = 64f;

        // The answer area. The text keeps one size and scrolls, so a long answer never shrinks into small print.
        private const float AnswerFontSize = 24f;
        private const float AnswerInsetSide = 16f;
        private const float AnswerInsetTop = 14f;
        private const float AnswerInsetBottom = 12f;
        private const float ScrollbarWidth = 14f;
        private const float ScrollbarGap = 6f;
        private const float AnswerScrollSensitivity = 30f;

        // The headset copy of the screen (HudCanvasAdapter).
        private const float VrDistanceMetres = 1.5f;
        private static readonly Vector2 VrSizePixels = new Vector2(1500f, 800f);

        private static readonly Color AccentColour = new Color(0.31f, 0.76f, 0.97f, 1f);
        private static readonly Color CardColour = new Color(0.07f, 0.10f, 0.15f, 0.98f);
        private static readonly Color AnswerColour = new Color(0.12f, 0.15f, 0.21f, 0.95f);
        private static readonly Color DimColour = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color FinishTint = new Color(0.78f, 0.93f, 0.80f, 1f);
        private static readonly Color DoneColour = new Color(0.55f, 0.90f, 0.60f, 1f);
        private static readonly Color ScrollTrackColour = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color ScrollHandleColour = new Color(0.31f, 0.76f, 0.97f, 0.85f);

        [MenuItem("Lab/Build student screen (timer, Finish, bigger assistant)")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabUiBuilder]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        /// <summary>The parts of the notice that the controller needs links to.</summary>
        private sealed class NoticeParts
        {
            public GameObject Root;
            public GameObject ConfirmGroup;
            public GameObject DoneGroup;
            public TMP_Text ConfirmText;
            public TMP_Text DoneTitle;
            public TMP_Text DoneText;
            public Button Yes;
            public Button KeepWorking;
            public Button Export;
            public Button Close;
        }

        public static bool Run(StringBuilder log)
        {
            Transform hudPanel = LabSceneFixes.FindSceneObject(HudPanelName);
            Transform chatPanel = LabSceneFixes.FindSceneObject(ChatPanelName);
            Transform labUi = LabSceneFixes.FindSceneObject(LabUiName);
            if (hudPanel == null || chatPanel == null || labUi == null)
            {
                log.AppendLine("  ERROR: HUDPanel, ChatPanel or Lab UI is missing from the scene. Nothing was changed.");
                return false;
            }

            Transform nextButton = hudPanel.Find(NextButtonName);
            Transform stepLabel = hudPanel.Find(StepLabelName);
            Transform statusLabel = chatPanel.Find(StatusLabelName);
            if (nextButton == null || stepLabel == null || statusLabel == null)
            {
                log.AppendLine("  ERROR: NextStepButton, StepLabel or StatusLabel is missing, so there is nothing to copy the style from. Nothing was changed.");
                return false;
            }

            TMP_Text timerLabel = BuildHudPanel(hudPanel, stepLabel, nextButton, out Button finishButton, out TMP_Text finishLabel);
            BuildChatPanel(chatPanel, statusLabel);
            BuildAssistantToggle(chatPanel, nextButton);
            NoticeParts notice = BuildNotice(labUi, statusLabel, nextButton);
            bool wired = WireSession(labUi, timerLabel, finishButton, finishLabel, notice, log);
            if (wired) MakeHeadsetScreenBigger(log);

            log.AppendLine("  Student screen built: timer and Finish on the left, bigger assistant (scrolling answer, Hide button, H key) on the right, Finish notice made.");
            return wired;
        }

        // ---------- left panel ----------

        private static TMP_Text BuildHudPanel(Transform hudPanel, Transform stepLabel, Transform nextButton, out Button finishButton, out TMP_Text finishLabel)
        {
            Transform timer = FindOrCopy(hudPanel, TimerLabelName, stepLabel.gameObject);
            var timerText = timer.GetComponent<TMP_Text>();
            timerText.text = "Time left  " + LabTimer.FormatClock(Mathf.RoundToInt(LabTimer.DefaultTotalSeconds));
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.fontStyle = FontStyles.Bold;
            SetAutoSize(timerText, 22f, 32f);

            Transform finish = FindOrCopy(hudPanel, FinishButtonName, nextButton.gameObject);
            finishLabel = finish.GetComponentInChildren<TMP_Text>();
            finishLabel.text = "Finish";
            finish.GetComponent<Image>().color = FinishTint;
            finishButton = finish.GetComponent<Button>();
            finishButton.interactable = true;

            // The order in the vertical layout is the order of the children.
            string[] order = { TimerLabelName, StepLabelName, ReadingLabelName, CountLabelName, NextButtonName, RecordButtonName, ExportButtonName, FinishButtonName };
            float[] heights = { HudTimerHeight, HudStepHeight, HudLabelHeight, HudLabelHeight, HudButtonHeight, HudButtonHeight, HudButtonHeight, HudButtonHeight };
            float total = 0f;
            for (int i = 0; i < order.Length; i++)
            {
                Transform row = hudPanel.Find(order[i]);
                if (row == null) continue;

                row.SetSiblingIndex(i);
                SetPreferredHeight(row.gameObject, heights[i]);
                total += heights[i];
            }

            SetAutoSize(stepLabel.GetComponent<TMP_Text>(), 18f, 30f);
            ResizeHudPanel(hudPanel, total, order.Length);
            return timerText;
        }

        // The panel is as tall as its rows, the spaces between them and its padding.
        private static void ResizeHudPanel(Transform hudPanel, float rowsHeight, int rowCount)
        {
            var layout = hudPanel.GetComponent<VerticalLayoutGroup>();
            float spaces = layout != null ? layout.spacing * (rowCount - 1) : 0f;
            float padding = layout != null ? layout.padding.vertical : 20f;

            var rect = (RectTransform)hudPanel;
            Undo.RecordObject(rect, "HUD panel size");
            rect.sizeDelta = new Vector2(HudWidth, rowsHeight + spaces + padding);
        }

        // ---------- right panel: the assistant ----------

        private static void BuildChatPanel(Transform chatPanel, Transform statusLabel)
        {
            var panel = (RectTransform)chatPanel;
            Undo.RecordObject(panel, "Chat panel size");
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-12f, 0f);
            panel.sizeDelta = new Vector2(ChatWidth, ChatHeight);

            Transform title = FindOrCopy(chatPanel, ChatTitleName, statusLabel.gameObject);
            var titleText = title.GetComponent<TMP_Text>();
            titleText.text = "Lab assistant";
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = AccentColour;
            SetAutoSize(titleText, 28f, 40f);
            titleText.alignment = TextAlignmentOptions.Left;
            SetTopRow((RectTransform)title, ChatSide, ChatSide, 14f, 48f);

            BuildQuestionGrid(chatPanel.Find(QuestionGridName));

            var status = (RectTransform)statusLabel;
            SetTopRow(status, ChatSide, ChatSide, 2f * ChatButtonHeight + ChatGap + 82f, 34f);
            var statusText = statusLabel.GetComponent<TMP_Text>();
            statusText.alignment = TextAlignmentOptions.Left;
            SetAutoSize(statusText, 18f, 24f);

            BuildAnswerArea(chatPanel);
        }

        // Four buttons in two columns. A grid cell is as wide as half the panel, so a question fits on two lines.
        private static void BuildQuestionGrid(Transform grid)
        {
            if (grid == null) return;

            float top = 70f;
            float gridHeight = 2f * ChatButtonHeight + ChatGap;
            SetTopRow((RectTransform)grid, ChatSide, ChatSide, top, gridHeight);

            var layout = grid.GetComponent<GridLayoutGroup>();
            Undo.RecordObject(layout, "Question grid");
            layout.cellSize = new Vector2((ChatWidth - 2f * ChatSide - ChatGap) / 2f, ChatButtonHeight);
            layout.spacing = new Vector2(ChatGap, ChatGap);
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;

            for (int i = 1; i <= 4; i++)
            {
                Transform button = grid.Find("QuestionButton" + i);
                if (button == null) continue;

                button.SetSiblingIndex(i - 1);   // the grid fills in the order of the children
                var text = button.GetComponentInChildren<TMP_Text>();
                if (text == null) continue;

                text.alignment = TextAlignmentOptions.Center;
                SetAutoSize(text, 16f, 26f);
            }
        }

        // The answer sits on a lighter panel and has the rest of the space.
        // The text is inside a scroll area, so a long answer scrolls and keeps its size.
        private static void BuildAnswerArea(Transform chatPanel)
        {
            Transform answer = FindDeep(chatPanel, AnswerLabelName);   // it moves into the scroll area, so look below the panel
            if (answer == null) return;

            Transform background = FindOrCreateImage(chatPanel, AnswerBackgroundName, AnswerColour);
            background.SetSiblingIndex(0);
            float top = 2f * ChatButtonHeight + ChatGap + 82f + 34f + 12f;
            SetFill((RectTransform)background, 16f, 16f, 16f, top);

            ScrollRect scroll = BuildAnswerScroll(chatPanel, top);
            PlaceAnswerInScroll(answer, scroll);

            var presenter = chatPanel.GetComponent<ChatPanelPresenter>();
            if (presenter != null) LabSerialized.SetObject(presenter, "answerScroll", scroll);
        }

        // AnswerScroll (the ScrollRect) holds a Viewport that cuts the text off at its edge, and a thin bar on the right.
        // The Viewport has an invisible Image so a click or a drag anywhere in the area scrolls it.
        private static ScrollRect BuildAnswerScroll(Transform chatPanel, float top)
        {
            RectTransform root = FindOrMakeRect(chatPanel, AnswerScrollName);
            root.SetSiblingIndex(1);   // above the answer background, below the buttons
            SetFill(root, 16f, 16f, 16f, top);
            var scroll = GetOrAdd<ScrollRect>(root.gameObject);

            RectTransform viewport = FindOrMakeRect(root, ViewportName);
            SetFill(viewport, AnswerInsetSide, AnswerInsetBottom, ScrollbarWidth + 2f * ScrollbarGap, AnswerInsetTop);
            var hitArea = GetOrAdd<Image>(viewport.gameObject);
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            GetOrAdd<RectMask2D>(viewport.gameObject);

            Scrollbar bar = BuildAnswerScrollbar(root);

            Undo.RecordObject(scroll, "Answer scroll");
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = AnswerScrollSensitivity;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;   // the bar shows only when the answer is longer than the area
            return scroll;
        }

        // A flat bar made by hand: a track, a sliding area and a handle. No sprite is needed, so it is plain colour.
        private static Scrollbar BuildAnswerScrollbar(RectTransform scrollRoot)
        {
            RectTransform bar = FindOrMakeRect(scrollRoot, ScrollbarName);
            Undo.RecordObject(bar, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(ScrollbarWidth, -2f * AnswerInsetBottom);
            bar.anchoredPosition = new Vector2(-ScrollbarGap, 0f);
            GetOrAdd<Image>(bar.gameObject).color = ScrollTrackColour;

            RectTransform area = FindOrMakeRect(bar, SlidingAreaName);
            SetFill(area, 0f, 0f, 0f, 0f);

            RectTransform handle = FindOrMakeRect(area, HandleName);
            Undo.RecordObject(handle, "Scrollbar handle");
            handle.offsetMin = Vector2.zero;   // the Scrollbar moves the anchors, so the offsets must be 0
            handle.offsetMax = Vector2.zero;
            var handleImage = GetOrAdd<Image>(handle.gameObject);
            handleImage.color = ScrollHandleColour;

            var scrollbar = GetOrAdd<Scrollbar>(bar.gameObject);
            Undo.RecordObject(scrollbar, "Scrollbar");
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        // The answer text becomes the content of the scroll area: full width, as tall as its text (ContentSizeFitter),
        // fixed size, top left. It hangs from the top, so a new answer starts at the top (ChatPanelPresenter).
        private static void PlaceAnswerInScroll(Transform answer, ScrollRect scroll)
        {
            if (answer.parent != scroll.viewport) Undo.SetTransformParent(answer, scroll.viewport, "Answer in scroll");

            var rect = (RectTransform)answer;
            Undo.RecordObject(rect, "Answer text");
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var fitter = GetOrAdd<ContentSizeFitter>(answer.gameObject);
            Undo.RecordObject(fitter, "Answer fit");
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = answer.GetComponent<TMP_Text>();
            Undo.RecordObject(text, "Answer text size");
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = false;
            text.fontSize = AnswerFontSize;

            Undo.RecordObject(scroll, "Answer content");
            scroll.content = rect;
        }

        // ---------- hide and show the assistant ----------

        // The Hide button sits in the panel header, on the right. The Assistant tab sits at the right edge of the screen
        // and shows only while the panel is hidden. AssistantPanelToggle does the work (and the H key).
        private static void BuildAssistantToggle(Transform chatPanel, Transform nextButton)
        {
            Transform screen = chatPanel.parent;
            var panelGroup = GetOrAdd<CanvasGroup>(chatPanel.gameObject);

            Button hide = FindOrCopyButton(chatPanel, HideButtonName, nextButton, "Hide (H)");
            SetAnchored((RectTransform)hide.transform, new Vector2(1f, 1f), new Vector2(HideButtonWidth, HideButtonHeight), new Vector2(-ChatSide, -HideButtonTop));
            hide.transform.SetAsLastSibling();   // on top of the title row

            Button tab = FindOrCopyButton(screen, ShowTabName, nextButton, "Assistant (H)");
            SetAnchored((RectTransform)tab.transform, new Vector2(1f, 0.5f), new Vector2(TabWidth, TabHeight), new Vector2(-ChatGap, 0f));
            Undo.RecordObject(tab.gameObject, "Assistant tab");
            tab.gameObject.SetActive(false);   // the panel is open at the start

            AssistantPanelToggle toggle = GetOrAdd<AssistantPanelToggle>(screen.gameObject);
            LabSerialized.SetObject(toggle, "panelGroup", panelGroup);
            LabSerialized.SetObject(toggle, "hideButton", hide);
            LabSerialized.SetObject(toggle, "showTab", tab);
        }

        // A button copied from the Next step button, so it looks the same.
        private static Button FindOrCopyButton(Transform parent, string buttonName, Transform template, string label)
        {
            Transform button = FindOrCopy(parent, buttonName, template.gameObject);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            SetAutoSize(text, 16f, 26f);

            var component = button.GetComponent<Button>();
            Undo.RecordObject(component, "Button state");
            component.interactable = true;
            return component;
        }

        // ---------- the Finish notice ----------

        private static NoticeParts BuildNotice(Transform labUi, Transform textTemplate, Transform buttonTemplate)
        {
            Transform old = labUi.Find(NoticeName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var parts = new NoticeParts();
            RectTransform root = MakeRect(labUi, NoticeName);
            SetFill(root, 0f, 0f, 0f, 0f);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = DimColour;   // dims the screen behind the notice and takes the clicks, so nothing behind it is pressed by mistake

            RectTransform card = MakeRect(root, "Card");
            SetCentre(card, new Vector2(800f, 460f));
            card.gameObject.AddComponent<Image>().color = CardColour;

            RectTransform confirm = MakeRect(card, "ConfirmGroup");
            SetFill(confirm, 0f, 0f, 0f, 0f);
            parts.ConfirmText = MakeNoticeText(confirm, textTemplate, "ConfirmText", 40f, 40f, 270f, Color.white);
            parts.Yes = MakeNoticeButton(confirm, buttonTemplate, "YesButton", "Yes, finish", true);
            parts.KeepWorking = MakeNoticeButton(confirm, buttonTemplate, "KeepWorkingButton", "Keep working", false);

            RectTransform done = MakeRect(card, "DoneGroup");
            SetFill(done, 0f, 0f, 0f, 0f);
            parts.DoneTitle = MakeNoticeText(done, textTemplate, "DoneTitle", 40f, 30f, 80f, DoneColour);
            parts.DoneTitle.fontStyle = FontStyles.Bold;
            SetAutoSize(parts.DoneTitle, 40f, 64f);
            parts.DoneText = MakeNoticeText(done, textTemplate, "DoneText", 40f, 115f, 205f, Color.white);
            parts.Export = MakeNoticeButton(done, buttonTemplate, "ExportButton", "Export results", true);
            parts.Close = MakeNoticeButton(done, buttonTemplate, "CloseButton", "Close", false);
            done.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
            parts.Root = root.gameObject;
            parts.ConfirmGroup = confirm.gameObject;
            parts.DoneGroup = done.gameObject;
            return parts;
        }

        // A block of text, copied from an existing label so it has the same font and look.
        // The numbers are the gap to the left and right, the gap to the top, and the height of the block.
        // The buttons sit at the bottom of the card (40 + 80 from the bottom), so a block must end 140 above the bottom.
        private static TMP_Text MakeNoticeText(RectTransform parent, Transform template, string textName, float sideGap, float top, float height, Color colour)
        {
            GameObject copy = Object.Instantiate(template.gameObject, parent);
            copy.name = textName;
            Undo.RegisterCreatedObjectUndo(copy, textName);
            SetTopRow((RectTransform)copy.transform, sideGap, sideGap, top, height);

            var text = copy.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.text = string.Empty;
            SetAutoSize(text, 24f, 38f);
            return text;
        }

        // A button copied from the Next step button. The left button sits left of the middle, the other one right of it.
        private static Button MakeNoticeButton(RectTransform parent, Transform template, string buttonName, string label, bool leftOfMiddle)
        {
            GameObject copy = Object.Instantiate(template.gameObject, parent);
            copy.name = buttonName;
            Undo.RegisterCreatedObjectUndo(copy, buttonName);

            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(leftOfMiddle ? 1f : 0f, 0f);
            rect.sizeDelta = new Vector2(300f, 80f);
            rect.anchoredPosition = new Vector2(leftOfMiddle ? -12f : 12f, 40f);

            copy.GetComponentInChildren<TMP_Text>().text = label;
            return copy.GetComponent<Button>();
        }

        // ---------- the session object and the headset screen ----------

        // LabSession is always active, so the timer and the Finish controller work while the student screen is hidden.
        private static bool WireSession(Transform labUi, TMP_Text timerLabel, Button finishButton, TMP_Text finishLabel, NoticeParts notice, StringBuilder log)
        {
            var roles = Object.FindAnyObjectByType<RoleScreens>(FindObjectsInactive.Include);
            var recorder = Object.FindAnyObjectByType<MeasurementRecorder>(FindObjectsInactive.Include);
            var exporter = Object.FindAnyObjectByType<ReportExporter>(FindObjectsInactive.Include);
            if (roles == null || recorder == null || exporter == null)
            {
                log.AppendLine("  ERROR: RoleScreens, MeasurementRecorder or ReportExporter is missing from the scene, so the timer and Finish were not wired.");
                return false;
            }

            Transform session = labUi.Find(SessionName);
            if (session == null)
            {
                var go = new GameObject(SessionName);
                Undo.RegisterCreatedObjectUndo(go, SessionName);
                go.transform.SetParent(labUi, false);
                session = go.transform;
            }

            LabTimer timer = GetOrAdd<LabTimer>(session.gameObject);
            LabSerialized.SetObject(timer, "roleScreens", roles);
            LabSerialized.SetObject(timer, "timerLabel", timerLabel);

            LabFinishController finish = GetOrAdd<LabFinishController>(session.gameObject);
            LabSerialized.SetObject(finish, "timer", timer);
            LabSerialized.SetObject(finish, "recorder", recorder);
            LabSerialized.SetObject(finish, "exporter", exporter);
            LabSerialized.SetObject(finish, "finishButton", finishButton);
            LabSerialized.SetObject(finish, "finishButtonLabel", finishLabel);
            LabSerialized.SetObject(finish, "noticeRoot", notice.Root);
            LabSerialized.SetObject(finish, "confirmGroup", notice.ConfirmGroup);
            LabSerialized.SetObject(finish, "doneGroup", notice.DoneGroup);
            LabSerialized.SetObject(finish, "confirmText", notice.ConfirmText);
            LabSerialized.SetObject(finish, "doneTitle", notice.DoneTitle);
            LabSerialized.SetObject(finish, "doneText", notice.DoneText);
            LabSerialized.SetObject(finish, "yesButton", notice.Yes);
            LabSerialized.SetObject(finish, "keepWorkingButton", notice.KeepWorking);
            LabSerialized.SetObject(finish, "exportButton", notice.Export);
            LabSerialized.SetObject(finish, "closeButton", notice.Close);
            return true;
        }

        // The headset gets a copy of the screen in the air in front of the player. The bigger panels need a bigger copy.
        private static void MakeHeadsetScreenBigger(StringBuilder log)
        {
            var adapter = Object.FindAnyObjectByType<HudCanvasAdapter>(FindObjectsInactive.Include);
            if (adapter == null)
            {
                log.AppendLine("  Warning: no HudCanvasAdapter, so the headset screen size was not changed.");
                return;
            }

            var serialized = new SerializedObject(adapter);
            serialized.FindProperty("distanceMetres").floatValue = VrDistanceMetres;
            serialized.FindProperty("worldSizePixels").vector2Value = VrSizePixels;
            serialized.ApplyModifiedProperties();
            log.AppendLine($"  Headset screen set to {VrSizePixels.x:0} x {VrSizePixels.y:0} pixels at {VrDistanceMetres:0.0} m (not tested in a headset).");
        }

        // ---------- small helpers ----------

        // Finds a child by name, or makes it as a copy of a part that is already there.
        private static Transform FindOrCopy(Transform parent, string childName, GameObject template)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing;

            GameObject copy = Object.Instantiate(template, parent);
            copy.name = childName;
            Undo.RegisterCreatedObjectUndo(copy, childName);
            return copy.transform;
        }

        private static Transform FindOrCreateImage(Transform parent, string childName, Color colour)
        {
            Transform existing = parent.Find(childName);
            if (existing == null)
            {
                RectTransform made = MakeRect(parent, childName);
                made.gameObject.AddComponent<Image>();
                existing = made;
            }

            var image = existing.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;   // the answer background must not take clicks
            return existing;
        }

        // Finds a child rectangle by name, or makes an empty one.
        private static RectTransform FindOrMakeRect(Transform parent, string rectName)
        {
            Transform existing = parent.Find(rectName);
            return existing != null ? (RectTransform)existing : MakeRect(parent, rectName);
        }

        // Finds a part by name at any depth below the root (inactive parts too).
        private static Transform FindDeep(Transform root, string partName)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == partName) return part;
            }
            return null;
        }

        private static RectTransform MakeRect(Transform parent, string rectName)
        {
            var go = new GameObject(rectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, rectName);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static void SetPreferredHeight(GameObject row, float height)
        {
            LayoutElement element = GetOrAdd<LayoutElement>(row);
            Undo.RecordObject(element, "Row height");
            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static void SetAutoSize(TMP_Text text, float minSize, float maxSize)
        {
            Undo.RecordObject(text, "Text size");
            text.enableAutoSizing = true;
            text.fontSizeMin = minSize;
            text.fontSizeMax = maxSize;
        }

        // A row along the top, as wide as its parent. The numbers are the gaps to the left, right and top, and the height.
        private static void SetTopRow(RectTransform rect, float left, float right, float top, float height)
        {
            Undo.RecordObject(rect, "Row");
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        // Fills the parent, with a gap on each side.
        private static void SetFill(RectTransform rect, float left, float bottom, float right, float top)
        {
            Undo.RecordObject(rect, "Fill");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // Fixed size at a corner or an edge. The anchor and the pivot are the same point, so the numbers are the gap to that edge.
        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            Undo.RecordObject(rect, "Anchored");
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetCentre(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
#endif
