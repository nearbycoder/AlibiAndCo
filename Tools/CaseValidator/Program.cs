using System;
using System.IO;
using System.Linq;
using AlibiCo.Logic;

// Usage: dotnet run --project Tools/CaseValidator [-- [--verbose] [caseId...]]
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
