using System.Collections;
using System.IO;
using System.Linq;
using AlibiCo.Logic;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// Self-test: launched with -alibiAutoplay [dir] (or -alibiCapture dir for screenshots) the game
    /// plays all three cases through the real session code, using the solver's moves, and prints
    /// PASS/FAIL lines to the log. Exceptions during the run fail it.
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        string dir;
        bool capture;
        int errors;
        int shot;

        public void Run(string outDir, bool withCapture)
        {
            dir = outDir;
            capture = withCapture;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(Go());
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) errors++;
        }

        IEnumerator Shot(string name)
        {
            if (!capture) yield break;
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(dir, $"{++shot:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AutoPilot] shot " + path);
            yield return Wait(0.2f);
        }

        static IEnumerator Wait(float s)
        {
            while (s > 0) { s -= Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator Go()
        {
            var root = GameRoot.I;
            SaveData.UnlockAll = true;
            SaveData.Current.inProgress = null;
            float pace = capture ? 1f : 0.6f;
            if (capture)
            {
                root.ShowTitle(true);
                yield return Wait(3.5f);
                yield return Shot("title");
                root.ShowSelect();
                yield return Wait(1.5f);
                yield return Shot("case_files");
            }
            int passed = 0;
            foreach (var c in Cases.All)
            {
                int errorsBefore = errors;
                if (capture)
                {
                    root.ShowIntro(c);
                    yield return Wait(3.5f);
                    yield return Shot(c.Id + "_intro");
                }
                root.StartCase(c, false);
                yield return Wait(2.6f);
                var s = root.Session;
                yield return Shot(c.Id + "_dealt");
                foreach (var id in s.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).ToList())
                {
                    s.AutoPin(id);
                    yield return Wait(0.18f * pace);
                }
                yield return Wait(1.6f);
                yield return Shot(c.Id + "_pinned");
                // Hover a contradiction card so the inspector shows up in captures.
                int step = 0;
                bool ok = true;
                while (true)
                {
                    var shadow = Solver.Shadow(s.Board);
                    var path = Solver.ShortestSolution(shadow);
                    if (path == null) { Debug.LogError($"[AutoPilot] FAIL {c.Id}: no solution from state {s.Board.StateKey()}"); ok = false; break; }
                    if (path.Count == 0) break;
                    var m = path[0];
                    if (m.Kind == "link")
                    {
                        if (!s.Board.Pinned.Contains(m.A)) s.AutoPin(m.A);
                        if (!s.Board.Pinned.Contains(m.B)) s.AutoPin(m.B);
                        yield return Wait(0.4f);
                        s.AutoLink(m.A, m.B);
                    }
                    else s.Confront(s.ViewOf(m.A));
                    yield return Wait(2.4f * pace + 0.6f);
                    foreach (var id in s.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).ToList())
                    {
                        s.AutoPin(id);
                        yield return Wait(0.2f * pace);
                    }
                    yield return Wait(1.2f);
                    yield return Shot($"{c.Id}_step{++step}_{m.Kind}");
                    if (step > 20) { ok = false; Debug.LogError("[AutoPilot] FAIL too many steps"); break; }
                }
                if (ok)
                {
                    var chk = s.Board.CheckAccusation(c.Incident.Culprit);
                    if (!chk.Ok) { Debug.LogError($"[AutoPilot] FAIL {c.Id}: can't accuse: {chk.Message}"); ok = false; }
                    foreach (var p in c.Suspects.Where(p => p.Id != c.Incident.Culprit))
                        if (s.Board.CheckAccusation(p.Id).Ok) { Debug.LogError($"[AutoPilot] FAIL {c.Id}: {p.Id} accusable too"); ok = false; }
                }
                if (ok)
                {
                    s.AutoAccuse(c.Incident.Culprit);
                    float t = 0;
                    yield return Wait(4f);
                    yield return Shot(c.Id + "_reconstruction");
                    while (root.Flow != Flow.Closed && t < 90) { t += Time.unscaledDeltaTime; yield return null; }
                    if (root.Flow != Flow.Closed) { ok = false; Debug.LogError($"[AutoPilot] FAIL {c.Id}: never reached Case Closed"); }
                    yield return Wait(3.5f);
                    yield return Shot(c.Id + "_closed");
                }
                if (errors > errorsBefore) { ok = false; Debug.LogError($"[AutoPilot] FAIL {c.Id}: {errors - errorsBefore} errors logged"); }
                Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {c.Id} \"{c.Title}\" badges={s.Badges} steps={step}");
                if (ok) passed++;
            }
            Debug.Log($"[AutoPilot] done: {passed}/{Cases.All.Count} cases passed, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(passed == Cases.All.Count && errors == 0 ? 0 : 1);
        }
    }
}
