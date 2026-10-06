using System;
using System.Collections.Generic;
using System.Linq;

namespace AlibiCo.Logic
{
    /// <summary>A card placed on a lane, in board time (clock corrections applied where known).</summary>
    public sealed class LaneEvent
    {
        public CardDef Card;
        public string Lane, Location;
        public int From, To;
        /// <summary>An unknown-person card the player pinned here on a hunch (not established).</summary>
        public bool Hypothesis;
    }

    /// <summary>Two cards in one lane that can't both be true: the walk between them doesn't fit.</summary>
    public sealed class Conflict
    {
        public string Lane;
        public LaneEvent A, B;   // A starts first
        public int Need, Have;   // walking minutes needed vs. minutes available (negative when overlapping)
        public bool Overlap;
        public bool Established => !A.Hypothesis && !B.Hypothesis;
        public bool Involves(string cardId) => A.Card.Id == cardId || B.Card.Id == cardId;
        public bool IsPair(string x, string y) =>
            (A.Card.Id == x && B.Card.Id == y) || (A.Card.Id == y && B.Card.Id == x);
        public string Key => Lane + ":" + string.CompareOrdinal(A.Card.Id, B.Card.Id) switch
        {
            < 0 => A.Card.Id + "|" + B.Card.Id,
            _ => B.Card.Id + "|" + A.Card.Id,
        };
    }

    /// <summary>Whether the incident could be slotted into a lane, and where.</summary>
    public sealed class IncidentFit
    {
        public string Lane;
        public bool Fits;
        public int EarliestStart, LatestStart;
        /// <summary>Minutes of wiggle room for the start time (0 means it fits exactly).</summary>
        public int Spare => Fits ? LatestStart - EarliestStart : -1;
        /// <summary>The card that blocks the most start times (for explanations), if it doesn't fit.</summary>
        public LaneEvent Blocker;
    }

    public enum AccuseStatus { Ready, CardsInTray, Unidentified, Contradictions, DoesNotFitHere, FitsSeveral }

    public sealed class AccuseCheck
    {
        public AccuseStatus Status;
        public string Message;
        public List<string> OtherFits = new List<string>();
        public bool Ok => Status == AccuseStatus.Ready;
    }

    /// <summary>What an action changed, for the UI to animate and narrate.</summary>
    public sealed class Outcome
    {
        public bool Accepted;
        public bool CostBadge;
        public string Message, Reply;
        public string CalibratedClock;
        public int Shift;                    // minutes every card on that clock moved (negative = earlier)
        public readonly List<string> NewCards = new List<string>();
        public readonly List<string> Questions = new List<string>();
        public readonly List<string> Memos = new List<string>();
        public readonly List<string> Confirmed = new List<string>();
        public readonly List<string> FiredTriggers = new List<string>();

        public void Absorb(Outcome o)
        {
            NewCards.AddRange(o.NewCards);
            Questions.AddRange(o.Questions);
            Memos.AddRange(o.Memos);
            Confirmed.AddRange(o.Confirmed);
            FiredTriggers.AddRange(o.FiredTriggers);
        }
    }

    /// <summary>
    /// The whole rule set of the board. Pure C#, shared by the game and the case validator.
    /// State is what the player has done (cards unlocked/pinned, clocks corrected, statements struck,
    /// identities confirmed). Everything else (lane contents, contradictions, who an unknown card
    /// could be, where the incident fits) is derived by <see cref="Refresh"/>.
    /// </summary>
    public sealed class Board
    {
        public const string TownLane = "town";

        public readonly CaseDef Case;
        public readonly TownMap Map;

        // --- player state ---
        public readonly HashSet<string> Unlocked = new HashSet<string>();
        public readonly HashSet<string> Pinned = new HashSet<string>();
        public readonly HashSet<string> Calibrated = new HashSet<string>();
        public readonly HashSet<string> Struck = new HashSet<string>();
        public readonly HashSet<string> Fired = new HashSet<string>();
        public readonly Dictionary<string, string> Confirmed = new Dictionary<string, string>();
        public readonly Dictionary<string, string> Hypotheses = new Dictionary<string, string>();
        public readonly List<KeyValuePair<string, string>> Links = new List<KeyValuePair<string, string>>();
        public int Mistakes;
        public bool Solved;
        /// <summary>When true every unlocked card counts as pinned (used by the validator).</summary>
        public bool AutoPin;

        // --- derived ---
        public readonly Dictionary<string, List<LaneEvent>> Lanes = new Dictionary<string, List<LaneEvent>>();
        public readonly List<Conflict> Conflicts = new List<Conflict>();
        public readonly Dictionary<string, List<string>> CandidatesLeft = new Dictionary<string, List<string>>();
        public readonly Dictionary<string, IncidentFit> Fits = new Dictionary<string, IncidentFit>();
        public readonly List<string> LaneOrder = new List<string>();

        public Board(CaseDef c, TownMap map, bool autoPin = false)
        {
            Case = c;
            Map = map;
            AutoPin = autoPin;
            foreach (var p in c.People) LaneOrder.Add(p.Id);
            if (c.HasTownLane) LaneOrder.Add(TownLane);
            foreach (var card in c.Cards)
                if (card.StartsAvailable)
                {
                    Unlocked.Add(card.Id);
                    if (autoPin) Pinned.Add(card.Id);
                }
            foreach (var k in c.Clocks)
                if (k.KnownAtStart && !k.Reference) Calibrated.Add(k.Id);
            Settle(new Outcome());
        }

        public Board Clone() => CopyInto(new Board(Case, Map, this));

        // Private copy constructor used by Clone: skips the initial settle.
        Board(CaseDef c, TownMap map, Board _) { Case = c; Map = map; }

        Board CopyInto(Board b)
        {
            b.AutoPin = AutoPin;
            b.Unlocked.UnionWith(Unlocked);
            b.Pinned.UnionWith(Pinned);
            b.Calibrated.UnionWith(Calibrated);
            b.Struck.UnionWith(Struck);
            b.Fired.UnionWith(Fired);
            foreach (var kv in Confirmed) b.Confirmed[kv.Key] = kv.Value;
            foreach (var kv in Hypotheses) b.Hypotheses[kv.Key] = kv.Value;
            b.Links.AddRange(Links);
            b.Mistakes = Mistakes;
            b.Solved = Solved;
            b.LaneOrder.AddRange(LaneOrder);
            b.Refresh();
            return b;
        }

        // ------------------------------------------------------------------ clocks & times

        public bool IsTrusted(string clockId)
        {
            var k = Case.ClockById[clockId];
            return k.Reference || Calibrated.Contains(clockId);
        }

        /// <summary>Minutes to add to a printed time to get board time (0 until the clock is corrected).</summary>
        public int Correction(string clockId)
        {
            var k = Case.ClockById[clockId];
            if (k.Reference || !Calibrated.Contains(clockId)) return 0;
            return -k.TrueOffset;
        }

        public int BoardFrom(CardDef c) => c.From + Correction(c.Clock);
        public int BoardTo(CardDef c) => c.To + Correction(c.Clock);
        public int IncidentFrom => Case.Incident.From + Correction(Case.Incident.Clock);
        public int IncidentTo => Case.Incident.To + Correction(Case.Incident.Clock);

        // ------------------------------------------------------------------ queries

        public bool IsPinned(string cardId) => AutoPin ? Unlocked.Contains(cardId) : Pinned.Contains(cardId);

        /// <summary>The lanes a card currently sits in (empty for an unpinned/unplaced unknown).</summary>
        public List<string> LanesOf(CardDef c)
        {
            var r = new List<string>();
            if (c.Town) { r.Add(TownLane); return r; }
            if (!c.IsUnknown) { r.AddRange(c.Subjects); return r; }
            if (Confirmed.TryGetValue(c.Id, out var who)) r.Add(who);
            else if (Hypotheses.TryGetValue(c.Id, out var guess)) r.Add(guess);
            return r;
        }

        public bool IsEstablished(CardDef c) => !c.IsUnknown || Confirmed.ContainsKey(c.Id);

        public IEnumerable<CardDef> UnlockedCards => Case.Cards.Where(c => Unlocked.Contains(c.Id));
        public IEnumerable<CardDef> TrayCards => Case.Cards.Where(c => Unlocked.Contains(c.Id) && !IsPinned(c.Id));

        public bool InConflict(string cardId, bool establishedOnly = false) =>
            Conflicts.Any(k => k.Involves(cardId) && (!establishedOnly || k.Established));

        public IEnumerable<Conflict> EstablishedConflicts => Conflicts.Where(k => k.Established);

        // ------------------------------------------------------------------ derivation

        public void Refresh()
        {
            Lanes.Clear();
            Conflicts.Clear();
            CandidatesLeft.Clear();
            Fits.Clear();
            foreach (var lane in LaneOrder) Lanes[lane] = new List<LaneEvent>();

            foreach (var c in Case.Cards)
            {
                if (!Unlocked.Contains(c.Id) || !IsPinned(c.Id) || Struck.Contains(c.Id)) continue;
                bool hyp = c.IsUnknown && !Confirmed.ContainsKey(c.Id);
                foreach (var lane in LanesOf(c))
                {
                    if (!Lanes.ContainsKey(lane)) continue;
                    Lanes[lane].Add(new LaneEvent
                    {
                        Card = c, Lane = lane, Location = c.Location,
                        From = BoardFrom(c), To = BoardTo(c), Hypothesis = hyp,
                    });
                }
            }
            foreach (var kv in Lanes)
            {
                kv.Value.Sort(CompareEvents);
                if (kv.Key == TownLane) continue;
                FindConflicts(kv.Key, kv.Value, Conflicts);
            }

            foreach (var c in Case.Cards)
                if (c.IsUnknown && Unlocked.Contains(c.Id) && !Confirmed.ContainsKey(c.Id))
                    CandidatesLeft[c.Id] = ComputeCandidates(c);

            foreach (var p in Case.Suspects)
                Fits[p.Id] = ComputeFit(p.Id, Lanes[p.Id].Where(e => !e.Hypothesis));
        }

        static int CompareEvents(LaneEvent a, LaneEvent b)
        {
            int d = a.From.CompareTo(b.From);
            if (d != 0) return d;
            d = a.To.CompareTo(b.To);
            return d != 0 ? d : string.CompareOrdinal(a.Card.Id, b.Card.Id);
        }

        /// <summary>Pairwise check: with fixed times and metric walking times this is exact.</summary>
        void FindConflicts(string lane, List<LaneEvent> evs, List<Conflict> into)
        {
            for (int i = 0; i < evs.Count; i++)
                for (int j = i + 1; j < evs.Count; j++)
                {
                    var k = Check(evs[i], evs[j]);
                    if (k != null) { k.Lane = lane; into.Add(k); }
                }
        }

        Conflict Check(LaneEvent a, LaneEvent b)
        {
            if (b.From < a.From || (b.From == a.From && b.To < a.To)) { var t = a; a = b; b = t; }
            if (a.To <= b.From)
            {
                int have = b.From - a.To;
                int need = Map.Minutes(a.Location, b.Location);
                return have < need ? new Conflict { A = a, B = b, Need = need, Have = have } : null;
            }
            // Overlapping in time: only fine if it's the same place.
            if (a.Location == b.Location) return null;
            return new Conflict { A = a, B = b, Need = Map.Minutes(a.Location, b.Location), Have = b.From - a.To, Overlap = true };
        }

        /// <summary>True if being at loc during [from,to] is compatible with event e.</summary>
        bool Compatible(LaneEvent e, string loc, int from, int to)
        {
            if (e.To <= from) return from - e.To >= Map.Minutes(e.Location, loc);
            if (e.From >= to) return e.From - to >= Map.Minutes(loc, e.Location);
            return e.Location == loc;
        }

        /// <summary>
        /// Who could an unknown card belong to? Paper beats people: only records and confirmed
        /// identity cards can rule a candidate out, never somebody's word. Every unlocked record
        /// counts, pinned or still in the tray: the paper is on the desk either way, and an identity
        /// is confirmed for good, so it mustn't depend on the order the player happens to pin
        /// things in. (The validator searches with everything pinned; this keeps the game equal to it.)
        /// </summary>
        List<string> ComputeCandidates(CardDef c)
        {
            var left = new List<string>();
            int from = BoardFrom(c), to = BoardTo(c);
            foreach (var who in c.Candidates)
            {
                if (!Lanes.ContainsKey(who)) continue;
                bool ok = true;
                foreach (var r in Case.Cards)
                {
                    if (r == c || !r.IsRecord || r.Town || !Unlocked.Contains(r.Id) || Struck.Contains(r.Id)) continue;
                    if (r.IsUnknown && !(Confirmed.TryGetValue(r.Id, out var owner) && owner == who)) continue;
                    if (!r.IsUnknown && !r.Subjects.Contains(who)) continue;
                    var e = new LaneEvent { Card = r, Lane = who, Location = r.Location, From = BoardFrom(r), To = BoardTo(r) };
                    if (!Compatible(e, c.Location, from, to)) { ok = false; break; }
                }
                if (ok) left.Add(who);
            }
            return left;
        }

        IncidentFit ComputeFit(string lane, IEnumerable<LaneEvent> events)
        {
            var evs = events.ToList();
            var inc = Case.Incident;
            var fit = new IncidentFit { Lane = lane, EarliestStart = int.MaxValue, LatestStart = int.MinValue };
            var blockCount = new Dictionary<LaneEvent, int>();
            int lo = IncidentFrom, hi = IncidentTo - inc.Duration;
            for (int s = lo; s <= hi; s++)
            {
                LaneEvent blocker = null;
                foreach (var e in evs)
                    if (!Compatible(e, inc.Location, s, s + inc.Duration)) { blocker = e; break; }
                if (blocker == null)
                {
                    fit.Fits = true;
                    fit.EarliestStart = Math.Min(fit.EarliestStart, s);
                    fit.LatestStart = Math.Max(fit.LatestStart, s);
                }
                else
                {
                    blockCount.TryGetValue(blocker, out int n);
                    blockCount[blocker] = n + 1;
                }
            }
            if (!fit.Fits && blockCount.Count > 0)
                fit.Blocker = blockCount.OrderByDescending(kv => kv.Value).First().Key;
            return fit;
        }

        public List<string> FittingLanes() => Fits.Where(kv => kv.Value.Fits).Select(kv => kv.Key).ToList();

        // ------------------------------------------------------------------ actions

        public void Pin(string cardId, string lane = null)
        {
            var c = Case.CardById[cardId];
            if (!Unlocked.Contains(cardId)) return;
            Pinned.Add(cardId);
            if (c.IsUnknown && !Confirmed.ContainsKey(cardId))
            {
                if (lane != null && c.Candidates.Contains(lane)) Hypotheses[cardId] = lane;
                else Hypotheses.Remove(cardId);
            }
            Refresh();
        }

        public Outcome PinAndSettle(string cardId, string lane = null)
        {
            Pin(cardId, lane);
            var o = new Outcome { Accepted = true };
            Settle(o);
            return o;
        }

        public void Unpin(string cardId)
        {
            Pinned.Remove(cardId);
            Hypotheses.Remove(cardId);
            Refresh();
        }

        public bool CanConfront(string cardId, out string reason)
        {
            var c = Case.CardById[cardId];
            reason = null;
            if (c.IsRecord) { reason = "Records don't lie. Their clocks might."; return false; }
            if (Struck.Contains(cardId)) { reason = "Already struck off."; return false; }
            if (!IsPinned(cardId)) { reason = "Pin it to the board first."; return false; }
            if (!Conflicts.Any(k => k.Involves(cardId)))
            {
                reason = "Nothing on the board contradicts this yet.";
                return false;
            }
            if (!Conflicts.Any(k => k.Involves(cardId) && k.Established))
            {
                reason = "You can't confront anyone with a card you can't pin on someone.";
                return false;
            }
            return true;
        }

        public Outcome Confront(string cardId)
        {
            var o = new Outcome();
            if (!CanConfront(cardId, out var reason)) { o.Message = reason; return o; }
            var c = Case.CardById[cardId];
            if (c.IsFalse)
            {
                o.Accepted = true;
                o.Reply = c.Reply;
                Struck.Add(cardId);
                Refresh();
                foreach (var id in c.UnlocksOnStrike) UnlockInto(id, o);
                Settle(o);
            }
            else
            {
                o.CostBadge = true;
                o.Reply = c.Firm;
                Mistakes++;
            }
            return o;
        }

        public Outcome Link(string a, string b)
        {
            var o = new Outcome();
            if (a == b) return o;
            var ca = Case.CardById[a];
            var cb = Case.CardById[b];
            if (!Unlocked.Contains(a) || !Unlocked.Contains(b)) { o.Message = "Both cards need to be on the board."; return o; }
            if (Links.Any(l => (l.Key == a && l.Value == b) || (l.Key == b && l.Value == a)))
            {
                o.Message = "Already linked.";
                return o;
            }
            if (ca.Event == null || ca.Event != cb.Event)
            {
                o.CostBadge = true;
                Mistakes++;
                o.Message = "Those aren't the same moment.";
                return o;
            }
            if (ca.Clock == cb.Clock) { o.Message = "Same clock on both. Nothing to learn."; return o; }
            bool ta = IsTrusted(ca.Clock), tb = IsTrusted(cb.Clock);
            if (ta && tb)
            {
                o.Accepted = true;
                Links.Add(new KeyValuePair<string, string>(a, b));
                o.Message = "Same moment, and both clocks already agree.";
                return o;
            }
            if (!ta && !tb)
            {
                o.Message = "Same moment, but neither clock can be trusted yet.";
                return o;
            }
            var clock = ta ? cb.Clock : ca.Clock;
            o.Accepted = true;
            o.CalibratedClock = clock;
            o.Shift = -Case.ClockById[clock].TrueOffset;
            Links.Add(new KeyValuePair<string, string>(a, b));
            Calibrated.Add(clock);
            Refresh();
            Settle(o);
            return o;
        }

        public AccuseCheck CheckAccusation(string lane)
        {
            var r = new AccuseCheck();
            int inTray = TrayCards.Count();
            if (inTray > 0)
            {
                r.Status = AccuseStatus.CardsInTray;
                r.Message = inTray == 1 ? "Not yet. One card is still in the tray." : $"Not yet. {inTray} cards are still in the tray.";
                return r;
            }
            if (CandidatesLeft.Count > 0)
            {
                r.Status = AccuseStatus.Unidentified;
                r.Message = "Not yet. There's still a card nobody has been tied to.";
                return r;
            }
            if (EstablishedConflicts.Any())
            {
                r.Status = AccuseStatus.Contradictions;
                r.Message = "Not yet. The board still contradicts itself.";
                return r;
            }
            var fits = FittingLanes();
            if (!fits.Contains(lane))
            {
                r.Status = AccuseStatus.DoesNotFitHere;
                var name = Case.PersonById.TryGetValue(lane, out var p) ? p.Name : lane;
                r.Message = $"It doesn't fit. {name} couldn't have been there.";
                return r;
            }
            r.OtherFits.AddRange(fits.Where(f => f != lane));
            if (r.OtherFits.Count > 0)
            {
                r.Status = AccuseStatus.FitsSeveral;
                var names = string.Join(" and ", r.OtherFits.Select(f => Case.PersonById[f].Name));
                r.Message = $"Not yet. It fits {names} too. Rule them out first.";
                return r;
            }
            r.Status = AccuseStatus.Ready;
            return r;
        }

        public Outcome Accuse(string lane)
        {
            var o = new Outcome();
            var chk = CheckAccusation(lane);
            o.Message = chk.Message;
            if (!chk.Ok) return o;
            o.Accepted = true;
            Solved = true;
            return o;
        }

        // ------------------------------------------------------------------ triggers

        void UnlockInto(string id, Outcome o)
        {
            if (!Case.CardById.ContainsKey(id)) throw new InvalidOperationException($"unknown card id '{id}' in unlocks");
            if (Unlocked.Add(id))
            {
                o.NewCards.Add(id);
                if (AutoPin) Pinned.Add(id);
            }
        }

        /// <summary>Fire triggers and confirm identities until nothing changes.</summary>
        public void Settle(Outcome o)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                Refresh();
                bool changed = false;

                foreach (var kv in CandidatesLeft.ToList())
                {
                    if (kv.Value.Count != 1) continue;
                    var id = kv.Key;
                    Confirmed[id] = kv.Value[0];
                    Hypotheses.Remove(id);
                    Pinned.Add(id);
                    o.Confirmed.Add(id);
                    foreach (var u in Case.CardById[id].UnlocksOnConfirm) UnlockInto(u, o);
                    changed = true;
                }
                if (changed) continue;

                foreach (var t in Case.Triggers)
                {
                    if (Fired.Contains(t.Key)) continue;
                    bool hit = t.Kind switch
                    {
                        TriggerKind.Conflict => EstablishedConflicts.Any(k => k.IsPair(t.A, t.B)),
                        TriggerKind.Calibrate => Calibrated.Contains(t.A),
                        TriggerKind.Strike => Struck.Contains(t.A),
                        TriggerKind.Confirm => Confirmed.ContainsKey(t.A),
                        _ => false,
                    };
                    if (!hit) continue;
                    Fired.Add(t.Key);
                    o.FiredTriggers.Add(t.Key);
                    if (!string.IsNullOrEmpty(t.Question)) o.Questions.Add(t.Question);
                    if (!string.IsNullOrEmpty(t.Memo)) o.Memos.Add(t.Memo);
                    foreach (var u in t.Unlocks) UnlockInto(u, o);
                    changed = true;
                }
                if (!changed) return;
            }
            throw new InvalidOperationException("trigger loop did not settle");
        }

        /// <summary>A compact key of the player-driven state (for the validator's search).</summary>
        public string StateKey()
        {
            string Join(IEnumerable<string> s) => string.Join(",", s.OrderBy(x => x, StringComparer.Ordinal));
            return Join(Unlocked) + "/" + Join(Calibrated) + "/" + Join(Struck) + "/" +
                   Join(Confirmed.Select(kv => kv.Key + "=" + kv.Value));
        }
    }
}
