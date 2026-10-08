using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AlibiCo
{
    public sealed partial class AutoPilot
    {
        public void RunFidelityBench(string outDir)
        {
            dir = outDir;
            capture = Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(FidelityBench());
        }

        /// <summary>
        /// The settings panel is open: set the fidelity slider with the test's own pointer. A click on the
        /// Low name, a click on the track at Medium's notch, a drag of the handle to Ultra, and a click on
        /// High's name to finish where the game started; each must leave Fidelity.Level on that step.
        /// </summary>
        IEnumerator FidelityByHand(string how, System.Func<Vector2, IEnumerator> click, System.Func<Vector2, Vector2, IEnumerator> drag, System.Action<string> fail)
        {
            yield return Wait(0.6f);
            var slider = GameRoot.I.Screens.FidelitySlider;
            if (slider == null || !slider.isActiveAndEnabled) { fail($"{how}: no fidelity slider in Settings"); yield break; }
            Vector2 Centre(RectTransform r)
            {
                var c = new Vector3[4];
                r.GetWorldCorners(c);
                return (c[0] + c[2]) / 2;
            }
            Vector2 Notch(int i) => Centre((RectTransform)slider.transform.Find("notch" + i));
            Vector2 Name(int i) => Centre((RectTransform)slider.transform.parent.Find("name_" + Fidelity.Names[i]));
            Vector2 Handle() => Centre(slider.handleRect);
            int start = Fidelity.Level;
            var steps = new (string what, System.Func<IEnumerator> act, int want)[]
            {
                ("a click on Low", () => click(Name(0)), 0),
                ("a click on the track at Medium", () => click(Notch(1) + new Vector2(0, 2)), 1),
                ("a drag of the handle to Ultra", () => drag(Handle(), Notch(3)), 3),
                ("a click on High", () => click(Name(2)), 2),
            };
            foreach (var (what, act, want) in steps)
            {
                yield return act();
                yield return Wait(0.4f);
                if (Fidelity.Level != want || (int)slider.value != want)
                    fail($"{how}: {what} left fidelity at {Fidelity.Level} (slider {slider.value}), wanted {Fidelity.Names[want]}");
                else Debug.Log($"[Fidelity] {how}: {what} set {Fidelity.Names[want]}");
                if (want == 3) yield return Shot($"{how}_fidelity_ultra");
            }
            Debug.Log($"[Fidelity] {how}: slider by hand {Fidelity.Names[start]} -> Low -> Medium -> Ultra -> {Fidelity.Names[Fidelity.Level]}");
        }

        /// <summary>The machine's one-minute load average (Linux; -1 elsewhere), noted with each measurement.</summary>
        static float LoadAverage()
        {
            try { return float.Parse(File.ReadAllText("/proc/loadavg").Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture); }
            catch (System.Exception) { return -1; }
        }

        sealed class FrameStats
        {
            public int Frames;
            public float Mean, Median, P95, Fps, Load;
            public override string ToString() => $"mean {Mean:0.00} ms, median {Median:0.00} ms, 95th percentile {P95:0.00} ms ({Fps:0} fps over {Frames} frames, load {Load:0.0})";
        }

        /// <summary>Frame times over a stretch of real time, uncapped (vsync off), so they show the work, not the display.</summary>
        static IEnumerator FrameTimes(float seconds, FrameStats into)
        {
            var times = new List<float>();
            float t0 = Time.realtimeSinceStartup, last = t0;
            yield return null;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                float now = Time.realtimeSinceStartup;
                times.Add((now - last) * 1000f);
                last = now;
                yield return null;
            }
            times.RemoveAt(0);   // the first one straddles the switch
            times.Sort();
            into.Frames = times.Count;
            into.Mean = times.Average();
            into.Median = times[times.Count / 2];
            into.P95 = times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.95f))];
            into.Fps = 1000f / into.Mean;
            into.Load = LoadAverage();
        }

        /// <summary>
        /// -alibiFidelityBench [dir] [-alibiBenchSeconds n]: the title's polaroid wall and a busy board (case 3,
        /// four suspects and the town line, every named card pinned) held still, then each fidelity step in
        /// turn, Low to Ultra and High again: a screenshot of the same moment and the frame times with vsync
        /// off. Writes fidelity-bench.md to the folder.
        /// </summary>
        IEnumerator FidelityBench()
        {
            var root = GameRoot.I;
            var args = System.Environment.GetCommandLineArgs();
            int secArg = System.Array.IndexOf(args, "-alibiBenchSeconds");
            float seconds = secArg >= 0 && secArg + 1 < args.Length && float.TryParse(args[secArg + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sv) ? sv : 6f;
            SaveData.UnlockAll = true;
            int startLevel = Fidelity.Level;
            var rows = new List<(string scene, int level, FrameStats stats, string shot)>();
            var order = new[] { 0, 1, 2, 3, 2 };

            IEnumerator Steps(string scene)
            {
                Stage.I.Frozen = true;
                Clock.Held = true;   // the conflict glow, the memo and every tween stand still for the photographs
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                for (int i = 0; i < order.Length; i++)
                {
                    int level = order[i];
                    Settings.GraphicsFidelity = level;
                    yield return new WaitForSecondsRealtime(1.5f);   // shaders and buffers for the new step settle
                    var st = new FrameStats();
                    yield return FrameTimes(seconds, st);
                    string name = $"{scene}-{level}-{Fidelity.Names[level].ToLowerInvariant()}{(i == order.Length - 1 ? "-again" : "")}";
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{++shot:00}_{name}.png"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    Debug.Log($"[Fidelity] bench {scene} {Fidelity.Names[level]}: {st} at {UnityEngine.Screen.width}x{UnityEngine.Screen.height}");
                    rows.Add((scene, level, st, $"{shot:00}_{name}.png"));
                }
                Clock.Held = false;
                Stage.I.Frozen = false;
            }

            root.ShowTitle(false);
            yield return Wait(4f);
            yield return Steps("title");

            var c = Cases.All[2];
            root.StartCase(c, false, true);
            yield return Wait(3f);
            var s = root.Session;
            foreach (var id in s.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).ToList())
            {
                s.AutoPin(id);
                yield return Wait(0.3f);
            }
            yield return Wait(8f);   // the memos finish typing
            yield return Steps("board");

            var md = new StringBuilder();
            md.AppendLine($"# Fidelity bench, {UnityEngine.Screen.width}x{UnityEngine.Screen.height}, {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            md.AppendLine();
            md.AppendLine("| Scene | Step | Mean ms | Median ms | 95th pct ms | fps | Frames | Load | Screenshot |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows)
                md.AppendLine($"| {r.scene} | {Fidelity.Names[r.level]} | {r.stats.Mean:0.00} | {r.stats.Median:0.00} | {r.stats.P95:0.00} | {r.stats.Fps:0} | {r.stats.Frames} | {r.stats.Load:0.0} | {r.shot} |");
            File.WriteAllText(Path.Combine(dir, "fidelity-bench.md"), md.ToString());

            Settings.GraphicsFidelity = startLevel;
            bool ok = errors == 0 && rows.Count == order.Length * 2;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} fidelity bench ({rows.Count} steps)");
            Debug.Log($"[AutoPilot] done: fidelity bench, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
        }

        public void RunScreensTour(string outDir)
        {
            dir = outDir;
            capture = Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(ScreensTour());
        }

        /// <summary>
        /// -alibiScreensTour [dir]: the menus at the window's size and text size, quickly: Settings over the title
        /// (with its layout check), then case 1's pause menu with the controls list, Settings over it, and the
        /// pause menu caught opening, a few frames apart.
        /// </summary>
        IEnumerator ScreensTour()
        {
            var root = GameRoot.I;
            bool ok = true;
            void Layout(string when)
            {
                var found = root.Screens.SettingsLayoutReport(out int rows);
                if (found.Count == 0) Debug.Log($"[Settings] layout {when}: {rows} rows clear at {UnityEngine.Screen.width}x{UnityEngine.Screen.height}, text size {Settings.TextSize}");
                else { Debug.LogError($"[AutoPilot] FAIL settings layout {when} at {UnityEngine.Screen.width}x{UnityEngine.Screen.height}: {string.Join("; ", found)}"); ok = false; }
            }
            root.ShowTitle(true);
            yield return Wait(3.5f);
            root.Screens.ShowSettings();
            yield return Wait(0.8f);
            yield return Shot("settings_title");
            Layout("over the title");
            root.Screens.CloseTopOverlay();
            yield return Wait(0.5f);
            root.StartCase(Cases.All[0], false, true);
            yield return Wait(3f);
            // The pause menu opening: its panel's offset and scale every frame for half a second, and a few frames caught.
            root.SetPaused(true);
            var pausePanel = UiKit.Root.Find("Pause/panel");
            var series = new StringBuilder();
            float t0 = Time.realtimeSinceStartup;
            int caught = 0;
            while (Time.realtimeSinceStartup - t0 < 0.5f)
            {
                yield return new WaitForEndOfFrame();
                if (pausePanel != null && pausePanel.TryGetComponent<UiKit.Settle>(out var st))
                    series.Append($" {(Time.realtimeSinceStartup - t0) * 1000:0}ms:{st.Offset:0.0}px/{st.Scale:0.000}");
                if (caught < 4 && Time.realtimeSinceStartup - t0 > caught * 0.06f)
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{++shot:00}_pause_opening_{caught++}.png"));
            }
            Debug.Log($"[Settle] pause panel (offset/scale, reduced motion {Settings.ReducedMotion}):{series}");
            // With Reduced motion the panel only fades: it's in place from the first frame.
            root.SetPaused(false);
            yield return Wait(0.6f);
            Settings.ReducedMotion = true;
            root.SetPaused(true);
            var still = new StringBuilder();
            bool moved = false;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.3f)
            {
                yield return new WaitForEndOfFrame();
                if (pausePanel != null && pausePanel.TryGetComponent<UiKit.Settle>(out var st))
                {
                    still.Append($" {st.Offset:0.0}px/{st.Scale:0.000}");
                    moved |= Mathf.Abs(st.Offset) > 0.01f || Mathf.Abs(st.Scale - 1) > 0.0001f;
                }
            }
            Debug.Log($"[Settle] pause panel with reduced motion:{still}");
            if (moved) { Debug.LogError("[AutoPilot] FAIL screens tour: with Reduced motion the pause panel still moved"); ok = false; }
            if (series.ToString().Split(' ').Count(x => x.Contains("px/") && !x.Contains(":0.0px/1.000")) < 2) { Debug.LogError("[AutoPilot] FAIL screens tour: the pause panel didn't settle in"); ok = false; }
            Settings.ReducedMotion = false;
            yield return Wait(0.6f);
            yield return Wait(0.8f);
            yield return Shot("pause");
            var controls = root.Screens.ControlsLayoutReportAll();
            if (controls.Count == 0) Debug.Log($"[Controls] list fits at {UnityEngine.Screen.width}x{UnityEngine.Screen.height}, text size {Settings.TextSize}");
            else { Debug.LogError($"[AutoPilot] FAIL controls list at {UnityEngine.Screen.width}x{UnityEngine.Screen.height}: {string.Join("; ", controls)}"); ok = false; }
            root.Screens.ShowSettings();
            yield return Wait(0.8f);
            yield return Shot("settings_pause");
            Layout("over the pause menu");
            root.Screens.CloseTopOverlay();
            yield return Wait(0.5f);
            root.SetPaused(false);
            yield return Wait(0.5f);
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} screens tour at {UnityEngine.Screen.width}x{UnityEngine.Screen.height}, text size {Settings.TextSize}");
            Debug.Log($"[AutoPilot] done: screens tour, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
        }
    }
}
