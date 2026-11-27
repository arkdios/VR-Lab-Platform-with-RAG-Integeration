#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Menu: Lab > Export assistant test set.
    /// Writes the setups of AssistantTestSetBuilder to the folder AssistantTestSet next to Assets (it is not imported
    /// by Unity, so commit it to Git):
    ///   test_set.jsonl  one line per setup and hint level: {"config_id","hint_level","payload"}. The payload is exactly
    ///                   what the lab would POST. Send each line several times and keep every answer.
    ///   answers.json    for each setup: the checks that must fail, the known fix and a plain description.
    ///                   Keep it away from the assistant. It is for scoring only.
    /// Before it writes, it checks each setup: the judge must fail exactly the checks the setup lists, and no payload may
    /// hold more than its hint level allows. A mismatch is an error in the log.
    /// </summary>
    public static class LabAssistantTestSetExporter
    {
        private const string FolderName = "AssistantTestSet";
        private const int HintLevelCount = 3;

        [Serializable]
        private sealed class AnswerEntry
        {
            public string config_id;
            public string case_id;
            public string step;
            public string description;
            public string[] expected_failed_checks;
            public string[] known_fix;
        }

        [Serializable]
        private sealed class AnswerFile
        {
            public string note = "Scoring only. Never send this file, or any known_fix text, to the assistant.";
            public AnswerEntry[] setups;
        }

        [MenuItem("Lab/Export assistant test set")]
        private static void Menu()
        {
            var log = new StringBuilder("[LabAssistantTestSetExporter]\n");
            bool ok = Run(log);
            if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
        }

        public static bool Run(StringBuilder log)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));
            Directory.CreateDirectory(folder);

            var lines = new StringBuilder();
            var answers = new List<AnswerEntry>();
            int requestId = 0;
            bool ok = true;

            foreach (AssistantTestConfig config in AssistantTestSetBuilder.Configs())
            {
                AssistantTestRun run = AssistantTestSetBuilder.Judge(config);
                ok &= CheckSetup(config, run, log);
                answers.Add(MakeAnswer(config, run));

                for (int level = HintLadder.TableLevel; level <= HintLevelCount; level++)
                {
                    LabStatePayload payload = AssistantTestSetBuilder.BuildPayload(config, run, level, ++requestId);
                    string json = JsonUtility.ToJson(payload);
                    ok &= CheckLevel(config, json, level, log);
                    lines.Append(WrapLine(config.Id, level, json)).Append('\n');
                }
            }

            File.WriteAllText(Path.Combine(folder, "test_set.jsonl"), lines.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "answers.json"),
                JsonUtility.ToJson(new AnswerFile { setups = answers.ToArray() }, true), new UTF8Encoding(false));

            log.AppendLine($"  {answers.Count} setups x {HintLevelCount} hint levels = {requestId} payloads written to {folder}");
            log.AppendLine("  Commit the folder. Run the setups with Assets/Tools/run_assistant_test_set.py.");
            return ok;
        }

        // The judge must fail exactly the checks the setup lists. Otherwise the "known fix" is not the lab's own diagnosis.
        private static bool CheckSetup(AssistantTestConfig config, AssistantTestRun run, StringBuilder log)
        {
            string expected = string.Join(",", config.ExpectedFailedChecks);
            string actual = string.Join(",", AssistantTestSetBuilder.FailedIds(run));
            if (expected == actual) return true;

            log.AppendLine($"  ERROR {config.Id}: the judge fails [{actual}] but the setup lists [{expected}].");
            return false;
        }

        // A payload must not hold more than its hint level allows. PayloadGuard is the same check the lab runs before it sends.
        private static bool CheckLevel(AssistantTestConfig config, string json, int level, StringBuilder log)
        {
            if (PayloadGuard.TryFindForbiddenKey(json, out string forbidden))
            {
                log.AppendLine($"  ERROR {config.Id} level {level}: the payload holds the forbidden key '{forbidden}'.");
                return false;
            }
            if (PayloadGuard.TryFindHintLeak(json, level, out string leaked))
            {
                log.AppendLine($"  ERROR {config.Id} level {level}: the payload holds '{leaked}', which this level does not allow.");
                return false;
            }
            return true;
        }

        private static AnswerEntry MakeAnswer(AssistantTestConfig config, AssistantTestRun run) => new AnswerEntry
        {
            config_id = config.Id,
            case_id = run.Step.CaseId,
            step = run.Step.Id,
            description = config.Description,
            expected_failed_checks = config.ExpectedFailedChecks,
            known_fix = config.KnownFix
        };

        // The payload is already JSON text, so it is put into the line as it is.
        private static string WrapLine(string configId, int level, string payloadJson) =>
            "{\"config_id\":\"" + configId + "\",\"hint_level\":" + level + ",\"payload\":" + payloadJson + "}";
    }
}
#endif
