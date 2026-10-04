using System.Collections;
using System.IO;
using System.Linq;
using AlibiCo.Logic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

        public void Run(string outDir, bool withCapture, bool inputTest = false)
        {
            dir = outDir;
            capture = withCapture;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(inputTest ? InputTest() : Go());
        }

        // ------------------------------------------------------------------ real-input test

        static void MouseTo(Vector2 p, bool left = false, bool right = false)
        {
            var st = new MouseState { position = p };
            if (left) st = st.WithButton(MouseButton.Left, true);
            if (right) st = st.WithButton(MouseButton.Right, true);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }

        static Vector2 Screen(Vector3 world) => Stage.I.Cam.WorldToScreenPoint(world);

        /// <summary>A point on the card that isn't covered by its neighbours (tray cards overlap left to right).</summary>
        static Vector2 Grab(CardView v) => v.Compact ? Screen(v.transform.position)
            : Screen(v.transform.TransformPoint(new Vector3(-CardView.FullSize.x / 2 + 0.45f, 0.3f, 0)));

        IEnumerator Glide(Vector2 a, Vector2 b, bool held, int frames = 18)
        {
            for (int i = 1; i <= frames; i++)
            {
                MouseTo(Vector2.Lerp(a, b, Easing.Apply(Ease.InOutSine, i / (float)frames)), held);
                yield return null;
            }
        }

        IEnumerator Drag(Vector2 from, Vector2 to, string shotMid = null)
        {
            MouseTo(from);
            yield return Wait(0.15f);
            MouseTo(from, true);
            yield return null;
            yield return Glide(from, Vector2.Lerp(from, to, 0.85f), true);
            if (shotMid != null) yield return Shot(shotMid);
            yield return Glide(Vector2.Lerp(from, to, 0.85f), to, true, 6);
            yield return Wait(0.15f);
            MouseTo(to, false);
            yield return Wait(0.2f);
        }

        IEnumerator Click(Vector2 p)
        {
            MouseTo(p);
            yield return Wait(0.12f);
            MouseTo(p, true);
            yield return null;
            yield return null;
            MouseTo(p, false);
            yield return Wait(0.15f);
        }

        IEnumerator InputTest()
        {
            var root = GameRoot.I;
            SaveData.UnlockAll = true;
            SaveData.Current.inProgress = null;
            var c = Cases.All[0];
            root.StartCase(c, false);
            yield return Wait(3f);
            var s = root.Session;
            bool ok = true;
            int n = 0;
            // 1. Drag every tray card onto the middle of the board.
            foreach (var id in s.Board.TrayCards.Select(x => x.Id).ToList())
            {
                var v = s.ViewOf(id);
                var from = Grab(v);
                var to = Screen(Stage.I.BoardToWorld(new Vector2(0, 0)));
                yield return Drag(from, to, n == 0 ? "input_drag" : null);
                yield return Wait(0.5f);
                if (!s.Board.Pinned.Contains(id)) { Debug.LogError($"[AutoPilot] FAIL input: drag didn't pin {id}"); ok = false; }
                n++;
            }
            yield return Wait(1.2f);
            yield return Shot("input_pinned");
            // 2. Hover a chip: the inspector should appear.
            var hoverCard = s.ViewOf("b_receipt");
            MouseTo(Screen(hoverCard.transform.position));
            yield return Wait(0.8f);
            yield return Shot("input_hover");
            // 3. Right-click sends a card back; drag it back on.
            var rc = s.ViewOf("a_tab");
            yield return Click(Screen(rc.transform.position) + new Vector2(0, 0));
            MouseTo(Screen(rc.transform.position), false, true);
            yield return null;
            MouseTo(Screen(rc.transform.position), false, false);
            yield return Wait(0.8f);
            if (s.Board.Pinned.Contains("a_tab")) { Debug.LogError("[AutoPilot] FAIL input: right-click didn't unpin"); ok = false; }
            yield return Drag(Grab(rc), Screen(Stage.I.BoardToWorld(Vector2.zero)));
            yield return Wait(0.8f);
            // 4. Confront each liar by clicking the chip and then the Confront button.
            foreach (var id in new[] { "a_claim", "c_claim", "b_claim" })
            {
                yield return Click(Screen(s.ViewOf(id).transform.position));
                yield return Wait(0.5f);
                var btn = root.Screens.ConfrontButtonScreen();
                if (btn == null) { Debug.LogError($"[AutoPilot] FAIL input: no confront button for {id}"); ok = false; continue; }
                if (id == "a_claim") yield return Shot("input_actions");
                yield return Click(btn.Value);
                yield return Wait(2.6f);
                if (!s.Board.Struck.Contains(id)) { Debug.LogError($"[AutoPilot] FAIL input: confronting {id} didn't strike it"); ok = false; }
                foreach (var nid in s.Board.TrayCards.Select(x => x.Id).ToList())
                {
                    yield return Drag(Grab(s.ViewOf(nid)), Screen(Stage.I.BoardToWorld(Vector2.zero)));
                    yield return Wait(0.5f);
                }
            }
            yield return Wait(1f);
            yield return Shot("input_ready");
            // 5. Drag the incident into the culprit's lane at its slot.
            var lane = s.View.LaneById[c.Incident.Culprit];
            var fit = s.Board.Fits[c.Incident.Culprit];
            var target = Screen(Stage.I.BoardToWorld(new Vector2(s.View.TimeToX(fit.EarliestStart), lane.Track + 0.5f)));
            yield return Drag(Screen(s.IncidentView.transform.position), target, "input_accuse_drag");
            float t = 0;
            while (root.Flow != Flow.Closed && t < 60) { t += Clock.Dt; yield return null; }
            yield return Wait(2f);
            yield return Shot("input_closed");
            if (root.Flow != Flow.Closed) { Debug.LogError("[AutoPilot] FAIL input: accusation by drag didn't close the case"); ok = false; }
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} input test (drag, hover, right-click, click, UI button, incident drag)");
            Debug.Log($"[AutoPilot] done: input test, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
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
            while (s > 0) { s -= Clock.Dt; yield return null; }
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
                root.Screens.ShowSettings();
                yield return Wait(0.8f);
                yield return Shot("settings");
                root.Screens.CloseTopOverlay();
                yield return Wait(0.5f);
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
                    if (step == 2)
                    {
                        GameRoot.I.Screens.ToggleNotebook();
                        yield return Wait(0.7f);
                        if (!GameRoot.I.Screens.NotebookOpen) { Debug.LogError($"[AutoPilot] FAIL {c.Id}: notebook didn't open"); ok = false; }
                        yield return Shot(c.Id + "_notebook");
                        GameRoot.I.Screens.ToggleNotebook();
                        yield return Wait(0.5f);
                    }
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
                    while (root.Flow != Flow.Closed && t < 90) { t += Clock.Dt; yield return null; }
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
