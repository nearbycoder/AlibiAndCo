using System.IO;
using System.Linq;
using AlibiCo.Logic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AlibiCo.EditorTools
{
    /// <summary>Menu items and batch entry points: build the players, validate the cases.</summary>
    public static class BuildScript
    {
        public const string BundleId = "com.nearbycoder.alibiandco";

        [MenuItem("Alibi & Co/Build Linux Player")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/AlibiAndCo.x86_64", true);

        /// <summary>For a resident (batch) Editor driven by the CLI: build without quitting.</summary>
        public static string BuildLinuxResident() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/AlibiAndCo.x86_64", false);

        /// <summary>A Universal (Intel + Apple Silicon) Mono app. Unsigned and not notarized.</summary>
        [MenuItem("Alibi & Co/Build macOS Player")]
        public static void BuildMac()
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/AlibiAndCo.app", true);
        }

        /// <summary>Needs Unity's "Windows Build Support (Mono)" module, which isn't installed on the dev machine.</summary>
        [MenuItem("Alibi & Co/Build Windows Player")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AlibiAndCo.exe", true);

        static string Build(BuildTarget target, string path, bool exitWhenBatch)
        {
            string Fail(string why)
            {
                Debug.LogError("[Build] " + why);
                if (Application.isBatchMode && exitWhenBatch) EditorApplication.Exit(1);
                return why;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                return Fail($"{target} isn't supported by this Editor (install its build support module in Unity Hub)");
            int failed = ValidateAll();
            if (failed > 0) return Fail($"{failed} case(s) failed validation, refusing to build");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, BundleId);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            var msg = $"[Build] {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s -> {s.outputPath}";
            Debug.Log(msg);
            if (Application.isBatchMode && exitWhenBatch) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
            return msg;
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
