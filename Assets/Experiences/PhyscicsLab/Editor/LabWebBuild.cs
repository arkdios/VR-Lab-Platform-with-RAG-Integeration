#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Web build. The project is a WebXR lab, so the browser build is the main build.
    /// "1 Check settings" looks for the things that stop or break a WebGL build, before you wait for a build.
    /// "2 Build WebGL" runs the same check, builds into Builds/WebGL, and writes the result and the errors to the Console.
    /// Nothing here changes a setting. It only reports, so you decide.
    /// </summary>
    public static class LabWebBuild
    {
        private const string BuildFolder = "Builds/WebGL";
        private const string JslibPath = "Assets/Core/Plugins/WebGL/BrowserBridge.jslib";
        private const string ManifestPath = "Packages/manifest.json";
        private const string WebXrPackage = "com.de-panther.webxr";
        private const float MaxCatchUpSeconds = 0.12f;   // more than this lets a slow browser frame run too many physics steps

        // A "}" followed by the next name with no comma between: the mistake that stops the WebGL link step.
        private static readonly Regex MissingComma =
            new Regex(@"\}\s*(?://[^\n]*\s*)*[A-Za-z_]\w*\s*:\s*function", RegexOptions.Compiled);

        [MenuItem("Lab/Web build/1 Check settings")]
        private static void CheckMenu()
        {
            var log = new StringBuilder("[LabWebBuild] check\n");
            int problems = Check(log);
            log.AppendLine(problems == 0 ? "  All checks passed. Run Lab > Web build > 2 Build WebGL." : $"  {problems} problem(s) to fix first.");
            if (problems == 0) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        [MenuItem("Lab/Web build/2 Build WebGL")]
        private static void BuildMenu()
        {
            var log = new StringBuilder("[LabWebBuild] build\n");
            if (Check(log) > 0)
            {
                log.AppendLine("  Not built. Fix the problems above first.");
                Debug.LogError(log.ToString());
                return;
            }

            Directory.CreateDirectory(BuildFolder);
            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = BuildFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteResult(report, log);
        }

        [MenuItem("Lab/Web build/Open build folder")]
        private static void OpenFolder()
        {
            if (Directory.Exists(BuildFolder)) EditorUtility.RevealInFinder(BuildFolder);
            else Debug.LogWarning("[LabWebBuild] No build folder yet. Run Lab > Web build > 2 Build WebGL.");
        }

        // ---------- the check ----------

        // Returns the number of problems. A warning does not count: it is only something to know.
        private static int Check(StringBuilder log)
        {
            int problems = 0;
            problems += CheckPlatform(log);
            problems += CheckScenes(log);
            problems += CheckTemplate(log);
            problems += CheckJslib(log);
            problems += CheckWebXrPackage(log);
            WarnAboutCompression(log);
            WarnAboutCatchUp(log);
            return problems;
        }

        private static int CheckPlatform(StringBuilder log)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                log.AppendLine("  PROBLEM: the Web Build Support module is not installed. Add it in Unity Hub (Installs > your version > Add modules).");
                return 1;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                log.AppendLine("  PROBLEM: the active platform is not Web. Open File > Build Profiles, pick Web, press Switch Platform.");
                return 1;
            }
            log.AppendLine("  OK: the active platform is Web.");
            return 0;
        }

        private static int CheckScenes(StringBuilder log)
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                log.AppendLine("  PROBLEM: no scene is ticked in File > Build Profiles > Scene List.");
                return 1;
            }

            int problems = 0;
            foreach (string scene in scenes)
            {
                if (File.Exists(scene)) continue;
                log.AppendLine($"  PROBLEM: the scene list holds {scene}, but the file is missing.");
                problems++;
            }
            if (problems == 0) log.AppendLine($"  OK: {scenes.Length} scene(s) in the build: {string.Join(", ", scenes)}");
            return problems;
        }

        private static int CheckTemplate(StringBuilder log)
        {
            string template = PlayerSettings.WebGL.template;
            const string projectPrefix = "PROJECT:";
            if (!template.StartsWith(projectPrefix))
            {
                log.AppendLine($"  PROBLEM: the web page template is '{template}'. The lab needs the WebXR template from Assets/WebGLTemplates (Player Settings > Resolution and Presentation).");
                return 1;
            }

            string page = $"Assets/WebGLTemplates/{template.Substring(projectPrefix.Length)}/index.html";
            if (!File.Exists(page))
            {
                log.AppendLine($"  PROBLEM: the template '{template}' has no {page}.");
                return 1;
            }
            log.AppendLine($"  OK: web page template {template}.");
            return 0;
        }

        // The plugin file is read by the WebGL link step. One missing comma and the whole build fails.
        private static int CheckJslib(StringBuilder log)
        {
            if (!File.Exists(JslibPath))
            {
                log.AppendLine($"  PROBLEM: {JslibPath} is missing, so the CSV download and the file save do not work in the browser.");
                return 1;
            }

            string text = File.ReadAllText(JslibPath);
            if (MissingComma.IsMatch(text))
            {
                log.AppendLine($"  PROBLEM: {JslibPath} has two functions with no comma between them. The WebGL link step fails on that.");
                return 1;
            }
            log.AppendLine("  OK: BrowserBridge.jslib has a comma between its functions.");
            return 0;
        }

        private static int CheckWebXrPackage(StringBuilder log)
        {
            bool present = File.Exists(ManifestPath) && File.ReadAllText(ManifestPath).Contains(WebXrPackage);
            if (!present)
            {
                log.AppendLine($"  PROBLEM: the package {WebXrPackage} is not in Packages/manifest.json, so there is no WebXR in the build.");
                return 1;
            }
            log.AppendLine("  OK: the WebXR package is installed.");
            return 0;
        }

        private static void WarnAboutCompression(StringBuilder log)
        {
            if (PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled)
            {
                log.AppendLine("  OK: compression is off, so any plain web server (GitHub Pages, python -m http.server) can serve the build.");
                return;
            }
            log.AppendLine("  Warning: compression is on. The server must send a Content-Encoding header for the .br or .gz files, or the page will not load. GitHub Pages does not. Use Disabled for it.");
        }

        private static void WarnAboutCatchUp(StringBuilder log)
        {
            if (Time.maximumDeltaTime <= MaxCatchUpSeconds) return;
            log.AppendLine($"  Warning: Project Settings > Time > Maximum Allowed Timestep is {Time.maximumDeltaTime:0.00} s. After a slow frame the browser runs up to " +
                           $"{Mathf.CeilToInt(Time.maximumDeltaTime / Time.fixedDeltaTime)} physics steps at once, and the strings are costly. Set it to 0.1.");
        }

        // ---------- the build ----------

        private static string[] EnabledScenes()
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) paths.Add(scene.path);
            }
            return paths.ToArray();
        }

        private static void WriteResult(BuildReport report, StringBuilder log)
        {
            BuildSummary summary = report.summary;
            log.AppendLine($"  Result: {summary.result}");
            log.AppendLine($"  Size: {summary.totalSize / 1048576.0:0.0} MB, time: {summary.totalTime.TotalSeconds:0} s, errors: {summary.totalErrors}, warnings: {summary.totalWarnings}");
            log.AppendLine($"  Folder: {summary.outputPath}");

            if (summary.result == BuildResult.Succeeded)
            {
                log.AppendLine("  Test it: open a terminal in the folder and run  python -m http.server 8080  then open http://localhost:8080");
                Debug.Log(log.ToString());
                return;
            }

            AppendBuildErrors(report, log);
            Debug.LogError(log.ToString());
        }

        private static void AppendBuildErrors(BuildReport report, StringBuilder log)
        {
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception) log.AppendLine($"  ERROR in '{step.name}': {message.content}");
                }
            }
        }
    }
}
#endif