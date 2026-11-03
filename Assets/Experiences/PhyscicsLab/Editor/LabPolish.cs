#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > 9 Polish (UI and colours). Also runs at the end of Lab > 0.
    /// 1. UI: the student panels stop fading out, get a dark background and white text, and the text fits its box.
    /// 2. The two pulley button panels at the bottom are switched off (nothing is deleted).
    /// 3. Colours: URP materials that copy the real equipment in the lab slides (Phys 124 Experiment 5),
    ///    put on each model part by the part's name.
    /// 4. Degree scale: a flat printed disc on top of the force table (0 to 360, a tick every degree).
    /// Safe to run again. Ctrl+Z undoes the scene changes (the material, texture and mesh files stay in the project).
    /// </summary>
    public static class LabPolish
    {
        private const string StudentScreenName = "StudentScreen";
        private const string UiRootName = "Lab UI";
        private const string PropsRootName = "LabProps (Temp)";
        private const string EquipmentName = "LabEquipments";
        private const string TableCenterName = "TableCenter";

        // UI
        private const float LookingAlpha = 0.9f;   // the panels were fading to 0.35 whenever the mouse was captured
        private const float MinFontSize = 14f;
        private const float MaxFontSize = 34f;
        private static readonly Color PanelColour = new Color(0.07f, 0.09f, 0.13f, 0.9f);
        private static readonly string[] PanelNames = { "HUDPanel", "ChatPanel", "RoleSelectScreen" };
        private static readonly string[] LabelNames = { "StepLabel", "ReadingLabel", "MeasurementCountLabel", "AnswerLabel", "StatusLabel", "Title" };
        private static readonly string[] HiddenPanelNames = { "Pulley1Controls", "Pulley2Controls" };

        // Colours
        private const string MaterialFolder = "Assets/Experiences/PhyscicsLab/Materials/Lab";
        private const string MaterialParentFolder = "Assets/Experiences/PhyscicsLab/Materials";
        private const string WoodTexturePath = MaterialFolder + "/WoodGrain.png";
        private const int WoodTextureSize = 256;

        // Parts under LabEquipments that get a colour.
        private static readonly string[] EquipmentParts = { "Table", "Ring", "Pulley 1", "Pulley 2", "Mass 1", "Mass 2", "SpringScale", "CenterPin" };

        // Degree scale
        private const string DegreeScaleName = "DegreeScale";
        private const string DiskPartName = "Table_Disk";
        private const string DegreeTexturePath = MaterialFolder + "/DegreeScale.png";
        private const string DegreeMeshPath = "Assets/Experiences/PhyscicsLab/Models/Proxies/Table_DegreeScale.asset";
        private const int DegreeTextureSize = 1024;
        private const int DegreeSegments = 96;             // corners of the printed disc
        private const float DegreeInsetFactor = 0.985f;    // the print stops just inside the edge of the disc
        private const float DegreeLiftMetres = 0.0008f;    // above the disc, below the top of the glowing clamp spots
        private const float TickHalfWidthPx = 0.8f;
        private const float OuterMarginPx = 6f;
        private const float LabelInsetPx = 80f;            // how far in from the outer circle the numbers sit
        private const int LabelStepDegrees = 30;
        private const int GlyphScale = 4;                  // each font dot is 4 x 4 pixels
        private const int GlyphGapPx = 4;

        // A 5 x 7 dot font for the digits 0 to 9. Rows run from the top to the bottom. 1 is ink.
        private static readonly string[] Glyphs =
        {
            "01110/10001/10011/10101/11001/10001/01110",   // 0
            "00100/01100/00100/00100/00100/00100/01110",   // 1
            "01110/10001/00001/00010/00100/01000/11111",   // 2
            "11110/00001/00001/01110/00001/00001/11110",   // 3
            "00010/00110/01010/10010/11111/00010/00010",   // 4
            "11111/10000/11110/00001/00001/10001/01110",   // 5
            "00110/01000/10000/11110/10001/10001/01110",   // 6
            "11111/00001/00010/00100/01000/01000/01000",   // 7
            "01110/10001/10001/01110/10001/10001/01110",   // 8
            "01110/10001/10001/01111/00001/00010/01100"    // 9
        };

        [MenuItem("Lab/9 Polish (UI and colours)")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabPolish]\n");
            bool ok = Run(log);
            if (ok) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            if (EditorApplication.isPlaying)
            {
                log.AppendLine("  ERROR: stop Play Mode first. Changes made in Play Mode are lost.");
                return false;
            }

            PolishUi(log);
            HidePulleyPanels(log);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                log.AppendLine("  ERROR: the shader Universal Render Pipeline/Lit was not found, so no colours were set.");
                return false;
            }
            EnsureMaterialFolder();
            ColourScene(lit, log);
            AddDegreeScale(lit, log);

            AssetDatabase.SaveAssets();
            log.AppendLine("  Done. Save the scene (Ctrl+S) and press Play.");
            return true;
        }

        // ---------- 1. UI ----------

        private static void PolishUi(StringBuilder log)
        {
            Transform screen = LabSceneFixes.FindSceneObject(StudentScreenName);
            StudentScreenGate gate = screen != null ? screen.GetComponent<StudentScreenGate>() : null;
            if (gate != null && SetFloatField(gate, "lookingAlpha", LookingAlpha))
            {
                log.AppendLine($"  UI: panels now stay at {LookingAlpha:0.00} visibility while you look around (was 0.35).");
            }

            foreach (string panelName in PanelNames) TintPanel(panelName, log);

            Transform ui = LabSceneFixes.FindSceneObject(UiRootName);
            if (ui == null)
            {
                log.AppendLine($"  Warning: no object named {UiRootName}, so the text was not changed.");
                return;
            }
            int styled = StyleTexts(ui);
            log.AppendLine($"  UI: {styled} texts set to fit their box (white labels, dark button text).");
        }

        private static void TintPanel(string panelName, StringBuilder log)
        {
            Transform panel = LabSceneFixes.FindSceneObject(panelName);
            Image image = panel != null ? panel.GetComponent<Image>() : null;
            if (image == null)
            {
                log.AppendLine($"  Warning: {panelName} has no Image, so it was skipped.");
                return;
            }

            Undo.RecordObject(image, "Dark panel");
            image.color = PanelColour;
            EditorUtility.SetDirty(image);
        }

        // Labels are white (the panels are dark now). Button text stays dark, because the buttons are white.
        private static int StyleTexts(Transform ui)
        {
            int styled = 0;
            foreach (TMP_Text text in ui.GetComponentsInChildren<TMP_Text>(true))
            {
                bool onButton = text.GetComponentInParent<Button>(true) != null;
                bool isLabel = System.Array.IndexOf(LabelNames, text.name) >= 0;
                if (!onButton && !isLabel) continue;

                Undo.RecordObject(text, "Readable text");
                if (isLabel) text.color = Color.white;
                text.enableAutoSizing = true;
                text.fontSizeMin = MinFontSize;
                text.fontSizeMax = MaxFontSize;
                EditorUtility.SetDirty(text);
                styled++;
            }
            return styled;
        }

        // ---------- 2. pulley panels ----------

        private static void HidePulleyPanels(StringBuilder log)
        {
            foreach (string panelName in HiddenPanelNames)
            {
                Transform panel = LabSceneFixes.FindSceneObject(panelName);
                if (panel == null || !panel.gameObject.activeSelf) continue;

                Undo.RecordObject(panel.gameObject, "Hide pulley panel");
                panel.gameObject.SetActive(false);
                log.AppendLine($"  UI: {panelName} switched off (the pulleys are moved by hand now).");
            }
        }

        // ---------- 3. colours ----------

        private static void ColourScene(Shader lit, StringBuilder log)
        {
            Dictionary<string, Material> byPartName = BuildPartMaterials(lit);
            log.AppendLine("  Colours (every model part, by name):");

            int parts = 0;
            Transform props = LabSceneFixes.FindSceneObject(PropsRootName);
            if (props != null)
            {
                foreach (Transform prop in props)
                {
                    if (prop.gameObject.activeSelf) parts += ColourPart(prop, byPartName, log);   // hidden copies are skipped
                }
            }

            Transform equipment = LabSceneFixes.FindSceneObject(EquipmentName);
            if (equipment != null)
            {
                foreach (string partName in EquipmentParts)
                {
                    Transform part = equipment.Find(partName);
                    if (part != null) parts += ColourPart(part, byPartName, log);
                }
            }
            log.AppendLine($"  Colours: {parts} parts coloured.");
        }

        // The lab photos: white force table with blue feet and a black post, black clamp with a blue wheel,
        // silver ring on black string, black hooked masses, a pale plastic spring scale with a steel hook.
        // Each model part is named in the FBX, so the colour is chosen by that exact name.
        private static Dictionary<string, Material> BuildPartMaterials(Shader lit)
        {
            Material wood = MakeMaterial(lit, "LabWood", Color.white, 0f, 0.25f, GetWoodTexture());
            Material bluePlastic = MakeMaterial(lit, "LabBluePlastic", new Color(0.12f, 0.33f, 0.62f), 0f, 0.55f, null);
            Material chrome = MakeMaterial(lit, "LabChrome", new Color(0.90f, 0.90f, 0.92f), 0.7f, 0.75f, null);

            Material tableTop = MakeMaterial(lit, "LabTableTop", new Color(0.93f, 0.93f, 0.92f), 0f, 0.35f, null);
            Material tableLeg = MakeMaterial(lit, "LabTableLeg", new Color(0.86f, 0.87f, 0.89f), 0.3f, 0.5f, null);
            Material blueFoot = MakeMaterial(lit, "LabBlueFoot", new Color(0.08f, 0.33f, 0.85f), 0f, 0.5f, null);
            Material blackPlastic = MakeMaterial(lit, "LabBlackPlastic", new Color(0.05f, 0.05f, 0.055f), 0f, 0.4f, null);
            Material silver = MakeMaterial(lit, "LabSilver", new Color(0.80f, 0.81f, 0.83f), 0.4f, 0.6f, null);
            Material clampGrey = MakeMaterial(lit, "LabClampGrey", new Color(0.12f, 0.12f, 0.13f), 0f, 0.35f, null);
            Material blueWheel = MakeMaterial(lit, "LabBlueWheel", new Color(0.05f, 0.38f, 0.95f), 0f, 0.6f, null);
            Material scaleCase = MakeMaterial(lit, "LabScaleCase", new Color(0.88f, 0.91f, 0.94f), 0f, 0.8f, null);
            Material scalePointer = MakeMaterial(lit, "LabScalePointer", new Color(0.85f, 0.10f, 0.10f), 0f, 0.5f, null);

            return new Dictionary<string, Material>
            {
                // bench and chair (the look you already have, with steel legs on the chair)
                { "LabBench", wood }, { "Bench_Back", wood }, { "Bench_CabL", wood }, { "Bench_CabR", wood }, { "Bench_Top", wood },
                { "LabChair", bluePlastic }, { "Chair_Plastic", bluePlastic }, { "Chair_Steel", chrome },
                // force table
                { "Table_Disk", tableTop }, { "Table_Legs", tableLeg }, { "Table_Feet", blueFoot }, { "Table_Post", blackPlastic },
                { "CenterPin", blackPlastic },
                // central ring: silver ring, black string knots
                { "Ring_Steel", silver }, { "Ring_Knots", blackPlastic },
                // pulley: dark clamp, silver screw and axle, blue wheel
                { "Pulley_Clamp", clampGrey }, { "Pulley_Steel", silver }, { "Pulley_Wheel", blueWheel },
                // spring scale
                { "SpringScale", scaleCase }, { "Scale_Case", scaleCase }, { "Scale_Pointer", scalePointer }, { "Scale_Steel", silver },
                // hooked masses (black, as in the photo)
                { "HookedMass50g", blackPlastic }, { "HookedMass100g", blackPlastic }, { "HookedMass200g", blackPlastic },
                { "RealModel_50g", blackPlastic }, { "RealModel_100g", blackPlastic }, { "RealModel_200g", blackPlastic }
            };
        }

        private static int ColourPart(Transform part, Dictionary<string, Material> byPartName, StringBuilder log)
        {
            var done = new List<string>();
            var unknown = new List<string>();
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
            {
                if (!IsColourable(renderer)) continue;
                if (!byPartName.TryGetValue(renderer.name, out Material material))
                {
                    unknown.Add(renderer.name);
                    continue;
                }

                Undo.RecordObject(renderer, "Colour part");
                renderer.sharedMaterials = SameOnEverySlot(renderer.sharedMaterials.Length, material);
                EditorUtility.SetDirty(renderer);
                done.Add(renderer.name);
            }

            if (done.Count > 0) log.AppendLine($"    {part.name}: {string.Join(", ", done)}");
            if (unknown.Count > 0) log.AppendLine($"    {part.name}: no colour rule for {string.Join(", ", unknown)} (left as it was)");
            return done.Count > 0 ? 1 : 0;
        }

        private static Material[] SameOnEverySlot(int slotCount, Material material)
        {
            var slots = new Material[slotCount];
            for (int i = 0; i < slotCount; i++) slots[i] = material;
            return slots;
        }

        // Skips the invisible proxy (it has no material), switched-off renderers, lines and 3D text.
        private static bool IsColourable(Renderer renderer)
        {
            bool isMesh = renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
            if (!isMesh || !renderer.enabled || renderer.sharedMaterials.Length == 0) return false;
            return renderer.GetComponent<TMP_Text>() == null;
        }

        private static Material MakeMaterial(Shader lit, string materialName, Color colour, float metallic, float smoothness, Texture2D map)
        {
            string path = MaterialFolder + "/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BaseMap", map);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(MaterialParentFolder, "Lab");
        }

        // Soft stripes that bend a little, plus fine streaks along the grain. Made once, then reused.
        private static Texture2D GetWoodTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodTexturePath);
            if (existing != null) return existing;

            var texture = new Texture2D(WoodTextureSize, WoodTextureSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < WoodTextureSize; y++)
            {
                for (int x = 0; x < WoodTextureSize; x++) texture.SetPixel(x, y, WoodPixel(x, y));
            }
            texture.Apply();

            File.WriteAllBytes(WoodTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(WoodTexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(WoodTexturePath);
        }

        private static Color WoodPixel(int x, int y)
        {
            float bend = Mathf.PerlinNoise(x * 0.015f, y * 0.004f) * 8f;   // the grain lines wobble
            float stripes = Mathf.Sin(y * 0.09f + bend) * 0.5f + 0.5f;
            float streaks = Mathf.PerlinNoise(x * 0.4f, y * 0.02f);
            float shade = Mathf.Lerp(0.72f, 1f, stripes * 0.6f + streaks * 0.4f);
            return new Color(0.74f * shade, 0.52f * shade, 0.30f * shade, 1f);
        }

        // ---------- 4. degree scale ----------

        // A flat disc just above the force table top. It has its own mesh and UVs, so it does not depend on how the
        // table model is unwrapped. Texture right = world +X (0 degrees), texture up = world +Z (90 degrees), the same
        // angles the pulleys use. It has no collider, so it never blocks the mouse ray or a part.
        private static void AddDegreeScale(Shader lit, StringBuilder log)
        {
            Transform equipment = LabSceneFixes.FindSceneObject(EquipmentName);
            Transform table = equipment != null ? equipment.Find("Table") : null;
            Renderer disk = table != null ? FindRendererByName(table, DiskPartName) : null;
            if (disk == null)
            {
                log.AppendLine($"  Warning: no {DiskPartName} found under Table, so the degree scale was not made.");
                return;
            }

            Bounds bounds = disk.bounds;
            float radius = bounds.extents.x * DegreeInsetFactor;
            var centre = new Vector3(bounds.center.x, bounds.max.y + DegreeLiftMetres, bounds.center.z);

            Transform decal = equipment.Find(DegreeScaleName);
            if (decal == null) decal = CreateDecal(equipment);

            Undo.RecordObject(decal, "Degree scale");
            decal.SetPositionAndRotation(centre, Quaternion.identity);
            decal.GetComponent<MeshFilter>().sharedMesh = BuildDiscMesh(radius);

            Material material = MakeMaterial(lit, "LabDegreeScale", Color.white, 0f, 0.3f, MakeDegreeTexture());
            MeshRenderer meshRenderer = decal.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(meshRenderer);

            Transform marker = LabSceneFixes.FindSceneObject(TableCenterName);
            float markerY = marker != null ? marker.position.y : float.NaN;
            log.AppendLine($"  Degree scale: radius {radius:0.000} m, disc top y {bounds.max.y:0.000}, TableCenter y {markerY:0.000}. These two heights should be within a few mm.");
        }

        private static Transform CreateDecal(Transform parent)
        {
            var decal = new GameObject(DegreeScaleName, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(decal, "Degree scale");
            decal.transform.SetParent(parent, true);
            return decal.transform;
        }

        private static Renderer FindRendererByName(Transform root, string rendererName)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == rendererName) return renderer;
            }
            return null;
        }

        // A fan of triangles around the centre, facing up. The mesh is saved as an asset so the scene can point at it.
        private static Mesh BuildDiscMesh(float radius)
        {
            var vertices = new Vector3[DegreeSegments + 1];
            var normals = new Vector3[DegreeSegments + 1];
            var uvs = new Vector2[DegreeSegments + 1];
            var triangles = new int[DegreeSegments * 3];

            uvs[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;
            for (int i = 0; i < DegreeSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / DegreeSegments;
                var rim = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                vertices[i + 1] = rim;
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(0.5f + rim.x / (2f * radius), 0.5f + rim.z / (2f * radius));

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % DegreeSegments + 1;   // the next corner, so the face looks up
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DegreeMeshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "Table_DegreeScale" };

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, DegreeMeshPath); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Made again on every run (the result is the same each time, so git sees no change).
        private static Texture2D MakeDegreeTexture()
        {
            var pixels = new Color32[DegreeTextureSize * DegreeTextureSize];
            DrawPaperAndTicks(pixels);
            DrawNumbers(pixels);

            var texture = new Texture2D(DegreeTextureSize, DegreeTextureSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(DegreeTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(DegreeTexturePath);
            SharpenImport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(DegreeTexturePath);
        }

        // Ticks every 1 degree, longer at 5 and 10, plus an outer circle.
        private static void DrawPaperAndTicks(Color32[] pixels)
        {
            float centre = (DegreeTextureSize - 1) * 0.5f;
            float outer = DegreeTextureSize * 0.5f - OuterMarginPx;
            var paper = new Color32(246, 246, 244, 255);
            var ink = new Color32(20, 20, 22, 255);

            for (int y = 0; y < DegreeTextureSize; y++)
            {
                for (int x = 0; x < DegreeTextureSize; x++)
                {
                    float dx = x - centre;
                    float dy = y - centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 colour = paper;

                    if (Mathf.Abs(r - outer) <= 1.5f) colour = ink;
                    else if (r < outer && r >= outer - TickLength(0)) colour = TickColour(dx, dy, r, outer, paper, ink);

                    pixels[y * DegreeTextureSize + x] = colour;
                }
            }
        }

        private static Color32 TickColour(float dx, float dy, float r, float outer, Color32 paper, Color32 ink)
        {
            float degrees = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;

            float nearest = Mathf.Round(degrees);
            float offsetPx = Mathf.Abs(degrees - nearest) * Mathf.Deg2Rad * r;   // distance to the closest tick line
            bool onTick = offsetPx <= TickHalfWidthPx && r >= outer - TickLength(Mathf.RoundToInt(nearest) % 360);
            return onTick ? ink : paper;
        }

        private static float TickLength(int degrees)
        {
            if (degrees % 10 == 0) return 40f;
            return degrees % 5 == 0 ? 28f : 16f;
        }

        private static void DrawNumbers(Color32[] pixels)
        {
            float centre = (DegreeTextureSize - 1) * 0.5f;
            float labelRadius = DegreeTextureSize * 0.5f - OuterMarginPx - LabelInsetPx;

            for (int degrees = 0; degrees < 360; degrees += LabelStepDegrees)
            {
                float radians = degrees * Mathf.Deg2Rad;
                int cx = Mathf.RoundToInt(centre + Mathf.Cos(radians) * labelRadius);
                int cy = Mathf.RoundToInt(centre + Mathf.Sin(radians) * labelRadius);
                DrawText(pixels, degrees.ToString(), cx, cy);
            }
        }

        // Draws a number centred on (cx, cy). The top of each digit points to world +Z.
        private static void DrawText(Color32[] pixels, string text, int cx, int cy)
        {
            int glyphWidth = 5 * GlyphScale;
            int glyphHeight = 7 * GlyphScale;
            int totalWidth = text.Length * glyphWidth + (text.Length - 1) * GlyphGapPx;
            int left = cx - totalWidth / 2;
            int bottom = cy - glyphHeight / 2;
            var ink = new Color32(20, 20, 22, 255);

            for (int i = 0; i < text.Length; i++)
            {
                string[] rows = Glyphs[text[i] - '0'].Split('/');
                int glyphLeft = left + i * (glyphWidth + GlyphGapPx);
                for (int row = 0; row < 7; row++)
                {
                    for (int col = 0; col < 5; col++)
                    {
                        if (rows[row][col] != '1') continue;
                        FillDot(pixels, glyphLeft + col * GlyphScale, bottom + (6 - row) * GlyphScale, ink);
                    }
                }
            }
        }

        private static void FillDot(Color32[] pixels, int x0, int y0, Color32 colour)
        {
            for (int y = y0; y < y0 + GlyphScale; y++)
            {
                for (int x = x0; x < x0 + GlyphScale; x++)
                {
                    if (x < 0 || y < 0 || x >= DegreeTextureSize || y >= DegreeTextureSize) continue;
                    pixels[y * DegreeTextureSize + x] = colour;
                }
            }
        }

        // The disc is seen at a slant, so the texture needs anisotropic filtering to stay sharp.
        private static void SharpenImport()
        {
            var importer = AssetImporter.GetAtPath(DegreeTexturePath) as TextureImporter;
            if (importer == null || importer.anisoLevel == 8) return;

            importer.anisoLevel = 8;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // ---------- helpers ----------

        private static bool SetFloatField(Object owner, string field, float value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return false;

            property.floatValue = value;
            serialized.ApplyModifiedProperties();
            return true;
        }
    }
}
#endif