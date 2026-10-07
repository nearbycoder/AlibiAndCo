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
    /// plays every case through the real session code, using the solver's moves, and prints
    /// PASS/FAIL lines to the log. Exceptions during the run fail it.
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        string dir;
        bool capture;
        int errors;
        int shot;

        public void Run(string outDir, bool withCapture, bool inputTest = false, bool padTest = false)
        {
            dir = outDir;
            // In a browser there's no disk to write screenshots to; Tools/webtest.mjs takes them instead.
            capture = withCapture && Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(padTest ? PadTest() : inputTest ? InputTest() : Go());
        }

        /// <summary>Every card in an established contradiction carries the shape marker, and no other card does.</summary>
        void CheckConflictMarks(CaseSession s, string when)
        {
            var report = s.ConflictReport(out bool match);
            if (match) Debug.Log($"[Conflicts] {when}: {report}");
            else Debug.LogError($"[AutoPilot] FAIL conflict markers {when}: {report}");
        }

        // ------------------------------------------------------------------ gamepad test

        Gamepad pad;

        void PadState(Vector2 stick, params GamepadButton[] held)
        {
            var st = new GamepadState { leftStick = stick };
            foreach (var b in held) st = st.WithButton(b, true);
            InputSystem.QueueStateEvent(pad, st);
        }

        /// <summary>Steer the pad cursor onto a screen point with the left stick, like a player would.</summary>
        IEnumerator PadMoveTo(Vector2 target, params GamepadButton[] held)
        {
            for (int i = 0; i < 900; i++)
            {
                var d = target - PadCursor.I.Position;
                // Close enough: 3 px, or one frame of the cursor's slowest speed when the frame rate is
                // low (at ~11 fps a single frame moves it ~20 px, and a 3 px target is never hit).
                if (d.magnitude < Mathf.Max(3f, UnityEngine.Screen.height * 0.25f * Clock.Dt)) break;
                var stick = Vector2.ClampMagnitude(d / (UnityEngine.Screen.height * 0.12f), 1f);
                if (stick.magnitude < 0.3f) stick = stick.normalized * 0.3f;
                PadState(stick, held);
                yield return null;
            }
            PadState(Vector2.zero, held);
            yield return null;
            yield return null;
        }

        IEnumerator PadTap(GamepadButton b)
        {
            PadState(Vector2.zero, b);
            yield return null;
            yield return null;
            PadState(Vector2.zero);
            yield return Wait(0.25f);
        }

        IEnumerator PadDrag(Vector2 from, Vector2 to)
        {
            yield return PadMoveTo(from);
            PadState(Vector2.zero, GamepadButton.South);
            yield return Wait(0.15f);
            yield return PadMoveTo(to, GamepadButton.South);
            yield return Wait(0.2f);
            PadState(Vector2.zero);
            yield return Wait(0.4f);
        }

        Vector2 TrayPoint(CardView v) => Screen(v.transform.TransformPoint(new Vector3(-CardView.FullSize.x / 2 + 0.45f, 0.3f, 0)));

        /// <summary>
        /// -alibiPadTest: case 1 from the dealt tray to CASE CLOSED with nothing but a (simulated)
        /// gamepad: stick, A, B, X, Y, LB/RB and Start.
        /// </summary>
        IEnumerator PadTest()
        {
            var root = GameRoot.I;
            bool ok = true;
            void Fail(string why) { Debug.LogError("[AutoPilot] FAIL pad: " + why); ok = false; }
            pad = InputSystem.AddDevice<Gamepad>("TestPad");
            pad.MakeCurrent();
            PadCursor.IgnoreRealMouse = true;   // the desktop is shared; only step 8 hands over on purpose
            SaveData.UnlockAll = true;
            SaveData.Current.inProgress = null;
            var c = Cases.All[0];
            root.StartCase(c, false);
            yield return Wait(3f);
            var s = root.Session;

            // 1. Touching the pad hands it the pointer; RB jumps onto a card.
            yield return PadTap(GamepadButton.RightShoulder);
            if (!PadCursor.Active) Fail("touching the pad didn't activate the pad cursor");
            var targets = s.CursorTargets();
            if (!targets.Any(t => (t - PadCursor.I.Position).magnitude < 40f)) Fail("RB didn't land on a card");   // a hovered card lifts a little
            yield return PadTap(GamepadButton.RightShoulder);
            yield return Wait(0.6f);
            yield return Shot("pad_jump_prompt");

            // 2. Hold A and steer: drag the first tray card onto the board.
            var firstId = s.Board.TrayCards.First().Id;
            yield return PadDrag(TrayPoint(s.ViewOf(firstId)), Screen(Stage.I.BoardToWorld(Vector2.zero)));
            yield return Wait(0.6f);
            if (!s.Board.Pinned.Contains(firstId)) Fail($"A-drag didn't pin {firstId}");

            // 3. Tap A on each remaining tray card to pin it.
            foreach (var id in s.Board.TrayCards.Select(x => x.Id).ToList())
            {
                yield return PadMoveTo(TrayPoint(s.ViewOf(id)));
                yield return PadTap(GamepadButton.South);
                yield return Wait(0.5f);
                if (!s.Board.Pinned.Contains(id)) Fail($"A didn't pin {id}");
            }
            yield return Wait(1f);

            // 4. B on a pinned card sends it back; A in the tray pins it again.
            var back = s.ViewOf("a_tab");
            yield return PadMoveTo(Screen(back.transform.position));
            yield return Wait(0.3f);
            yield return PadTap(GamepadButton.East);
            yield return Wait(0.7f);
            if (s.Board.Pinned.Contains("a_tab")) Fail("B didn't send the card back");
            yield return PadMoveTo(TrayPoint(back));
            yield return PadTap(GamepadButton.South);
            yield return Wait(0.7f);
            if (!s.Board.Pinned.Contains("a_tab")) Fail("A didn't re-pin the card");

            // 5. Y opens the notebook, B closes it; X asks for a hint; Start pauses and resumes.
            yield return PadTap(GamepadButton.North);
            yield return Wait(0.6f);
            if (!root.Screens.NotebookOpen) Fail("Y didn't open the notebook");
            yield return PadTap(GamepadButton.East);
            yield return Wait(0.4f);
            if (root.Screens.NotebookOpen) Fail("B didn't close the notebook");
            int memos = s.Memos.History.Count;
            yield return PadTap(GamepadButton.West);
            yield return Wait(0.4f);
            if (s.Memos.History.Count <= memos) Fail("X didn't ask for a hint");
            yield return PadTap(GamepadButton.Start);
            yield return Wait(0.8f);
            if (!GameRoot.Paused) Fail("Start didn't pause");
            yield return Shot("pad_pause_controls");
            // LB/RB in a menu jump between the buttons you can actually press.
            yield return PadTap(GamepadButton.RightShoulder);
            var menuTargets = PadCursor.Targets();
            if (menuTargets.Count < 5 || !menuTargets.Any(t => (t - PadCursor.I.Position).magnitude < 2f)) Fail($"RB in the pause menu didn't land on a button ({menuTargets.Count} targets)");
            yield return PadTap(GamepadButton.Start);
            yield return Wait(0.5f);
            if (GameRoot.Paused) Fail("Start didn't resume");

            // 6. Confront each liar: A on the chip, then A on Confront.
            foreach (var id in new[] { "a_claim", "c_claim", "b_claim" })
            {
                yield return PadMoveTo(Screen(s.ViewOf(id).transform.position));
                yield return PadTap(GamepadButton.South);
                yield return Wait(0.5f);
                var btn = root.Screens.ConfrontButtonScreen();
                if (btn == null) { Fail($"no Confront button for {id}"); continue; }
                if (id == "a_claim") yield return Shot("pad_actions");
                yield return PadMoveTo(btn.Value);
                yield return PadTap(GamepadButton.South);
                yield return Wait(2.6f);
                if (!s.Board.Struck.Contains(id)) Fail($"confronting {id} didn't strike it");
                foreach (var nid in s.Board.TrayCards.Select(x => x.Id).ToList())
                {
                    yield return PadMoveTo(TrayPoint(s.ViewOf(nid)));
                    yield return PadTap(GamepadButton.South);
                    yield return Wait(0.5f);
                }
            }
            yield return Wait(1f);

            // 7. Hold A on the incident card and drop it in the culprit's line.
            var lane = s.View.LaneById[c.Incident.Culprit];
            var fit = s.Board.Fits[c.Incident.Culprit];
            var slot = Screen(Stage.I.BoardToWorld(new Vector2(s.View.TimeToX(fit.EarliestStart), lane.Track + 0.5f)));
            yield return PadDrag(Screen(s.IncidentView.transform.position), slot);
            float t = 0;
            while (root.Flow != Flow.Closed && t < 60) { t += Clock.Dt; yield return null; }
            yield return Wait(2f);
            yield return Shot("pad_closed");
            if (root.Flow != Flow.Closed) Fail("the incident drag didn't close the case");

            // 8. Moving the real mouse hands control back.
            PadCursor.IgnoreRealMouse = false;
            if (Mouse.current != null && PadCursor.Active)
            {
                var real = InputSystem.devices.OfType<Mouse>().FirstOrDefault(m => m.name != "PadCursor");
                if (real != null)
                {
                    InputSystem.QueueStateEvent(real, new MouseState { position = new Vector2(200, 200), delta = new Vector2(40, 0) });
                    yield return null;
                    yield return null;
                    if (PadCursor.Active) Fail("moving the mouse didn't hand control back");
                }
            }
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} pad test (stick, RB jump, A-drag, A pin, B send back, Y notebook, B close, X hint, Start pause, A confront, incident drag, mouse takes over)");
            Debug.Log($"[AutoPilot] done: pad test, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
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
            // 3b. Changing the text size mid-case rebuilds the board in place: same pins, bigger chips.
            {
                int pinned = s.Board.Pinned.Count;
                float chip = s.View.ChipScale;
                int memos = s.Memos.History.Count;
                Settings.TextSizeOverride = 2;
                UiKit.ApplyScale();
                root.TextSizeChanged();
                yield return Wait(1.5f);
                var s2 = root.Session;
                if (s2 == s || s2 == null || s2.Board.Pinned.Count != pinned || s2.Memos.History.Count != memos || s2.View.ChipScale <= chip)
                {
                    Debug.LogError($"[AutoPilot] FAIL input: text size rebuild (pins {pinned}->{(s2 ? s2.Board.Pinned.Count : -1)}, chips {chip}->{(s2 ? s2.View.ChipScale : -1)})");
                    ok = false;
                }
                yield return Shot("input_text_larger");
                Settings.TextSizeOverride = -1;
                UiKit.ApplyScale();
                root.TextSizeChanged();
                yield return Wait(1.5f);
                s = root.Session;
            }
            // 3c. Ask Connie once: the case can no longer earn the Unaided seal.
            s.Hint();
            yield return Wait(0.5f);
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
            var rec = SaveData.Current.Record(c.Id);
            if (rec.sealUnaided || !rec.sealClean) { Debug.LogError($"[AutoPilot] FAIL input: after a hint, seals were clean={rec.sealClean} unaided={rec.sealUnaided}"); ok = false; }
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} input test (drag, hover, right-click, text size rebuild, hint withholds Unaided, click, UI button, incident drag)");
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
            // Frame count and real time, so a run that crawls (a throttled window, a busy machine) shows it.
            Debug.Log($"[AutoPilot] shot {path} (frame {Time.frameCount}, {Time.realtimeSinceStartup:0}s)");
            yield return Wait(0.2f);
        }

        static IEnumerator Wait(float s)
        {
            while (s > 0) { s -= Clock.Dt; yield return null; }
        }

        /// <summary>After a firm stand, Connie's next memo explains it and names the clock to blame.</summary>
        static bool CheckWhyFirm(CaseSession s, string caseId, string cardId, string clock)
        {
            var h = s.Memos.History;
            int firm = h.FindLastIndex(m => m.Kind == MemoKind.Firm);
            var why = firm >= 0 && firm + 1 < h.Count ? h[firm + 1] : null;
            bool ok = why != null && why.Kind == MemoKind.Connie && why.Text.Contains(clock);
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {caseId} why-firm for {cardId}: {(why != null ? why.Text : "no memo")}");
            return ok;
        }

        /// <summary>Capture only: skip queued slips until Connie's explanation of a firm stand is on the desk.</summary>
        IEnumerator ShotWhyFirm(CaseSession s, string name)
        {
            if (!capture) yield break;
            for (int i = 0; i < 12 && !(s.Memos.Showing != null && s.Memos.Showing.Kind == MemoKind.Connie && s.Memos.Showing.Text.Contains("stood firm")); i++)
            {
                s.Memos.Skip();
                s.Memos.Skip();
                yield return Wait(0.3f);
            }
            yield return Wait(2.5f);
            yield return Shot(name);
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
            // The five cases, then today's Daily Docket and three fixed days (two with a wrong clock).
            var toPlay = new System.Collections.Generic.List<CaseDef>(Cases.All);
            var trapDocket = Docket.IdFor(new System.DateTime(2026, 10, 7));
            foreach (var day in new[] { Cases.Today, new System.DateTime(2026, 10, 7), new System.DateTime(2026, 10, 13), new System.DateTime(2026, 12, 25) })
            {
                var dk = Cases.DocketFor(day);
                if (dk == null) { Debug.LogError($"[AutoPilot] FAIL no docket for {day:yyyy-MM-dd}"); continue; }
                if (!toPlay.Contains(dk)) toPlay.Add(dk);
            }
            foreach (var c in toPlay)
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
                Debug.Log($"[Legibility] {c.Id} pinned: {s.LegibilityReport()}");
                CheckConflictMarks(s, c.Id + " pinned");
                // Hover a contradiction card so the inspector shows up in captures.
                int step = 0;
                bool ok = true;
                int trapBadges = 0;
                if (c.Id == "case4")
                {
                    // The finale's trap, played on purpose: Agnes's lie gives way to Maud, whose true
                    // statement is red only because of the Town Hall clock. Confronting her must cost a
                    // badge and leave the card standing.
                    s.Confront(s.ViewOf("a_claim"));
                    yield return Wait(3f);
                    foreach (var id in s.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).ToList()) s.AutoPin(id);
                    yield return Wait(1.2f);
                    int before = s.Board.Mistakes;
                    bool canConfront = s.Board.CanConfront("a_maud", out var why);
                    s.Confront(s.ViewOf("a_maud"));
                    yield return Wait(2.5f);
                    yield return Shot(c.Id + "_trap_stands_firm");
                    CheckConflictMarks(s, c.Id + " trap");
                    bool firm = canConfront && s.Board.Mistakes == before + 1 && !s.Board.Struck.Contains("a_maud")
                                && s.Memos.History.Any(m => m.Kind == MemoKind.Firm);
                    if (!CheckWhyFirm(s, c.Id, "a_maud", c.ClockById["hall"].InSentence)) ok = false;
                    yield return ShotWhyFirm(s, c.Id + "_why_firm");
                    Debug.Log($"[AutoPilot] {(firm ? "PASS" : "FAIL")} {c.Id} trap: confronting Maud {(firm ? "stands firm and costs a badge" : "did not behave (" + why + ")")}");
                    if (!firm) ok = false;
                    trapBadges = 1;
                }
                else if (c.Id == trapDocket)
                {
                    // A clock day's trap: the honest story is red only because of the wrong clock.
                    // Confronting it must cost a badge and leave the card standing.
                    int before = s.Board.Mistakes;
                    bool canConfront = s.Board.CanConfront("h_claim", out var why);
                    s.Confront(s.ViewOf("h_claim"));
                    yield return Wait(2.5f);
                    yield return Shot(c.Id + "_trap_stands_firm");
                    CheckConflictMarks(s, c.Id + " trap");
                    bool firm = canConfront && s.Board.Mistakes == before + 1 && !s.Board.Struck.Contains("h_claim");
                    if (!CheckWhyFirm(s, c.Id, "h_claim", c.ClockById["k"].InSentence)) ok = false;
                    yield return ShotWhyFirm(s, c.Id + "_why_firm");
                    Debug.Log($"[AutoPilot] {(firm ? "PASS" : "FAIL")} {c.Id} trap: confronting the honest story {(firm ? "stands firm and costs a badge" : "did not behave (" + why + ")")}");
                    if (!firm) ok = false;
                    trapBadges = 1;
                }
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
                    CheckConflictMarks(s, $"{c.Id} step {step}");
                    if (step == 2)
                    {
                        GameRoot.I.Screens.ToggleNotebook();
                        yield return Wait(0.7f);
                        if (!GameRoot.I.Screens.NotebookOpen) { Debug.LogError($"[AutoPilot] FAIL {c.Id}: notebook didn't open"); ok = false; }
                        yield return Shot(c.Id + "_notebook");
                        GameRoot.I.Screens.ToggleNotebook();
                        yield return Wait(0.5f);
                        if (capture && c == Cases.All[0])
                        {
                            root.SetPaused(true);
                            yield return Wait(0.8f);
                            yield return Shot(c.Id + "_pause");
                            root.SetPaused(false);
                            yield return Wait(0.5f);
                        }
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
                Debug.Log($"[Legibility] {c.Id} solved: {s.LegibilityReport()}");
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
                if (s.Badges != 3 - trapBadges) { ok = false; Debug.LogError($"[AutoPilot] FAIL {c.Id}: expected {3 - trapBadges} badges, got {s.Badges}"); }
                // Seals: no hints and well under par, so Unaided and Swift; Clean unless the trap cost a badge.
                var rec = SaveData.Current.Record(c.Id);
                if (!rec.sealUnaided || !rec.sealSwift || rec.sealClean != (trapBadges == 0))
                {
                    ok = false;
                    Debug.LogError($"[AutoPilot] FAIL {c.Id}: seals clean={rec.sealClean} unaided={rec.sealUnaided} swift={rec.sealSwift}");
                }
                Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {c.Id} \"{c.Title}\" badges={s.Badges} steps={step}");
                if (ok) passed++;
            }
            if (capture)
            {
                root.ShowSelect();
                yield return Wait(1.5f);
                yield return Shot("case_files_sealed");
            }
            Debug.Log($"[AutoPilot] done: {passed}/{toPlay.Count} cases passed ({Cases.All.Count} cases, {toPlay.Count - Cases.All.Count} dockets), {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(passed == toPlay.Count && errors == 0 ? 0 : 1);
        }
    }
}
