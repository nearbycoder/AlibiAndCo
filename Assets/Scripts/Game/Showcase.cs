using System.Collections;
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
    /// </summary>
    public sealed class Showcase : MonoBehaviour
    {
        VideoRecorder rec;
        Vector2 mouse;
        int misses, maxCases;

        public void Run(string dir, int caseCount = 99)
        {
            maxCases = caseCount;
            SaveData.UnlockAll = false;
            rec = gameObject.AddComponent<VideoRecorder>();
            mouse = new Vector2(Screen.width * 0.62f, Screen.height * 0.42f);
            Send(mouse);
            rec.Begin(dir);
            StartCoroutine(Go());
        }

        // ------------------------------------------------------------------ input

        static void Send(Vector2 p, bool left = false)
        {
            var st = new MouseState { position = p };
            if (left) st = st.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(Mouse.current, st);
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

        IEnumerator Drag(Vector2 from, Vector2 to, float hold = 0f)
        {
            yield return MoveTo(from);
            yield return Wait(0.18f);
            Send(mouse, true);
            yield return Wait(0.1f);
            yield return MoveTo(Vector2.Lerp(from, to, 0.88f), true, 0.85f);
            if (hold > 0) yield return Wait(hold);
            yield return MoveTo(to, true, 1.6f);
            yield return Wait(0.18f);
            Send(mouse, false);
            yield return Wait(0.25f);
        }

        IEnumerator Wait(float s)
        {
            while (s > 0) { s -= Time.unscaledDeltaTime; yield return null; }
        }

        static Vector2 ScreenOf(Vector3 world) => Stage.I.Cam.WorldToScreenPoint(world);

        static Vector2 Grab(CardView v) => v.Compact ? ScreenOf(v.transform.position)
            : ScreenOf(v.transform.TransformPoint(new Vector3(-CardView.FullSize.x / 2 + 0.45f, 0.3f, 0)));

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
            Debug.LogWarning("[Showcase] gesture missed, applying directly: " + what);
        }

        // ------------------------------------------------------------------ script

        IEnumerator Go()
        {
            var root = GameRoot.I;
            root.ShowTitle(true);
            yield return Wait(5f);
            yield return Press("Case Files", root.ShowSelect);
            yield return Wait(2.2f);
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
                yield return Wait(6.5f);
                yield return Press("Open the board", () => root.StartCase(c, false));
                yield return PlayCase(root, c);
                bool last = Cases.IndexOf(c.Id) == Mathf.Min(maxCases, Cases.All.Count) - 1;
                yield return Wait(6f);
                if (!last) yield return Press("Next case", () => root.NextCase(c));
                else yield return Press(Cases.IndexOf(c.Id) == Cases.All.Count - 1 ? "Back to the case files" : "Case files", root.ShowSelect);
                yield return Wait(last ? 4f : 0.8f);
            }
            rec.End();
            Debug.Log($"[Showcase] done: {rec.Frames} frames ({rec.Seconds:0.0}s), {misses} missed gestures");
            yield return null;
            Application.Quit(0);
        }

        IEnumerator PlayCase(GameRoot root, CaseDef c)
        {
            float t = 0;
            while ((root.Session == null || root.Session.Case != c) && t < 5) { t += Time.unscaledDeltaTime; yield return null; }
            var s = root.Session;
            yield return Wait(3.2f);
            yield return PinTray(s);
            yield return Wait(1.5f);

            for (int step = 0; step < 20; step++)
            {
                var path = Solver.ShortestSolution(Solver.Shadow(s.Board));
                if (path == null) { Debug.LogError($"[Showcase] {c.Id}: no solution from here"); yield break; }
                if (path.Count == 0) break;
                var m = path[0];
                if (m.Kind == "link") yield return Link(s, m.A, m.B);
                else yield return Confront(root, s, m.A);
                yield return Wait(1.2f);
                yield return PinTray(s);
                yield return Wait(1.2f);
            }
            yield return Accuse(root, s, c);
        }

        /// <summary>Drag each tray card to its own lane, near its printed time.</summary>
        IEnumerator PinTray(CaseSession s)
        {
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
                yield return Drag(Grab(v), to);
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
            float mid = (lane.Top + lane.Bottom) / 2;
            float[] dys = { 0.15f, 0.45f, -0.2f };
            for (int k = 0; k < 14; k++)
            {
                float dx = (k + 1) / 2 * 0.9f * (k % 2 == 0 ? 1 : -1);
                float px = Mathf.Clamp(x + dx, s.View.X0 + 0.3f, s.View.X1 - 0.3f);
                foreach (var dy in dys)
                {
                    var p = ScreenOf(Stage.I.BoardToWorld(new Vector2(px, mid + dy)));
                    if (!OverCard(p, dragged)) return p;
                }
            }
            return ScreenOf(Stage.I.BoardToWorld(new Vector2(x, mid + 0.15f)));
        }

        static bool OverCard(Vector2 screen, CardView ignore)
        {
            foreach (var h in Physics.RaycastAll(Stage.I.Cam.ScreenPointToRay(screen), 200f))
            {
                var v = h.collider.GetComponentInParent<CardView>();
                if (v != null && v != ignore && v.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        IEnumerator Confront(GameRoot root, CaseSession s, string id)
        {
            var v = s.ViewOf(id);
            yield return MoveTo(ScreenOf(v.transform.position));
            yield return Wait(1.6f);
            yield return Click(ScreenOf(v.transform.position));
            yield return Wait(0.7f);
            var btn = root.Screens.ConfrontButtonScreen();
            if (btn != null) yield return Click(btn.Value);
            if (!s.Board.Struck.Contains(id))
            {
                yield return Wait(0.3f);
                if (!s.Board.Struck.Contains(id)) { Miss("confront " + id); s.Confront(v); }
            }
            yield return MoveTo(mouse + new Vector2(Screen.width * 0.06f, -Screen.height * 0.14f));
            yield return Wait(3.4f);
        }

        IEnumerator Link(CaseSession s, string a, string b)
        {
            foreach (var id in new[] { a, b })
                if (!s.Board.Pinned.Contains(id)) { yield return PinTray(s); if (!s.Board.Pinned.Contains(id)) s.AutoPin(id); }
            yield return Wait(0.5f);
            var va = s.ViewOf(a);
            var vb = s.ViewOf(b);
            yield return MoveTo(ScreenOf(va.transform.position));
            yield return Wait(1.0f);
            string before = s.Board.StateKey();
            yield return Drag(ScreenOf(va.transform.position), ScreenOf(vb.transform.position), 0.5f);
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
            if (other != null)
            {
                var o = ScreenOf(Stage.I.BoardToWorld(new Vector2(s.View.TimeToX(fit.EarliestStart), other.Track + 0.5f)));
                yield return MoveTo(o, true, 0.8f);
                yield return Wait(1.3f);
            }
            yield return MoveTo(target, true, 0.7f);
            yield return Wait(1.4f);
            Send(mouse, false);
            yield return Wait(1.0f);
            if (root.Flow == Flow.Playing && !s.InputLocked) { Miss("accuse " + c.Id); s.AutoAccuse(c.Incident.Culprit); }
            yield return MoveTo(new Vector2(Screen.width * 0.985f, Screen.height * 0.64f), false, 0.5f);
            float t = 0;
            while (root.Flow != Flow.Closed && t < 120) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}
