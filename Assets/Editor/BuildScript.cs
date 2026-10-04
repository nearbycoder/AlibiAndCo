using System.IO;
using System.Linq;
using AlibiCo.Logic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AlibiCo.EditorTools
{
    /// <summary>Menu items and batch entry points: build the Linux player, validate the cases.</summary>
    public static class BuildScript
    {
        [MenuItem("Alibi & Co/Build Linux Player")]
        public static void BuildLinux()
        {
            int failed = ValidateAll();
            if (failed > 0)
            {
                Debug.LogError($"[Build] {failed} case(s) failed validation, refusing to build");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/Linux/AlibiAndCo.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[Build] {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s -> {s.outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Alibi & Co/Validate Cases")]
        public static void ValidateMenu() => ValidateAll();

        /// <summary>Batch: -executeMethod AlibiCo.EditorTools.BuildScript.ValidateCases</summary>
        public static void ValidateCases()
        {
            int failed = ValidateAll();
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        static int ValidateAll()
        {
            var dir = "Assets/Resources/Data";
            var map = new TownMap(TownData.FromJson(File.ReadAllText(Path.Combine(dir, "town.json"))));
            int failed = 0;
            foreach (var f in Directory.GetFiles(dir, "case*.json").OrderBy(x => x))
            {
                var c = CaseDef.FromJson(File.ReadAllText(f));
                var r = CaseValidator.Validate(c, map);
                Debug.Log($"[Validate] {c.Id} \"{c.Title}\": {(r.Ok ? "AIRTIGHT" : "FAILED")}\n" +
                          string.Join("\n", r.Errors.Select(e => "ERROR " + e).Concat(r.Warnings.Select(w => "warn " + w)).Concat(r.Info)));
                if (!r.Ok) failed++;
            }
            return failed;
        }
    }
}
