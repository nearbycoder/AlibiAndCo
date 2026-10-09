using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AlibiCo
{
    /// <summary>
    /// Launched with -alibiRecord dir: plays every case start to finish with simulated mouse input
    /// (glides, drags, hovers, button clicks) at a watchable pace while VideoRecorder captures it.
    /// The solver picks the moves; if a gesture ever misses, the move is applied directly and logged.
    /// With -alibiTrailer it also stages the beats the solver never plays (a hover route, the map
    /// zoom, a witness standing firm, a wrong link, a hint, the notebook, pause and settings), logs a
    /// frame-numbered marker for every beat to markers.txt and saves cursor-free stills to stills/.
    /// Tools/make_trailer.py cuts the trailer from those markers.
    /// </summary>
    public sealed class Showcase : MonoBehaviour
    {
        VideoRecorder rec;
        Vector2 mouse;
        int misses, maxCases;
        bool trailer, shownUnknown, linkedByPanel;
        StreamWriter markers;
        string stillsDir;

        public void Run(string dir, int caseCount = 99, bool trailerMode = false)
        {
            maxCases = caseCount;
            trailer = trailerMode;
            SaveData.UnlockAll = false;
            rec = gameObject.AddComponent<VideoRecorder>();
            mouse = new Vector2(Screen.width * 0.62f, Screen.height * 0.42f);
            Send(mouse);
            rec.Begin(dir);
            if (trailer)
            {
                markers = new StreamWriter(Path.Combine(dir, "markers.txt"));
                stillsDir = Path.Combine(dir, "stills");
                StartCoroutine(Watch());
            }
            StartCoroutine(Go());
        }

        // ------------------------------------------------------------------ trailer markers and stills

        void Mark(string label)
        {
            if (markers == null) return;
            markers.WriteLine($"{rec.Frames} {label}");
            markers.Flush();
        }

        void Still(string name)
        {
            if (!trailer) return;
            DevCapture.Capture(Path.Combine(stillsDir, name + ".png"));
        }

        IEnumerator StillAfter(string name, float delay)
        {
            yield return Wait(delay);
            Still(name);
        }

        /// <summary>Logs a marker whenever the board or the screen flow changes in a way worth cutting to.</summary>
        IEnumerator Watch()
        {
            var root = GameRoot.I;
            var flow = Flow.Boot;
            CaseSession session = null;
            int conflicts = 0, mistakes = 0;
            bool anyPinned = false;
            var struck = new HashSet<string>();
            var clocks = new HashSet<string>();
            var confirmed = new HashSet<string>();
            var open = new HashSet<string>();
            while (true)
            {
                var s = root.Session;
                if (s != session)
                {
                    session = s;
                    conflicts = mistakes = 0;
                    anyPinned = false;
                    struck.Clear(); clocks.Clear(); confirmed.Clear(); open.Clear();
                    if (s != null) Mark("case " + s.Case.Id);
                }
                if (root.Flow != flow)
                {
                    flow = root.Flow;
                    Mark("flow " + flow);
                    if (flow == Flow.Closing && s != null) StartCoroutine(StillAfter(s.Case.Id + "-reconstruction", 3.2f));
                }
                if (s != null)
                {
                    var b = s.Board;
                    string id = s.Case.Id;
                    if (!anyPinned && b.Pinned.Count > 0) { anyPinned = true; Mark("first-pin"); }
                    int c = b.EstablishedConflicts.Count();
                    if (c > conflicts) Mark("conflict " + c);
                    conflicts = c;
                    foreach (var x in b.Struck)
                        if (struck.Add(x)) { Mark("struck " + x); StartCoroutine(StillAfter($"{id}-struck-{x}", 1.4f)); }
                    foreach (var x in b.Calibrated)
                        if (clocks.Add(x)) { Mark("clock " + x); StartCoroutine(StillAfter($"{id}-clock-{x}", 1.9f)); }
                    foreach (var kv in b.Confirmed)
                        if (confirmed.Add(kv.Key)) { Mark($"confirmed {kv.Key} {kv.Value}"); StartCoroutine(StillAfter($"{id}-only-{kv.Value}", 1.0f)); }
                    if (b.Mistakes > mistakes) Mark("badge-lost");
                    mistakes = b.Mistakes;
                    // A lane opening after the tray is empty is an alibi breaking, not the empty board at the deal.
                    bool settled = !b.TrayCards.Any();
                    foreach (var kv in b.Fits)
                    {
                        if (!kv.Value.Fits) { open.Remove(kv.Key); continue; }
                        if (open.Add(kv.Key) && settled) { Mark("open " + kv.Key); StartCoroutine(StillAfter($"{id}-open-{kv.Key}", 1.2f)); }
                    }
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ input

        static void Send(Vector2 p, bool left = false)
        {
            var st = new MouseState { position = p };
            if (left) st = st.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }

        IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        /// <summary>Move the cursor along a slightly curved, eased path (speed scales with distance).</summary>
        IEnumerator MoveTo(Vector2 to, bool held = false, float speed = 1f)
        {
            var from = mouse;
            float dist = (to - from).magnitude;
            float dur = Mathf.Clamp(0.25f + dist / Screen.height * 0.55f, 0.25f, 1.1f) / speed;
            var bend = Vector2.Perpendicular((to - from).normalized) * Mathf.Min(60f, dist * 0.08f) * (Random.value < 0.5f ? -1 : 1);
            int frames = Mathf.Max(2, Mathf.RoundToInt(dur * rec.Fps));
            for (int i = 1; i <= frames; i++)
            {
                float k = Easing.Apply(Ease.InOutSine, i / (float)frames);
                mouse = Vector2.Lerp(from, to, k) + bend * Mathf.Sin(k * Mathf.PI);
                Send(mouse, held);
                yield return null;
            }
            mouse = to;
            Send(mouse, held);
        }

        IEnumerator Click(Vector2 at)
        {
            yield return MoveTo(at);
            yield return Wait(0.12f);
            Send(mouse, true);
            yield return Wait(0.08f);
            Send(mouse, false);
            yield return Wait(0.15f);
        }

        IEnumerator Drag(Vector2 from, Vector2 to, float hold = 0f, string still = null, System.Func<bool> armed = null)
        {
            yield return MoveTo(from);
            yield return Wait(0.18f);
            Send(mouse, true);
            yield return Wait(0.1f);
            yield return MoveTo(Vector2.Lerp(from, to, 0.88f), true, 0.85f);
            if (still != null) Still(still);
            if (hold > 0) yield return Wait(hold);
            yield return MoveTo(to, true, 1.6f);
            yield return Wait(0.18f);
            // A link arms only once the card has rested on the other one (CaseSession.LinkArmSeconds).
            for (float t = 0; armed != null && !armed() && t < 1.5f; t += Clock.Dt) yield return null;
            if (armed != null) yield return Wait(0.35f);   // the LINK tag on screen for a beat
            Send(mouse, false);
            yield return Wait(0.25f);
        }

        IEnumerator Wait(float s)
        {
            while (s > 0) { s -= Clock.Dt; yield return null; }
        }

        static Vector2 ScreenOf(Vector3 world) => Stage.I.Cam.WorldToScreenPoint(world);

        /// <summary>A spot on the desk below the board, clear of the cards, the memo and the map.</summary>
        static Vector2 Desk => new Vector2(Screen.width * 0.6f, Screen.height * 0.16f);

        /// <summary>
        /// A point on the card that's on screen and where this card is the topmost one under the
        /// cursor (tray cards overlap, and the second tray row runs off the bottom edge).
        /// </summary>
        static Vector2 Grab(CardView v)
        {
            var size = v.Compact ? CardView.ChipSize : CardView.FullSize;
            float[] fxs = { -0.38f, -0.2f, 0f, 0.2f, 0.38f };
            float[] fys = { 0.3f, 0.1f, -0.1f, 0.38f };
            foreach (var fy in fys)
                foreach (var fx in fxs)
                {
                    var p = ScreenOf(v.transform.TransformPoint(new Vector3(size.x * fx, size.y * fy, 0)));
                    float m = Screen.height * 0.03f;
                    if (p.x < m || p.y < m || p.x > Screen.width - m || p.y > Screen.height - m) continue;
                    if (TopCard(p) == v) return p;
                }
            return v.Compact ? ScreenOf(v.transform.position)
                : ScreenOf(v.transform.TransformPoint(new Vector3(-CardView.FullSize.x / 2 + 0.45f, 0.3f, 0)));
        }

        /// <summary>Same rule as CaseSession.Pick: the highest card under the point wins.</summary>
        static CardView TopCard(Vector2 screen)
        {
            CardView best = null;
            float bestH = float.MinValue;
            foreach (var h in Physics.RaycastAll(Stage.I.Cam.ScreenPointToRay(screen), 200f))
            {
                var c = h.collider.GetComponentInParent<CardView>();
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                float y = h.point.y + c.CurrentLift * 0.1f;
                if (y > bestH) { bestH = y; best = c; }
            }
            return best;
        }

        /// <summary>Screen centre of the active button whose label contains the text.</summary>
        static Vector2? ButtonAt(string label)
        {
            foreach (var b in Object.FindObjectsByType<Button>())
            {
                if (!b.isActiveAndEnabled) continue;
                var t = b.GetComponentInChildren<TMP_Text>();
                if (t == null || !t.text.Contains(label)) continue;
                return Centre((RectTransform)b.transform);
            }
            return null;
        }

        static Vector2 Centre(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return (c[0] + c[2]) / 2;
        }

        IEnumerator Press(string label, System.Action fallback)
        {
            var at = ButtonAt(label);
            if (at == null) { Miss($"no button '{label}'"); fallback(); yield break; }
            yield return Click(at.Value);
        }

        void Miss(string what)
        {
            misses++;
            Debug.LogWarning($"[Showcase] gesture missed at frame {rec.Frames}, applying directly: " + what);
        }

        // ------------------------------------------------------------------ script

        IEnumerator Go()
        {
            var root = GameRoot.I;
            root.ShowTitle(true);
            Mark("title");
            yield return Wait(4.6f);
            Still("title");
            yield return Wait(0.4f);
            yield return Press("Case Files", root.ShowSelect);
            yield return Wait(2.2f);
            Still("case-files");
            foreach (var c in Cases.All.Take(maxCases))
            {
                if (root.Flow == Flow.Select)
                {
                    var folder = GameObject.Find("folder_" + c.Id);
                    if (folder != null) yield return Click(Centre((RectTransform)folder.transform));
                    else { Miss("folder " + c.Id); root.ShowIntro(c); }
                    yield return Wait(0.6f);
                }
                if (root.Flow != Flow.Intro) { Miss("intro " + c.Id); root.ShowIntro(c); }
                Mark("intro " + c.Id);
                yield return Wait(6.5f);
                Still(c.Id + "-intro");
                yield return Press("Open the board", () => root.StartCase(c, false));
                yield return PlayCase(root, c);
                bool last = Cases.IndexOf(c.Id) == Mathf.Min(maxCases, Cases.All.Count) - 1;
                yield return Wait(3f);
                Still(c.Id + "-closed");
                yield return Wait(3f);
                if (!last) yield return Press("Next case", () => root.NextCase(c));
                else yield return Press(Cases.IndexOf(c.Id) == Cases.All.Count - 1 ? "Back to the case files" : "Case files", root.ShowSelect);
                if (last) Mark("case-files-final");
                yield return Wait(last ? 4f : 0.8f);
                if (last) Still("case-files-final");
            }
            if (trailer && Cases.DocketUnlocked) yield return PlayDocket(root);
            Mark("end");
            rec.End();
            markers?.Close();
            Debug.Log($"[Showcase] done: {rec.Frames} frames ({rec.Seconds:0.0}s), {misses} missed gestures");
            yield return null;
            Application.Quit(0);
        }

        /// <summary>Trailer only: the Daily Docket button, the week's drawer, and today's docket played to its close.</summary>
        IEnumerator PlayDocket(GameRoot root)
        {
            var c = Cases.TodaysDocket;
            if (c == null) { Debug.LogWarning("[Showcase] no docket today"); yield break; }
            Mark("docket-drawer");
            var btn = root.Screens.ButtonScreen("btn_docket");
            if (btn != null) yield return Click(btn.Value); else Miss("btn_docket");
            yield return Wait(2.4f);
            Still("docket-drawer");
            var row = root.Screens.ButtonScreen("docket_" + Cases.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            if (row != null) yield return Click(row.Value); else { Miss("today's docket row"); root.ShowIntro(c); }
            yield return Wait(0.6f);
            if (root.Flow != Flow.Intro) { Miss("docket intro"); root.ShowIntro(c); }
            Mark("intro " + c.Id);
            yield return Wait(4.5f);
            Still("docket-intro");
            yield return Press("Open the board", () => root.StartCase(c, false));
            yield return PlayCase(root, c);
            yield return Wait(3f);
            Mark("docket-closed");
            Still("docket-closed");
            yield return Wait(3f);
        }

        IEnumerator PlayCase(GameRoot root, CaseDef c)
        {
            float t = 0;
            while ((root.Session == null || root.Session.Case != c) && t < 5) { t += Clock.Dt; yield return null; }
            var s = root.Session;
            Mark("dealt " + c.Id);
            yield return Wait(2.4f);
            Still(c.Id + "-dealt");
            yield return Wait(0.8f);
            yield return Extras(root, s, "dealt");
            yield return PinTray(s);
            yield return Wait(1.5f);
            yield return Extras(root, s, "pinned");

            for (int step = 0; step < 20; step++)
            {
                var path = Solver.ShortestSolution(Solver.Shadow(s.Board));
                if (path == null) { Debug.LogError($"[Showcase] {c.Id}: no solution from here"); yield break; }
                if (path.Count == 0) break;
                var m = path[0];
                bool panel = trailer && c.Id == "case3" && !linkedByPanel;
                if (m.Kind == "link") { yield return Link(s, m.A, m.B, panel); linkedByPanel |= panel; }
                else yield return Confront(root, s, m.A);
                yield return Wait(1.2f);
                yield return PinTray(s);
                yield return Wait(1.2f);
                yield return Extras(root, s, "step");
            }
            yield return Extras(root, s, "solved");
            yield return Accuse(root, s, c);
        }

        /// <summary>Trailer-only beats the solver never plays, staged at fixed points in each case.</summary>
        IEnumerator Extras(GameRoot root, CaseSession s, string phase)
        {
            if (!trailer) yield break;
            string id = s.Case.Id;
            if (id == "case1" && phase == "pinned")
            {
                // Hover a card at the far end of a red ribbon: the map draws the walk that can't be made.
                var k = s.Board.EstablishedConflicts.FirstOrDefault(x => !x.Overlap) ?? s.Board.EstablishedConflicts.FirstOrDefault();
                var v = k != null ? s.ViewOf(k.B.Card.Id) : null;
                if (v != null)
                {
                    Mark("hover " + v.Id);
                    yield return MoveTo(ScreenOf(v.transform.position));
                    yield return Wait(2.6f);
                    Still("case1-hover-route");
                    yield return Wait(0.6f);
                }
                // Hover Connie's memo: it's held up off the desk, larger, until the pointer leaves it.
                if (s.Memos.Showing != null)
                {
                    var slip = s.Memos.ScreenRect(out _);
                    Mark("memo-held");
                    yield return MoveTo(slip.center);
                    yield return Wait(2.6f);
                    Still("case1-memo-held");
                    yield return Wait(0.6f);
                    yield return MoveTo(new Vector2(Screen.width * 0.62f, Screen.height * 0.5f));
                    yield return Wait(0.8f);
                }
            }
            else if (id == "case2" && phase == "pinned")
            {
                // Sid's statement is true; his clock is what's wrong. Confronting him costs a badge.
                Mark("firm i_sid");
                yield return Confront(root, s, "i_sid", firm: true);
                Still("case2-stands-firm");
                yield return Wait(0.8f);
            }
            else if (id == "case2" && phase == "solved")
            {
                Mark("map");
                yield return MoveTo(ScreenOf(s.Map.transform.position));
                yield return Wait(2.4f);
                Still("case2-map-zoom");
                yield return Wait(0.8f);
                yield return MoveTo(new Vector2(Screen.width * 0.62f, Screen.height * 0.5f));
                Mark("notebook");
                yield return PressKey(Key.Tab);
                yield return Wait(3.2f);
                Still("case2-notebook");
                yield return Wait(1.0f);
                yield return PressKey(Key.Tab);
                yield return Wait(0.8f);
            }
            else if (id == "case3" && phase == "dealt")
            {
                Mark("pause");
                yield return PressKey(Key.Escape);
                yield return Wait(2.6f);
                Still("case3-pause");
                Mark("settings");
                yield return Press("Settings", root.Screens.ShowSettings);
                yield return Wait(2.6f);
                Still("case3-settings");
                yield return Wait(0.4f);
                yield return PressKey(Key.Escape);
                yield return Wait(0.6f);
                yield return Press("Resume", () => root.SetPaused(false));
                yield return Wait(0.8f);
            }
            else if (id == "case3" && phase == "step" && !shownUnknown && s.Board.Unlocked.Contains("u_photo") && !s.Board.Confirmed.ContainsKey("u_photo"))
            {
                // The figure in the photograph: lift it so its candidate faces (some already crossed out) read.
                shownUnknown = true;
                var v = s.ViewOf("u_photo");
                Mark("unknown u_photo");
                yield return MoveTo(Grab(v));
                yield return Wait(2.8f);
                Still("case3-unknown-faces");
                yield return Wait(0.8f);
                yield return MoveTo(new Vector2(Screen.width * 0.62f, Screen.height * 0.5f));
                yield return Wait(0.6f);
            }
            else if (id == "case3" && phase == "pinned")
            {
                // Ask Connie twice: a nudge first, then the cards to look at, which wear a CONNIE tag.
                yield return MoveTo(Desk);
                yield return ReadMemos(s);
                Mark("hint");
                yield return PressKey(Key.H);
                yield return Wait(1.8f);
                yield return PressKey(Key.H);
                yield return Wait(0.3f);
                yield return ReadMemos(s);
                yield return Wait(1.6f);
                if (!s.TaggedViews.Any()) Debug.LogWarning("[Showcase] the hint tagged no cards");
                Still("case3-hint");
                yield return Wait(1.0f);

                // Two cards that aren't one moment: Connie says so, and it costs a badge.
                if (s.Board.Pinned.Contains("n_kiosk") && s.Board.Pinned.Contains("x_coast"))
                {
                    yield return ReadMemos(s);
                    Mark("wrong-link");
                    var a = s.ViewOf("n_kiosk");
                    var b = s.ViewOf("x_coast");
                    int before = s.Board.Mistakes;
                    yield return MoveTo(ScreenOf(a.transform.position));
                    yield return Wait(0.6f);
                    yield return Drag(ScreenOf(a.transform.position), ScreenOf(b.transform.position), 0.4f, null, () => s.LinkTarget == b);
                    if (s.Board.Mistakes == before) { Miss("wrong link"); s.AutoLink("n_kiosk", "x_coast"); }
                    yield return MoveTo(Desk);
                    yield return Wait(0.3f);
                    yield return ReadMemos(s);
                    yield return Wait(1.3f);
                    Still("case3-wrong-link");
                    yield return Wait(1.6f);
                }
            }
        }

        /// <summary>Space through any memos waiting, so the next one lands on the desk at once.</summary>
        IEnumerator ReadMemos(CaseSession s)
        {
            for (int i = 0; i < 8 && s.Memos.Waiting > 0; i++) { yield return PressKey(Key.Space); yield return Wait(0.15f); }
        }

        /// <summary>Drag each tray card to its own lane, near its printed time.</summary>
        IEnumerator PinTray(CaseSession s)
        {
            bool first = s.Board.Pinned.Count == 0;
            while (true)
            {
                var next = s.Board.TrayCards.FirstOrDefault(x => !x.IsUnknown);
                if (next == null) yield break;
                var v = s.ViewOf(next.Id);
                string laneId = next.Town ? Board.TownLane : next.Subjects[0];
                Vector2 to;
                if (s.View.LaneById.TryGetValue(laneId, out var lane))
                    to = ClearSpot(s, v, lane, s.View.TimeToX((next.From + next.To) / 2f));
                else to = ScreenOf(Stage.I.BoardToWorld(Vector2.zero));
                // Arrive first, then re-aim: the camera parallaxes with the cursor.
                yield return MoveTo(Grab(v));
                yield return Wait(0.1f);
                if (first) Mark("drag " + next.Id);
                yield return Drag(Grab(v), to, 0f, first ? s.Case.Id + "-drag" : null);
                first = false;
                yield return Wait(0.45f);
                if (!s.Board.Pinned.Contains(next.Id)) { Miss("pin " + next.Id); s.AutoPin(next.Id); yield return Wait(0.4f); }
            }
        }

        /// <summary>
        /// A drop point in the lane near x that isn't over another card: dropping onto a card means
        /// "these are one moment" and would try a link instead of pinning.
        /// </summary>
        static Vector2 ClearSpot(CaseSession s, CardView dragged, BoardView.Lane lane, float x)
        {
            float h = lane.Top - lane.Bottom;
            float[] fy = { 0.55f, 0.3f, 0.75f };
            // Right of the moment first (cards to the left are usually earlier and already pinned),
            // then left, but never into the portrait column at the lane's start.
            for (int k = 0; k < 24; k++)
            {
                float dx = (k + 1) / 2 * 0.8f * (k % 2 == 1 ? 1 : -1);
                float px = x + dx;
                if (px < s.View.X0 + 1.2f || px > s.View.X1 - 0.6f) continue;
                foreach (var f in fy)
                {
                    var local = new Vector2(px, lane.Bottom + h * f);
                    if (s.View.LaneAt(local) != lane || !s.View.OnBoard(local)) continue;
                    var p = ScreenOf(Stage.I.BoardToWorld(local));
                    if (!OverCard(p, dragged)) return p;
                }
            }
            return ScreenOf(Stage.I.BoardToWorld(new Vector2(Mathf.Max(x, s.View.X0 + 1.2f), lane.Bottom + h * 0.55f)));
        }

        /// <summary>
        /// Clear in a ring around the point too: the camera parallaxes with the mouse, so by the time
        /// the cursor arrives the board has shifted a little under the spot picked from the tray.
        /// </summary>
        static bool OverCard(Vector2 screen, CardView ignore)
        {
            float r = Screen.height * 0.045f;
            if (OverCardAt(screen, ignore)) return true;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                if (OverCardAt(screen + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, ignore)) return true;
            }
            return false;
        }

        static bool OverCardAt(Vector2 screen, CardView ignore)
        {
            foreach (var h in Physics.RaycastAll(Stage.I.Cam.ScreenPointToRay(screen), 200f))
            {
                var v = h.collider.GetComponentInParent<CardView>();
                if (v != null && v != ignore && v.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        /// <summary>Confront a statement. firm: it's true, so the witness should stand firm (and cost a badge).</summary>
        IEnumerator Confront(GameRoot root, CaseSession s, string id, bool firm = false)
        {
            var v = s.ViewOf(id);
            int mistakes = s.Board.Mistakes;
            Mark("confront " + id);
            yield return MoveTo(ScreenOf(v.transform.position));
            yield return Wait(1.6f);
            yield return Click(ScreenOf(v.transform.position));
            yield return Wait(0.7f);
            var btn = root.Screens.ConfrontButtonScreen();
            if (btn != null) yield return Click(btn.Value);
            System.Func<bool> landed = () => firm ? s.Board.Mistakes > mistakes : s.Board.Struck.Contains(id);
            if (!landed())
            {
                yield return Wait(0.3f);
                if (!landed()) { Miss("confront " + id); s.Confront(v); }
            }
            yield return MoveTo(mouse + new Vector2(Screen.width * 0.06f, -Screen.height * 0.14f));
            yield return Wait(3.4f);
        }

        IEnumerator Link(CaseSession s, string a, string b, bool viaPanel = false)
        {
            foreach (var id in new[] { a, b })
                if (!s.Board.Pinned.Contains(id)) { yield return PinTray(s); if (!s.Board.Pinned.Contains(id)) s.AutoPin(id); }
            yield return Wait(0.5f);
            var va = s.ViewOf(a);
            var vb = s.ViewOf(b);
            Mark($"link {a} {b}{(viaPanel ? " panel" : "")}");
            yield return MoveTo(ScreenOf(va.transform.position));
            yield return Wait(1.0f);
            string before = s.Board.StateKey();
            if (viaPanel)
            {
                // Without a drag: click the card, Same moment as…, then click the other card.
                yield return Click(ScreenOf(va.transform.position));
                yield return Wait(0.8f);
                var btn = GameRoot.I.Screens.LinkButtonScreen();
                if (btn != null)
                {
                    yield return Click(btn.Value);
                    yield return Wait(0.5f);
                    yield return MoveTo(ScreenOf(vb.transform.position));
                    yield return Wait(0.7f);
                    yield return Click(ScreenOf(vb.transform.position));
                }
                else Miss("Same moment as… " + a);
            }
            else yield return Drag(ScreenOf(va.transform.position), ScreenOf(vb.transform.position), 0.5f, null, () => s.LinkTarget == vb);
            yield return Wait(0.5f);
            if (s.Board.StateKey() == before) { Miss($"link {a}+{b}"); s.AutoLink(a, b); }
            yield return MoveTo(mouse + new Vector2(0, -Screen.height * 0.18f));
            yield return Wait(4.2f);
        }

        IEnumerator Accuse(GameRoot root, CaseSession s, CaseDef c)
        {
            var lane = s.View.LaneById[c.Incident.Culprit];
            var fit = s.Board.Fits[c.Incident.Culprit];
            var target = ScreenOf(Stage.I.BoardToWorld(new Vector2(s.View.TimeToX(fit.EarliestStart), lane.Track + 0.5f)));
            var from = ScreenOf(s.IncidentView.transform.position);
            yield return MoveTo(from);
            yield return Wait(0.3f);
            Send(mouse, true);
            yield return Wait(0.1f);
            // Sweep across an innocent lane first so the refusal preview shows, then settle on the culprit.
            var other = s.View.Lanes.FirstOrDefault(l => !l.IsTown && l.Id != c.Incident.Culprit);
            Mark("accuse " + c.Id);
            if (other != null)
            {
                var o = ScreenOf(Stage.I.BoardToWorld(new Vector2(s.View.TimeToX(fit.EarliestStart), other.Track + 0.5f)));
                yield return MoveTo(o, true, 0.8f);
                yield return Wait(1.0f);
                Still(c.Id + "-incident-refused");
                yield return Wait(0.3f);
            }
            yield return MoveTo(target, true, 0.7f);
            yield return Wait(1.1f);
            Still(c.Id + "-incident-fits");
            yield return Wait(0.3f);
            Mark("pin-incident " + c.Id);
            Send(mouse, false);
            yield return Wait(1.0f);
            if (root.Flow == Flow.Playing && !s.InputLocked) { Miss("accuse " + c.Id); s.AutoAccuse(c.Incident.Culprit); }
            yield return MoveTo(new Vector2(Screen.width * 0.985f, Screen.height * 0.64f), false, 0.5f);
            float t = 0;
            while (root.Flow != Flow.Closed && t < 120) { t += Clock.Dt; yield return null; }
        }
    }
}
