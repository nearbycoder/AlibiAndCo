using System;
using System.Collections.Generic;
using System.Linq;
using AlibiCo.Logic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlibiCo
{
    /// <summary>
    /// One case in play: owns the rules (Board), the board view, every card object and the desk
    /// interactions. Every player action goes through here and comes back out as feedback.
    /// </summary>
    public sealed class CaseSession : MonoBehaviour
    {
        public static CaseSession Current { get; private set; }

        public CaseDef Case { get; private set; }
        public Board Board { get; private set; }
        public BoardView View { get; private set; }
        public MemoDesk Memos { get; private set; }
        public float Elapsed { get; private set; }
        public int Badges => Mathf.Max(1, 3 - Board.Mistakes);
        public bool Solved => Board.Solved;
        public int TrayCount => Board.TrayCards.Count();
        public bool UsedHints { get; private set; }

        // Seals for this run (see SaveData.CaseRecord).
        public bool SealClean => Board.Mistakes == 0;
        public bool SealUnaided => !UsedHints;
        public bool SealSwift => Case.ParSeconds > 0 && Elapsed <= Case.ParSeconds;
        public bool InputLocked;
        public event Action<CaseSession> SolvedEvent;
        public event Action BadgeLost;
        public Action<CardView, Vector2> ShowActions;   // card, screen position
        public Action HideActions;

        Stage stage;
        Transform cardsRoot;
        readonly Dictionary<string, CardView> views = new Dictionary<string, CardView>();
        readonly Dictionary<string, List<CardView>> echoes = new Dictionary<string, List<CardView>>();
        readonly Dictionary<CardView, string> echoOf = new Dictionary<CardView, string>();
        readonly Dictionary<string, CardView> inspectors = new Dictionary<string, CardView>();
        readonly List<string> trayOrder = new List<string>();
        readonly HashSet<string> seenMemos = new HashSet<string>();
        readonly HashSet<string> freshCards = new HashSet<string>();
        readonly HashSet<string> knownConflicts = new HashSet<string>();
        Dictionary<string, BoardView.ChipPlace> chips = new Dictionary<string, BoardView.ChipPlace>();
        CardView incident;
        Transform staples;
        public MapView Map { get; private set; }

        // interaction
        CardView hover, pressed, dragging, selected, linkTarget, inspecting;
        // A link arms only after the dragged card has been held over the same card for a moment, so
        // a drop that merely lands on another card (pinning anywhere on a lane, or tossing a card back
        // into the tray) never costs a badge by accident.
        CardView linkAim;
        float linkAimTime;
        public const float LinkArmSeconds = 0.45f;
        string inspectingId;
        Vector2 pressScreen;
        float hoverTime;
        Vector3 dragVel, dragPrev;
        string dropLane;
        int hintLevel;
        string hintState;
        /// <summary>The cards Connie's last hint names, tagged on the board until the board changes.</summary>
        readonly HashSet<string> hinted = new HashSet<string>();
        string hintedState;
        bool hintJumpPending;
        const string IncidentKey = "@incident";

        // ------------------------------------------------------------------ lifecycle

        /// <param name="quiet">Rebuilding the same board (a text size change), so no "case reopened" memo.</param>
        public static CaseSession Begin(Stage stage, CaseDef c, SaveData.Snapshot snap, bool quiet = false)
        {
            var go = new GameObject("CaseSession_" + c.Id);
            var s = go.AddComponent<CaseSession>();
            Current = s;
            s.Init(stage, c, snap, quiet);
            return s;
        }

        public void End()
        {
            if (Current == this) Current = null;
            if (stage) stage.SetNewspaper(null);
            View?.Destroy();
            if (cardsRoot) Destroy(cardsRoot.gameObject);
            if (Memos) Destroy(Memos.gameObject);
            if (Map) Destroy(Map.gameObject);
            HideActions?.Invoke();
            Destroy(gameObject);
        }

        void Init(Stage stage, CaseDef c, SaveData.Snapshot snap, bool quiet)
        {
            this.stage = stage;
            Case = c;
            Board = new Board(c, Locations.Map);
            if (snap != null && snap.caseId == c.Id) Restore(snap);

            View = new BoardView(stage, c);
            Memos = MemoDesk.Create(stage);
            if (snap != null && snap.caseId == c.Id)
                foreach (var m in snap.memoLog) Memos.History.Add(MemoDesk.Memo.Unpack(m));
            Map = MapView.Create(stage, c);
            cardsRoot = new GameObject("Cards").transform;
            cardsRoot.SetParent(stage.transform, false);
            staples = new GameObject("Staples").transform;
            staples.SetParent(cardsRoot, false);

            foreach (var card in c.Cards)
            {
                var v = CardView.Create(card, c, cardsRoot);
                views[card.Id] = v;
                v.gameObject.SetActive(false);
                if (!card.Town && card.Subjects.Count > 1)
                {
                    var list = new List<CardView>();
                    for (int i = 1; i < card.Subjects.Count; i++)
                    {
                        var e = CardView.Create(card, c, cardsRoot);
                        e.name += "_echo_" + card.Subjects[i];
                        e.gameObject.SetActive(false);
                        echoOf[e] = card.Id;
                        list.Add(e);
                    }
                    echoes[card.Id] = list;
                }
                if (Board.Unlocked.Contains(card.Id) && !Board.Pinned.Contains(card.Id)) trayOrder.Add(card.Id);
            }
            incident = CardView.CreateIncident(c, cardsRoot);
            incident.SetCompact(true, false);
            incident.transform.localScale = Vector3.one * 1.25f;
            incident.SetTimes(Board.IncidentFrom, Board.IncidentTo, Board);
            incident.transform.position = stage.BoardToWorld(View.IncidentSlot, 0.05f);
            incident.SetGlow(GlowKind.Incident);

            foreach (var v in views.Values)
            {
                if (!Board.Unlocked.Contains(v.Id)) continue;
                v.gameObject.SetActive(true);
                PlaceInstant(v);
            }
            foreach (var kv in knownConflictsSeed()) knownConflicts.Add(kv);
            Relayout(false);

            if (snap == null || snap.caseId != c.Id)
            {
                Deal();
                // Guarded: a window resized in the first second rebuilds the board and ends this session.
                Tween.Delay(1.1f, () => { if (this) PostCaseMemo("start", null); });
            }
            else if (!quiet) Memos.Post(MemoKind.Notice, "CASE REOPENED", "Right where you left it. Every pin is still in place.");
            else if (Memos.History.Count > 0) Memos.Reshow(Memos.History[Memos.History.Count - 1]);
            AudioDirector.I?.PlayMusic("music_board", 3f);
            stage.SetNewspaper(c.Id);
        }

        IEnumerable<string> knownConflictsSeed() => Board.EstablishedConflicts.Select(k => k.Key);

        void Restore(SaveData.Snapshot s)
        {
            Board.Unlocked.Clear();
            Board.Unlocked.UnionWith(s.unlocked);
            Board.Pinned.UnionWith(s.pinned);
            Board.Calibrated.UnionWith(s.calibrated);
            Board.Struck.UnionWith(s.struck);
            Board.Fired.UnionWith(s.fired);
            for (int i = 0; i < s.confirmedKeys.Count; i++) Board.Confirmed[s.confirmedKeys[i]] = s.confirmedVals[i];
            for (int i = 0; i < s.hypKeys.Count; i++) Board.Hypotheses[s.hypKeys[i]] = s.hypVals[i];
            for (int i = 0; i < s.linkA.Count; i++) Board.Links.Add(new KeyValuePair<string, string>(s.linkA[i], s.linkB[i]));
            Board.Mistakes = s.mistakes;
            Elapsed = s.elapsed;
            UsedHints = s.usedHints;
            seenMemos.UnionWith(s.seenMemos);
            Board.Refresh();
        }

        public void SaveProgress()
        {
            if (Solved) { SaveData.Current.Drop(Case.Id); SaveData.Write(); return; }
            var s = new SaveData.Snapshot { caseId = Case.Id, mistakes = Board.Mistakes, elapsed = Elapsed, usedHints = UsedHints };
            s.unlocked.AddRange(Board.Unlocked);
            s.pinned.AddRange(Board.Pinned);
            s.calibrated.AddRange(Board.Calibrated);
            s.struck.AddRange(Board.Struck);
            s.fired.AddRange(Board.Fired);
            foreach (var kv in Board.Confirmed) { s.confirmedKeys.Add(kv.Key); s.confirmedVals.Add(kv.Value); }
            foreach (var kv in Board.Hypotheses) { s.hypKeys.Add(kv.Key); s.hypVals.Add(kv.Value); }
            foreach (var kv in Board.Links) { s.linkA.Add(kv.Key); s.linkB.Add(kv.Value); }
            s.seenMemos.AddRange(seenMemos);
            foreach (var m in Memos.History) s.memoLog.Add(m.Pack());
            SaveData.Current.Keep(s);   // any other case's board stays on the shelf
            SaveData.Write();
        }

        /// <summary>Cards slide in from the bottom edge of the desk one after another.</summary>
        void Deal()
        {
            int i = 0;
            foreach (var id in trayOrder)
            {
                var v = views[id];
                var target = v.transform.position;
                v.transform.position = target + new Vector3(UnityEngine.Random.Range(-1f, 1f), 2f, -9f);
                float delay = 0.15f + i * 0.09f;
                v.transform.MoveWorld(target, 0.55f, Ease.OutCubic, null, delay);
                Tween.Delay(delay, () => Sfx.Play("paper_deal", 0.45f, 1f, 0.1f));
                i++;
            }
        }

        // ------------------------------------------------------------------ layout

        List<(CardDef card, string lane)> PlacedChips()
        {
            var r = new List<(CardDef, string)>();
            foreach (var card in Case.Cards)
            {
                if (!Board.Unlocked.Contains(card.Id) || !Board.Pinned.Contains(card.Id)) continue;
                if (Board.Struck.Contains(card.Id)) continue;
                if (dragging != null && dragging.Id == card.Id) continue;
                foreach (var lane in Board.LanesOf(card)) r.Add((card, lane));
            }
            return r;
        }

        Vector3 TraySlot(string id, out float rotZ, out int order)
        {
            var tray = trayOrder.Where(t => Board.Unlocked.Contains(t) && !Board.Pinned.Contains(t) && !Board.Struck.Contains(t) && (dragging == null || dragging.Id != t)).ToList();
            int i = tray.IndexOf(id);
            int n = tray.Count;
            order = i;
            var r = stage.Tray;
            if (Board.Struck.Count > 0) r = Rect.MinMaxRect(r.xMin + 3.3f, r.yMin, r.xMax, r.yMax);
            float w = CardView.FullSize.x;
            bool twoRows = n > 7;
            int perRow = twoRows ? Mathf.CeilToInt(n / 2f) : n;
            int row = twoRows ? i / perRow : 0;
            int col = twoRows ? i % perRow : i;
            int rowCount = twoRows ? (row == 0 ? perRow : n - perRow) : n;
            float span = r.width - w;
            float step = rowCount > 1 ? Mathf.Min(w + 0.2f, span / (rowCount - 1)) : 0;
            float total = step * (rowCount - 1);
            float x = r.center.x - total / 2 + col * step + (twoRows && row == 1 ? step * 0.5f : 0);
            float y = twoRows ? r.center.y + (row == 0 ? 0.62f : -0.62f) : r.center.y;
            int h = id.GetHashCode();
            rotZ = ((h & 0xFF) / 255f - 0.5f) * 5f;
            float lift = 0.05f + i * 0.09f + (twoRows && row == 1 ? 0.6f : 0);
            return stage.DeskToWorld(new Vector2(x, y), lift);
        }

        /// <summary>Struck statements come off the timeline and go on the spike, stamped.</summary>
        Vector3 SpikeSlot(string id, out Quaternion rot)
        {
            var struck = Case.Cards.Where(c => Board.Struck.Contains(c.Id)).Select(c => c.Id).ToList();
            int k = Mathf.Max(0, struck.IndexOf(id));
            var r = stage.Tray;
            int h = id.GetHashCode();
            rot = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, ((h & 0xFF) / 255f - 0.5f) * 16f);
            return stage.DeskToWorld(new Vector2(r.xMin + 1.55f + k * 0.12f, r.center.y + 0.15f - k * 0.1f), 0.25f + k * 0.12f);
        }

        void PlaceSpike()
        {
            if (stage.Spike == null) return;
            var r = stage.Tray;
            bool any = Board.Struck.Count > 0;
            var target = stage.DeskToWorld(new Vector2(r.xMin + 1.55f, r.center.y + 0.15f), 0);
            target.y = 0;
            stage.Spike.SetActive(any);
            stage.Spike.transform.position = target;
        }

        readonly HashSet<string> justStruck = new HashSet<string>();

        void PlaceInstant(CardView v) => Place(v, false);

        void Place(CardView v, bool animate, float dur = 0.38f)
        {
            if (v == dragging) return;
            var card = v.Def;
            bool pinned = Board.Pinned.Contains(card.Id) && Board.Unlocked.Contains(card.Id);
            Vector3 target;
            Quaternion rot;
            if (Board.Struck.Contains(card.Id))
            {
                target = SpikeSlot(card.Id, out rot);
                float delay = justStruck.Remove(card.Id) ? 1.0f : 0f;
                if (animate)
                {
                    Tween.Delay(delay, () =>
                    {
                        if (!v) return;
                        v.SetCompact(false, true);
                        v.transform.MoveWorld(target, 0.55f, Ease.InOutCubic);
                        v.transform.RotateLocal(rot, 0.5f, Ease.OutCubic);
                        v.transform.ScaleTo(Vector3.one * 0.62f, 0.5f, Ease.OutCubic);
                        Sfx.Play("paper_slide", 0.35f);
                    }, (v, "spike"));
                }
                else
                {
                    v.SetCompact(false, false);
                    v.transform.SetPositionAndRotation(target, rot);
                    v.transform.localScale = Vector3.one * 0.62f;
                }
                return;
            }
            if (pinned)
            {
                var lanes = Board.LanesOf(card);
                if (lanes.Count == 0 || !chips.TryGetValue(card.Id + "@" + lanes[0], out var place))
                {
                    pinned = false;
                    target = TraySlot(card.Id, out var rz, out _);
                    rot = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, rz);
                }
                else
                {
                    target = stage.BoardToWorld(place.Pos, 0.05f + place.Order * 0.07f);
                    int h = card.Id.GetHashCode();
                    rot = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, ((h >> 8 & 0xFF) / 255f - 0.5f) * 3f);
                }
            }
            else
            {
                target = TraySlot(card.Id, out var rz, out _);
                rot = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, rz);
            }
            v.SetCompact(pinned, animate);
            v.EnableCollider(true);
            var scale = Vector3.one * (pinned ? View.ScaleFor(Board.LanesOf(card)[0]) : 1f);
            if (animate)
            {
                v.transform.MoveWorld(target, dur, Ease.OutCubic);
                v.transform.RotateLocal(rot, dur, Ease.OutCubic);
                v.transform.ScaleTo(scale, dur, Ease.OutCubic);
            }
            else
            {
                Tween.Kill((v.transform, "pos"));
                v.transform.SetPositionAndRotation(target, rot);
                v.transform.localScale = scale;
            }
        }

        void PlaceEchoes(bool animate)
        {
            foreach (var kv in echoes)
            {
                var card = Case.CardById[kv.Key];
                bool pinned = Board.Pinned.Contains(card.Id) && Board.Unlocked.Contains(card.Id) && !Board.Struck.Contains(card.Id) && (dragging == null || dragging.Id != card.Id);
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    var e = kv.Value[i];
                    string lane = card.Subjects[i + 1];
                    bool show = pinned && chips.TryGetValue(card.Id + "@" + lane, out var place);
                    if (!show)
                    {
                        if (e.gameObject.activeSelf && Board.Struck.Contains(card.Id) && animate)
                        {
                            e.SetStruck(true, card.Truth == Logic.Truth.Mistaken ? "MISTAKEN" : "FALSE", false);
                            var ee = e;
                            Tween.Delay(0.9f, () => { if (ee) ee.Body.ScaleTo(Vector3.one * 0.01f, 0.25f, Ease.InBack, () => { if (ee) { ee.gameObject.SetActive(false); ee.Body.localScale = Vector3.one; } }); }, (e, "hide"));
                        }
                        else if (!Tween.Running((e, "hide"))) e.gameObject.SetActive(false);
                        continue;
                    }
                    chips.TryGetValue(card.Id + "@" + lane, out place);
                    bool wasActive = e.gameObject.activeSelf;
                    e.gameObject.SetActive(true);
                    e.SetCompact(true, false);
                    e.transform.localScale = Vector3.one * View.ChipScale;
                    var target = stage.BoardToWorld(place.Pos, 0.05f + place.Order * 0.07f);
                    if (animate && wasActive) e.transform.MoveWorld(target, 0.38f);
                    else e.transform.SetPositionAndRotation(target, Quaternion.Euler(90, 0, 0));
                    if (!wasActive && animate) { e.Body.localScale = Vector3.one * 0.2f; e.Body.ScaleTo(Vector3.one, 0.35f, Ease.OutBack); }
                }
            }
        }

        /// <summary>Recompute everything visible from the rules and move cards where they belong.</summary>
        public void Relayout(bool animate, float dur = 0.38f, string slowClock = null)
        {
            Board.Refresh();
            View.RefreshIncident(Board, animate);
            if (incident) incident.SetTimes(Board.IncidentFrom, Board.IncidentTo, Board);
            chips = View.LayoutChips(Board, PlacedChips());
            foreach (var v in views.Values)
            {
                bool unlocked = Board.Unlocked.Contains(v.Id);
                if (!unlocked) { v.gameObject.SetActive(false); continue; }
                if (!v.gameObject.activeSelf) v.gameObject.SetActive(true);
                bool slow = slowClock != null && v.Def.Clock == slowClock;
                Place(v, animate, slow ? 1.25f : dur);
                v.SetTimes(Board.BoardFrom(v.Def), Board.BoardTo(v.Def), Board);
                v.SetStruck(Board.Struck.Contains(v.Id), v.Def.Truth == Logic.Truth.Mistaken ? "MISTAKEN" : "FALSE", false);
                v.SetHypothesis(v.Def.IsUnknown && !Board.Confirmed.ContainsKey(v.Id) && Board.Pinned.Contains(v.Id));
                v.SetNew(freshCards.Contains(v.Id) && !Board.Pinned.Contains(v.Id));
                if (v.Def.IsUnknown) v.SetCandidates(Candidates(v.Def), animate);
            }
            foreach (var list in echoes.Values)
                foreach (var e in list)
                {
                    e.SetTimes(Board.BoardFrom(e.Def), Board.BoardTo(e.Def), Board);
                    e.SetStruck(Board.Struck.Contains(e.Id), e.Def.Truth == Logic.Truth.Mistaken ? "MISTAKEN" : "FALSE", false);
                }
            PlaceEchoes(animate);
            PlaceSpike();
            if (slowClock != null)
                Tween.Delay(1.3f, () => { if (this) RefreshVisuals(true); }, (this, "slowRefresh"));
            else RefreshVisuals(animate);
        }

        List<string> Candidates(CardDef c)
        {
            if (Board.Confirmed.TryGetValue(c.Id, out var who)) return new List<string> { who };
            return Board.CandidatesLeft.TryGetValue(c.Id, out var l) ? l : new List<string>(c.Candidates);
        }

        void RefreshVisuals(bool animate)
        {
            View.RefreshDynamic(Board, chips, Board.Struck, hover != null ? hover.Id : null);
            View.RefreshClocks(Board);
            var opened = View.RefreshLocks(Board, animate);
            foreach (var lane in opened)
            {
                Sfx.Play("lock_break", 0.9f);
                AudioDirector.I?.Duck(0.5f, 1.5f);
                stage.Shake(0.12f, 0.35f);
            }
            // Strings between multi-person echoes.
            foreach (var kv in echoes)
            {
                var card = Case.CardById[kv.Key];
                if (!Board.Pinned.Contains(card.Id) || Board.Struck.Contains(card.Id)) continue;
                if (!chips.TryGetValue(card.Id + "@" + card.Subjects[0], out var a)) continue;
                for (int i = 1; i < card.Subjects.Count; i++)
                    if (chips.TryGetValue(card.Id + "@" + card.Subjects[i], out var b))
                        View.DrawString(a.Pos + new Vector2(-View.ChipSize.x / 2 + 0.2f, View.ChipSize.y / 2 - 0.1f), b.Pos + new Vector2(-View.ChipSize.x / 2 + 0.2f, View.ChipSize.y / 2 - 0.1f));
            }
            UpdateGlows();
            DrawStaples();
        }

        void DrawStaples()
        {
            for (int i = staples.childCount - 1; i >= 0; i--) Destroy(staples.GetChild(i).gameObject);
            foreach (var v in views.Values)
                foreach (Transform ch in v.Body)
                    if (ch.name == "icon_clip") Destroy(ch.gameObject);
            foreach (var l in Board.Links)
            {
                if (!views.TryGetValue(l.Key, out var a) || !views.TryGetValue(l.Value, out var b)) continue;
                if (!a.Compact || !b.Compact) continue;
                // A little paper clip on each linked card.
                foreach (var v in new[] { a, b })
                {
                    var clip = Shapes.Icon(v.Body, "clip", 0.32f, Pal.Hex("8C96A0"), new Vector3(-CardView.ChipSize.x / 2 + 0.2f, CardView.ChipSize.y / 2 - 0.02f, -0.08f), 12);
                    clip.transform.SetParent(v.Body, true);
                }
            }
        }

        void UpdateGlows()
        {
            var conflictCards = new HashSet<string>();
            foreach (var k in Board.Conflicts) { conflictCards.Add(k.A.Card.Id); conflictCards.Add(k.B.Card.Id); }
            var established = new HashSet<string>();
            foreach (var k in Board.Conflicts) if (k.Established) { established.Add(k.A.Card.Id); established.Add(k.B.Card.Id); }
            foreach (var v in AllViews())
            {
                // The marker lives on the chip face, so it's set even while a just-pinned card is
                // still shrinking into a chip; it shows the moment the chip does.
                v.SetConflict(established.Contains(v.Id) && !Board.Struck.Contains(v.Id));
                GlowKind g = GlowKind.None;
                if (conflictCards.Contains(v.Id)) g = GlowKind.Conflict;
                if (v == selected) g = GlowKind.Selected;
                if (v == linkTarget) g = GlowKind.LinkTarget;
                if (v == hover && g == GlowKind.None && v != dragging) g = GlowKind.Hover;
                v.SetGlow(g);
            }
            bool ready = Board.CheckAccusation(Case.Incident.Culprit).Ok;
            incident.SetGlow(ready ? GlowKind.LinkTarget : (incident == hover ? GlowKind.Hover : GlowKind.Incident));
            UpdateHintTags();
        }

        IEnumerable<CardView> AllViews()
        {
            foreach (var v in views.Values) if (v.gameObject.activeSelf) yield return v;
            foreach (var l in echoes.Values) foreach (var e in l) if (e.gameObject.activeSelf) yield return e;
        }

        CardView Primary(CardView v) => v != null && echoOf.TryGetValue(v, out var id) ? views[id] : v;

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!Solved && !InputLocked && !GameRoot.Paused && ((GameRoot.Attended && !GameRoot.JustBack) || GameRoot.TimerIgnoresFocus)) Elapsed += Clock.Dt;
            if (GameRoot.Paused || InputLocked) { EndHover(); Memos.Hover(false); return; }
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mp = mouse.position.ReadValue();
            stage.SetParallax(new Vector2(mp.x / Screen.width, mp.y / Screen.height));
            bool overUi = UiKit.PointerOverUi();
            debugUpdates++;
            debugOverUi = overUi;
            debugMouse = mp;

            if (dragging != null) { Memos.Hover(false); UpdateDrag(mp, mouse); return; }

            // Hover. (With touch, only while a finger is down.)
            bool fingerUp = PadCursor.FingerUp;
            var hit = overUi || fingerUp ? null : Pick(mp, null);
            if (DebugHoverId != null) hit = DebugHoverId == "incident" ? incident : ViewOf(DebugHoverId);
            // The memo slip, held up to read, lies over the board: the cards under it don't take the pointer.
            bool onMemo = !overUi && !fingerUp && DebugHoverId == null && Memos.Under(mp);
            if (onMemo && Memos.HeldUp) hit = null;
            Memos.Hover(onMemo && hit == null);
            if (hit != hover)
            {
                var old = hover;
                hover = hit;
                hoverTime = 0;
                if (old != null && old != selected && !old.Compact) old.LiftTarget = 0;
                if (hover != null)
                {
                    if (!hover.Compact) hover.LiftTarget = 0.9f;
                    Sfx.Play("paper_touch", 0.18f, 1f, 0.15f);
                }
                UpdateGlows();
            }
            hoverTime += Clock.Dt;
            UpdateInspector();
            bool overMap = (!overUi && !fingerUp && hit == null && Map != null && MapHit(mp)) || DebugMapZoom;
            Map.SetZoom(overMap);
            UpdateRoutes();

            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                pressed = hit;
                pressScreen = mp;
                if (hit == null)
                {
                    Deselect();
                    // A finger may be resting on the memo to read it: it moves on when the finger lifts, unless it was held.
                    if (PadCursor.Using == PadCursor.Pointer.Touch) skipOnLift = true;
                    else Memos.Skip();
                }
            }
            if (skipOnLift && !mouse.leftButton.isPressed)
            {
                skipOnLift = false;
                bool read = PadCursor.TakeHeldToRead();
                if (!read && !Memos.HeldUp) Memos.Skip();
            }
            if (mouse.rightButton.wasPressedThisFrame && !overUi && hit != null)
            {
                var p = Primary(hit);
                if (!p.IsIncident && Board.Pinned.Contains(p.Id) && !Board.Struck.Contains(p.Id) && !Solved) Unpin(p);
            }
            float slop = PadCursor.Using == PadCursor.Pointer.Touch ? PadCursor.TouchSlop : 7f;
            if (pressed != null && mouse.leftButton.isPressed && (mp - pressScreen).magnitude > slop && !Solved && !Board.Struck.Contains(Primary(pressed).Id))
            {
                var p = Primary(pressed);
                pressed = null;
                StartDrag(p, mp);
                return;
            }
            if (pressed != null && mouse.leftButton.wasReleasedThisFrame)
            {
                var p = Primary(pressed);
                pressed = null;
                if (!PadCursor.TakeHeldToRead()) Click(p, mp);
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Memos.Skip();
        }

        bool skipOnLift;

        bool MapHit(Vector2 mp)
        {
            var ray = stage.Cam.ScreenPointToRay(mp);
            foreach (var h in Physics.RaycastAll(ray, 200f))
                if (h.collider.GetComponent<MapView>() != null) return true;
            return false;
        }

        string routeKey;

        /// <summary>Hovering a card shows how its owner got there and where they went next.</summary>
        void UpdateRoutes()
        {
            var v = hover != null ? hover : null;
            string key = v == null ? null : v.Id + "|" + Board.StateKey();
            if (dragging != null) return;
            if (key == routeKey) return;
            routeKey = key;
            Map.Clear();
            if (v == null || v.IsIncident) { if (v != null) Map.Route(Case.Incident.Location, Case.Incident.Location, Pal.Red); return; }
            var card = v.Def;
            if (card.Town) return;
            string lane = echoOf.ContainsKey(v) ? null : null;
            var lanes = Board.LanesOf(card);
            if (lanes.Count == 0 || Board.Struck.Contains(card.Id)) { Map.Route(card.Location, card.Location, Pal.Ribbon); return; }
            lane = lanes[0];
            if (echoOf.ContainsKey(v))
            {
                int idx = echoes[card.Id].IndexOf(v);
                if (idx >= 0 && idx + 1 < card.Subjects.Count) lane = card.Subjects[idx + 1];
            }
            if (!Board.Lanes.TryGetValue(lane, out var evs)) return;
            var me = evs.FirstOrDefault(e => e.Card.Id == card.Id);
            if (me == null) { Map.Route(card.Location, card.Location, Pal.Ribbon); return; }
            var before = evs.Where(e => e != me && e.To <= me.From).OrderByDescending(e => e.To).FirstOrDefault();
            var after = evs.Where(e => e != me && e.From >= me.To).OrderBy(e => e.From).FirstOrDefault();
            Map.Route(me.Location, me.Location, Pal.Ribbon);
            if (before != null && before.Location != me.Location)
            {
                int need = Board.Map.Minutes(before.Location, me.Location), have = me.From - before.To;
                Map.Route(before.Location, me.Location, need > have ? Pal.Red : Pal.Ribbon, need > have ? $"{need} min · only {Mathf.Max(0, have)}" : $"{need} min walk");
            }
            if (after != null && after.Location != me.Location)
            {
                int need = Board.Map.Minutes(me.Location, after.Location), have = after.From - me.To;
                Map.Route(me.Location, after.Location, need > have ? Pal.Red : Pal.Slack, need > have ? $"{need} min · only {Mathf.Max(0, have)}" : $"then {need} min");
            }
        }

        CardView Pick(Vector2 mp, CardView ignore)
        {
            var ray = stage.Cam.ScreenPointToRay(mp);
            var hits = Physics.RaycastAll(ray, 200f);
            CardView best = null;
            float bestH = float.MinValue;
            foreach (var h in hits)
            {
                var v = h.collider.GetComponentInParent<CardView>();
                if (v == null || v == ignore || (ignore != null && Primary(v) == ignore) || !v.gameObject.activeInHierarchy) continue;
                // Highest card wins (closest to the camera).
                float y = h.point.y + v.CurrentLift * 0.1f;
                if (y > bestH) { bestH = y; best = v; }
            }
            return best;
        }

        void EndHover()
        {
            if (hover != null && hover != dragging && !hover.Compact) hover.LiftTarget = 0;
            hover = null;
            HideInspector();
        }

        // ------------------------------------------------------------------ inspector (hovered chips)

        void UpdateInspector()
        {
            // A finger has to rest a moment longer than a mouse, so a quick tap doesn't flash the card.
            float delay = PadCursor.Using == PadCursor.Pointer.Touch ? 0.3f : 0.12f;
            var target = hover != null && hover.Compact && hoverTime > delay && selected == null ? hover : null;
            string id = target != null ? (target.IsIncident ? "incident" : target.Id) : null;
            if (id == inspectingId)
            {
                if (inspecting != null && target != null) PositionInspector(target);
                return;
            }
            HideInspector();
            if (target == null) return;
            inspectingId = id;
            if (!inspectors.TryGetValue(id, out var ins) || ins == null)
            {
                ins = target.IsIncident ? CardView.CreateIncident(Case, cardsRoot) : CardView.Create(target.Def, Case, cardsRoot);
                ins.EnableCollider(false);
                foreach (var r in ins.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ins.name = "inspector_" + id;
                inspectors[id] = ins;
            }
            ins.gameObject.SetActive(true);
            inspecting = ins;
            if (target.IsIncident) ins.SetTimes(Board.IncidentFrom, Board.IncidentTo, Board);
            else
            {
                ins.SetTimes(Board.BoardFrom(target.Def), Board.BoardTo(target.Def), Board);
                ins.SetStruck(Board.Struck.Contains(id), target.Def.Truth == Logic.Truth.Mistaken ? "MISTAKEN" : "FALSE", false);
                if (target.Def.IsUnknown) ins.SetCandidates(Candidates(target.Def), false);
            }
            PositionInspector(target, true);
            Sfx.Play("paper_lift", 0.25f, 1.1f);
        }

        void PositionInspector(CardView chip, bool snap = false)
        {
            var ins = inspecting;
            if (ins == null) return;
            var view = stage.View;
            var chipWorld = chip.transform.position;
            var size = CardView.FullSize;
            float scale = 1.08f * Settings.TextScale;
            // Above the chip if there's room on screen, else below; clamped to the view.
            float up = chipWorld.z + CardView.ChipSize.y / 2 + size.y * scale / 2 + 0.15f;
            float down = chipWorld.z - CardView.ChipSize.y / 2 - size.y * scale / 2 - 0.15f;
            float z = up + size.y * scale / 2 < view.yMax - 0.1f ? up : down;
            float x = Mathf.Clamp(chipWorld.x, view.xMin + size.x * scale / 2 + 0.1f, view.xMax - size.x * scale / 2 - 0.1f);
            var pos = KeepOnScreen(stage.Cam, new Vector3(x, 4.5f, z), size * scale / 2);
            if (snap)
            {
                ins.transform.position = Vector3.Lerp(chipWorld, pos, 0.35f);
                ins.transform.rotation = Quaternion.Euler(90, 0, 0);
                ins.transform.localScale = Vector3.one * 0.4f;
                ins.transform.ScaleTo(Vector3.one * scale, 0.22f, Ease.OutBack);
                ins.transform.MoveWorld(pos, 0.2f, Ease.OutCubic);
            }
            else if (!Tween.Running((ins.transform, "pos"))) ins.transform.position = Vector3.Lerp(ins.transform.position, pos, 0.4f);
            ins.LiftTarget = 0;
        }

        /// <summary>The inspector floats well above the board, so perspective can push it past the frame edge; nudge it back.</summary>
        static Vector3 KeepOnScreen(Camera cam, Vector3 pos, Vector2 half, float margin = 0.012f)
        {
            float height = pos.y;
            for (int i = 0; i < 3; i++)
            {
                var a = cam.WorldToViewportPoint(pos + new Vector3(-half.x, 0, -half.y));
                var b = cam.WorldToViewportPoint(pos + new Vector3(half.x, 0, half.y));
                float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x), minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
                float dx = minX < margin ? margin - minX : (maxX > 1 - margin ? 1 - margin - maxX : 0);
                float dy = minY < margin ? margin - minY : (maxY > 1 - margin ? 1 - margin - maxY : 0);
                if (Mathf.Approximately(dx, 0) && Mathf.Approximately(dy, 0)) break;
                float depth = Vector3.Dot(pos - cam.transform.position, cam.transform.forward);
                var w0 = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
                var w1 = cam.ViewportToWorldPoint(new Vector3(0.5f + dx, 0.5f + dy, depth));
                pos += w1 - w0;
                pos.y = height;
            }
            return pos;
        }

        void HideInspector()
        {
            if (inspecting != null) inspecting.gameObject.SetActive(false);
            inspecting = null;
            inspectingId = null;
        }

        // ------------------------------------------------------------------ click & select

        void Click(CardView v, Vector2 mp)
        {
            if (Solved) return;
            if (v.IsIncident)
            {
                Memos.Post(MemoKind.Notice, "THE INCIDENT", "Drag this card onto the one line it fits. If it fits more than one, or none, keep working.");
                v.Punch(0.08f);
                return;
            }
            bool pinned = Board.Pinned.Contains(v.Id);
            if (Board.Struck.Contains(v.Id))
            {
                v.Punch(0.06f);
                Memos.Post(MemoKind.Notice, "ON THE SPIKE", $"“{v.Def.Title}” was struck off. It's no longer part of the timeline.");
                return;
            }
            if (!pinned)
            {
                // Click-to-pin for cards that know where they go.
                if (!v.Def.IsUnknown) { PinCard(v, null); return; }
                Select(v, mp);
                Memos.Post(MemoKind.Notice, "WHO WAS IT?", "This card doesn't say who. Drag it onto a line to try it there. The faces on it cross out as the paper rules people out.");
                return;
            }
            Select(v, mp);
        }

        void Select(CardView v, Vector2 mp)
        {
            if (selected != null && selected != v) selected.LiftTarget = 0;
            selected = v;
            v.Punch(0.06f, 0.25f);
            Sfx.Play("paper_touch", 0.35f);
            UpdateGlows();
            ShowActions?.Invoke(v, stage.WorldToScreen(v.transform.position));
        }

        public void Deselect()
        {
            if (selected == null) return;
            selected = null;
            HideActions?.Invoke();
            UpdateGlows();
        }

        public CardView Selected => selected;

        /// <summary>The card whose full text is showing beside the pointer, if any (for the touch test).</summary>
        public string InspectingId => inspectingId;

        /// <summary>A link is armed: the dragged card will be linked to this one if it's dropped now.</summary>
        public CardView LinkTarget => dragging != null ? linkTarget : null;

        // ------------------------------------------------------------------ drag

        void StartDrag(CardView v, Vector2 mp)
        {
            Deselect();
            HideInspector();
            dragging = v;
            linkAim = null;
            linkAimTime = 0;
            v.EnableCollider(false);
            v.LiftTarget = 0;
            dragPrev = v.transform.position;
            dragVel = Vector3.zero;
            Sfx.Play("paper_lift", 0.6f);
            if (!v.IsIncident && Board.Pinned.Contains(v.Id))
            {
                // Lift it off the board: the rest of the lane re-flows without it.
                Relayout(true);
            }
            v.transform.SetAsLastSibling();
        }

        void UpdateDrag(Vector2 mp, Mouse mouse)
        {
            var v = dragging;
            const float dragHeight = 3.2f;
            stage.MouseOnPlane(mp, Stage.BoardHeight + dragHeight, out var world);
            var cur = v.transform.position;
            var next = Vector3.Lerp(cur, world, 1 - Mathf.Exp(-Clock.Dt * 22f));
            dragVel = Vector3.Lerp(dragVel, (next - cur) / Mathf.Max(0.001f, Clock.Dt), 0.2f);
            v.transform.position = next;
            float tiltX = Mathf.Clamp(dragVel.z * 0.9f, -14, 14);
            float tiltZ = Mathf.Clamp(-dragVel.x * 0.9f, -14, 14);
            v.transform.rotation = Quaternion.Euler(90 + tiltX, 0, 0) * Quaternion.Euler(0, tiltZ * 0.6f, -dragVel.x * 0.25f);

            var local = stage.MouseBoard(mp);
            bool onBoard = View.OnBoard(local);
            View.ClearPreview();
            var oldLink = linkTarget;
            linkTarget = null;
            dropLane = null;

            if (v.IsIncident)
            {
                var lane = onBoard ? View.LaneAt(local) : null;
                Map.Clear();
                routeKey = null;
                if (lane != null && lane.LockRoot != null)
                {
                    dropLane = lane.Id;
                    View.PreviewIncident(Board, lane.Id);
                    var fit = Board.Fits[lane.Id];
                    var inc = Case.Incident;
                    var evs = Board.Lanes[lane.Id].Where(e => !e.Hypothesis).ToList();
                    int s0 = fit.Fits ? fit.EarliestStart : Board.IncidentFrom;
                    var before = evs.Where(e => e.To <= s0).OrderByDescending(e => e.To).FirstOrDefault();
                    var after = evs.Where(e => e.From >= s0 + inc.Duration).OrderBy(e => e.From).FirstOrDefault();
                    Map.Route(inc.Location, inc.Location, Pal.Red);
                    if (before != null) Map.Route(before.Location, inc.Location, fit.Fits ? Pal.Red : Pal.Green, $"{Board.Map.Minutes(before.Location, inc.Location)} min");
                    if (after != null) Map.Route(inc.Location, after.Location, fit.Fits ? Pal.Red : Pal.Green, $"{Board.Map.Minutes(inc.Location, after.Location)} min");
                }
            }
            else
            {
                var target = Primary(Pick(mp, v));
                var aim = target != null && target != v && !target.IsIncident ? target : null;
                if (aim != linkAim) { linkAim = aim; linkAimTime = 0; }
                else if (aim != null) linkAimTime += Clock.Dt;
                if (aim != null && linkAimTime >= LinkArmSeconds) linkTarget = aim;
                if (linkTarget == null && onBoard)
                {
                    var lane = View.LaneAt(local);
                    var card = v.Def;
                    if (card.Town) dropLane = Board.TownLane;
                    else if (!card.IsUnknown) dropLane = card.Subjects[0];
                    else if (Board.Confirmed.TryGetValue(card.Id, out var who)) dropLane = who;
                    else if (lane != null && card.Candidates.Contains(lane.Id)) dropLane = lane.Id;
                    if (dropLane != null)
                    {
                        View.HighlightLane(dropLane, Pal.Lamp);
                        if (!card.Town && !card.IsUnknown)
                            foreach (var s in card.Subjects) View.PreviewMarker(s, Board.BoardFrom(card), Board.BoardTo(card), Pal.Lamp);
                        else View.PreviewMarker(dropLane, Board.BoardFrom(card), Board.BoardTo(card), Pal.Lamp);
                    }
                }
                bool wantCompact = onBoard && linkTarget == null;
                if (v.Compact != wantCompact) v.SetCompact(wantCompact, true);
                var wantScale = Vector3.one * (wantCompact ? View.ChipScale : 1f);
                v.transform.localScale = Vector3.Lerp(v.transform.localScale, wantScale, 1 - Mathf.Exp(-Clock.Dt * 12f));
            }
            if (oldLink != linkTarget)
            {
                if (linkTarget != null) Sfx.Play("paper_touch", 0.4f, 1.3f);
                UpdateGlows();
            }

            if (!mouse.leftButton.isPressed) EndDrag();
        }

        void EndDrag()
        {
            var v = dragging;
            dragging = null;
            View.ClearPreview();
            v.EnableCollider(true);
            var link = linkTarget;
            linkTarget = null;
            linkAim = null;
            if (v.IsIncident)
            {
                if (dropLane != null) Accuse(dropLane);
                else ReturnIncident();
                return;
            }
            if (link != null) { LinkCards(v, link); return; }
            if (dropLane != null) { PinCard(v, dropLane); return; }
            // Dropped on the desk: back to the tray.
            if (Board.Pinned.Contains(v.Id)) Unpin(v);
            else { Sfx.Play("paper_drop", 0.4f); Relayout(true); }
        }

        void ReturnIncident()
        {
            incident.transform.MoveWorld(stage.BoardToWorld(View.IncidentSlot, 0.05f), 0.4f, Ease.OutBack);
            incident.transform.RotateLocal(Quaternion.Euler(90, 0, 0), 0.3f);
            incident.SetCompact(true, true);
            Sfx.Play("paper_drop", 0.4f);
        }

        // ------------------------------------------------------------------ actions

        void PinCard(CardView v, string lane)
        {
            Deselect();
            var before = ConflictKeys();
            bool wasPinned = Board.Pinned.Contains(v.Id);
            var o = Board.PinAndSettle(v.Id, lane);
            SaveData.Learn("pin");
            freshCards.Remove(v.Id);
            Relayout(true);
            // Landing: pin thunk, dust and a little punch.
            Tween.Delay(0.3f, () =>
            {
                if (!v) return;
                Sfx.Play("pin", 0.75f);
                v.Punch(0.12f, 0.3f);
                Fx.Dust(v.transform.position);
            });
            AfterAction(o, before, v);
            if (!wasPinned && !seenMemos.Contains("firstPin")) PostCaseMemo("firstPin", null);
            if (v.Def.IsUnknown && !Board.Confirmed.ContainsKey(v.Id))
                Memos.Post(MemoKind.Notice, "A HUNCH", "Pinned there on a hunch. It counts as a guess, not evidence, until the paper rules everyone else out.");
            SaveProgress();
        }

        public void Unpin(CardView v)
        {
            Deselect();
            if (!trayOrder.Contains(v.Id)) trayOrder.Add(v.Id);
            Board.Unpin(v.Id);
            Sfx.Play("paper_lift", 0.5f, 0.9f);
            Relayout(true);
            SaveProgress();
        }

        void LinkCards(CardView a, CardView b)
        {
            Deselect();
            var before = ConflictKeys();
            var o = Board.Link(a.Id, b.Id);
            SaveData.Learn("link");
            if (o.Accepted && !Board.Pinned.Contains(a.Id) && !a.Def.IsUnknown) Board.Pin(a.Id);
            if (o.CalibratedClock != null)
            {
                // The trailer moment: every card on that clock glides to its true time.
                var clock = Case.ClockById[o.CalibratedClock];
                Sfx.Play("link_clip", 0.9f);
                AudioDirector.I?.Duck(0.45f, 2.2f);
                Tween.Delay(0.25f, () => Sfx.Play("clock_ratchet", 0.75f));
                stage.Shake(0.05f, 0.2f);
                Relayout(true, 0.38f, o.CalibratedClock);
                foreach (var v in AllViews().Where(x => x.Def.Clock == o.CalibratedClock)) Tween.Delay(1.2f, () => { if (v) v.Punch(0.08f, 0.25f); });
                int m = Mathf.Abs(o.Shift);
                Memos.Post(MemoKind.Notice, "CLOCK CORRECTED",
                    $"{clock.Name}: {m} minutes {(o.Shift < 0 ? "fast" : "slow")}. Every card timed by it has moved {m} minutes {(o.Shift < 0 ? "earlier" : "later")}." + (string.IsNullOrEmpty(clock.Note) ? "" : "\n\n" + clock.Note));
                Tween.Delay(1.35f, () => { if (this) AfterAction(o, before, a); });
            }
            else if (o.CostBadge)
            {
                Sfx.Play("wrong", 0.8f);
                a.Body.Shake(0.15f, 0.4f);
                b.Body.Shake(0.1f, 0.35f);
                Memos.Post(MemoKind.Notice, "NOT THE SAME MOMENT", "Those two cards describe different things. Linking cards claims they're one moment seen on two clocks.");
                // Why that link couldn't work, by the two clocks (the lesson the badge paid for, not a hint).
                Memos.Post(MemoKind.Connie, null, Board.WhyNotLinked(a.Id, b.Id));
                BadgeLost?.Invoke();
                Relayout(true);
            }
            else
            {
                if (o.Accepted) Sfx.Play("link_clip", 0.6f);
                else Sfx.Play("nope", 0.5f);
                Memos.Post(MemoKind.Notice, o.Accepted ? "LINKED" : "HMM", o.Message);
                Relayout(true);
            }
            SaveProgress();
        }

        public void Confront(CardView v)
        {
            if (!Board.CanConfront(v.Id, out var reason))
            {
                Sfx.Play("nope", 0.5f);
                Memos.Post(MemoKind.Notice, "NOT YET", reason);
                return;
            }
            Deselect();
            var before = ConflictKeys();
            var o = Board.Confront(v.Id);
            SaveData.Learn("confront");
            var who = v.Def.Title;
            InputLocked = true;
            v.Body.Shake(0.08f, 0.5f, 30f);
            Sfx.Play("confront", 0.7f);
            Tween.Delay(0.55f, () =>
            {
                InputLocked = false;
                if (!this) return;
                if (o.Accepted)
                {
                    string label = v.Def.Truth == Logic.Truth.Mistaken ? "MISTAKEN" : "FALSE";
                    justStruck.Add(v.Id);
                    v.SetStruck(true, label, true);
                    if (echoes.TryGetValue(v.Id, out var list)) foreach (var e in list) e.SetStruck(true, label, true);
                    Sfx.Play("stamp", 0.95f);
                    stage.Shake(0.09f, 0.22f);
                    Fx.Ink(v.transform.position);
                    Memos.Post(MemoKind.Witness, who, o.Reply);
                    Relayout(true);
                    AfterAction(o, before, v);
                    if (!seenMemos.Contains("firstStrike")) PostCaseMemo("firstStrike", null);
                }
                else if (o.CostBadge)
                {
                    Sfx.Play("firm", 0.85f);
                    v.Body.Shake(0.2f, 0.45f, 20f);
                    Memos.Post(MemoKind.Firm, who, o.Reply);
                    // What made a true story red: the lesson the badge just paid for (not a hint).
                    Memos.Post(MemoKind.Connie, null, Board.WhyFirm(v.Id));
                    BadgeLost?.Invoke();
                    Relayout(true);
                }
                SaveProgress();
            });
        }

        void Accuse(string lane)
        {
            var chk = Board.CheckAccusation(lane);
            if (!chk.Ok)
            {
                Sfx.Play("nope", 0.7f);
                incident.Body.Shake(0.15f, 0.35f);
                Memos.Post(MemoKind.Notice, "NOT YET", chk.Message);
                ReturnIncident();
                return;
            }
            Board.Accuse(lane);
            SaveData.Learn("accuse");
            Memos.Clear();   // the reconstruction has the floor; no stray slip behind the case-closed panel
            InputLocked = true;
            Deselect();
            var fit = Board.Fits[lane];
            var laneView = View.LaneById[lane];
            var slot = new Vector2(View.TimeToX(fit.EarliestStart + Case.Incident.Duration / 2f), laneView.Track + 0.55f);
            incident.SetCompact(true, true);
            incident.transform.MoveWorld(stage.BoardToWorld(slot, 0.08f), 0.35f, Ease.OutBack);
            incident.transform.RotateLocal(Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, -4), 0.3f);
            View.PreviewIncident(Board, lane);
            Tween.Delay(0.35f, () =>
            {
                if (!this) return;
                Sfx.Play("pin", 1f, 0.8f);
                Sfx.Play("lock_break", 0.8f);
                stage.Shake(0.14f, 0.3f);
                incident.Punch(0.2f, 0.4f);
                Fx.Dust(incident.transform.position);
            });
            SaveData.Current.Drop(Case.Id);
            SaveData.Write();
            Tween.Delay(1.1f, () => SolvedEvent?.Invoke(this));
        }

        HashSet<string> ConflictKeys() => new HashSet<string>(Board.EstablishedConflicts.Select(k => k.Key));

        /// <summary>Shared feedback for whatever an action changed.</summary>
        void AfterAction(Outcome o, HashSet<string> conflictsBefore, CardView actor)
        {
            if (!this) return;
            // New contradictions.
            var now = ConflictKeys();
            var fresh = now.Where(k => !conflictsBefore.Contains(k) && !knownConflicts.Contains(k)).ToList();
            if (fresh.Count > 0)
            {
                Tween.Delay(0.35f, () =>
                {
                    if (!this) return;
                    Sfx.Play("conflict", 0.75f);
                    stage.Shake(0.07f, 0.25f);
                });
                foreach (var k in fresh) knownConflicts.Add(k);
                if (!seenMemos.Contains("firstConflict")) PostCaseMemo("firstConflict", "Red means impossible. Records don't lie, but people do. Click a statement in the red and confront them.");
            }
            else if (conflictsBefore.Count > now.Count)
            {
                Tween.Delay(0.3f, () => Sfx.Play("resolve", 0.6f));
            }
            foreach (var q in o.Questions)
            {
                // A contradiction's question is moot once the board has cleared it (a link, a confrontation).
                var t = Case.Triggers.FirstOrDefault(x => x.Question == q && x.Kind == TriggerKind.Conflict);
                Memos.Post(MemoKind.Question, null, q, t == null ? null : (System.Func<bool>)(() => !Board.EstablishedConflicts.Any(k => k.IsPair(t.A, t.B))));
            }
            foreach (var m in o.Memos) Memos.Post(MemoKind.Connie, null, m);
            foreach (var id in o.Confirmed)
            {
                var v = views[id];
                var who = Board.Confirmed[id];
                Tween.Delay(0.4f, () =>
                {
                    if (!v) return;
                    v.Stamp("ONLY " + Portraits.FirstName(Case, who).ToUpperInvariant(), Pal.Hex("2F6B4F"), true);
                    Sfx.Play("stamp", 0.8f, 1.15f);
                    Tween.Delay(1.6f, () => { if (v && !Board.Struck.Contains(v.Id)) v.ClearStamp(); });
                });
            }
            if (o.NewCards.Count > 0)
            {
                foreach (var id in o.NewCards)
                {
                    if (!trayOrder.Contains(id)) trayOrder.Add(id);
                    freshCards.Add(id);
                    if (Case.CardById[id].IsUnknown && !seenMemos.Contains("unknown")) PostCaseMemo("unknown", null);
                }
                Tween.Delay(0.5f, () =>
                {
                    if (!this) return;
                    Relayout(true);
                    int i = 0;
                    foreach (var id in o.NewCards)
                    {
                        var v = views[id];
                        // Its tray slot, not where Relayout's tween is starting it from: two cards
                        // unlocked together would otherwise land on the same spot.
                        var target = Board.Pinned.Contains(id) ? v.transform.position : TraySlot(id, out _, out _);
                        v.transform.position = target + new Vector3(0, 2f, -8f);
                        v.transform.MoveWorld(target, 0.6f, Ease.OutCubic, null, i * 0.12f);
                        Tween.Delay(i * 0.12f, () => Sfx.Play("paper_deal", 0.6f));
                        i++;
                    }
                    Memos.Post(MemoKind.Notice, o.NewCards.Count == 1 ? "NEW EVIDENCE" : "NEW EVIDENCE ×" + o.NewCards.Count,
                        string.Join("\n", o.NewCards.Select(id => "• " + NewCardLine(Case.CardById[id]))));
                });
            }
            CheckMilestones();
        }

        void CheckMilestones()
        {
            bool trayEmpty = !Board.TrayCards.Any();
            bool clean = trayEmpty && Board.CandidatesLeft.Count == 0 && !Board.EstablishedConflicts.Any();
            var fits = Board.FittingLanes();
            if (trayEmpty && !Board.EstablishedConflicts.Any() && fits.Count == 0 && !seenMemos.Contains("allCovered"))
                PostCaseMemo("allCovered", "Everyone's covered. Then somebody's cover is false. Look harder at the cards holding the alibis up.");
            if (clean && fits.Count == 1 && !seenMemos.Contains("incidentHint"))
                PostCaseMemo("incidentHint", "Only one alibi has a hole in it. Drag the incident card (top left) into that line.");
        }

        /// <summary>"Card authorisation (Rolf Abernethy) — Wrenhaven Savings Bank": name who it's about when the title alone doesn't.</summary>
        string NewCardLine(CardDef c)
        {
            string who = "";
            var names = c.Subjects.Where(id => Case.PersonById.ContainsKey(id)).Select(id => Case.PersonById[id].Name).ToList();
            if (c.IsRecord && !c.Town && names.Count > 0 && !c.Title.Contains(names[0].Split(' ').Last()))
                who = " (" + string.Join(", ", names) + ")";
            return c.Title + who + " — " + c.SourceName;
        }

        void PostCaseMemo(string when, string fallback)
        {
            if (seenMemos.Contains(when)) return;
            seenMemos.Add(when);
            var m = Case.Memos.FirstOrDefault(x => x.When == when);
            var text = m != null ? m.Text : fallback;
            if (!string.IsNullOrEmpty(text)) Memos.Post(MemoKind.Connie, null, text);
        }

        // ------------------------------------------------------------------ controls strip

        /// <summary>
        /// The one gesture worth reminding the player of right now, or null. Each tip retires for good
        /// once the player has used that gesture (SaveData.learned).
        /// </summary>
        public string CoachTip() => PadCursor.Label(CoachTipFor());

        string CoachTipFor()
        {
            if (Solved || Board == null) return null;
            var using_ = PadCursor.Using;
            bool pad = using_ == PadCursor.Pointer.Pad, keys = using_ == PadCursor.Pointer.Keys, touch = using_ == PadCursor.Pointer.Touch;
            if (!SaveData.Learned("pin") && Board.TrayCards.Any())
                return touch ? "<b>Drag</b> a card onto the board, or <b>tap</b> it   ·   <b>press and hold</b> any card to read it in full"
                     : pad ? "<b>[A]</b> on a card pins it   ·   hold <b>[A]</b> and steer to drag   ·   <b>[LB] [RB]</b> jump between cards"
                     : keys ? "<b>Enter</b> on a card pins it   ·   hold <b>Enter</b> and use the <b>arrows</b> to drag   ·   <b>Q</b> / <b>E</b> jump between cards"
                           : "<b>Drag</b> a card onto the board, or <b>click</b> it   ·   <b>hover</b> any card to read it in full";
            if (!SaveData.Learned("confront") && Board.EstablishedConflicts.Any(k => k.A.Card.IsTestimony || k.B.Card.IsTestimony))
                return touch ? "<b>Tap</b> a statement in the red, then <b>Confront</b> the witness"
                     : pad ? "Point at a statement in the red, <b>[A]</b>, then <b>Confront</b> the witness"
                     : keys ? "Move onto a statement in the red, <b>Enter</b>, then <b>Confront</b> the witness"
                           : "<b>Click</b> a statement in the red, then <b>Confront</b> the witness";
            if (!SaveData.Learned("link") && Board.UnlockedCards.Any(c => !Board.IsTrusted(c.Clock)))
                return pad ? "One moment on two clocks? Hold <b>[A]</b> on one card, steer it <b>onto the other</b> and wait for <b>LINK</b>"
                     : keys ? "One moment on two clocks? Hold <b>Enter</b> on one card, steer it <b>onto the other</b> and wait for <b>LINK</b>"
                           : "One moment on two clocks? Drag one card <b>onto the other</b> and hold it there until it says <b>LINK</b>";
            if (!SaveData.Learned("accuse") && Board.CheckAccusation(Case.Incident.Culprit).Ok)
                return pad ? "Hold <b>[A]</b> on the <b>incident card</b> (top left) and drop it on the one line it fits"
                     : keys ? "Hold <b>Enter</b> on the <b>incident card</b> (top left) and steer it onto the one line it fits"
                           : "Drag the <b>incident card</b> (top left) onto the one line it fits";
            return null;
        }

        // ------------------------------------------------------------------ hints

        public void Hint()
        {
            UsedHints = true;
            string state = HintBoardKey();
            if (state != hintState) { hintState = state; hintLevel = 0; }
            hintLevel++;
            PointAt(null);
            var tray = Board.TrayCards.ToList();
            if (tray.Count > 0 && tray.Any(x => !x.IsUnknown))
            {
                Memos.Post(MemoKind.Connie, null, $"Get everything on the board first. {tray.Count} card{(tray.Count == 1 ? " is" : "s are")} still in the tray. Click one to pin it.");
                return;
            }
            var shadow = Solver.Shadow(Board);
            var path = Solver.ShortestSolution(shadow);
            if (path == null)
            {
                Memos.Post(MemoKind.Connie, null, "Something on the board is a guess. Take any hunches back to the tray and let the paper decide.");
                PointAt(AllViews().Where(v => v.Hypothesis).Select(v => v.Id));
                return;
            }
            if (path.Count == 0)
            {
                Memos.Post(MemoKind.Connie, null, "It's all there. One suspect's lock is open. Drag the incident card (top left) into that line.");
                PointAt(new[] { IncidentKey });
                return;
            }
            var m = path[0];
            if (m.Kind == "link")
            {
                var a = Case.CardById[m.A];
                var b = Case.CardById[m.B];
                var untrusted = Board.IsTrusted(a.Clock) ? b : a;
                if (hintLevel == 1)
                {
                    Memos.Post(MemoKind.Connie, null, $"Somebody's clock is wrong. Look at cards timed by {Case.ClockById[untrusted.Clock].InSentence}. Is one of them the same moment as a card on a reliable clock?");
                    PointAt(OnBoard().Where(x => x.Clock == untrusted.Clock).Select(x => x.Id));
                }
                else
                {
                    Memos.Post(MemoKind.Connie, null, $"“{a.Title}” and “{b.Title}” are the same moment. Drag one onto the other and hold it there until it says LINK.");
                    PointAt(new[] { a.Id, b.Id });
                }
            }
            else
            {
                var c = Case.CardById[m.A];
                if (hintLevel == 1)
                    Memos.Post(MemoKind.Connie, null, "One of the statements in the red can't be true. Which one is the paper against?");
                else
                {
                    Memos.Post(MemoKind.Connie, null, $"{c.Title}'s statement doesn't hold up. Click it and confront them.");
                    PointAt(new[] { c.Id });
                }
            }
        }

        string HintBoardKey() => Board.StateKey() + "|" + string.Join(",", Board.Pinned.OrderBy(x => x));

        /// <summary>Cards on the board right now: pinned, unlocked and not struck.</summary>
        IEnumerable<CardDef> OnBoard() => Board.UnlockedCards.Where(x => Board.Pinned.Contains(x.Id) && !Board.Struck.Contains(x.Id));

        /// <summary>Tag the cards a hint names (the incident card as IncidentKey) until the board changes.</summary>
        void PointAt(IEnumerable<string> ids)
        {
            hinted.Clear();
            if (ids != null) hinted.UnionWith(ids);
            hintedState = HintBoardKey();
            hintJumpPending = hinted.Count > 0;
            if (hinted.Count > 0) Debug.Log("[Hint] points at " + string.Join(", ", hinted.OrderBy(x => x)));
            UpdateHintTags();
        }

        void UpdateHintTags()
        {
            if (hinted.Count > 0 && HintBoardKey() != hintedState) { hinted.Clear(); hintJumpPending = false; }
            foreach (var v in AllViews()) v.SetHinted(hinted.Contains(v.Id) && !Board.Struck.Contains(v.Id));
            if (incident) incident.SetHinted(hinted.Contains(IncidentKey) && !Solved);
        }

        /// <summary>Every card view wearing a hint tag (two-person statements have one per line).</summary>
        public IEnumerable<CardView> TaggedViews => AllViews().Where(v => v.Hinted).Concat(incident && incident.Hinted ? new[] { incident } : new CardView[0]);

        /// <summary>The ids the hint tags are on right now, as drawn (the incident card as "@incident").</summary>
        public SortedSet<string> HintTagged()
        {
            var r = new SortedSet<string>(AllViews().Where(v => v.Hinted).Select(v => v.Id));
            if (incident && incident.Hinted) r.Add(IncidentKey);
            return r;
        }

        /// <summary>
        /// The pad's or keyboard's next jump after a hint goes to the first card it names (once), so
        /// nobody has to hunt for it with a cursor. Screen point, or null.
        /// </summary>
        public Vector2? TakeHintJump()
        {
            if (!hintJumpPending) return null;
            hintJumpPending = false;
            var v = hinted.Contains(IncidentKey) ? incident : AllViews().Where(x => x.Hinted && x.Compact).OrderByDescending(x => x.transform.position.y).ThenBy(x => x.transform.position.x).FirstOrDefault();
            return v ? (Vector2?)(Vector2)stage.Cam.WorldToScreenPoint(v.transform.position) : null;
        }

        // ------------------------------------------------------------------ automation (autopilot & tests)

        public CardView ViewOf(string id) => views.TryGetValue(id, out var v) ? v : null;

        /// <summary>
        /// Screen points for the pad's LB/RB jumps, in reading order: chips lane by lane, the
        /// incident card, then the tray left to right (aimed at each card's uncovered left edge).
        /// </summary>
        public List<Vector2> CursorTargets()
        {
            var cam = stage.Cam;
            var r = new List<Vector2>();
            var chipsOnBoard = AllViews().Where(v => v.Compact && !v.IsIncident && Board.Pinned.Contains(Primary(v).Id) && !Board.Struck.Contains(v.Id))
                .Select(v => (Vector2)cam.WorldToScreenPoint(v.transform.position)).ToList();
            chipsOnBoard.Sort((a, b) => Mathf.Abs(a.y - b.y) > 20 ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
            r.AddRange(chipsOnBoard);
            if (!Solved) r.Add(cam.WorldToScreenPoint(incident.transform.position));
            var tray = views.Values.Where(v => v.gameObject.activeSelf && !v.Compact && Board.Unlocked.Contains(v.Id) && !Board.Pinned.Contains(v.Id) && !Board.Struck.Contains(v.Id))
                .Select(v => (Vector2)cam.WorldToScreenPoint(v.transform.TransformPoint(new Vector3(-CardView.FullSize.x / 2 + 0.45f, 0.3f, 0))))
                .OrderBy(p => p.x);
            r.AddRange(tray);
            return r;
        }

        /// <summary>Smallest on-screen em heights (time, place, source) over every chip on the board.</summary>
        public string LegibilityReport()
        {
            float t = float.MaxValue, l = float.MaxValue, w = float.MaxValue;
            string tId = "", lId = "", wId = "";
            int n = 0;
            foreach (var v in AllViews())
            {
                if (!v.Compact || v.IsIncident || Board.Struck.Contains(v.Id)) continue;
                var e = v.ChipEmPixels(stage.Cam);
                if (e.x > 0 && e.x < t) { t = e.x; tId = v.Id; }
                if (e.y > 0 && e.y < l) { l = e.y; lId = v.Id; }
                if (e.z > 0 && e.z < w) { w = e.z; wId = v.Id; }
                n++;
            }
            return n == 0 ? "no chips" : $"time {t:0.0} px ({tId}), place {l:0.0} px ({lId}), source {w:0.0} px ({wId}); em, min over {n} chips at {Screen.width}x{Screen.height}, text size {Settings.TextSizeNames[Settings.TextSize]}, chip scale {View.ChipScale:0.00}, lane {View.Lanes[0].Height:0.00} track +{View.Lanes[0].Track - View.Lanes[0].Bottom:0.00}";
        }
        /// <summary>
        /// Chips showing the contradiction marker against the cards in established contradictions
        /// (pinned, not struck). They must match; the autopilot logs and checks this.
        /// </summary>
        public string ConflictReport(out bool match)
        {
            var expected = new SortedSet<string>();
            foreach (var k in Board.Conflicts)
                if (k.Established)
                    foreach (var id in new[] { k.A.Card.Id, k.B.Card.Id })
                        if (!Board.Struck.Contains(id)) expected.Add(id);
            var marked = new SortedSet<string>(AllViews().Where(v => v.ConflictMarked).Select(v => v.Id));
            match = expected.SetEquals(marked);
            return $"marked [{string.Join(",", marked)}] expected [{string.Join(",", expected)}]";
        }

        /// <summary>
        /// Text drawn over other text or icons on the board right now: chips, the cards in the tray and
        /// the clock legend against the ruler. Empty when nothing collides; the autopilot checks this.
        /// </summary>
        public List<string> CollisionReport(out int checkedCards)
        {
            var found = new List<string>();
            checkedCards = 0;
            foreach (var v in AllViews().Concat(new[] { incident }))
            {
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                checkedCards++;
                var c = v.Collisions();
                if (c != null) found.Add(c);
            }
            var legend = View.LegendCollision();
            if (legend != null) found.Add(legend);
            return found;
        }

        public CardView IncidentView => incident;
        public void AutoPin(string id) { var v = views[id]; if (!Board.Pinned.Contains(id)) PinCard(v, null); }
        public void AutoLink(string a, string b) => LinkCards(views[a], views[b]);
        public void AutoAccuse(string lane) => Accuse(lane);

        /// <summary>Headless testing: pretend the mouse is over this card / the map.</summary>
        public string DebugHoverId;
        public bool DebugMapZoom;
        int debugUpdates;
        bool debugOverUi;
        Vector2 debugMouse;
        public string DebugState => $"updates={debugUpdates} overUi={debugOverUi} mouse={debugMouse} hover={(hover ? hover.Id : "null")} drag={(dragging ? dragging.Id : "null")} locked={InputLocked}";

        public bool CanConfrontSelected(out string reason)
        {
            reason = null;
            if (selected == null || selected.IsIncident) return false;
            return Board.CanConfront(selected.Id, out reason);
        }
    }
}
