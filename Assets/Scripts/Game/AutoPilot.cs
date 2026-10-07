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

        public void Run(string outDir, bool withCapture, bool inputTest = false, bool padTest = false, bool keysTest = false)
        {
            dir = outDir;
            // In a browser there's no disk to write screenshots to; Tools/webtest.mjs takes them instead.
            capture = withCapture && Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(keysTest ? KeysTest() : padTest ? PadTest() : inputTest ? InputTest() : Go());
        }

        /// <summary>Every card in an established contradiction carries the shape marker, and no other card does.</summary>
        void CheckConflictMarks(CaseSession s, string when)
        {
            var report = s.ConflictReport(out bool match);
            if (match) Debug.Log($"[Conflicts] {when}: {report}");
            else Debug.LogError($"[AutoPilot] FAIL conflict markers {when}: {report}");
        }

        // ------------------------------------------------------------------ the docket drawer, by hand

        /// <summary>
        /// From case 1's closed panel to an earlier day's docket on the board, through the case files
        /// and the docket drawer, with whatever pointer the test drives: <paramref name="click"/> moves
        /// it onto a screen point and clicks, <paramref name="back"/> is that pointer's "close".
        /// </summary>
        IEnumerator DrawerByHand(string how, System.Func<Vector2, IEnumerator> click, System.Func<IEnumerator> back, System.Action<string> fail)
        {
            var root = GameRoot.I;
            var day = Cases.Today.AddDays(-3);
            string rowName = "docket_" + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var docket = Cases.DocketFor(day);
            IEnumerator Press(string name, string what)
            {
                Vector2? at = null;
                for (float t = 0; t < 3f && at == null; t += Clock.Dt) { at = root.Screens.ButtonScreen(name); if (at == null) yield return null; }
                if (at == null) { fail($"{how}: {what} ({name}) isn't on screen"); yield break; }
                yield return click(at.Value);
                yield return Wait(0.8f);
            }

            yield return Press("btn_Case files", "the closed panel's Case files button");
            if (root.Flow != Flow.Select) fail($"{how}: Case files didn't open the case files (flow {root.Flow})");
            yield return Wait(0.6f);
            yield return Press("btn_docket", "the Daily Docket button");
            if (!root.Screens.DocketWeekOpen) fail($"{how}: the Daily Docket button didn't open the drawer");
            int rows = Docket.Week(Cases.Today).Count(d => root.Screens.ButtonScreen("docket_" + d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) != null);
            if (rows != Docket.DaysOnFile) fail($"{how}: {rows} of the drawer's {Docket.DaysOnFile} rows can be reached");
            if (how != "mouse")
            {
                // The jump buttons (RB / E) can land on every row and on Close.
                var targets = PadCursor.Targets();
                int jumpable = Docket.Week(Cases.Today).Count(d => root.Screens.ButtonScreen("docket_" + d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) is Vector2 at && targets.Any(t => (t - at).magnitude < 2f));
                var close = root.Screens.ButtonScreen("btn_Close");
                if (jumpable != Docket.DaysOnFile || close == null || !targets.Any(t => (t - close.Value).magnitude < 2f))
                    fail($"{how}: the jumps reach {jumpable} of {Docket.DaysOnFile} rows{(close == null ? " and no Close" : "")} ({targets.Count} targets)");
            }
            yield return Shot(how + "_drawer");
            yield return back();
            yield return Wait(0.6f);
            if (root.Screens.DocketWeekOpen || root.Flow != Flow.Select) fail($"{how}: closing the drawer didn't leave the case files (flow {root.Flow})");
            yield return Press("btn_docket", "the Daily Docket button");
            yield return Press(rowName, "the row three days back");
            if (root.Flow != Flow.Intro || root.IntroCase?.Id != docket.Id) fail($"{how}: the row didn't open {docket.Id}'s intro (flow {root.Flow})");
            yield return Wait(1f);
            yield return Press("btn_Back to the dockets", "the intro's Back to the dockets");
            if (!root.Screens.DocketWeekOpen) fail($"{how}: Back to the dockets didn't reopen the drawer");
            yield return Press(rowName, "the row three days back, again");
            yield return Wait(1f);
            yield return Shot(how + "_docket_intro");
            yield return Press("btn_Open the board", "the intro's Open the board");
            yield return Wait(2.5f);
            if (root.Flow != Flow.Playing || root.Session?.Case.Id != docket.Id) fail($"{how}: Open the board didn't start {docket.Id} (flow {root.Flow})");
            else Debug.Log($"[AutoPilot] {how}: opened {docket.Id} \"{docket.Title}\" from the drawer by hand");
            yield return Shot(how + "_docket_board");
        }

        // ------------------------------------------------------------------ share check

        public void RunShareCheck(string outDir)
        {
            dir = outDir;
            capture = Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(ShareCheck());
        }

        /// <summary>
        /// -alibiShareCheck -alibiClipboardCheck: solve today's docket, click Copy result with the
        /// mouse and leave the line on the system clipboard for a few seconds, so a script can read it
        /// from outside the game.
        /// </summary>
        IEnumerator ShareCheck()
        {
            var root = GameRoot.I;
            bool ok = true;
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
            var c = Cases.TodaysDocket;
            root.StartCase(c, false);
            yield return Wait(2.6f);
            var s = root.Session;
            for (int step = 0; step < 20; step++)
            {
                foreach (var id in s.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).ToList()) s.AutoPin(id);
                yield return Wait(1f);
                var path = Solver.ShortestSolution(Solver.Shadow(s.Board));
                if (path == null) { Debug.LogError("[AutoPilot] FAIL share: no solution"); ok = false; break; }
                if (path.Count == 0) break;
                var m = path[0];
                if (m.Kind == "link") s.AutoLink(m.A, m.B); else s.Confront(s.ViewOf(m.A));
                yield return Wait(2.5f);
            }
            s.AutoAccuse(c.Incident.Culprit);
            float t = 0;
            while (root.Flow != Flow.Closed && t < 90) { t += Clock.Dt; yield return null; }
            yield return Wait(3f);
            var btn = root.Screens.ButtonScreen("btn_copy_result");
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            if (btn == null) { Debug.LogError("[AutoPilot] FAIL share: no Copy result button"); ok = false; }
            else if (web)
            {
                // In a browser the page's clipboard wants a real click: Tools/webtest.mjs clicks here.
                Debug.Log($"[Share] waiting for a click at {btn.Value.x:0},{btn.Value.y:0} of {UnityEngine.Screen.width}x{UnityEngine.Screen.height}");
                for (float w = 0; w < 60f && Clipboard.Last == null; w += Time.unscaledDeltaTime) yield return null;
            }
            else yield return Click(btn.Value);
            yield return Wait(0.6f);
            yield return Shot("share_copied");
            Docket.TryParseId(c.Id, out var day);
            var want = Docket.ShareLine(day, s.Badges, s.Elapsed, s.SealClean, s.SealUnaided, s.SealSwift);
            Debug.Log($"[Share] expected: {want}");
            if (web)
            {
                if (Clipboard.Last != want) { Debug.LogError("[AutoPilot] FAIL share: the click didn't copy the line"); ok = false; }
            }
            else
            {
                var sys = GUIUtility.systemCopyBuffer;
                Debug.Log($"[Share] Unity's clipboard reads back: {sys}");
                if (sys != want) { Debug.LogError("[AutoPilot] FAIL share: the clipboard doesn't hold the line"); ok = false; }
                Debug.Log("[Share] holding the clipboard for 8 s");
                yield return new WaitForSecondsRealtime(8f);
            }
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} share check");
            Debug.Log($"[AutoPilot] done: share check, {errors} errors");
            Application.Quit(ok ? 0 : 1);
        }

        // ------------------------------------------------------------------ every case keeps its board

        public void RunBoards(string outDir)
        {
            dir = outDir;
            capture = Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(BoardsTest());
        }

        /// <summary>
        /// -alibiBoardsTest: leave three boards part-way (case 1, case 2 with a badge lost, today's
        /// docket), then check each one is still there: the folders and the drawer say IN PROGRESS,
        /// Continue resumes the last one, each case reopens with its own pins, badges and timer, and
        /// the intro's Start over asks before it wipes anything.
        /// </summary>
        IEnumerator BoardsTest()
        {
            var root = GameRoot.I;
            bool ok = true;
            void Fail(string why) { Debug.LogError("[AutoPilot] FAIL boards: " + why); ok = false; }
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
            var c1 = Cases.All[0];
            var c2 = Cases.All[1];
            var dk = Cases.TodaysDocket;

            IEnumerator Leave(CaseDef c, int pins, bool wrongLink)
            {
                root.StartCase(c, false);
                yield return Wait(2.6f);
                var s = root.Session;
                foreach (var id in s.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).Take(pins).ToList())
                {
                    s.AutoPin(id);
                    yield return Wait(0.4f);
                }
                if (wrongLink)
                {
                    // Two cards that aren't one moment: a badge lost.
                    var cards = s.Board.UnlockedCards.ToList();
                    var pair = cards.SelectMany(a => cards.Where(b => b.Id != a.Id && (a.Event == null || a.Event != b.Event)).Select(b => (a, b))).First();
                    s.AutoLink(pair.a.Id, pair.b.Id);
                    yield return Wait(0.8f);
                }
                yield return Wait(1f);
                Debug.Log($"[Boards] left {c.Id} with {s.Board.Pinned.Count} pins, {s.Badges} badges, {s.Elapsed:0.0}s");
            }

            // Three boards, left one after another through the case files.
            yield return Leave(c1, 2, false);
            int pins1 = root.Session.Board.Pinned.Count;
            float time1 = root.Session.Elapsed;
            root.ShowSelect();
            yield return Wait(1f);
            yield return Leave(c2, 1, true);
            int pins2 = root.Session.Board.Pinned.Count, badges2 = root.Session.Badges;
            root.ShowSelect();
            yield return Wait(1f);
            yield return Leave(dk, 1, false);
            int pinsD = root.Session.Board.Pinned.Count;
            root.ShowSelect();
            yield return Wait(1.2f);

            var save = SaveData.Current;
            Debug.Log($"[Boards] save: inProgress={save.inProgress?.caseId ?? "none"} shelved=[{string.Join(",", save.shelved.Select(b => $"{b.caseId}:{b.pinned.Count}pins/{b.mistakes}mistakes"))}]");
            if (save.inProgress?.caseId != dk.Id) Fail($"the most recent board isn't {dk.Id}");
            if (save.BoardFor(c1.Id)?.pinned.Count != pins1) Fail($"{c1.Id}'s board wasn't kept with {pins1} pins");
            if (save.BoardFor(c2.Id) is var b2 && (b2 == null || b2.pinned.Count != pins2 || b2.mistakes != 1)) Fail($"{c2.Id}'s board wasn't kept with {pins2} pins and a badge lost");
            foreach (var c in new[] { c1, c2 })
            {
                var text = root.Screens.FolderText(c.Id) ?? "";
                if (!text.Contains("IN PROGRESS")) Fail($"{c.Id}'s folder doesn't say IN PROGRESS: {text}");
            }
            if ((root.Screens.FolderText(Cases.All[2].Id) ?? "").Contains("IN PROGRESS")) Fail("case 3's folder says IN PROGRESS but was never opened");
            yield return Shot("case_files_two_in_progress");
            root.Screens.Press("btn_docket");
            yield return Wait(1f);
            var row = root.Screens.DocketRowText(Cases.Today) ?? "";
            if (!row.Contains("IN PROGRESS")) Fail("today's drawer row doesn't say IN PROGRESS: " + row);
            yield return Shot("drawer_in_progress");
            // Today's docket, in progress: its file has the widest row of buttons of all.
            root.Screens.Press("docket_" + Cases.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            yield return Wait(2f);
            if (root.Screens.ButtonScreen("btn_Continue the board") == null) Fail($"{dk.Id}'s intro doesn't offer Continue the board");
            CheckIntroSeals(dk);
            yield return Shot("docket_intro_in_progress");
            root.ShowSelect();
            yield return Wait(1f);

            // Continue: the board played last.
            root.ShowTitle(false);
            yield return Wait(1.5f);
            yield return Shot("title_continue");
            if (!root.Screens.Press("btn_Continue")) Fail("no Continue button on the title");
            yield return Wait(2.6f);
            if (root.Session?.Case.Id != dk.Id || root.Session.Board.Pinned.Count != pinsD) Fail($"Continue didn't resume {dk.Id} with {pinsD} pins");
            else Debug.Log($"[Boards] Continue resumed {dk.Id} with {pinsD} pins");

            // Case 1 from its folder: Start over asks, No keeps the board, Continue the board resumes it.
            root.ShowSelect();
            yield return Wait(1f);
            root.ShowIntro(c1);
            yield return Wait(1.5f);
            if (root.Screens.ButtonScreen("btn_Continue the board") == null) Fail($"{c1.Id}'s intro doesn't offer Continue the board");
            CheckIntroSeals(c1);   // with the widest row of buttons a case file has
            if (!root.Screens.Press("btn_Start over")) Fail($"{c1.Id}'s intro has no Start over");
            yield return Wait(0.6f);
            if (!root.Screens.ConfirmOpen) Fail("Start over didn't ask first");
            yield return Shot("start_over_asks");
            root.Screens.Press("btn_No");
            yield return Wait(0.6f);
            if (root.Flow != Flow.Intro || save.BoardFor(c1.Id)?.pinned.Count != pins1) Fail($"answering No to Start over didn't keep {c1.Id}'s board (flow {root.Flow})");
            float saved1 = save.BoardFor(c1.Id)?.elapsed ?? -1;
            if (saved1 < time1) Fail($"{c1.Id}'s board kept {saved1:0.0}s on its timer, not {time1:0.0}s");
            root.Screens.Press("btn_Continue the board");
            yield return null;
            var s1 = root.Session;
            // The timer picks up where it stopped (the board has only just reopened).
            if (s1?.Case.Id != c1.Id || s1.Board.Pinned.Count != pins1 || s1.Elapsed < saved1 || s1.Elapsed > saved1 + 0.5f)
                Fail($"{c1.Id} didn't reopen with {pins1} pins at {saved1:0.0}s ({(s1 != null ? $"{s1.Case.Id}, {s1.Board.Pinned.Count} pins, {s1.Elapsed:0.0}s" : "no session")})");
            else Debug.Log($"[Boards] {c1.Id} reopened with {pins1} pins at {s1.Elapsed:0.0}s (left at {time1:0.0}s)");
            yield return Wait(2.6f);
            yield return Shot("case1_reopened");

            // Case 2 the same way: its pins, and the badge it lost.
            root.ShowSelect();
            yield return Wait(1f);
            root.ShowIntro(c2);
            yield return Wait(1.5f);
            root.Screens.Press("btn_Continue the board");
            yield return Wait(2.6f);
            var s2 = root.Session;
            if (s2?.Case.Id != c2.Id || s2.Board.Pinned.Count != pins2 || s2.Badges != badges2)
                Fail($"{c2.Id} didn't reopen with {pins2} pins and {badges2} badges");
            else Debug.Log($"[Boards] {c2.Id} reopened with {pins2} pins and {s2.Badges} badges");

            // Start over, answered Yes, wipes that case's board and nothing else.
            root.ShowSelect();
            yield return Wait(1f);
            root.ShowIntro(c2);
            yield return Wait(1.5f);
            root.Screens.Press("btn_Start over");
            yield return Wait(0.6f);
            root.Screens.Press("btn_Yes");
            yield return Wait(2.6f);
            if (root.Session?.Case.Id != c2.Id || root.Session.Board.Pinned.Count != 0 || root.Session.Badges != 3) Fail($"Start over, Yes, didn't start {c2.Id} afresh");
            if (save.BoardFor(c1.Id)?.pinned.Count != pins1 || save.BoardFor(dk.Id)?.pinned.Count != pinsD) Fail("starting case 2 over touched another board");

            // Solving a case drops its board only; Continue then moves to the next most recent one.
            var sv = root.Session;
            for (int step = 0; step < 20; step++)
            {
                foreach (var id in sv.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).ToList()) sv.AutoPin(id);
                yield return Wait(0.8f);
                var path = Solver.ShortestSolution(Solver.Shadow(sv.Board));
                if (path == null || path.Count == 0) break;
                var m = path[0];
                if (m.Kind == "link") sv.AutoLink(m.A, m.B); else sv.Confront(sv.ViewOf(m.A));
                yield return Wait(2.2f);
            }
            sv.AutoAccuse(c2.Incident.Culprit);
            for (float t = 0; root.Flow != Flow.Closed && t < 90; t += Clock.Dt) yield return null;
            if (save.BoardFor(c2.Id) != null) Fail($"{c2.Id}'s board outlived solving it");
            if (save.BoardFor(c1.Id) == null || save.BoardFor(dk.Id) == null) Fail("solving case 2 dropped another board");
            Debug.Log($"[Boards] after solving {c2.Id}: inProgress={save.inProgress?.caseId ?? "none"} shelved=[{string.Join(",", save.shelved.Select(b => b.caseId))}]");

            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} boards test");
            Debug.Log($"[AutoPilot] done: boards test, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
        }

        // ------------------------------------------------------------------ midnight test

        public void RunFocus(string outDir, bool real)
        {
            dir = outDir;
            capture = Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(FocusTest(real));
        }

        /// <summary>
        /// -alibiFocusTest: the case timer stops while the game doesn't have the player's attention.
        /// By default it drives the handler Unity calls on a focus change; with -alibiFocusReal it
        /// waits for a real one from outside (Tools/webtest.mjs, or a person) and times it.
        /// </summary>
        /// <summary>
        /// Frames a second and CPU use in percent of one core over a stretch of real time: the game's
        /// own threads (main, rendering, jobs, audio), and apart from them the engine's HIDInput
        /// thread, which polls input devices at its own pace whatever the frame rate (-1: unknown).
        /// </summary>
        sealed class Sample
        {
            public float Fps, Cpu = -1, Input = -1;
            public override string ToString() => $"{Fps:0.0} fps, CPU {(Cpu < 0 ? "n/a" : Cpu.ToString("0.0") + "%")}" + (Input < 0 ? "" : $" (+{Input:0}% in Unity's HIDInput thread)");
        }

        /// <summary>CPU seconds so far: the game's threads, and the engine's HIDInput thread. Linux only (else -1).</summary>
        static (double game, double input) ThreadCpu()
        {
            double game = 0, input = 0;
            try
            {
                foreach (var task in Directory.GetDirectories("/proc/self/task"))
                {
                    string stat;
                    try { stat = File.ReadAllText(Path.Combine(task, "stat")); } catch (System.Exception) { continue; }   // a thread that just ended
                    var name = stat.Substring(stat.IndexOf('(') + 1, stat.LastIndexOf(')') - stat.IndexOf('(') - 1);
                    var f = stat.Substring(stat.LastIndexOf(')') + 2).Split(' ');
                    double ticks = long.Parse(f[11]) + long.Parse(f[12]);   // utime + stime
                    if (name == "HIDInput") input += ticks; else game += ticks;
                }
                return (game / 100.0, input / 100.0);   // USER_HZ is 100 on Linux
            }
            catch (System.Exception) { return (-1, -1); }   // not Linux (or a browser)
        }

        static IEnumerator Measure(float seconds, Sample into)
        {
            var c0 = ThreadCpu();
            int f0 = Time.frameCount;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
            float dt = Time.realtimeSinceStartup - t0;
            into.Fps = (Time.frameCount - f0) / dt;
            var c1 = ThreadCpu();
            if (c0.game >= 0 && c1.game >= 0)
            {
                into.Cpu = (float)((c1.game - c0.game) / dt * 100);
                into.Input = (float)((c1.input - c0.input) / dt * 100);
            }
        }

        IEnumerator FocusTest(bool real)
        {
            var root = GameRoot.I;
            bool ok = true;
            void Fail(string why) { Debug.LogError("[AutoPilot] FAIL focus: " + why); ok = false; }
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();

            // The game rests while it's away: about 10 frames a second, and far less CPU.
            void CheckRest(string where, Sample here, Sample away)
            {
                Debug.Log($"[AutoPilot] focus: {where} attended {here}; away {away}");
                if (away.Fps > GameRoot.AwayFrameRate + 2) Fail($"{where}: still drawing {away.Fps:0.0} fps while away");
                // The saving only shows when the game runs well above the cap with focus. On a busy
                // machine (or a throttled window) it may already crawl at about 10 fps.
                if (here.Fps < GameRoot.AwayFrameRate * 2.5f)
                    Debug.Log($"[AutoPilot] focus: {where}: only {here.Fps:0.0} fps with focus, so the rest can't be told apart here (not judged)");
                else if (here.Cpu > 0 && away.Cpu > here.Cpu * 0.5f) Fail($"{where}: the game's threads used {away.Cpu:0.0}% away against {here.Cpu:0.0}% attended (should be under half)");
            }
            if (!real)
            {
                root.ShowTitle(true);
                yield return Wait(3f);
                var titleHere = new Sample();
                var titleAway = new Sample();
                yield return Measure(4f, titleHere);
                root.SendMessage("OnApplicationFocus", false);
                yield return Measure(4f, titleAway);
                root.SendMessage("OnApplicationFocus", true);
                CheckRest("title", titleHere, titleAway);
            }

            root.StartCase(Cases.All[0], false);
            yield return Wait(3f);
            var s = root.Session;
            float awayReal, awayTimer;
            var boardHere = new Sample();
            var boardAway = new Sample();
            if (!real)
            {
                root.SendMessage("OnApplicationFocus", true);
                yield return Wait(1f);
                yield return Measure(4f, boardHere);
                float e0 = s.Elapsed, t0 = Time.realtimeSinceStartup;
                root.SendMessage("OnApplicationFocus", false);
                yield return Shot("focus_away");
                yield return Measure(4f, boardAway);
                awayTimer = s.Elapsed - e0;
                awayReal = Time.realtimeSinceStartup - t0;
                root.SendMessage("OnApplicationFocus", true);
                CheckRest("board", boardHere, boardAway);
            }
            else
            {
                yield return Measure(3f, boardHere);
                Debug.Log("[AutoPilot] focus: ready, waiting for the game to lose focus");
                float t = 0;
                while (GameRoot.Attended && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
                if (GameRoot.Attended) { Fail("the game never lost focus"); Finish(); yield break; }
                float e0 = s.Elapsed, t0 = Time.realtimeSinceStartup;
                int f0 = Time.frameCount;
                t = 0;
                while (!GameRoot.Attended && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
                awayTimer = s.Elapsed - e0;
                awayReal = Time.realtimeSinceStartup - t0;
                // In a background browser tab the page may draw nothing at all, which is fine too.
                boardAway.Fps = (Time.frameCount - f0) / Mathf.Max(0.01f, awayReal);
                if (!GameRoot.Attended) Fail("the game never got focus back");
                Debug.Log($"[AutoPilot] focus: board attended {boardHere}; away {boardAway.Fps:0.0} fps");
                if (boardAway.Fps > GameRoot.AwayFrameRate + 2) Fail($"still drawing {boardAway.Fps:0.0} fps while away");
            }
            float e1 = s.Elapsed;
            var back = new Sample();
            yield return Measure(2f, back);
            float backTimer = s.Elapsed - e1;
            Debug.Log($"[AutoPilot] focus: back {back}");
            if (back.Fps < boardHere.Fps * 0.6f) Fail($"only {back.Fps:0.0} fps after focus came back, against {boardHere.Fps:0.0} before");
            yield return Shot("focus_back");
            Debug.Log($"[AutoPilot] focus: away {awayReal:0.0}s, the case timer moved {awayTimer:0.00}s; back 2s, it moved {backTimer:0.00}s");
            if (awayReal < 2f) Fail($"away for only {awayReal:0.0}s");
            if (awayTimer > 0.15f) Fail($"the case timer ran {awayTimer:0.00}s while the game was away");
            if (backTimer < 1.5f) Fail($"the case timer only ran {backTimer:0.00}s in 2s back in focus");
            Finish();

            void Finish()
            {
                if (errors > 0) ok = false;
                Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} focus test ({(real ? "a real focus change" : "the focus handler")})");
                Debug.Log($"[AutoPilot] done: focus test, {errors} errors");
                Application.Quit(ok ? 0 : 1);
            }
        }

        public void RunMidnight(string outDir)
        {
            dir = outDir;
            capture = Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(MidnightTest());
        }

        /// <summary>
        /// -alibiMidnightTest: leave today's docket in progress, open the docket drawer and wait for
        /// midnight (start the clock near it with -alibiClockAt). The drawer must redraw on its own
        /// for the new day, and yesterday's docket must still continue.
        /// </summary>
        IEnumerator MidnightTest()
        {
            var root = GameRoot.I;
            bool ok = true;
            void Fail(string why) { Debug.LogError("[AutoPilot] FAIL midnight: " + why); ok = false; }
            string Iso(System.DateTime d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
            var day0 = Cases.Today;
            var day1 = day0.AddDays(1);
            var toMidnight = day1 - Cases.Now;
            Debug.Log($"[Midnight] clock {Cases.Now:yyyy-MM-dd HH:mm:ss} (shift {Cases.ClockShift.TotalSeconds:0}s), {toMidnight.TotalSeconds:0}s to midnight");
            if (toMidnight.TotalMinutes > 20) { Fail($"midnight is {toMidnight.TotalMinutes:0} minutes away; start the clock nearer it (-alibiClockAt)"); goto end; }

            // A docket for today, two cards pinned, left in progress.
            var dk = Cases.TodaysDocket;
            root.StartCase(dk, false);
            yield return Wait(2.6f);
            var s = root.Session;
            foreach (var id in s.Board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).Take(2).ToList())
            {
                s.AutoPin(id);
                yield return Wait(0.4f);
            }
            yield return Wait(1f);
            int pinned = s.Board.Pinned.Count;
            root.ShowSelect();
            yield return Wait(1.2f);
            if (SaveData.Current.inProgress?.caseId != dk.Id) Fail($"{dk.Id} wasn't left in progress");
            root.Screens.Press("btn_docket");
            yield return Wait(1f);
            string before = root.Screens.DocketRowText(day0) ?? "";
            Debug.Log($"[Midnight] before: today {Iso(day0)} row: {before}");
            if (!before.Contains("TODAY") || !before.Contains("IN PROGRESS")) Fail("before midnight, today's row isn't TODAY and IN PROGRESS: " + before);
            if (root.Screens.DocketRowText(day0.AddDays(-6)) == null) Fail("before midnight, the week's oldest day isn't in the drawer");
            yield return Shot("drawer_before_midnight");

            // Wait for the date to change, then for the drawer to notice on its own.
            while (Cases.Now.Date == day0) yield return null;
            float t = 0;
            while (Cases.Today != day1 && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.6f);
            Debug.Log($"[Midnight] clock {Cases.Now:yyyy-MM-dd HH:mm:ss}: Today={Iso(Cases.Today)}, redrawn {t:0.0}s after midnight, drawer open={root.Screens.DocketWeekOpen}");
            if (Cases.Today != day1) Fail("the case files didn't move on to the new day");
            if (!root.Screens.DocketWeekOpen) Fail("the drawer closed at midnight");
            string newRow = root.Screens.DocketRowText(day1) ?? "", oldRow = root.Screens.DocketRowText(day0) ?? "";
            Debug.Log($"[Midnight] after: {Iso(day1)} row: {newRow}");
            Debug.Log($"[Midnight] after: {Iso(day0)} row: {oldRow}");
            if (!newRow.Contains("TODAY") || newRow.Contains("YESTERDAY")) Fail("the new day's row isn't TODAY: " + newRow);
            if (!oldRow.Contains("YESTERDAY") || !oldRow.Contains("IN PROGRESS")) Fail("the old day's row isn't YESTERDAY and IN PROGRESS: " + oldRow);
            if (root.Screens.DocketRowText(day0.AddDays(-6)) != null) Fail("the day that's now eight days old is still in the drawer");
            if (root.Screens.DocketRowText(day1.AddDays(-6)) == null) Fail("the week's oldest day is missing after midnight");
            yield return Shot("drawer_after_midnight");

            // Yesterday's docket still continues, with its pins.
            root.Screens.CloseTopOverlay();
            yield return Wait(0.6f);
            yield return Shot("case_files_after_midnight");
            root.ShowTitle(false);
            yield return Wait(1.5f);
            if (!root.Screens.Press("btn_Continue")) Fail("no Continue button on the title");
            yield return Wait(2.6f);
            s = root.Session;
            if (s == null || s.Case.Id != dk.Id || s.Board.Pinned.Count != pinned)
                Fail($"Continue didn't resume {dk.Id} with {pinned} pins ({(s != null ? s.Case.Id + ", " + s.Board.Pinned.Count + " pins" : "no session")})");
            else Debug.Log($"[Midnight] Continue resumed {dk.Id} with {pinned} pins");
            yield return Shot("continued_after_midnight");
        end:
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} midnight test ({Iso(day0)} -> {Iso(day1)})");
            Debug.Log($"[AutoPilot] done: midnight test, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
        }

        // ------------------------------------------------------------------ gamepad test

        Gamepad pad;

        void PadState(Vector2 stick, params GamepadButton[] held) => PadState(stick, Vector2.zero, held);

        void PadState(Vector2 stick, Vector2 right, params GamepadButton[] held)
        {
            var st = new GamepadState { leftStick = stick, rightStick = right };
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

        IEnumerator PadClick(Vector2 at)
        {
            yield return PadMoveTo(at);
            yield return PadTap(GamepadButton.South);
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
            // -alibiPadLayout ps | nintendo: a PlayStation pad (by its Input System layout) or a Switch Pro
            // controller (by its name only, as a browser reports one); otherwise a plain (Xbox-named) pad.
            var cmd = System.Environment.GetCommandLineArgs();
            int layoutArg = System.Array.IndexOf(cmd, "-alibiPadLayout");
            string layout = layoutArg >= 0 && layoutArg + 1 < cmd.Length ? cmd[layoutArg + 1] : "xbox";
            pad = layout == "ps" ? InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualShockGamepad>("TestPad")
                : layout == "nintendo" ? (Gamepad)InputSystem.AddDevice(new UnityEngine.InputSystem.Layouts.InputDeviceDescription { deviceClass = "Gamepad", manufacturer = "Nintendo Co., Ltd.", product = "Pro Controller" })
                : InputSystem.AddDevice<Gamepad>("TestPad");
            pad.MakeCurrent();
            var family = layout == "ps" ? PadFamily.PlayStation : layout == "nintendo" ? PadFamily.Nintendo : PadFamily.Xbox;
            // The labels each prompt must show for this pad, written out by hand (not through PadLabels).
            string south = family == PadFamily.PlayStation ? "✕" : family == PadFamily.Nintendo ? "B" : "A";
            string east = family == PadFamily.PlayStation ? "○" : family == PadFamily.Nintendo ? "A" : "B";
            string west = family == PadFamily.PlayStation ? "□" : family == PadFamily.Nintendo ? "Y" : "X";
            string north = family == PadFamily.PlayStation ? "△" : family == PadFamily.Nintendo ? "X" : "Y";
            string bumpers = family == PadFamily.PlayStation ? "[L1]  [R1]" : family == PadFamily.Nintendo ? "[L]  [R]" : "[LB]  [RB]";
            string start = family == PadFamily.PlayStation ? "Options" : family == PadFamily.Nintendo ? "+" : "Start";
            void Labels(string where, string text, params string[] want)
            {
                var missing = want.Where(w => !text.Contains(w)).ToList();
                if (missing.Count > 0) Fail($"{where} doesn't name the {family} buttons: missing {string.Join(", ", missing)} in \"{text.Replace("\n", " / ")}\"");
                else Debug.Log($"[AutoPilot] pad labels ({family}) {where}: " + string.Join(" ", want));
                if (family == PadFamily.PlayStation && PadLabels.HasXboxTokens(text)) Fail($"{where} still shows Xbox button names: \"{text.Replace("\n", " / ")}\"");
            }
            PadCursor.IgnoreRealMouse = true;   // the desktop is shared; only step 8 hands over on purpose
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
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
            for (float w = 0; w < 3f && !root.Screens.HelpShown.Contains("jump between cards"); w += Clock.Dt) yield return null;
            yield return Shot("pad_jump_prompt");
            if (PadCursor.PadFamily != family) Fail($"the pad reads as {PadCursor.PadFamily}, not {family}");
            Labels("the controls strip", root.Screens.HelpShown, $"[{north}]</b> notebook", $"[{west}]</b> hint", $"[{east}]</b> sends", $"[{start}]</b> menu",
                $"<b>[{south}]</b> on a card pins it", "<b>" + bumpers.Replace("  ", " ") + "</b> jump between cards");

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
            Labels("the pause menu's controls", root.Screens.ControlsShown, bumpers, $"[{south}] on a card", $"Hold [{south}] and steer", $"[{east}]</b>", $"[{north}]  ·  [{west}]", $"[{start}]");
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

            // 6b. With a page of replies in it, Y opens the notebook: the right stick scrolls down to
            //     the oldest notes, the D-pad brings them back up, and B closes it.
            IEnumerator PadHold(Vector2 right, GamepadButton[] held, float seconds)
            {
                for (float t = 0; t < seconds; t += Clock.Dt) { PadState(Vector2.zero, right, held); yield return null; }
                PadState(Vector2.zero);
                yield return Wait(0.2f);
            }
            yield return PadTap(GamepadButton.North);
            yield return Wait(0.6f);
            Labels("the notebook's footer", root.Screens.OpenNotebook != null ? root.Screens.OpenNotebook.FootText : "", $"<b>[{north}]</b> or <b>[{east}]</b> to close");
            if (family != PadFamily.Xbox) yield return Shot("pad_notebook_footer");
            yield return CheckNotebookScroll("pad", PadHold(new Vector2(0, -1), new GamepadButton[0], 2f), PadHold(Vector2.zero, new[] { GamepadButton.DpadUp }, 2f), Fail);
            yield return PadTap(GamepadButton.East);
            yield return Wait(0.4f);
            if (root.Screens.NotebookOpen) Fail("B didn't close the notebook after scrolling");

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

            // 8. RB/LB reach the closed panel's buttons; then on to the docket drawer, with A and B.
            yield return PadTap(GamepadButton.RightShoulder);
            if (!PadCursor.Targets().Any(p => (p - PadCursor.I.Position).magnitude < 2f)) Fail("RB on the closed panel didn't land on a button");
            yield return DrawerByHand("pad", p => PadClick(p), () => PadTap(GamepadButton.East), Fail);

            // 9. Moving the real mouse hands control back.
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
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} pad test (stick, RB jump, A-drag, A pin, B send back, Y notebook, B close, X hint, Start pause, A confront, incident drag, the docket drawer, mouse takes over)");
            Debug.Log($"[AutoPilot] done: pad test, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
        }

        // ------------------------------------------------------------------ touch test

        Touchscreen touchscreen;
        int touchId;

        void Finger(Vector2 at, UnityEngine.InputSystem.TouchPhase phase) =>
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = touchId, phase = phase, position = at, pressure = 1 });

        IEnumerator TouchTap(Vector2 at, float hold = 0.1f)
        {
            touchId++;
            Finger(at, UnityEngine.InputSystem.TouchPhase.Began);
            yield return null;
            yield return Wait(hold);
            Finger(at, UnityEngine.InputSystem.TouchPhase.Ended);
            yield return null;
            yield return Wait(0.35f);
        }

        IEnumerator TouchDrag(Vector2 from, Vector2 to)
        {
            touchId++;
            Finger(from, UnityEngine.InputSystem.TouchPhase.Began);
            yield return Wait(0.1f);
            for (int i = 1; i <= 20; i++)
            {
                Finger(Vector2.Lerp(from, to, Easing.Apply(Ease.InOutSine, i / 20f)), UnityEngine.InputSystem.TouchPhase.Moved);
                yield return null;
            }
            yield return Wait(0.12f);
            Finger(to, UnityEngine.InputSystem.TouchPhase.Ended);
            yield return null;
            yield return Wait(0.5f);
        }

        public void RunTouch(string outDir, bool real)
        {
            dir = outDir;
            capture = Application.platform != RuntimePlatform.WebGLPlayer;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(real ? TouchReal() : TouchTest());
        }

        /// <summary>
        /// -alibiTouchTest: case 1 from the dealt tray to CASE CLOSED with nothing but a (simulated)
        /// touchscreen: taps, finger drags, a press held to read a chip, the card panel, the HUD's
        /// buttons and the incident drag. Each HUD button must act exactly once per tap.
        /// </summary>
        IEnumerator TouchTest()
        {
            var root = GameRoot.I;
            bool ok = true;
            void Fail(string why) { Debug.LogError("[AutoPilot] FAIL touch: " + why); ok = false; }
            touchscreen = InputSystem.AddDevice<Touchscreen>("TestTouch");
            PadCursor.IgnoreRealMouse = true;   // the desktop is shared; only the last step hands over on purpose
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
            var c = Cases.All[0];
            root.StartCase(c, false);
            yield return Wait(3f);
            var s = root.Session;
            var middle = Screen(Stage.I.BoardToWorld(Vector2.zero));
            Vector2 Button(string name) => root.Screens.ButtonScreen(name) ?? new Vector2(-1, -1);

            // 1. A tap on a tray card pins it, and the touch pointer takes over (no cursor drawn).
            var firstId = s.Board.TrayCards.First().Id;
            yield return TouchTap(TrayPoint(s.ViewOf(firstId)));
            yield return Wait(0.4f);
            if (PadCursor.Using != PadCursor.Pointer.Touch) Fail("a touch didn't hand the pointer to touch");
            if (!s.Board.Pinned.Contains(firstId)) Fail($"a tap didn't pin {firstId}");
            yield return Shot("touch_tap_prompt");

            // 2. A finger drag onto the board pins a card.
            var dragId = s.Board.TrayCards.First().Id;
            yield return TouchDrag(TrayPoint(s.ViewOf(dragId)), middle);
            yield return Wait(0.5f);
            if (!s.Board.Pinned.Contains(dragId)) Fail($"a finger drag didn't pin {dragId}");

            // 3. Taps pin the rest.
            foreach (var id in s.Board.TrayCards.Select(x => x.Id).ToList())
            {
                yield return TouchTap(TrayPoint(s.ViewOf(id)));
                yield return Wait(0.4f);
                if (!s.Board.Pinned.Contains(id)) Fail($"a tap didn't pin {id}");
            }
            yield return Wait(1f);

            // 4. Press and hold a chip: its full card shows; letting go doesn't open its panel.
            var read = s.ViewOf("b_receipt");
            touchId++;
            Finger(Screen(read.transform.position), UnityEngine.InputSystem.TouchPhase.Began);
            yield return Wait(1f);
            string inspecting = s.InspectingId;
            yield return Shot("touch_hold_to_read");
            Finger(Screen(read.transform.position), UnityEngine.InputSystem.TouchPhase.Ended);
            yield return Wait(0.6f);
            if (inspecting != "b_receipt") Fail($"holding a finger on b_receipt showed {inspecting ?? "nothing"}");
            if (s.Selected != null || root.Screens.ConfrontButtonScreen() != null) Fail("letting go after a hold opened the card's panel");
            if (s.InspectingId != null) Fail($"the full card ({s.InspectingId}) stayed up after the finger lifted");

            // 5. A tap on a pinned card opens its panel; Back to the tray sends it back; a tap pins it again.
            var back = s.ViewOf("a_tab");
            yield return TouchTap(Screen(back.transform.position));
            yield return Wait(0.4f);
            var backBtn = Button("btn_Back to the tray");
            if (backBtn.x < 0) Fail("no Back to the tray button after tapping a_tab");
            else
            {
                yield return Shot("touch_panel");
                yield return TouchTap(backBtn);
                yield return Wait(0.6f);
                if (s.Board.Pinned.Contains("a_tab")) Fail("Back to the tray didn't send a_tab back");
                yield return TouchTap(TrayPoint(back));
                yield return Wait(0.6f);
                if (!s.Board.Pinned.Contains("a_tab")) Fail("a tap didn't re-pin a_tab");
            }

            // 6. The HUD's buttons, once per tap: Notes opens (and stays open), the shade closes it,
            //    Hint posts one memo, Menu pauses and Resume resumes.
            yield return TouchTap(Button("btn_Notes"));
            yield return Wait(0.6f);
            if (!root.Screens.NotebookOpen) Fail("a tap on Notes didn't leave the notebook open");
            yield return TouchTap(new Vector2(UnityEngine.Screen.width * 0.03f, UnityEngine.Screen.height * 0.5f));
            yield return Wait(0.5f);
            if (root.Screens.NotebookOpen) Fail("a tap on the shade didn't close the notebook");
            int memos = s.Memos.History.Count;
            yield return TouchTap(Button("btn_Hint"));
            yield return Wait(0.5f);
            if (s.Memos.History.Count != memos + 1) Fail($"a tap on Hint posted {s.Memos.History.Count - memos} memos");
            yield return TouchTap(Button("btn_Menu"));
            yield return Wait(0.8f);
            if (!GameRoot.Paused) Fail("a tap on Menu didn't pause");
            yield return Shot("touch_pause_controls");
            yield return TouchTap(Button("btn_Resume"));
            yield return Wait(0.6f);
            if (GameRoot.Paused) Fail("a tap on Resume didn't resume");

            // 7. Confront each liar: tap the chip, tap Confront; tap the new cards in.
            foreach (var id in new[] { "a_claim", "c_claim", "b_claim" })
            {
                var at = Screen(s.ViewOf(id).transform.position);
                yield return TouchTap(at);
                yield return Wait(0.5f);
                var btn = root.Screens.ConfrontButtonScreen();
                if (btn == null)
                {
                    Fail($"no Confront button for {id} (tapped {at}, selected {(s.Selected ? s.Selected.Id : "none")}, pointer {PadCursor.Using}, frame time {Clock.Dt:0.000}s)");
                    yield return Shot("touch_no_confront_" + id);
                    continue;
                }
                yield return TouchTap(btn.Value);
                yield return Wait(2.6f);
                if (!s.Board.Struck.Contains(id)) Fail($"confronting {id} didn't strike it");
                foreach (var nid in s.Board.TrayCards.Select(x => x.Id).ToList())
                {
                    yield return TouchTap(TrayPoint(s.ViewOf(nid)));
                    yield return Wait(0.5f);
                }
            }
            yield return Wait(1f);

            // 7b. With a page of replies in it, a tap on Notes opens the notebook: a finger drags the
            //     notes up to read older ones and back down, and a tap outside the page closes it.
            yield return TouchTap(Button("btn_Notes"));
            yield return Wait(0.6f);
            if (!root.Screens.NotebookOpen) Fail("a tap on Notes didn't open the notebook");
            else
            {
                var r = root.Screens.OpenNotebook.LogScreenRect;
                IEnumerator Swipes(float dir)
                {
                    for (int i = 0; i < 3; i++) yield return TouchDrag(new Vector2(r.center.x, r.center.y - dir * r.height * 0.4f), new Vector2(r.center.x, r.center.y + dir * r.height * 0.4f));
                }
                yield return CheckNotebookScroll("touch", Swipes(1), Swipes(-1), Fail);
                if (!root.Screens.NotebookOpen) Fail("dragging the notes closed the notebook");
                yield return TouchTap(new Vector2(UnityEngine.Screen.width * 0.03f, UnityEngine.Screen.height * 0.5f));
                yield return Wait(0.5f);
                if (root.Screens.NotebookOpen) Fail("a tap on the shade didn't close the notebook after scrolling");
            }

            // 8. Drag the incident into the culprit's line.
            var lane = s.View.LaneById[c.Incident.Culprit];
            var fit = s.Board.Fits[c.Incident.Culprit];
            var slot = Screen(Stage.I.BoardToWorld(new Vector2(s.View.TimeToX(fit.EarliestStart), lane.Track + 0.5f)));
            yield return TouchDrag(Screen(s.IncidentView.transform.position), slot);
            float t = 0;
            while (root.Flow != Flow.Closed && t < 60) { t += Clock.Dt; yield return null; }
            yield return Wait(2f);
            yield return Shot("touch_closed");
            if (root.Flow != Flow.Closed) Fail("the incident drag didn't close the case");
            if (s.Board.Mistakes != 0) Fail($"{s.Board.Mistakes} badges lost");

            // 9. Moving the real mouse hands control back.
            PadCursor.IgnoreRealMouse = false;
            yield return Wait(0.8f);
            var real = InputSystem.devices.OfType<Mouse>().FirstOrDefault(m => m.name != "PadCursor");
            if (real != null && PadCursor.Active)
            {
                InputSystem.QueueStateEvent(real, new MouseState { position = new Vector2(200, 200), delta = new Vector2(40, 0) });
                yield return null;
                yield return null;
                if (PadCursor.Active) Fail("moving the mouse didn't hand control back");
            }
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} touch test (tap pin, finger drag, hold to read, panel, Back to the tray, Notes, Hint, Menu, Resume, confront, incident drag, mouse takes over)");
            Debug.Log($"[AutoPilot] done: touch test, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
        }

        /// <summary>
        /// -alibiTouchTest -alibiTouchReal (the browser's ?touchreal): asks for real touches from
        /// outside (Tools/webtest.mjs sends them through the browser) and checks what they did.
        /// Each request is one log line, "touch: waiting for a tap|hold|drag at x,y [to x,y] of WxH".
        /// </summary>
        IEnumerator TouchReal()
        {
            var root = GameRoot.I;
            bool ok = true;
            void Fail(string why) { Debug.LogError("[AutoPilot] FAIL touch (real): " + why); ok = false; }
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
            root.StartCase(Cases.All[0], false);
            yield return Wait(3f);
            var s = root.Session;
            string Px(Vector2 p) => $"{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.y)}";
            string Of() => $"of {UnityEngine.Screen.width}x{UnityEngine.Screen.height}";
            IEnumerator Until(System.Func<bool> done, float limit = 30f)
            {
                for (float t = 0; t < limit && !done(); t += Time.unscaledDeltaTime) yield return null;
            }

            // A tap pins a tray card.
            var a = s.Board.TrayCards.First().Id;
            Debug.Log($"[AutoPilot] touch: waiting for a tap at {Px(TrayPoint(s.ViewOf(a)))} {Of()}");
            yield return Until(() => s.Board.Pinned.Contains(a));
            if (!s.Board.Pinned.Contains(a)) Fail($"a real tap didn't pin {a}");
            else Debug.Log($"[AutoPilot] touch: a real tap pinned {a} (pointer {PadCursor.Using})");
            yield return Wait(1f);

            // A finger drag pins another.
            var b = s.Board.TrayCards.First().Id;
            Debug.Log($"[AutoPilot] touch: waiting for a drag at {Px(TrayPoint(s.ViewOf(b)))} to {Px(Screen(Stage.I.BoardToWorld(Vector2.zero)))} {Of()}");
            yield return Until(() => s.Board.Pinned.Contains(b));
            if (!s.Board.Pinned.Contains(b)) Fail($"a real finger drag didn't pin {b}");
            else Debug.Log($"[AutoPilot] touch: a real finger drag pinned {b}");
            yield return Wait(1f);

            // A press held still on a chip reads it, and letting go doesn't open its panel.
            var chip = s.ViewOf(a);
            string seen = null;
            Debug.Log($"[AutoPilot] touch: waiting for a hold at {Px(Screen(chip.transform.position))} {Of()}");
            for (float t = 0; t < 30f && seen == null; t += Time.unscaledDeltaTime) { seen = s.InspectingId; yield return null; }
            yield return Wait(2.5f);
            if (seen != a) Fail($"a real held finger on {a} showed {seen ?? "nothing"}");
            else if (s.Selected != null) Fail("letting go after a real hold opened the card's panel");
            else Debug.Log($"[AutoPilot] touch: a real held finger read {a} without opening it");

            // A tap on Notes opens the notebook once (a second press would close it again on the shade).
            var notes = root.Screens.ButtonScreen("btn_Notes");
            if (notes == null) Fail("no Notes button");
            else
            {
                Debug.Log($"[AutoPilot] touch: waiting for a tap at {Px(notes.Value)} {Of()}");
                yield return Until(() => root.Screens.NotebookOpen);
                yield return Wait(1.5f);
                if (!root.Screens.NotebookOpen) Fail("a real tap on Notes didn't leave the notebook open");
                else Debug.Log("[AutoPilot] touch: a real tap on Notes opened the notebook, once");
            }
            root.Screens.CloseTopOverlay();
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} touch test (real touches: tap, drag, hold, a HUD button)");
            Debug.Log($"[AutoPilot] done: touch test, {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(ok ? 0 : 1);
        }

        // ------------------------------------------------------------------ keyboard test

        Keyboard keyboard;

        void Keys(params Key[] held) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));

        /// <summary>Steer the cursor onto a screen point with the arrow keys, letting go near the end so it slows down.</summary>
        IEnumerator KeyMoveTo(Vector2 target, bool enter = false)
        {
            bool eased = false;
            var held = new System.Collections.Generic.List<Key>();
            for (int i = 0; i < 1500; i++)
            {
                var d = target - PadCursor.I.Position;
                // One frame of the slowest speed, so a low frame rate can't make the target unreachable.
                float tol = Mathf.Max(4f, UnityEngine.Screen.height * 0.2f * Clock.Dt * 1.2f);
                if (d.magnitude < tol) break;
                held.Clear();
                if (enter) held.Add(Key.Enter);
                if (!eased && d.magnitude < UnityEngine.Screen.height * 0.08f) { eased = true; Keys(held.ToArray()); yield return null; continue; }
                if (d.x > tol * 0.5f) held.Add(Key.RightArrow); else if (d.x < -tol * 0.5f) held.Add(Key.LeftArrow);
                if (d.y > tol * 0.5f) held.Add(Key.UpArrow); else if (d.y < -tol * 0.5f) held.Add(Key.DownArrow);
                Keys(held.ToArray());
                yield return null;
            }
            if (enter) Keys(Key.Enter); else Keys();
            yield return null;
            yield return null;
        }

        IEnumerator KeyTap(Key k)
        {
            Keys(k);
            yield return null;
            yield return null;
            Keys();
            yield return Wait(0.25f);
        }

        IEnumerator KeyClick(Vector2 at)
        {
            yield return KeyMoveTo(at);
            yield return KeyTap(Key.Enter);
        }

        IEnumerator KeyDrag(Vector2 from, Vector2 to)
        {
            yield return KeyMoveTo(from);
            Keys(Key.Enter);
            yield return Wait(0.15f);
            yield return KeyMoveTo(to, true);
            yield return Wait(0.2f);
            Keys();
            yield return Wait(0.4f);
        }

        /// <summary>
        /// -alibiKeysTest: case 1 from the dealt tray to CASE CLOSED with nothing but (simulated) key
        /// presses: arrows, Q/E, Enter, Backspace, Tab, H and Esc. Then on to the docket drawer.
        /// </summary>
        IEnumerator KeysTest()
        {
            var root = GameRoot.I;
            bool ok = true;
            void Fail(string why) { Debug.LogError("[AutoPilot] FAIL keys: " + why); ok = false; }
            keyboard = InputSystem.AddDevice<Keyboard>("TestKeys");
            keyboard.MakeCurrent();
            PadCursor.IgnoreRealMouse = true;   // the desktop is shared; only the last step hands over on purpose
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
            var c = Cases.All[0];
            root.StartCase(c, false);
            yield return Wait(3f);
            var s = root.Session;

            // 1. A key hands the keyboard the pointer; E jumps onto a card.
            yield return KeyTap(Key.E);
            if (PadCursor.Using != PadCursor.Pointer.Keys) Fail("pressing E didn't hand the keyboard the cursor");
            if (!s.CursorTargets().Any(t => (t - PadCursor.I.Position).magnitude < 40f)) Fail("E didn't land on a card");
            yield return KeyTap(Key.E);
            var afterE = PadCursor.I.Position;
            yield return KeyTap(Key.Q);
            if ((PadCursor.I.Position - afterE).magnitude < 2f) Fail("Q didn't jump back");
            yield return Wait(0.6f);
            yield return Shot("keys_jump_prompt");

            // 2. Hold Enter and steer with the arrows: drag the first tray card onto the board.
            var firstId = s.Board.TrayCards.First().Id;
            yield return KeyDrag(TrayPoint(s.ViewOf(firstId)), Screen(Stage.I.BoardToWorld(Vector2.zero)));
            yield return Wait(0.6f);
            if (!s.Board.Pinned.Contains(firstId)) Fail($"an Enter-held drag didn't pin {firstId}");

            // 3. Enter on each remaining tray card pins it.
            foreach (var id in s.Board.TrayCards.Select(x => x.Id).ToList())
            {
                yield return KeyClick(TrayPoint(s.ViewOf(id)));
                yield return Wait(0.5f);
                if (!s.Board.Pinned.Contains(id)) Fail($"Enter didn't pin {id}");
            }
            yield return Wait(1f);

            // 4. Backspace on a pinned card sends it back; Enter in the tray pins it again.
            var back = s.ViewOf("a_tab");
            yield return KeyMoveTo(Screen(back.transform.position));
            yield return Wait(0.3f);
            yield return KeyTap(Key.Backspace);
            yield return Wait(0.7f);
            if (s.Board.Pinned.Contains("a_tab")) Fail("Backspace didn't send the card back");
            yield return KeyClick(TrayPoint(back));
            yield return Wait(0.7f);
            if (!s.Board.Pinned.Contains("a_tab")) Fail("Enter didn't re-pin the card");

            // 5. Tab opens the notebook, Backspace closes it; H asks for a hint; Esc pauses, E reaches a button, Esc resumes.
            yield return KeyTap(Key.Tab);
            yield return Wait(0.6f);
            if (!root.Screens.NotebookOpen) Fail("Tab didn't open the notebook");
            yield return KeyTap(Key.Backspace);
            yield return Wait(0.4f);
            if (root.Screens.NotebookOpen) Fail("Backspace didn't close the notebook");
            int memos = s.Memos.History.Count;
            yield return KeyTap(Key.H);
            yield return Wait(0.4f);
            if (s.Memos.History.Count <= memos) Fail("H didn't ask for a hint");
            yield return KeyTap(Key.Escape);
            yield return Wait(0.8f);
            if (!GameRoot.Paused) Fail("Esc didn't pause");
            yield return KeyTap(Key.E);
            var menuTargets = PadCursor.Targets();
            if (menuTargets.Count < 5 || !menuTargets.Any(t => (t - PadCursor.I.Position).magnitude < 2f)) Fail($"E in the pause menu didn't land on a button ({menuTargets.Count} targets)");
            yield return Shot("keys_pause_controls");
            yield return KeyTap(Key.Escape);
            yield return Wait(0.5f);
            if (GameRoot.Paused) Fail("Esc didn't resume");

            // 6. Confront each liar: Enter on the chip, then Enter on Confront.
            foreach (var id in new[] { "a_claim", "c_claim", "b_claim" })
            {
                yield return KeyClick(Screen(s.ViewOf(id).transform.position));
                yield return Wait(0.5f);
                var btn = root.Screens.ConfrontButtonScreen();
                if (btn == null) { Fail($"no Confront button for {id}"); continue; }
                if (id == "a_claim") yield return Shot("keys_actions");
                yield return KeyClick(btn.Value);
                yield return Wait(2.6f);
                if (!s.Board.Struck.Contains(id)) Fail($"confronting {id} didn't strike it");
                foreach (var nid in s.Board.TrayCards.Select(x => x.Id).ToList())
                {
                    yield return KeyClick(TrayPoint(s.ViewOf(nid)));
                    yield return Wait(0.5f);
                }
            }
            yield return Wait(1f);

            // 6b. With a page of replies in it, Tab opens the notebook: Down scrolls to the oldest
            //     notes, Page Up pages back to the newest, and Tab closes it.
            IEnumerator KeyHold(Key k, float seconds)
            {
                for (float t = 0; t < seconds; t += Clock.Dt) { Keys(k); yield return null; }
                Keys();
                yield return Wait(0.2f);
            }
            IEnumerator PageUps()
            {
                for (int i = 0; i < 6; i++) yield return KeyTap(Key.PageUp);
            }
            yield return KeyTap(Key.Tab);
            yield return Wait(0.6f);
            yield return CheckNotebookScroll("keys", KeyHold(Key.DownArrow, 2f), PageUps(), Fail);
            yield return KeyTap(Key.Tab);
            yield return Wait(0.4f);
            if (root.Screens.NotebookOpen) Fail("Tab didn't close the notebook after scrolling");

            // 7. Hold Enter on the incident card and steer it into the culprit's line.
            var lane = s.View.LaneById[c.Incident.Culprit];
            var fit = s.Board.Fits[c.Incident.Culprit];
            var slot = Screen(Stage.I.BoardToWorld(new Vector2(s.View.TimeToX(fit.EarliestStart), lane.Track + 0.5f)));
            yield return KeyDrag(Screen(s.IncidentView.transform.position), slot);
            float t = 0;
            while (root.Flow != Flow.Closed && t < 60) { t += Clock.Dt; yield return null; }
            yield return Wait(2f);
            yield return Shot("keys_closed");
            if (root.Flow != Flow.Closed) Fail("the Enter-held incident drag didn't close the case");

            // 8. On to the docket drawer, with Enter and Backspace.
            yield return DrawerByHand("keys", p => KeyClick(p), () => KeyTap(Key.Backspace), Fail);

            // 9. Moving the real mouse hands control back.
            PadCursor.IgnoreRealMouse = false;
            var real = InputSystem.devices.OfType<Mouse>().FirstOrDefault(m => m.name != "PadCursor");
            if (real != null && PadCursor.Active)
            {
                InputSystem.QueueStateEvent(real, new MouseState { position = new Vector2(200, 200), delta = new Vector2(40, 0) });
                yield return null;
                yield return null;
                if (PadCursor.Active) Fail("moving the mouse didn't hand control back");
            }
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} keys test (arrows, Q/E jumps, Enter-held drag, Enter pin, Backspace send back, Tab notebook, Backspace close, H hint, Esc pause, Enter confront, incident drag, the docket drawer, mouse takes over)");
            Debug.Log($"[AutoPilot] done: keys test, {errors} errors");
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

        /// <summary>
        /// A drag that ends on another card and lets go on arrival, like a player dropping a card
        /// anywhere on its lane: a quarter of a second in real time, so a slow frame rate doesn't
        /// turn it into a long hover.
        /// </summary>
        IEnumerator QuickDrop(Vector2 from, Vector2 to)
        {
            MouseTo(from);
            yield return Wait(0.15f);
            MouseTo(from, true);
            yield return null;
            int frames = 0;
            for (float t = 0; t < 0.25f; t += Clock.Dt, frames++)
            {
                MouseTo(Vector2.Lerp(from, to, t / 0.25f), true);
                yield return null;
            }
            MouseTo(to, false);
            Debug.Log($"[AutoPilot] links: quick drop took {frames + 1} frames");
            yield return Wait(0.8f);
        }

        /// <summary>
        /// Case 2 with a moving cursor: a card dropped in one movement onto a pinned chip of another
        /// moment pins to its own lane, a card tossed onto another tray card goes back to the tray,
        /// and neither costs a badge. A card held over its twin until the LINK tag shows links them
        /// and corrects the clock.
        /// </summary>
        IEnumerator LinkByHand(System.Action<string> fail)
        {
            var root = GameRoot.I;
            root.StartCase(Cases.All[1], false);
            yield return Wait(3f);
            var s = root.Session;
            var board = s.Board;
            // Play the case in code up to its first link (the pair only turns up partway through).
            Move link = null;
            for (int guard = 0; guard < 12 && link == null; guard++)
            {
                foreach (var id in board.TrayCards.Where(x => !x.IsUnknown).Select(x => x.Id).ToList()) s.AutoPin(id);
                yield return Wait(1f);
                var path = Solver.ShortestSolution(Solver.Shadow(board));
                if (path == null || path.Count == 0) break;
                if (path[0].Kind == "link") { link = path[0]; break; }
                s.Confront(s.ViewOf(path[0].A));
                yield return Wait(3f);
            }
            if (link == null) { fail("links: case 2's solution never reached a link"); yield break; }
            var ca = s.Case.CardById[link.A];
            var cb = s.Case.CardById[link.B];
            var trusted = board.IsTrusted(ca.Clock) ? ca : cb;
            var twin = trusted == ca ? cb : ca;
            if (!board.Pinned.Contains(trusted.Id)) s.AutoPin(trusted.Id);
            // Back to the tray: the wrong clock's card, and two cards of other moments.
            var others = board.Pinned.Select(id => s.Case.CardById[id])
                .Where(x => x.Id != trusted.Id && x.Id != twin.Id && !x.IsUnknown && !board.Struck.Contains(x.Id) && x.Event != trusted.Event)
                .Take(2).ToList();
            if (others.Count < 2) { fail("links: not enough other cards on case 2's board"); yield break; }
            foreach (var x in new[] { twin }.Concat(others)) if (board.Pinned.Contains(x.Id)) s.Unpin(s.ViewOf(x.Id));
            yield return Wait(1.5f);
            Debug.Log($"[AutoPilot] links: case 2 at its link ({twin.Id} onto {trusted.Id}); {others[0].Id} and {others[1].Id} back in the tray, {board.Mistakes} badges lost so far");
            int lost = board.Mistakes;

            // Toss one tray card onto another on the desk: it stays in the tray.
            int memos = s.Memos.History.Count;
            yield return QuickDrop(Grab(s.ViewOf(others[0].Id)), Grab(s.ViewOf(others[1].Id)));
            bool wrongLink = s.Memos.History.Skip(memos).Any(m => m.Title == "NOT THE SAME MOMENT");
            if (board.Mistakes != lost || wrongLink || board.Pinned.Contains(others[0].Id))
                fail($"links: tossing {others[0].Id} onto {others[1].Id} in the tray (mistakes {board.Mistakes}, pinned {board.Pinned.Contains(others[0].Id)}, wrong-link memo {wrongLink})");
            else Debug.Log($"[AutoPilot] links: {others[0].Id} tossed onto {others[1].Id} went back to the tray, no badge lost");
            yield return Wait(1f);

            // Aim a card of another moment straight at the trusted card's chip: it pins to its own lane.
            var other = others[0];
            memos = s.Memos.History.Count;
            yield return QuickDrop(Grab(s.ViewOf(other.Id)), Screen(s.ViewOf(trusted.Id).transform.position));
            wrongLink = s.Memos.History.Skip(memos).Any(m => m.Title == "NOT THE SAME MOMENT");
            if (board.Mistakes != lost || wrongLink || !board.Pinned.Contains(other.Id))
                fail($"links: a quick drop of {other.Id} onto {trusted.Id}'s chip (mistakes {board.Mistakes}, pinned {board.Pinned.Contains(other.Id)}, wrong-link memo {wrongLink})");
            else Debug.Log($"[AutoPilot] links: {other.Id} dropped in one movement onto {trusted.Id}'s chip pinned to its own lane, no badge lost");
            yield return Wait(1f);

            // The real thing: hold the wrong clock's card over its twin until the tag says LINK.
            var from = Grab(s.ViewOf(twin.Id));
            var to = Screen(s.ViewOf(trusted.Id).transform.position);
            MouseTo(from);
            yield return Wait(0.15f);
            MouseTo(from, true);
            yield return null;
            yield return Glide(from, to, true);
            float t = 0;
            while (root.Screens.LinkTagScreen == null && t < 3f) { MouseTo(to, true); t += Clock.Dt; yield return null; }
            if (root.Screens.LinkTagScreen == null) fail("links: holding a card over its twin never showed the LINK tag");
            else Debug.Log($"[AutoPilot] links: LINK tag shown after {t:0.00}s held over {trusted.Id}");
            yield return Shot("input_link_armed");
            MouseTo(to, false);
            yield return Wait(2.5f);
            if (!board.Calibrated.Contains(twin.Clock) || board.Mistakes != lost)
                fail($"links: the held link {twin.Id} onto {trusted.Id} (calibrated {board.Calibrated.Contains(twin.Clock)}, mistakes {board.Mistakes})");
            else Debug.Log($"[AutoPilot] PASS links: {twin.Id} held onto {trusted.Id} linked and corrected {twin.Clock}; no badge lost");
        }

        IEnumerator InputTest()
        {
            var root = GameRoot.I;
            SaveData.UnlockAll = true;
            SaveData.Current.DropAllBoards();
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
            // 6. On to the docket drawer: click through the case files and the drawer to an earlier day's board.
            yield return DrawerByHand("mouse", p => Click(p), () => Click(root.Screens.ButtonScreen("btn_Close") ?? Vector2.zero),
                why => { Debug.LogError("[AutoPilot] FAIL input: " + why); ok = false; });
            // 7. Case 2: drops that land on another card don't link by accident; a held one does.
            yield return LinkByHand(why => { Debug.LogError("[AutoPilot] FAIL input: " + why); ok = false; });
            if (errors > 0) ok = false;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} input test (drag, hover, right-click, text size rebuild, hint withholds Unaided, click, UI button, incident drag, the docket drawer, no accidental links)");
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

        /// <summary>
        /// The notebook is open on a page of notes longer than it shows: run <paramref name="toOldest"/>
        /// and <paramref name="toNewest"/> (a pointer's scrolling) and check the notes moved both ways.
        /// </summary>
        IEnumerator CheckNotebookScroll(string how, IEnumerator toOldest, IEnumerator toNewest, System.Action<string> fail)
        {
            var nb = GameRoot.I.Screens.OpenNotebook;
            if (nb == null) { fail($"{how}: the notebook isn't open"); yield break; }
            if (!nb.Scrollable) { fail($"{how}: the notes fit on one page, so there's nothing to scroll"); yield break; }
            float top = nb.ScrollPosition;
            yield return toOldest;
            float bottom = nb.ScrollPosition;
            yield return Shot(how + "_notebook_scrolled");
            yield return toNewest;
            float again = nb.ScrollPosition;
            Debug.Log($"[AutoPilot] {how}: notebook scrolled {top:0.00} -> {bottom:0.00} -> {again:0.00} (1 is the newest note, 0 the oldest)");
            if (bottom > top - 0.5f) fail($"{how}: scrolling down only moved the notes from {top:0.00} to {bottom:0.00}");
            if (again < bottom + 0.5f) fail($"{how}: scrolling back up only moved the notes from {bottom:0.00} to {again:0.00}");
        }

        /// <summary>The case file names the three seals and the par time, clear of the stamp and the suspects.</summary>
        void CheckIntroSeals(CaseDef c)
        {
            var text = GameRoot.I.Screens.IntroSealsText ?? "";
            var clash = GameRoot.I.Screens.IntroSealsClash();
            bool ok = text.Contains("SEALS") && text.Contains("CLEAN") && text.Contains("UNAIDED") && text.Contains("no badge lost") && text.Contains("no hint") && text.IndexOf("CLEAN") < text.IndexOf("no badge lost")
                      && (c.ParSeconds <= 0 || (text.Contains("SWIFT") && text.Contains("under " + Screens.Clock(c.ParSeconds)))) && clash == null;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {c.Id} intro seals (par {c.ParSeconds}s){(clash != null ? ", runs into " + clash : "")}: {System.Text.RegularExpressions.Regex.Replace(text.Replace("\n", " / "), "<[^>]+>", "")}");
            if (!ok) errors++;
        }

        /// <summary>A docket's Copy result button copies its share line, which names nobody.</summary>
        bool CheckShare(CaseSession s)
        {
            Docket.TryParseId(s.Case.Id, out var day);
            var want = Docket.ShareLine(day, s.Badges, s.Elapsed, s.SealClean, s.SealUnaided, s.SealSwift);
            bool pressed = GameRoot.I.Screens.Press("btn_copy_result");
            var got = Clipboard.Last ?? "";
            bool ok = pressed && got == want && !s.Case.Suspects.Any(p => got.Contains(p.Name));
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {s.Case.Id} share line: {got}");
            return ok;
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
            SaveData.Current.DropAllBoards();
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
            // The five cases, then today's Daily Docket and three fixed days (two with a wrong clock), and a day from
            // earlier this week, opened from the docket drawer.
            var toPlay = new System.Collections.Generic.List<CaseDef>(Cases.All);
            var trapDocket = Docket.IdFor(new System.DateTime(2026, 10, 7));
            // A day from earlier in the week, opened from the docket drawer like a player who missed it.
            var weekDay = Cases.Today.AddDays(-3);
            var weekDocket = Docket.IdFor(weekDay);
            foreach (var day in new[] { Cases.Today, new System.DateTime(2026, 10, 7), new System.DateTime(2026, 10, 13), new System.DateTime(2026, 12, 25), weekDay })
            {
                var dk = Cases.DocketFor(day);
                if (dk == null) { Debug.LogError($"[AutoPilot] FAIL no docket for {day:yyyy-MM-dd}"); continue; }
                if (!toPlay.Contains(dk)) toPlay.Add(dk);
            }
            foreach (var c in toPlay)
            {
                int errorsBefore = errors;
                bool drawerOk = true;
                if (c.Id == weekDocket)
                {
                    // Through the case files' Docket button and the drawer's row for that day.
                    root.ShowSelect();
                    yield return Wait(1.5f);
                    bool pressed = root.Screens.Press("btn_docket");
                    yield return Wait(1f);
                    drawerOk = pressed && root.Screens.DocketWeekOpen && root.Screens.DocketRowText(weekDay) != null;
                    yield return Shot("docket_week");
                    pressed = root.Screens.Press("docket_" + weekDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                    yield return Wait(3.5f);
                    drawerOk &= pressed && root.Flow == Flow.Intro && root.IntroCase != null && root.IntroCase.Id == c.Id;
                    Debug.Log($"[AutoPilot] {(drawerOk ? "PASS" : "FAIL")} docket drawer: opened {c.Id} from its row ({(pressed ? "pressed" : "no row")}, flow {root.Flow})");
                    yield return Shot(c.Id + "_intro_from_drawer");
                    CheckIntroSeals(c);
                }
                else if (capture)
                {
                    root.ShowIntro(c);
                    yield return Wait(3.5f);
                    yield return Shot(c.Id + "_intro");
                    CheckIntroSeals(c);
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
                    if (Cases.IsDocket(c) && !CheckShare(s)) ok = false;
                }
                if (!drawerOk) ok = false;
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
            // The drawer shows the result of the day played from it.
            root.ShowSelect();
            yield return Wait(1f);
            root.Screens.Press("btn_docket");
            yield return Wait(1f);
            var row = root.Screens.DocketRowText(weekDay) ?? "";
            bool rowOk = row.Contains("★");
            Debug.Log($"[AutoPilot] {(rowOk ? "PASS" : "FAIL")} docket drawer shows {weekDocket}'s result: {row}");
            if (!rowOk) passed--;
            yield return Shot("docket_week_after");
            Debug.Log($"[AutoPilot] done: {passed}/{toPlay.Count} cases passed ({Cases.All.Count} cases, {toPlay.Count - Cases.All.Count} dockets), {errors} errors");
            yield return Wait(0.5f);
            Application.Quit(passed == toPlay.Count && errors == 0 ? 0 : 1);
        }
    }
}
