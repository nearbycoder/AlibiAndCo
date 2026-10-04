using System;
using System.Collections.Generic;
using System.Linq;

namespace AlibiCo.Logic
{
    /// <summary>A legal, state-changing player move (wrong moves change nothing but cost a badge).</summary>
    public sealed class Move
    {
        public string Kind;   // "link" | "confront"
        public string A, B;
        public override string ToString() => Kind == "link" ? $"link {A} + {B}" : $"confront {A}";
    }

    /// <summary>Breadth-first search over everything a player can legally do on a board.</summary>
    public static class Solver
    {
        public static List<Move> LegalMoves(Board b)
        {
            var moves = new List<Move>();
            var unlocked = b.UnlockedCards.ToList();
            for (int i = 0; i < unlocked.Count; i++)
                for (int j = i + 1; j < unlocked.Count; j++)
                {
                    var x = unlocked[i];
                    var y = unlocked[j];
                    if (x.Event == null || x.Event != y.Event || x.Clock == y.Clock) continue;
                    if (b.IsTrusted(x.Clock) != b.IsTrusted(y.Clock))
                        moves.Add(new Move { Kind = "link", A = x.Id, B = y.Id });
                }
            foreach (var c in unlocked)
                if (c.IsFalse && b.CanConfront(c.Id, out _))
                    moves.Add(new Move { Kind = "confront", A = c.Id });
            return moves;
        }

        public static Board Apply(Board b, Move m)
        {
            var n = b.Clone();
            if (m.Kind == "link") n.Link(m.A, m.B);
            else n.Confront(m.A);
            return n;
        }

        public static bool IsSolvedState(Board b, out List<string> fits)
        {
            fits = b.FittingLanes();
            return b.CandidatesLeft.Count == 0 && !b.EstablishedConflicts.Any() && fits.Count == 1;
        }

        /// <summary>Shortest sequence of moves from this board to an accusable state (null if none).</summary>
        public static List<Move> ShortestSolution(Board start)
        {
            var seen = new HashSet<string> { start.StateKey() };
            var queue = new Queue<(Board, List<Move>)>();
            queue.Enqueue((start, new List<Move>()));
            while (queue.Count > 0)
            {
                var (b, path) = queue.Dequeue();
                if (IsSolvedState(b, out var fits) && fits[0] == b.Case.Incident.Culprit) return path;
                foreach (var m in LegalMoves(b))
                {
                    var n = Apply(b, m);
                    if (!seen.Add(n.StateKey())) continue;
                    queue.Enqueue((n, new List<Move>(path) { m }));
                }
            }
            return null;
        }

        /// <summary>A board where every unlocked card counts as pinned, mirroring the player's state.</summary>
        public static Board Shadow(Board live)
        {
            var b = live.Clone();
            b.AutoPin = true;
            b.Hypotheses.Clear();
            b.Refresh();
            var o = new Outcome();
            b.Settle(o);
            return b;
        }
    }

    public sealed class ValidationReport
    {
        public string CaseId;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Info = new List<string>();
        public List<Move> Solution;
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// Proves a case is airtight: ground truth is coherent, every contradiction can be discovered,
    /// the case is always solvable, and in every reachable state where the board would accept an
    /// accusation, the incident fits exactly the culprit.
    /// </summary>
    public static class CaseValidator
    {
        public static ValidationReport Validate(CaseDef c, TownMap map)
        {
            var r = new ValidationReport { CaseId = c.Id };
            try
            {
                CheckReferences(c, map, r);
                if (!r.Ok) return r;
                CheckGroundTruth(c, map, r);
                CheckSearch(c, map, r);
            }
            catch (Exception e)
            {
                r.Errors.Add("exception: " + e);
            }
            return r;
        }

        static int TrueFrom(CaseDef c, CardDef card) => card.From - c.ClockById[card.Clock].TrueOffset;
        static int TrueTo(CaseDef c, CardDef card) => card.To - c.ClockById[card.Clock].TrueOffset;

        static void CheckReferences(CaseDef c, TownMap map, ValidationReport r)
        {
            var ids = new HashSet<string>();
            foreach (var card in c.Cards)
            {
                string where = $"card '{card.Id}'";
                if (!ids.Add(card.Id)) r.Errors.Add($"{where}: duplicate id");
                if (!card.Town && !map.Has(card.Location)) r.Errors.Add($"{where}: unknown location '{card.Location}'");
                if (!c.ClockById.ContainsKey(card.Clock)) r.Errors.Add($"{where}: unknown clock '{card.Clock}'");
                if (card.To < card.From) r.Errors.Add($"{where}: ends before it starts");
                foreach (var s in card.Subjects)
                    if (!c.PersonById.ContainsKey(s)) r.Errors.Add($"{where}: unknown subject '{s}'");
                if (card.Town && card.Subjects.Count > 0) r.Errors.Add($"{where}: a TOWN card can't have subjects");
                if (card.IsUnknown)
                {
                    if (card.Candidates.Count < 2) r.Errors.Add($"{where}: unknown card needs 2+ candidates");
                    if (card.TrueSubject == null || !card.Candidates.Contains(card.TrueSubject))
                        r.Errors.Add($"{where}: trueSubject must be one of the candidates");
                    if (!card.IsRecord) r.Errors.Add($"{where}: unknown-person cards must be records");
                    foreach (var s in card.Candidates)
                        if (!c.PersonById.ContainsKey(s)) r.Errors.Add($"{where}: unknown candidate '{s}'");
                }
                if (card.IsRecord && card.IsFalse) r.Errors.Add($"{where}: records are never false");
                if (card.IsTestimony && card.IsFalse && string.IsNullOrEmpty(card.Reply))
                    r.Errors.Add($"{where}: false statement needs a confrontation reply");
                if (card.IsTestimony && !card.IsFalse && string.IsNullOrEmpty(card.Firm))
                    r.Warnings.Add($"{where}: true statement has no 'stands firm' reply");
                foreach (var u in card.UnlocksOnStrike.Concat(card.UnlocksOnConfirm))
                    if (!c.CardById.ContainsKey(u)) r.Errors.Add($"{where}: unlocks unknown card '{u}'");
                int span0 = Math.Min(card.From, TrueFrom(c, card)), span1 = Math.Max(card.To, TrueTo(c, card));
                if (span0 < c.SpanFrom || span1 > c.SpanTo)
                    r.Errors.Add($"{where}: time {TimeFmt.Format(span0)}-{TimeFmt.Format(span1)} is off the board span");
            }
            foreach (var t in c.Triggers)
            {
                if (t.Kind == TriggerKind.Calibrate)
                {
                    if (!c.ClockById.ContainsKey(t.A)) r.Errors.Add($"trigger {t.Key}: unknown clock");
                }
                else
                {
                    if (!c.CardById.ContainsKey(t.A)) r.Errors.Add($"trigger {t.Key}: unknown card '{t.A}'");
                    if (t.Kind == TriggerKind.Conflict && (t.B == null || !c.CardById.ContainsKey(t.B)))
                        r.Errors.Add($"trigger {t.Key}: unknown card '{t.B}'");
                }
                foreach (var u in t.Unlocks)
                    if (!c.CardById.ContainsKey(u)) r.Errors.Add($"trigger {t.Key}: unlocks unknown card '{u}'");
            }
            var inc = c.Incident;
            if (!map.Has(inc.Location)) r.Errors.Add("incident: unknown location");
            if (!c.PersonById.TryGetValue(inc.Culprit ?? "", out var culprit) || !culprit.IsSuspect)
                r.Errors.Add("incident: culprit must be a suspect");
            if (inc.To - inc.From < inc.Duration) r.Errors.Add("incident: window shorter than the job");

            // Cards that describe the same moment must agree on the true time.
            foreach (var g in c.Cards.Where(x => x.Event != null).GroupBy(x => x.Event))
            {
                var times = g.Select(x => TrueFrom(c, x)).Distinct().ToList();
                if (times.Count > 1)
                    r.Errors.Add($"event '{g.Key}': cards disagree on the true time ({string.Join(", ", times.Select(TimeFmt.Format))})");
                if (g.Count() < 2) r.Warnings.Add($"event '{g.Key}' has only one card");
            }
            foreach (var k in c.Clocks.Where(k => !k.Reference && !k.KnownAtStart))
            {
                bool linkable = c.Cards.Where(x => x.Clock == k.Id && x.Event != null)
                    .Any(x => c.Cards.Any(y => y.Event == x.Event && y.Clock != k.Id));
                if (k.TrueOffset != 0 && !linkable)
                    r.Errors.Add($"clock '{k.Id}' is off by {k.TrueOffset} but no link can ever correct it");
            }
        }

        static void CheckGroundTruth(CaseDef c, TownMap map, ValidationReport r)
        {
            foreach (var p in c.People)
            {
                if (!c.Itineraries.TryGetValue(p.Id, out var stops) || stops.Count == 0)
                {
                    r.Errors.Add($"truth: no itinerary for {p.Id}");
                    continue;
                }
                for (int i = 0; i < stops.Count; i++)
                {
                    var s = stops[i];
                    if (!map.Has(s.Location)) r.Errors.Add($"truth {p.Id}: unknown place '{s.Location}'");
                    if (s.To < s.From) r.Errors.Add($"truth {p.Id}: stop at {s.Location} ends before it starts");
                    if (i > 0)
                    {
                        var prev = stops[i - 1];
                        int gap = s.From - prev.To, need = map.Minutes(prev.Location, s.Location);
                        if (gap < need)
                            r.Errors.Add($"truth {p.Id}: can't walk {prev.Location} {TimeFmt.Format(prev.To)} -> {s.Location} {TimeFmt.Format(s.From)} (needs {need}, has {gap})");
                    }
                }
            }

            bool Covered(string who, string loc, int from, int to) =>
                c.Itineraries.TryGetValue(who, out var st) && st.Any(s => s.Location == loc && s.From <= from && s.To >= to);

            foreach (var card in c.Cards)
            {
                if (card.Town) continue;
                var subjects = card.IsUnknown ? new List<string> { card.TrueSubject } : card.Subjects;
                int tf = TrueFrom(c, card), tt = TrueTo(c, card);
                bool allTrue = subjects.All(s => Covered(s, card.Location, tf, tt));
                if (!card.IsFalse && !allTrue)
                    r.Errors.Add($"truth: card '{card.Id}' is marked true but doesn't match the itineraries ({card.Location} {TimeFmt.Format(tf)}-{TimeFmt.Format(tt)})");
                if (card.IsFalse && allTrue)
                    r.Errors.Add($"truth: card '{card.Id}' is marked false but matches the itineraries");
            }

            var inc = c.Incident;
            int w0 = inc.From - c.ClockById[inc.Clock].TrueOffset, w1 = inc.To - c.ClockById[inc.Clock].TrueOffset;
            foreach (var p in c.Suspects)
            {
                if (!c.Itineraries.TryGetValue(p.Id, out var st)) continue;
                bool did = st.Any(s => s.Location == inc.Location &&
                                       Math.Min(s.To, w1) - Math.Max(s.From, w0) >= inc.Duration);
                bool there = st.Any(s => s.Location == inc.Location && s.From < w1 && s.To > w0);
                if (p.Id == inc.Culprit && !did) r.Errors.Add($"truth: culprit {p.Id} is never at the scene long enough");
                if (p.Id != inc.Culprit && there) r.Errors.Add($"truth: {p.Id} is at the scene during the window but isn't the culprit");
            }
        }

        static void CheckSearch(CaseDef c, TownMap map, ValidationReport r)
        {
            var start = new Board(c, map, autoPin: true);
            var seen = new Dictionary<string, Board> { [start.StateKey()] = start };
            var queue = new Queue<Board>();
            queue.Enqueue(start);
            var conflictPairsSeen = new HashSet<string>();
            var everUnlocked = new HashSet<string>();
            var everStruck = new HashSet<string>();
            var terminals = new List<Board>();
            int solvedStates = 0, readyEmpty = 0;
            var culprit = c.Incident.Culprit;

            while (queue.Count > 0)
            {
                var b = queue.Dequeue();
                everUnlocked.UnionWith(b.Unlocked);
                everStruck.UnionWith(b.Struck);
                foreach (var k in b.EstablishedConflicts)
                    conflictPairsSeen.Add(Pair(k.A.Card.Id, k.B.Card.Id));

                foreach (var kv in b.Confirmed)
                {
                    var truth = c.CardById[kv.Key].TrueSubject;
                    if (kv.Value != truth)
                        r.Errors.Add($"identity: '{kv.Key}' confirmed as {kv.Value} but it was {truth} (state {b.StateKey()})");
                }

                bool clean = b.CandidatesLeft.Count == 0 && !b.EstablishedConflicts.Any();
                if (clean)
                {
                    var fits = b.FittingLanes();
                    if (fits.Count == 1)
                    {
                        if (fits[0] != culprit)
                            r.Errors.Add($"WRONG ACCUSATION POSSIBLE: board is clean and only {fits[0]} fits (state {b.StateKey()})");
                        else solvedStates++;
                    }
                    else if (fits.Count == 0) readyEmpty++;
                }

                var moves = Solver.LegalMoves(b);
                if (moves.Count == 0) terminals.Add(b);
                foreach (var m in moves)
                {
                    var n = Solver.Apply(b, m);
                    var key = n.StateKey();
                    if (seen.ContainsKey(key)) continue;
                    seen[key] = n;
                    queue.Enqueue(n);
                }
            }

            r.Info.Add($"{seen.Count} reachable states, {terminals.Count} terminal, {solvedStates} accusable, {readyEmpty} clean-but-everyone-covered");

            foreach (var t in terminals)
            {
                if (!Solver.IsSolvedState(t, out var fits) || fits[0] != culprit)
                {
                    var why = new List<string>();
                    if (t.CandidatesLeft.Count > 0)
                        why.Add("unidentified: " + string.Join(", ", t.CandidatesLeft.Select(kv => $"{kv.Key}→[{string.Join(",", kv.Value)}]")));
                    foreach (var k in t.EstablishedConflicts)
                        why.Add($"conflict {k.A.Card.Id} vs {k.B.Card.Id} in {k.Lane} (needs {k.Need}, has {k.Have})");
                    why.Add("fits: [" + string.Join(",", fits) + "]");
                    r.Errors.Add($"DEAD END: no moves left and not solved — {string.Join("; ", why)} (state {t.StateKey()})");
                }
                foreach (var card in c.Cards)
                    if (!t.Unlocked.Contains(card.Id))
                        r.Errors.Add($"card '{card.Id}' is still locked in a final state");
                foreach (var card in c.Cards.Where(x => x.IsFalse))
                    if (!t.Struck.Contains(card.Id))
                        r.Warnings.Add($"false card '{card.Id}' can stay un-struck to the end");
                foreach (var k in c.Clocks.Where(k => !k.Reference && k.TrueOffset != 0))
                    if (!t.IsTrusted(k.Id))
                        r.Warnings.Add($"clock '{k.Id}' can stay uncorrected to the end");

                // The culprit's real crime time must be inside the slot the board shows.
                if (c.Itineraries.TryGetValue(culprit, out var st) && t.Fits.TryGetValue(culprit, out var fit) && fit.Fits)
                {
                    var inc = c.Incident;
                    int w0 = inc.From - c.ClockById[inc.Clock].TrueOffset, w1 = inc.To - c.ClockById[inc.Clock].TrueOffset;
                    var scene = st.FirstOrDefault(s => s.Location == inc.Location && Math.Min(s.To, w1) - Math.Max(s.From, w0) >= inc.Duration);
                    if (scene != null)
                    {
                        int s0 = Math.Max(scene.From, w0), s1 = Math.Min(scene.To, w1) - inc.Duration;
                        if (s1 < fit.EarliestStart || s0 > fit.LatestStart)
                            r.Errors.Add($"the board's slot for {culprit} ({TimeFmt.Format(fit.EarliestStart)}-{TimeFmt.Format(fit.LatestStart)}) misses the real crime time");
                    }
                }
            }

            foreach (var card in c.Cards)
                if (!everUnlocked.Contains(card.Id)) r.Errors.Add($"card '{card.Id}' can never be unlocked");
            foreach (var card in c.Cards.Where(x => x.IsFalse))
                if (!everStruck.Contains(card.Id))
                    r.Errors.Add($"false card '{card.Id}' can never be exposed (it's never in a confrontable contradiction)");
            foreach (var t in c.Triggers.Where(t => t.Kind == TriggerKind.Conflict))
                if (!conflictPairsSeen.Contains(Pair(t.A, t.B)))
                    r.Errors.Add($"designed contradiction {t.A} vs {t.B} can never be discovered");

            r.Solution = Solver.ShortestSolution(start);
            if (r.Solution == null) r.Errors.Add("no solution path found");
            else r.Info.Add("shortest solution: " + string.Join(" → ", r.Solution));
        }

        static string Pair(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
    }
}
