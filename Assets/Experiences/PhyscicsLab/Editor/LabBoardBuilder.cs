#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using PhysicsLab.Core.Rigs;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Build guide boards. Also the last step of Lab > 0.
    /// Makes two guide boards, one behind each bench:
    ///   the practice board: how to move, grab, hang a mass, clamp a pulley and use the spring scale,
    ///   the lab board: the steps of the lab manual, the four cases (Table 1), recording and finishing.
    /// A board has several pages and two buttons, Previous and Next (GuideBoard and BoardButton).
    /// The board stands behind its bench, so the student sees it above the bench and does not have to walk away to read it.
    /// A board is a world-space canvas (1 unit = 1 mm = 0.001 m) on a pole. The pole and the foot have no collider.
    /// The only colliders are the two buttons of each board, so the desktop aim dot and the headset ray can press them.
    /// The case table is read from Lab5Procedure, so it always matches the step rules.
    /// Both boards sit under one object, LabGuideBoards. This also removes the three old boards of Phase 4G.
    /// Also puts the headset start at the desktop start, so a headset user begins in front of the benches.
    /// Safe to run twice: it rebuilds the boards. Run the practice bench builder first: the practice board is placed behind it.
    /// </summary>
    public static class LabBoardBuilder
    {
        private const string RootName = "LabGuideBoards";
        private const string BenchName = "LabBench";
        private const string ChairName = "LabChair";
        private const string PolesMaterialPath = "Assets/Experiences/PhyscicsLab/Materials/Lab/LabAluminium.mat";
        private const string BaseMaterialPath = "Assets/Experiences/PhyscicsLab/Materials/Lab/LabBlackMatte.mat";

        // All sizes are in canvas units. One unit is one millimetre, because the canvas is scaled by 0.001.
        private const float MetresPerUnit = 0.001f;
        private const float BoardWidthUnits = 1600f;
        private const float MarginUnits = 60f;
        private const float TitleUnits = 80f;
        private const float BodyUnits = 52f;
        private const float LineHeightFactor = 1.25f;
        private const int MaxBodyLines = 9;             // a page with more lines would run into the buttons
        private const int MaxVisibleCharacters = 50;    // a longer line would wrap and add a line
        private const float TopBlockUnits = 200f;       // the title and the line under it
        private const float ButtonHeightUnits = 110f;
        private const float ButtonWidthUnits = 380f;
        private const float ButtonBarUnits = 190f;      // the buttons and the space around them
        private const float ButtonDepthUnits = 60f;     // how thick the button collider is
        private const float BoardHeightUnits = TopBlockUnits + MaxBodyLines * BodyUnits * LineHeightFactor + ButtonBarUnits;

        private const float BoardCentreHeightMetres = 1.75f;   // the bottom of the board stays above the pulleys and the hanging masses
        private const float BehindBenchMetres = 0.55f;         // from the far edge of the bench to the board

        private static readonly Color PanelColour = new Color(0.07f, 0.10f, 0.15f, 1f);
        private static readonly Color AccentColour = new Color(0.31f, 0.76f, 0.97f, 1f);
        private const string Heading = "<color=#FFD54F><b>";
        private const string HeadingEnd = "</b></color>";
        private const string Quiet = "<color=#9AA7B5>";
        private const string QuietEnd = "</color>";

        [MenuItem("Lab/Build guide boards")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabBoardBuilder]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                log.AppendLine("  ERROR: TextMesh Pro has no default font. Use Window > TextMeshPro > Import TMP Essential Resources.");
                return false;
            }

            Bounds labBench = MeasureBench();
            Vector3 front = FindFrontDirection(labBench);
            CheckPages(log);

            Transform root = RebuildRoot(log);
            Vector3 practiceShift = LabPracticeBenchBuilder.OffsetMetres;
            MakeBoard(root, "Board_Practice", "practice", WritePracticePages(), labBench, practiceShift, front);
            MakeBoard(root, "Board_Lab", "lab", WriteLabPages(), labBench, Vector3.zero, front);

            log.AppendLine($"  2 boards made under {RootName}: one behind the practice bench, one behind the lab bench. Each has Previous and Next buttons.");
            PlaceHeadsetAtDesktopSpawn(labBench.min.y, log);
            return true;
        }

        // ---------- where the boards go ----------

        // The bench is what the lab stands on. Its lowest point is the floor.
        private static Bounds MeasureBench()
        {
            Transform bench = LabSceneFixes.FindSceneObject(BenchName);
            Renderer[] parts = bench != null ? bench.GetComponentsInChildren<Renderer>() : new Renderer[0];
            if (parts.Length == 0)
            {
                Transform equipment = LabSceneFixes.FindSceneObject("LabEquipments");
                Vector3 table = equipment != null ? equipment.position : Vector3.zero;
                return new Bounds(new Vector3(table.x, 0.45f, table.z), new Vector3(2f, 0.9f, 1f));
            }

            Bounds all = parts[0].bounds;
            for (int i = 1; i < parts.Length; i++) all.Encapsulate(parts[i].bounds);
            return all;
        }

        // The student side of the bench: the way from the bench to its chair. Without a chair, the way to the desktop start.
        private static Vector3 FindFrontDirection(Bounds bench)
        {
            var centre = new Vector3(bench.center.x, 0f, bench.center.z);
            Transform chair = LabSceneFixes.FindSceneObject(ChairName);
            if (chair != null) return FlatDirection(chair.position - centre, Vector3.forward);

            var desktop = Object.FindAnyObjectByType<DesktopRigController>(FindObjectsInactive.Include);
            Vector3 spawn = desktop != null ? desktop.transform.position : centre + Vector3.forward * 6f;
            return FlatDirection(spawn - centre, Vector3.forward);
        }

        private static Vector3 FlatDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 1e-6f ? fallback : direction.normalized;
        }

        // ---------- what the boards say ----------

        // Plain words and plain ASCII letters only (no degree sign), so the default font draws every character.
        // The first entry of a page is its title, the others are the lines of the body.
        // A page has at most MaxBodyLines lines and MaxVisibleCharacters letters per line. CheckPages warns about a page that does not fit.
        private static string[][] WritePracticePages()
        {
            return new[]
            {
                new[]
                {
                    "PRACTICE BENCH",
                    "This bench is a copy of the lab bench.",
                    "Nothing you do here is recorded or marked.",
                    "Practice as long as you like.",
                    "",
                    Heading + "Press the buttons below" + HeadingEnd,
                    "Computer: aim at the button, then click.",
                    "Headset: point at it, squeeze the grip.",
                    Quiet + "Next goes forward. Previous goes back." + QuietEnd
                },
                new[]
                {
                    "1   MOVE AND LOOK",
                    Heading + "Headset" + HeadingEnd,
                    "Look around: turn your head.",
                    "Walk: push the LEFT stick.",
                    "Turn: push the RIGHT stick sideways.",
                    Heading + "Computer" + HeadingEnd,
                    "First, click the scene once.",
                    "Look: move the mouse. Walk: W, A, S, D.",
                    "Use the buttons on screen: press Tab.",
                    Quiet + "Feel dizzy? Stop and take the headset off." + QuietEnd
                },
                new[]
                {
                    "2   GRAB AND LET GO",
                    Heading + "Headset" + HeadingEnd,
                    "Grab: squeeze the side button (grip).",
                    "Let go: release the grip.",
                    Heading + "Computer" + HeadingEnd,
                    "Grab: hold the left mouse button on a part.",
                    "Push or pull it: roll the mouse wheel.",
                    "Let go: release the mouse button.",
                    Quiet + "Try it: pick up a mass and put it down." + QuietEnd
                },
                new[]
                {
                    "3   HANG A MASS",
                    "Pick up a mass from the bench.",
                    "Carry it to the small dark loop under a pulley.",
                    "Let go within 10 cm of the loop.",
                    "The mass hangs on the loop.",
                    "Grab it again to take it off.",
                    Heading + "150 g needs two masses" + HeadingEnd,
                    "Hang 100 g and 50 g on the same loop.",
                    Quiet + "One loop holds up to 4 masses." + QuietEnd
                },
                new[]
                {
                    "4   MOVE A PULLEY",
                    "Grab a pulley. Glowing spots show on the rim.",
                    "The spots are 5 degrees apart.",
                    "Let go within 10 cm of a spot.",
                    "The pulley snaps on and clamps.",
                    "Read its angle on the numbers on the table.",
                    Quiet + "A number shows every 30 degrees." + QuietEnd,
                    "",
                    Heading + "Try it: clamp a pulley at 30 degrees" + HeadingEnd
                },
                new[]
                {
                    "5   THE SPRING SCALE",
                    "Grab the spring scale.",
                    "Pull it away from the ring.",
                    "The string pulls the ring along.",
                    "Keep pulling until the ring sits on the post.",
                    "Hold still. The scale shows the force in N.",
                    Quiet + "Let go and it reads 0 N, like a real scale." + QuietEnd,
                    "",
                    Heading + "Try it: put the ring on the centre post" + HeadingEnd
                },
                new[]
                {
                    "READY FOR THE LAB?",
                    "Walk to the lab bench next to this one.",
                    "Its board shows the steps and the four cases.",
                    "",
                    Heading + "The timer" + HeadingEnd,
                    "The lab time counts down on the left screen.",
                    "Press Finish there when you are done.",
                    Quiet + "The time already runs while you practice." + QuietEnd
                }
            };
        }

        private static string[][] WriteLabPages()
        {
            string labTime = LabTimer.FormatClock(Mathf.RoundToInt(LabTimer.DefaultTotalSeconds));
            return new[]
            {
                new[]
                {
                    "LAB BENCH",
                    "Goal: the ring sits round the centre post, still.",
                    "",
                    Heading + "Steps" + HeadingEnd + Quiet + "  (your step shows on the left)" + QuietEnd,
                    "1  Setup: the spring scale reads 0 N.",
                    "2  Configure masses: clamp pulleys, hang masses.",
                    "3  Connect scale: hook it on and pull.",
                    "4  Equilibrium: ring centred and still.",
                    "5  Record: press Record, then Next step."
                },
                WriteCasePage(),
                new[]
                {
                    "SET UP A CASE",
                    "Press Next step. The left screen shows",
                    "Configure masses (n to fix). n must reach 0.",
                    Heading + "Clamp a pulley" + HeadingEnd,
                    "Let go within 10 cm of the spot for its angle.",
                    "It counts if it is within 2 degrees.",
                    Heading + "Hang the masses" + HeadingEnd,
                    "Hang them on the loop under that pulley.",
                    Quiet + "Cases A to C: leave Pulley 3 where it is." + QuietEnd
                },
                new[]
                {
                    "CONNECT THE SCALE",
                    "Press Next step.",
                    "Grab the spring scale. Pull it away from the ring.",
                    "Keep pulling until the ring sits on the post",
                    "and stays still. The step then shows (done).",
                    Quiet + "The scale reads only while you hold it." + QuietEnd,
                    Quiet + "Let go and the reading goes to 0 N." + QuietEnd,
                    Heading + "Stuck?" + HeadingEnd,
                    "Ask the assistant on the right screen."
                },
                new[]
                {
                    "RECORD AND REPEAT",
                    "Press Record while the ring is still.",
                    "Press Next step for the next case.",
                    "Do the four cases in order: A, B, C, D.",
                    "",
                    "Export saves your measurements",
                    "as a CSV file.",
                    Quiet + "Export before you close the page." + QuietEnd
                },
                new[]
                {
                    "FINISH THE LAB",
                    "When all four cases are recorded,",
                    "press Finish on the left screen.",
                    "",
                    Heading + "The timer" + HeadingEnd,
                    $"It counts down from {labTime}.",
                    "At 0:00:00 the lab ends by itself.",
                    Quiet + "It turns orange for the last 10 minutes." + QuietEnd
                }
            };
        }

        private static string[] WriteCasePage()
        {
            var lines = new List<string>
            {
                "THE FOUR CASES",
                Heading + "Do A, B, C and D" + HeadingEnd,
                Quiet + "Any pulley may take any row of the case." + QuietEnd
            };

            int caseCount = Lab5Procedure.CaseCountFor(Lab5Procedure.MaxPulleys);
            for (int i = 0; i < caseCount; i++) lines.Add(WriteCaseLine(Lab5Procedure.CaseAt(i)));

            lines.Add("");
            lines.Add(Quiet + "150 g = 100 g + 50 g.   300 g = 200 g + 100 g." + QuietEnd);
            lines.Add(Quiet + "Cases A to C use two pulleys. Case D uses three." + QuietEnd);
            return lines.ToArray();
        }

        // One row of Table 1, for example: A   200 g at 30 deg   200 g at 120 deg. The pos tags make the columns line up,
        // and the smaller size keeps three columns inside the board.
        private static string WriteCaseLine(Lab5Case lab5Case)
        {
            var line = new StringBuilder(lab5Case.Id);
            for (int i = 0; i < lab5Case.Loads.Length; i++)
            {
                CaseLoad load = lab5Case.Loads[i];
                line.Append($"<pos={6 + 29 * i}%><size=85%>{load.MassKg * 1000f:0} g at {load.AngleDeg:0} deg</size>");
            }
            return line.ToString();
        }

        // A page that is too long runs into the buttons. A line that is too long wraps and adds a line. Warn so the author fixes the words.
        private static void CheckPages(StringBuilder log)
        {
            CheckPageSet("practice", WritePracticePages(), log);
            CheckPageSet("lab", WriteLabPages(), log);
        }

        private static void CheckPageSet(string boardName, string[][] pages, StringBuilder log)
        {
            for (int p = 0; p < pages.Length; p++)
            {
                int lineCount = pages[p].Length - 1;   // entry 0 is the title
                if (lineCount > MaxBodyLines) log.AppendLine($"  Warning: {boardName} board, page {p + 1} has {lineCount} lines. The most that fits is {MaxBodyLines}.");

                for (int i = 1; i < pages[p].Length; i++)
                {
                    int letters = Regex.Replace(pages[p][i], "<[^>]+>", string.Empty).Length;
                    if (letters > MaxVisibleCharacters) log.AppendLine($"  Warning: {boardName} board, page {p + 1}, line {i} has {letters} letters. It may wrap.");
                }
            }
        }

        // ---------- one board ----------

        private static void MakeBoard(Transform root, string boardName, string idPart, string[][] pages, Bounds labBench, Vector3 shift, Vector3 front)
        {
            Bounds bench = labBench;
            bench.center += shift;   // the practice bench is the lab bench moved by a fixed amount

            float halfDepth = Mathf.Abs(front.x) * bench.extents.x + Mathf.Abs(front.z) * bench.extents.z;
            Vector3 behind = new Vector3(bench.center.x, 0f, bench.center.z) - front * (halfDepth + BehindBenchMetres);
            var centre = new Vector3(behind.x, bench.min.y + BoardCentreHeightMetres, behind.z);

            var board = new GameObject(boardName);
            Undo.RegisterCreatedObjectUndo(board, "Guide board");
            board.transform.SetParent(root, false);
            board.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(-front, Vector3.up));   // the front of the canvas looks at the student

            RectTransform canvas = MakeCanvas(board.transform);
            MakePanel(canvas);
            TextMeshProUGUI title = MakeTitle(canvas);
            TextMeshProUGUI body = MakeBody(canvas);
            TextMeshProUGUI pageLabel = MakePageLabel(canvas);
            BoardButton previous = MakeButton(canvas, "Previous page", "< Previous", new Vector2(0f, 0f), new Vector2(MarginUnits, 40f), $"board-{idPart}-previous");
            BoardButton next = MakeButton(canvas, "Next page", "Next >", new Vector2(1f, 0f), new Vector2(-MarginUnits, 40f), $"board-{idPart}-next");

            WireGuideBoard(board, pages, title, body, pageLabel, previous, next);
            MakeStand(board.transform, BoardHeightUnits * MetresPerUnit, bench.min.y);
        }

        // The component keeps the pages and changes the text when a button is pressed.
        // The first page is also written into the labels now, so the board is readable in the Scene view before Play.
        private static void WireGuideBoard(GameObject board, string[][] pages, TMP_Text title, TMP_Text body, TMP_Text pageLabel, BoardButton previous, BoardButton next)
        {
            GuideBoard guide = Undo.AddComponent<GuideBoard>(board);
            LabSerialized.SetObject(guide, "titleLabel", title);
            LabSerialized.SetObject(guide, "bodyLabel", body);
            LabSerialized.SetObject(guide, "pageLabel", pageLabel);
            LabSerialized.SetObject(guide, "previousButton", previous);
            LabSerialized.SetObject(guide, "nextButton", next);

            var serialized = new SerializedObject(guide);
            SerializedProperty list = serialized.FindProperty("pages");
            list.arraySize = pages.Length;
            for (int i = 0; i < pages.Length; i++)
            {
                SerializedProperty page = list.GetArrayElementAtIndex(i);
                page.FindPropertyRelative("title").stringValue = "<b>" + pages[i][0] + "</b>";
                page.FindPropertyRelative("body").stringValue = string.Join("\n", pages[i], 1, pages[i].Length - 1);
            }
            serialized.ApplyModifiedProperties();

            title.text = "<b>" + pages[0][0] + "</b>";
            body.text = string.Join("\n", pages[0], 1, pages[0].Length - 1);
            pageLabel.text = $"Page 1 of {pages.Length}";
        }

        private static RectTransform MakeCanvas(Transform board)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(board, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(BoardWidthUnits, BoardHeightUnits);
            rect.localScale = Vector3.one * MetresPerUnit;
            return rect;
        }

        private static void MakePanel(RectTransform canvas)
        {
            RectTransform panel = MakeChild(canvas, "Panel");
            Stretch(panel, 0f);
            AddColour(panel.gameObject, PanelColour);

            RectTransform line = MakeChild(canvas, "TitleLine");
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(-2f * MarginUnits, 4f);
            line.anchoredPosition = new Vector2(0f, -(MarginUnits + TitleUnits + 20f));
            AddColour(line.gameObject, AccentColour);
        }

        private static TextMeshProUGUI MakeTitle(RectTransform canvas)
        {
            RectTransform rect = MakeChild(canvas, "Title");
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-2f * MarginUnits, TitleUnits + 20f);
            rect.anchoredPosition = new Vector2(0f, -MarginUnits);

            TextMeshProUGUI text = AddText(rect.gameObject, TitleUnits);
            text.color = AccentColour;
            return text;
        }

        // The body fills the space between the title block and the button bar.
        private static TextMeshProUGUI MakeBody(RectTransform canvas)
        {
            RectTransform rect = MakeChild(canvas, "Body");
            Stretch(rect, MarginUnits);
            rect.offsetMin = new Vector2(MarginUnits, ButtonBarUnits);
            rect.offsetMax = new Vector2(-MarginUnits, -TopBlockUnits);

            TextMeshProUGUI text = AddText(rect.gameObject, BodyUnits);
            text.color = Color.white;
            return text;
        }

        // "Page 2 of 7", between the two buttons.
        private static TextMeshProUGUI MakePageLabel(RectTransform canvas)
        {
            RectTransform rect = MakeChild(canvas, "PageLabel");
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(500f, ButtonHeightUnits);
            rect.anchoredPosition = new Vector2(0f, 40f);

            TextMeshProUGUI text = AddText(rect.gameObject, 48f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.60f, 0.65f, 0.71f, 1f);
            return text;
        }

        // A press button: a picture with a label, a box collider for the aim dot and the headset ray,
        // and the same LabObject / XRSimpleInteractable pair that every lab part uses.
        // The object name is what the aim dot shows when it hovers.
        private static BoardButton MakeButton(RectTransform canvas, string buttonName, string label, Vector2 corner, Vector2 offset, string objectId)
        {
            RectTransform rect = MakeChild(canvas, buttonName);
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = new Vector2(ButtonWidthUnits, ButtonHeightUnits);
            rect.anchoredPosition = offset;

            var face = rect.gameObject.AddComponent<Image>();
            face.raycastTarget = false;

            var collider = rect.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(ButtonWidthUnits, ButtonHeightUnits, ButtonDepthUnits);

            LabObject labObject = rect.gameObject.AddComponent<LabObject>();
            LabSerialized.SetString(labObject, "objectId", objectId);
            rect.gameObject.AddComponent<XRSimpleInteractable>();
            BoardButton button = rect.gameObject.AddComponent<BoardButton>();
            LabSerialized.SetObject(button, "face", face);
            face.color = new Color(0.20f, 0.45f, 0.70f, 1f);

            RectTransform text = MakeChild(rect, "Label");
            Stretch(text, 0f);
            TextMeshProUGUI words = AddText(text.gameObject, 52f);
            words.alignment = TextAlignmentOptions.Center;
            words.text = "<b>" + label + "</b>";
            words.color = Color.white;
            return button;
        }

        private static TextMeshProUGUI AddText(GameObject target, float size)
        {
            var text = target.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;   // the text never takes a click, only the button collider does
            return text;
        }

        private static void AddColour(GameObject target, Color colour)
        {
            var image = target.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
        }

        private static RectTransform MakeChild(RectTransform parent, string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        // ---------- the stand ----------

        // A pole behind the board and a flat foot on the floor. No colliders, so a stand never blocks a mouse pick or a hand.
        private static void MakeStand(Transform board, float boardHeightMetres, float floorY)
        {
            float poleHeight = board.position.y - boardHeightMetres * 0.5f + 0.05f - floorY;

            GameObject pole = MakePart(board, "Pole", PolesMaterialPath);
            pole.transform.localScale = new Vector3(0.05f, poleHeight, 0.05f);
            pole.transform.position = new Vector3(board.position.x, floorY + poleHeight * 0.5f, board.position.z) + board.forward * 0.04f;

            GameObject foot = MakePart(board, "Foot", BaseMaterialPath);
            foot.transform.localScale = new Vector3(0.55f, 0.03f, 0.45f);
            foot.transform.position = new Vector3(board.position.x, floorY + 0.015f, board.position.z) + board.forward * 0.12f;
        }

        private static GameObject MakePart(Transform board, string partName, string materialPath)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(part, partName);
            part.name = partName;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(board, true);
            part.transform.rotation = board.rotation;

            var look = part.GetComponent<MeshRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null) look.sharedMaterial = material;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.BatchingStatic);
            return part;
        }

        // ---------- the root and the headset start ----------

        // Deletes the old boards (the two of this phase, or the three of Phase 4G) and makes a new, empty root at the world origin.
        // The boards are placed in world space.
        private static Transform RebuildRoot(StringBuilder log)
        {
            Transform old = LabSceneFixes.FindSceneObject(RootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
                log.AppendLine($"  Old {RootName} removed and made again.");
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, RootName);
            return root.transform;
        }

        // The headset rig starts at the world origin, but the lab is 4 m to the side. So the headset starts where the desktop view starts.
        private static void PlaceHeadsetAtDesktopSpawn(float floorY, StringBuilder log)
        {
            var switcher = Object.FindAnyObjectByType<RigModeSwitcher>(FindObjectsInactive.Include);
            if (switcher == null)
            {
                log.AppendLine("  Warning: no RigModeSwitcher, so the headset start was not moved.");
                return;
            }

            var serialized = new SerializedObject(switcher);
            var headset = serialized.FindProperty("headsetRig")?.objectReferenceValue as GameObject;
            var desktop = serialized.FindProperty("desktopRig")?.objectReferenceValue as GameObject;
            if (headset == null || desktop == null)
            {
                log.AppendLine("  Warning: the RigModeSwitcher has no desktop or headset rig, so the headset start was not moved.");
                return;
            }

            Vector3 start = desktop.transform.position;
            Undo.RecordObject(headset.transform, "Headset start");
            headset.transform.SetPositionAndRotation(new Vector3(start.x, floorY, start.z), Quaternion.Euler(0f, desktop.transform.eulerAngles.y, 0f));
            log.AppendLine($"  Headset start moved to ({start.x:0.00}, {floorY:0.00}, {start.z:0.00}), looking the same way as the desktop view.");
        }
    }
}
#endif
