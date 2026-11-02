#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Menu: Lab > 9 Polish (UI and colours). Also runs at the end of Lab > 0.
    /// 1. UI: the student panels stop fading out, get a dark background and white text, and the text fits its box.
    /// 2. The two pulley button panels at the bottom are switched off (nothing is deleted).
    /// 3. Colours: makes URP materials (and one wood texture) and puts them on the lab parts and the furniture.
    /// 4. Clean-up: removes the empty script slot left on the Desktop Rig by the first version of this tool.
    /// Safe to run again. Ctrl+Z undoes the scene changes (the new material files stay in the project).
    /// </summary>
    public static class LabPolish
    {
        private const string StudentScreenName = "StudentScreen";
        private const string UiRootName = "Lab UI";
        private const string PropsRootName = "LabProps (Temp)";
        private const string EquipmentName = "LabEquipments";

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
        private const float LightMassLimitGrams = 75f;    // under this: a 50 g mass
        private const float HeavyMassLimitGrams = 150f;   // over this: a 200 g mass
        private const float DefaultMassGrams = 100f;

        // Parts under LabEquipments that get a colour.
        private static readonly string[] EquipmentParts = { "Table", "Ring", "Pulley 1", "Pulley 2", "Mass 1", "Mass 2", "SpringScale", "CenterPin" };

        // A model part whose name has one of these words gets the accent material (legs, wheels, frames).
        // Everything else gets the main material. Change this list if a part gets the wrong one.
        private static readonly string[] AccentWords =
        {
            "leg", "post", "pin", "foot", "feet", "wheel", "groove", "hook", "screw", "bolt",
            "knob", "axle", "frame", "base", "stand", "rod", "pole", "handle"
        };

        private struct Look
        {
            public Material main;
            public Material accent;

            public Look(Material both) { main = both; accent = both; }
            public Look(Material mainMaterial, Material accentMaterial) { main = mainMaterial; accent = accentMaterial; }
        }

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
            ColourScene(log);
            RemoveOldWalkThrough(log);
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

        private static void ColourScene(StringBuilder log)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                log.AppendLine("  ERROR: the shader Universal Render Pipeline/Lit was not found, so no colours were set.");
                return;
            }

            EnsureMaterialFolder();
            Dictionary<string, Look> looks = BuildLooks(lit);
            log.AppendLine("  Colours (every part: main or accent):");

            int parts = 0;
            Transform props = LabSceneFixes.FindSceneObject(PropsRootName);
            if (props != null)
            {
                foreach (Transform prop in props)
                {
                    if (prop.gameObject.activeSelf) parts += ColourPart(prop, looks, log);   // hidden copies are skipped
                }
            }

            Transform equipment = LabSceneFixes.FindSceneObject(EquipmentName);
            if (equipment != null)
            {
                foreach (string partName in EquipmentParts)
                {
                    Transform part = equipment.Find(partName);
                    if (part != null) parts += ColourPart(part, looks, log);
                }
            }
            log.AppendLine($"  Colours: {parts} parts coloured.");
        }

        private static Dictionary<string, Look> BuildLooks(Shader lit)
        {
            Material wood = MakeMaterial(lit, "LabWood", Color.white, 0f, 0.25f, GetWoodTexture());
            Material darkSteel = MakeMaterial(lit, "LabDarkSteel", new Color(0.17f, 0.19f, 0.23f), 0.5f, 0.45f, null);
            Material blueSeat = MakeMaterial(lit, "LabBluePlastic", new Color(0.12f, 0.33f, 0.62f), 0f, 0.55f, null);
            Material aluminium = MakeMaterial(lit, "LabAluminium", new Color(0.80f, 0.82f, 0.86f), 0.5f, 0.5f, null);
            Material graphite = MakeMaterial(lit, "LabGraphite", new Color(0.14f, 0.14f, 0.16f), 0.4f, 0.4f, null);
            Material brass = MakeMaterial(lit, "LabBrass", new Color(0.86f, 0.66f, 0.26f), 0.6f, 0.6f, null);
            Material chrome = MakeMaterial(lit, "LabChrome", new Color(0.90f, 0.90f, 0.92f), 0.7f, 0.75f, null);
            Material blackMatte = MakeMaterial(lit, "LabBlackMatte", new Color(0.10f, 0.10f, 0.11f), 0.2f, 0.35f, null);
            Material orangeBody = MakeMaterial(lit, "LabOrangeBody", new Color(0.88f, 0.30f, 0.10f), 0f, 0.5f, null);

            // A part is matched by the start of its name. The masses are picked by weight in LookForMass.
            var looks = new Dictionary<string, Look>
            {
                { "LabBench", new Look(wood, darkSteel) },
                { "LabChair", new Look(blueSeat, chrome) },
                { "ForceTable", new Look(aluminium, graphite) },
                { "Table", new Look(aluminium, graphite) },
                { "Ring", new Look(brass) },
                { "CenterPin", new Look(brass) },
                { "Pulley", new Look(blackMatte, aluminium) },
                { "SpringScale", new Look(orangeBody, chrome) },
                { "MassLight", new Look(brass) },       // 50 g
                { "MassMiddle", new Look(chrome) },     // 100 g
                { "MassHeavy", new Look(graphite) }     // 200 g
            };
            return looks;
        }

        private static int ColourPart(Transform part, Dictionary<string, Look> looks, StringBuilder log)
        {
            if (!TryGetLook(part, looks, out Look look)) return 0;

            var done = new List<string>();
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
            {
                if (!IsColourable(renderer)) continue;

                bool accent = IsAccent(renderer.name);
                Undo.RecordObject(renderer, "Colour part");
                renderer.sharedMaterials = SlotsFor(renderer.sharedMaterials.Length, accent ? look.accent : look.main, look.accent);
                EditorUtility.SetDirty(renderer);
                done.Add(renderer.name + (accent ? "=accent" : "=main"));
            }

            if (done.Count > 0) log.AppendLine($"    {part.name}: {string.Join(", ", done)}");
            return done.Count > 0 ? 1 : 0;
        }

        // Slot 0 gets the part's material. The other slots of the same mesh get the accent.
        private static Material[] SlotsFor(int slotCount, Material first, Material others)
        {
            var slots = new Material[slotCount];
            for (int i = 0; i < slotCount; i++) slots[i] = i == 0 ? first : others;
            return slots;
        }

        // Skips the invisible proxy (it has no material), switched-off renderers, lines and 3D text.
        private static bool IsColourable(Renderer renderer)
        {
            bool isMesh = renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
            if (!isMesh || !renderer.enabled || renderer.sharedMaterials.Length == 0) return false;
            return renderer.GetComponent<TMP_Text>() == null;
        }

        private static bool IsAccent(string rendererName)
        {
            string lower = rendererName.ToLowerInvariant();
            foreach (string word in AccentWords)
            {
                if (lower.Contains(word)) return true;
            }
            return false;
        }

        private static bool TryGetLook(Transform part, Dictionary<string, Look> looks, out Look look)
        {
            if (part.name.StartsWith("HookedMass") || part.name.StartsWith("Mass "))
            {
                look = looks[LookForMass(part)];
                return true;
            }

            foreach (KeyValuePair<string, Look> entry in looks)
            {
                if (!part.name.StartsWith(entry.Key)) continue;
                look = entry.Value;
                return true;
            }
            look = default(Look);
            return false;
        }

        // "HookedMass50g" is read from its name. "Mass 1" and "Mass 2" are read from their Rigidbody.
        private static string LookForMass(Transform part)
        {
            float grams = DefaultMassGrams;
            Match fromName = Regex.Match(part.name, @"(\d+)g$");
            Rigidbody body = part.GetComponent<Rigidbody>();
            if (fromName.Success) grams = float.Parse(fromName.Groups[1].Value);
            else if (body != null) grams = body.mass * 1000f;

            if (grams < LightMassLimitGrams) return "MassLight";
            return grams > HeavyMassLimitGrams ? "MassHeavy" : "MassMiddle";
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

        // ---------- 4. clean-up ----------

        // The first version of this tool put a PlayerPassThrough script on the Desktop Rig. It is not wanted
        // (the colliders are fixed instead). After the file is deleted, its slot on the rig shows as a missing script.
        // This removes those empty slots. It does nothing when there are none.
        private static void RemoveOldWalkThrough(StringBuilder log)
        {
            DesktopRigController rig = Object.FindFirstObjectByType<DesktopRigController>(FindObjectsInactive.Include);
            if (rig == null) return;

            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(rig.gameObject);
            if (removed > 0) log.AppendLine($"  Clean-up: {removed} empty script slot(s) removed from {rig.name} (the old PlayerPassThrough).");
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