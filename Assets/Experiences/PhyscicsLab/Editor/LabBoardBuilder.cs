#if UNITY_EDITOR
using System.Collections.Generic;
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
    /// Menu: Lab > Build guide boards.
    /// Makes three signs beside the lab bench for a student who has never used VR:
    ///   1 Move and look, 2 Grab and hang, 3 The experiment (the steps of the lab manual and Table 1).
    /// Each sign is a world-space canvas (1 unit = 1 mm of canvas = 0.001 m) on a pole. A sign never moves,
    /// has no collider and has no script, so it costs no work while the lab runs (3 canvases, 2 meshes each).
    /// The case table on sign 3 is read from Lab5Procedure, so it always matches the step rules.
    /// All signs sit under one object, LabGuideBoards. Move that object by hand to move all three.
    /// Also puts the headset start at the desktop start, so a headset user begins in front of the lab.
    /// Safe to run twice: it rebuilds the signs and keeps the place of the LabGuideBoards object.
    /// </summary>
    public static class LabBoardBuilder
    {
        private const string RootName = "LabGuideBoards";
        private const string BenchName = "LabBench";
        private const string PolesMaterialPath = "Assets/Experiences/PhyscicsLab/Materials/Lab/LabAluminium.mat";
        private const string BaseMaterialPath = "Assets/Experiences/PhyscicsLab/Materials/Lab/LabBlackMatte.mat";

        // Canvas sizes are in canvas units. One unit is one millimetre, because the canvas is scaled by 0.001.
        private const float MetresPerUnit = 0.001f;
        private const float BoardWidthUnits = 1100f;
        private const float MarginUnits = 50f;
        private const float TitleUnits = 60f;
        private const float BodyUnits = 40f;
        private const float LineHeightFactor = 1.35f;   // a little more than the font's own line height, so the text never runs out of the sign
        private const float TitleBlockUnits = 100f;     // the title and the line under it

        private const float GapBetweenBoardsMetres = 0.12f;
        private const float BoardCentreHeightMetres = 1.45f;
        private const float SideGapFromBenchMetres = 0.6f;
        private const float TowardViewerMetres = 0.5f;
        private const float ReadingDistanceMetres = 2.2f;   // the signs turn a little toward a point this far in front of them

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

            Bounds bench = MeasureBench();
            string[][] pages = WritePages();
            BoardRow row = PlanRow(bench, FindDesktopSpawn(bench));
            row.HeightUnits = TallestBoardUnits(pages);

            Transform root = RebuildRoot(out Vector3 keptPosition, out Quaternion keptRotation, log);
            for (int i = 0; i < pages.Length; i++) MakeBoard(root, i, pages[i], row);
            root.SetPositionAndRotation(keptPosition, keptRotation);   // a group moved by hand stays where it was put

            log.AppendLine($"  3 boards made under {RootName}, beside the bench. Move that object if you want them elsewhere.");
            PlaceHeadsetAtDesktopSpawn(row.FloorY, log);
            return true;
        }

        // ---------- where the boards go ----------

        private sealed class BoardRow
        {
            public Vector3 Centre;          // the middle of the row, on the floor
            public Vector3 ToViewer;        // flat direction from the bench to the place where the student starts
            public Vector3 Left;            // the student's left when looking at the bench from the start
            public float FloorY;
            public float HeightUnits;       // the height of every board, in canvas units
        }

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

        private static Vector3 FindDesktopSpawn(Bounds bench)
        {
            var desktop = Object.FindAnyObjectByType<DesktopRigController>(FindObjectsInactive.Include);
            return desktop != null ? desktop.transform.position : bench.center + Vector3.forward * 6f;
        }

        private static BoardRow PlanRow(Bounds bench, Vector3 spawn)
        {
            var centre = new Vector3(bench.center.x, 0f, bench.center.z);
            Vector3 toViewer = FlatDirection(spawn - centre, Vector3.forward);
            Vector3 left = Vector3.Cross(Vector3.up, toViewer);

            // The signs stand to the left of the bench (the spare masses stand on the right), a bit toward the student.
            float benchHalfAlongRow = Mathf.Abs(left.x) * bench.extents.x + Mathf.Abs(left.z) * bench.extents.z;
            float rowHalf = 1.5f * BoardWidthUnits * MetresPerUnit + GapBetweenBoardsMetres;
            Vector3 rowCentre = centre + left * (benchHalfAlongRow + SideGapFromBenchMetres + rowHalf)
                                + toViewer * TowardViewerMetres;

            return new BoardRow { Centre = rowCentre, ToViewer = toViewer, Left = left, FloorY = bench.min.y };
        }

        private static Vector3 FlatDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 1e-6f ? fallback : direction.normalized;
        }

        // ---------- what the boards say ----------

        // Plain words and plain ASCII letters only (no degree sign), so the default font draws every character.
        private static string[][] WritePages()
        {
            return new[]
            {
                new[]
                {
                    "1   MOVE AND LOOK",
                    Heading + "Headset" + HeadingEnd,
                    "Look around: turn your head.",
                    "Walk: push the LEFT stick.",
                    "Turn: push the RIGHT stick sideways.",
                    Heading + "Computer" + HeadingEnd,
                    "First, click the scene once.",
                    "Look around: move the mouse.",
                    "Walk: press W, A, S and D.",
                    "Use the buttons: press Tab to free the mouse.",
                    Quiet + "Headset? Press the VR button on the web page." + QuietEnd,
                    Quiet + "Feel dizzy? Stop and take the headset off." + QuietEnd
                },
                new[]
                {
                    "2   GRAB AND HANG",
                    Heading + "Headset" + HeadingEnd,
                    "Grab: squeeze the side button (grip).",
                    "Let go: release the side button.",
                    "Press a button: point at it, pull the trigger.",
                    Heading + "Computer" + HeadingEnd,
                    "Grab: hold the left mouse button on a part.",
                    "Push or pull a mass: roll the mouse wheel.",
                    "Let go: release the mouse button.",
                    Heading + "In the lab" + HeadingEnd,
                    "Hang a mass: bring it to a string loop, let go.",
                    "Clamp a pulley: let go near a glowing spot.",
                    "Take a mass off: grab it again."
                },
                WriteExperimentPage()
            };
        }

        private static string[] WriteExperimentPage()
        {
            var lines = new List<string>
            {
                "3   THE EXPERIMENT",
                "Goal: the ring sits round the centre post, still.",
                Heading + "Steps" + HeadingEnd + Quiet + "  (your step shows on the left)" + QuietEnd,
                "1  Setup: the spring scale reads 0 N.",
                "2  Configure masses: set angles, hang masses.",
                "3  Connect scale: hook it on and pull.",
                "4  Equilibrium: ring centred and still.",
                "5  Record: press Record, then Next step.",
                Heading + "Cases" + HeadingEnd + Quiet + "  (do A, B, C and D)" + QuietEnd
            };

            int caseCount = Lab5Procedure.CaseCountFor(Lab5Procedure.MaxPulleys);
            for (int i = 0; i < caseCount; i++) lines.Add(WriteCaseLine(Lab5Procedure.CaseAt(i)));

            lines.Add(Quiet + "150 g = 100 g + 50 g.   300 g = 200 g + 100 g." + QuietEnd);
            return lines.ToArray();
        }

        // One row of Table 1, for example: A   200 g at 30 deg   200 g at 120 deg. The pos tags make the columns line up,
        // and the smaller size keeps three columns inside the sign.
        private static string WriteCaseLine(Lab5Case lab5Case)
        {
            var line = new StringBuilder(lab5Case.Id);
            for (int i = 0; i < lab5Case.Loads.Length; i++)
            {
                CaseLoad load = lab5Case.Loads[i];
                line.Append($"<pos={6 + 29 * i}%><size=90%>{load.MassKg * 1000f:0} g at {load.AngleDeg:0} deg</size>");
            }
            return line.ToString();
        }

        // ---------- one board ----------

        private static void MakeBoard(Transform root, int index, string[] page, BoardRow row)
        {
            Vector3 centre = BoardCentre(row, index);
            Vector3 awayFromViewer = FlatDirection(centre - (row.Centre + row.ToViewer * ReadingDistanceMetres), -row.ToViewer);

            var board = new GameObject($"Board_{index + 1}");
            Undo.RegisterCreatedObjectUndo(board, "Guide board");
            board.transform.SetParent(root, false);
            board.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(awayFromViewer, Vector3.up));

            RectTransform canvas = MakeCanvas(board.transform, row.HeightUnits);
            MakePanel(canvas);
            MakeTitle(canvas, page[0]);
            MakeBody(canvas, page);
            MakeStand(board.transform, row.HeightUnits * MetresPerUnit, row.FloorY);
        }

        // Every board is as tall as the longest page, so the three look alike.
        private static float TallestBoardUnits(string[][] pages)
        {
            int mostLines = 0;
            foreach (string[] page in pages) mostLines = Mathf.Max(mostLines, page.Length - 1);
            return 2f * MarginUnits + TitleBlockUnits + mostLines * BodyUnits * LineHeightFactor;
        }

        // Board 1 stands at the student's left, board 3 at the right.
        private static Vector3 BoardCentre(BoardRow row, int index)
        {
            float pitch = BoardWidthUnits * MetresPerUnit + GapBetweenBoardsMetres;
            Vector3 onFloor = row.Centre + row.Left * ((1 - index) * pitch);
            float floorToCentre = Mathf.Max(BoardCentreHeightMetres, row.HeightUnits * MetresPerUnit * 0.5f + 0.5f);
            return new Vector3(onFloor.x, row.FloorY + floorToCentre, onFloor.z);
        }

        private static RectTransform MakeCanvas(Transform board, float heightUnits)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(board, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(BoardWidthUnits, heightUnits);
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
            line.anchoredPosition = new Vector2(0f, -(MarginUnits + TitleUnits + 12f));
            AddColour(line.gameObject, AccentColour);
        }

        private static void MakeTitle(RectTransform canvas, string title)
        {
            RectTransform rect = MakeChild(canvas, "Title");
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-2f * MarginUnits, TitleUnits + 10f);
            rect.anchoredPosition = new Vector2(0f, -MarginUnits);

            TextMeshProUGUI text = AddText(rect.gameObject, TitleUnits);
            text.text = "<b>" + title + "</b>";
            text.color = AccentColour;
        }

        private static void MakeBody(RectTransform canvas, string[] page)
        {
            RectTransform rect = MakeChild(canvas, "Body");
            Stretch(rect, MarginUnits);
            rect.offsetMax = new Vector2(-MarginUnits, -(MarginUnits + TitleBlockUnits));

            TextMeshProUGUI text = AddText(rect.gameObject, BodyUnits);
            text.text = string.Join("\n", page, 1, page.Length - 1);   // line 0 is the title
            text.color = Color.white;
        }

        private static TextMeshProUGUI AddText(GameObject target, float size)
        {
            var text = target.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;   // a sign never takes a click
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

        // A pole behind the sign and a flat foot on the floor. No colliders, so a sign never blocks a mouse pick or a hand.
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

        // Deletes the old signs and makes a new, empty root at the world origin. The signs are placed in world space.
        // The place of the old root is given back, and Run puts it on the new root at the end.
        private static Transform RebuildRoot(out Vector3 keptPosition, out Quaternion keptRotation, StringBuilder log)
        {
            keptPosition = Vector3.zero;
            keptRotation = Quaternion.identity;
            Transform old = LabSceneFixes.FindSceneObject(RootName);
            if (old != null)
            {
                keptPosition = old.position;
                keptRotation = old.rotation;
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
