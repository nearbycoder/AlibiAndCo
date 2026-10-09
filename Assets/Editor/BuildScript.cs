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

        /// <summary>
        /// The browser build: Brotli-compressed with the JavaScript decompression fallback, so it also
        /// runs from a server that doesn't send Content-Encoding headers. It's built twice: once with
        /// the textures as ASTC, for phones and tablets (whose GPUs take ASTC but not the desktop's DXT,
        /// which they'd have to unpack to four times the size), and once as before. The ASTC build's
        /// data file goes beside the other as WebGL-astc.data.unityweb, and the page picks one; the
        /// code and loader are the same for both, which this checks.
        /// </summary>
        [MenuItem("Alibi & Co/Build WebGL Player")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:Alibi";   // Assets/WebGLTemplates/Alibi: full-window, saves synced to IndexedDB
            // The texture format goes in with the build's options (setting EditorUserBuildSettings.webGLBuildSubtarget
            // alone leaves a scripted build's textures as they were).
            Build(BuildTarget.WebGL, "Builds/WebGL-astc", false, (int)WebGLTextureSubtarget.ASTC);
            bool ok = lastOk;
            if (ok)
            {
                Build(BuildTarget.WebGL, "Builds/WebGL", false);
                ok = lastOk && PairWebGL("Builds/WebGL-astc/Build", "Builds/WebGL/Build");
            }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Copies the ASTC build's data file in beside the main build's, once their code is known to match.</summary>
        static bool PairWebGL(string astcDir, string mainDir)
        {
            // Unity names a build's files after its folder: WebGL-astc.wasm.unityweb beside WebGL.wasm.unityweb.
            foreach (var f in new[] { "wasm.unityweb", "framework.js.unityweb", "loader.js" })
            {
                var a = Path.Combine(astcDir, "WebGL-astc." + f);
                var b = Path.Combine(mainDir, "WebGL." + f);
                if (!File.Exists(a) || !File.Exists(b) || !File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b)))
                {
                    Debug.LogError($"[Build] the ASTC and DXT builds' {f} files differ (or one is missing): the page can't share it between them");
                    return false;
                }
            }
            File.Copy(Path.Combine(astcDir, "WebGL-astc.data.unityweb"), Path.Combine(mainDir, "WebGL-astc.data.unityweb"), true);
            Debug.Log($"[Build] paired: WebGL-astc.data.unityweb {new FileInfo(Path.Combine(mainDir, "WebGL-astc.data.unityweb")).Length / 1024} KB beside WebGL.data.unityweb {new FileInfo(Path.Combine(mainDir, "WebGL.data.unityweb")).Length / 1024} KB");
            return true;
        }

        static bool lastOk;

        static string Build(BuildTarget target, string path, bool exitWhenBatch, int subtarget = 0)
        {
            string Fail(string why)
            {
                Debug.LogError("[Build] " + why);
                lastOk = false;
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
                subtarget = subtarget,
            });
            var s = report.summary;
            lastOk = s.result == BuildResult.Succeeded;
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
