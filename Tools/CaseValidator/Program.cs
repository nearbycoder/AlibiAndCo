using System;
using System.IO;
using System.Linq;
using AlibiCo.Logic;

// Usage: dotnet run --project Tools/CaseValidator [-- [--verbose] [caseId...]]
//        ... -- --docket N [yyyy-MM-dd]     generate and prove N consecutive Daily Dockets
//        ... -- --docket-show yyyy-MM-dd    print one docket's text and walk through its solution
//        ... -- --docket-phrases N [yyyy-MM-dd]  sentences that recur on more than a quarter of N days
// Exit code 0 when every case is airtight.
static class Program
{
    static int Main(string[] args)
    {
        string root = FindRoot();
        string data = Path.Combine(root, "Assets", "Resources", "Data");
        var town = TownData.FromJson(File.ReadAllText(Path.Combine(data, "town.json")));
        var map = new TownMap(town);
        bool verbose = args.Contains("--verbose");
        int di = Array.IndexOf(args, "--docket");
        if (di >= 0) return DocketSweep(map, di + 1 < args.Length ? int.Parse(args[di + 1]) : 365,
                                        di + 2 < args.Length && !args[di + 2].StartsWith("--") ? DateTime.Parse(args[di + 2]) : DateTime.Today);
        int dp = Array.IndexOf(args, "--docket-phrases");
        if (dp >= 0) return DocketPhrases(map, dp + 1 < args.Length ? int.Parse(args[dp + 1]) : 28,
                                          dp + 2 < args.Length && !args[dp + 2].StartsWith("--") ? DateTime.Parse(args[dp + 2]) : DateTime.Today);
        int ds = Array.IndexOf(args, "--docket-show");
        if (ds >= 0) return DocketShow(map, ds + 1 < args.Length ? DateTime.Parse(args[ds + 1]) : DateTime.Today);
        var only = args.Where(a => !a.StartsWith("--")).ToList();

        int failed = 0;
        foreach (var file in Directory.GetFiles(data, "case*.json").OrderBy(f => f))
        {
            var c = CaseDef.FromJson(File.ReadAllText(file));
            if (only.Count > 0 && !only.Contains(c.Id)) continue;
            var r = CaseValidator.Validate(c, map);
            Console.WriteLine($"== {c.Id} \"{c.Title}\" — {(r.Ok ? "AIRTIGHT" : "FAILED")}");
            foreach (var e in r.Errors) Console.WriteLine("  ERROR " + e);
            foreach (var w in r.Warnings) Console.WriteLine("  warn  " + w);
            foreach (var i in r.Info) Console.WriteLine("  " + i);
            if (verbose && r.Solution != null) Walkthrough(c, map, r);
            if (!r.Ok) failed++;
        }
        Console.WriteLine(failed == 0 ? "All cases airtight." : $"{failed} case(s) failed.");
        return failed == 0 ? 0 : 1;
    }

    static int DocketSweep(TownMap map, int days, DateTime from)
    {
        int failed = 0, clockDays = 0, maxAttempt = 0;
        var attempts = new int[Docket.MaxAttempts];
        var states = new System.Collections.Generic.List<int>();
        var moves = new System.Collections.Generic.Dictionary<int, int>();
        var titles = new System.Collections.Generic.Dictionary<string, int>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double slowest = 0;
        for (int i = 0; i < days; i++)
        {
            var date = from.AddDays(i);
            var t0 = sw.Elapsed.TotalMilliseconds;
            var d = Docket.Generate(date, map);
            slowest = Math.Max(slowest, sw.Elapsed.TotalMilliseconds - t0);
            if (d == null) { failed++; Console.WriteLine($"  FAILED {Docket.IdFor(date)}: no airtight variation in {Docket.MaxAttempts} tries"); continue; }
            attempts[d.Attempt]++;
            maxAttempt = Math.Max(maxAttempt, d.Attempt);
            if (d.ClockDay) clockDays++;
            states.Add(int.Parse(d.Report.Info[0].Split(' ')[0]));
            moves.TryGetValue(d.Report.Solution.Count, out int n);
            moves[d.Report.Solution.Count] = n + 1;
            titles.TryGetValue(d.Case.Title, out int k);
            titles[d.Case.Title] = k + 1;
        }
        Console.WriteLine($"== Daily Docket: {days} days from {from:yyyy-MM-dd}: {days - failed} airtight, {failed} failed");
        Console.WriteLine($"  clock days: {clockDays}; worst day needed {maxAttempt + 1} variation(s); first-try airtight: {attempts[0]}");
        Console.WriteLine($"  reachable states {states.DefaultIfEmpty(0).Min()}–{states.DefaultIfEmpty(0).Max()}; solution lengths: {string.Join(", ", moves.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} moves ×{kv.Value}"))}");
        Console.WriteLine($"  crimes: {string.Join(", ", titles.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ×{kv.Value}"))}");
        Console.WriteLine($"  {sw.Elapsed.TotalSeconds:0.0}s in all, slowest day {slowest:0} ms");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// How samey the dockets read: every sentence a player can see (intro, cards, replies,
    /// questions, memos, reconstruction, epilogue), counted by the number of days it turns up on.
    /// Sentences with names, places and times in them vary by themselves; the ones that recur are
    /// the template showing through.
    /// </summary>
    static int DocketPhrases(TownMap map, int days, DateTime from)
    {
        var seen = new System.Collections.Generic.Dictionary<string, int>();
        int total = 0;
        for (int i = 0; i < days; i++)
        {
            var d = Docket.Generate(from.AddDays(i), map);
            if (d == null) continue;
            var c = d.Case;
            var texts = new System.Collections.Generic.List<string> { c.Lesson, c.Incident.Text };
            texts.AddRange(c.Intro);
            foreach (var x in c.Cards) texts.AddRange(new[] { x.Text, x.Reply, x.Firm });   // titles are labels, not prose
            foreach (var t in c.Triggers) texts.AddRange(new[] { t.Question, t.Memo });
            foreach (var m in c.Memos) texts.Add(m.Text);
            texts.AddRange(c.ReconstructionLines);
            texts.AddRange(c.Epilogue);
            var today = new System.Collections.Generic.HashSet<string>();
            foreach (var t in texts.Where(t => !string.IsNullOrWhiteSpace(t)))
                foreach (var raw in System.Text.RegularExpressions.Regex.Split(t.Replace("“", "").Replace("”", ""), @"(?<!\b[A-Z]\.)(?<!\bCapt\.)(?<!\bCAPT\.)(?<!\bNo\.)(?<=[.!?])\s+|\n"))
                {
                    var sentence = raw.Trim();
                    if (sentence.Length > 0) today.Add(sentence);
                }
            total += today.Count;
            foreach (var sentence in today) { seen.TryGetValue(sentence, out int n); seen[sentence] = n + 1; }
        }
        var common = seen.Where(kv => kv.Value * 4 > days).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
        Console.WriteLine($"== Docket phrases over {days} days from {from:yyyy-MM-dd}: {seen.Count} distinct sentences ({total / Math.Max(1, days)} a day); {common.Count} turn up on more than a quarter of the days");
        foreach (var kv in common) Console.WriteLine($"  {kv.Value,3}/{days}  {kv.Key}");
        return 0;
    }

    static int DocketShow(TownMap map, DateTime date)
    {
        var d = Docket.Generate(date, map);
        if (d == null) { Console.WriteLine("no airtight docket for " + date.ToString("yyyy-MM-dd")); return 1; }
        var c = d.Case;
        Console.WriteLine($"== {c.Id} \"{c.Title}\" ({(d.ClockDay ? "clock day" : "plain")}, variation {d.Attempt + 1}) {c.Date} · {c.Weather}");
        Console.WriteLine("  " + c.Tagline);
        foreach (var p in c.Intro) Console.WriteLine("  | " + p);
        Console.WriteLine($"  incident: {c.Incident.Text} [{c.Incident.Location} {TimeFmt.Format(c.Incident.From)}-{TimeFmt.Format(c.Incident.To)}, {c.Incident.Duration} min, culprit {c.Incident.Culprit}]");
        foreach (var x in c.Cards)
        {
            string when = x.IsInstant ? TimeFmt.Format(x.From) : TimeFmt.Format(x.From) + "-" + TimeFmt.Format(x.To);
            Console.WriteLine($"  [{x.Id}] {x.Kind} \"{x.Title}\" ({x.SourceName}) {x.Location} {when}{(x.Clock != "ref" ? " on " + x.Clock : "")}{(x.IsFalse ? " LIE" : "")}");
            Console.WriteLine("      " + x.Text.Replace("\n", " / "));
            if (!string.IsNullOrEmpty(x.Reply)) Console.WriteLine("      reply: " + x.Reply);
            if (!string.IsNullOrEmpty(x.Firm)) Console.WriteLine("      firm:  " + x.Firm);
        }
        foreach (var t in c.Triggers) Console.WriteLine($"  trigger {t.Key}: {t.Question}{t.Memo}");
        foreach (var m in c.Memos) Console.WriteLine($"  memo {m.When}: {m.Text}");
        foreach (var l in c.ReconstructionLines) Console.WriteLine("  > " + l);
        foreach (var e in c.Epilogue) Console.WriteLine("  ~ " + e);
        foreach (var i in d.Report.Info) Console.WriteLine("  " + i);
        Walkthrough(c, map, d.Report);
        return 0;
    }

    static void Walkthrough(CaseDef c, TownMap map, ValidationReport r)
    {
        var b = new Board(c, map, autoPin: true);
        Dump(b, "start");
        foreach (var m in r.Solution)
        {
            var o = m.Kind == "link" ? b.Link(m.A, m.B) : b.Confront(m.A);
            Console.WriteLine($"  >> {m}  {(o.CalibratedClock != null ? $"[{o.CalibratedClock} {o.Shift:+0;-0}]" : "")} new: {string.Join(",", o.NewCards)} confirmed: {string.Join(",", o.Confirmed)}");
            foreach (var q in o.Questions) Console.WriteLine("     ? " + q);
            Dump(b, m.ToString());
        }
    }

    static void Dump(Board b, string label)
    {
        Console.WriteLine($"  -- {label}");
        foreach (var lane in b.LaneOrder)
        {
            var evs = b.Lanes[lane];
            string fit = b.Fits.TryGetValue(lane, out var f) ? (f.Fits ? $"OPEN({TimeFmt.Format(f.EarliestStart)}..{TimeFmt.Format(f.LatestStart)})" : "covered") : "";
            Console.WriteLine($"     {lane,-8} {fit,-22} " + string.Join("  ", evs.Select(e =>
                $"{e.Card.Id}@{e.Location}{TimeFmt.Format(e.From)}{(e.To != e.From ? "-" + TimeFmt.Format(e.To) : "")}")));
        }
        foreach (var k in b.Conflicts)
            Console.WriteLine($"     ! {k.Lane}: {k.A.Card.Id} vs {k.B.Card.Id} needs {k.Need} has {k.Have}{(k.Established ? "" : " (hyp)")}");
        foreach (var kv in b.CandidatesLeft)
            Console.WriteLine($"     ? {kv.Key}: [{string.Join(",", kv.Value)}]");
    }

    static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets"))) d = d.Parent;
        if (d == null) d = new DirectoryInfo(Directory.GetCurrentDirectory());
        return d.FullName;
    }
}
