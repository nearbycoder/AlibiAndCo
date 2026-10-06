using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlibiCo.Logic;
using NUnit.Framework;

namespace AlibiCo.Tests
{
    /// <summary>
    /// Runs the shared case validator on every case in Assets/Resources/Data: each case must have
    /// exactly one consistent solution, every contradiction must be discoverable, and no state may
    /// allow a wrong accusation. Also replays the shortest solution through the real board rules.
    /// </summary>
    public class CaseValidationTests
    {
        static string DataDir => Path.Combine(UnityEngine.Application.dataPath, "Resources", "Data");

        static TownMap Map() => new TownMap(TownData.FromJson(File.ReadAllText(Path.Combine(DataDir, "town.json"))));

        static IEnumerable<string> CaseFiles() =>
            Directory.GetFiles(Path.Combine(UnityEngine.Application.dataPath, "Resources", "Data"), "case*.json").OrderBy(f => f);

        [Test]
        public void ThereAreFourCases() => Assert.AreEqual(4, CaseFiles().Count());

        /// <summary>The finale must punish reflex-confronting: a true statement turns red before its clock is fixed.</summary>
        [Test]
        public void FinaleHasATrueStatementInTheRed()
        {
            var c = CaseDef.FromJson(File.ReadAllText(CaseFiles().Last()));
            var r = CaseValidator.Validate(c, Map());
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            Assert.IsNotEmpty(r.Traps, "no true statement is ever in an established contradiction");
            Assert.IsTrue(c.Clocks.Any(k => k.Id == c.Incident.Clock && !k.Reference), "the finale's incident should be timed by an untrusted clock");
        }

        [TestCaseSource(nameof(CaseFiles))]
        public void CaseIsAirtight(string file)
        {
            var c = CaseDef.FromJson(File.ReadAllText(file));
            var r = CaseValidator.Validate(c, Map());
            foreach (var i in r.Info) UnityEngine.Debug.Log($"[{c.Id}] {i}");
            foreach (var w in r.Warnings) UnityEngine.Debug.LogWarning($"[{c.Id}] {w}");
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
        }

        [TestCaseSource(nameof(CaseFiles))]
        public void SolutionReplaysAndOnlyCulpritCanBeAccused(string file)
        {
            var c = CaseDef.FromJson(File.ReadAllText(file));
            var map = Map();
            var b = new Board(c, map, autoPin: true);
            var path = Solver.ShortestSolution(b);
            Assert.IsNotNull(path);
            foreach (var m in path)
            {
                var o = m.Kind == "link" ? b.Link(m.A, m.B) : b.Confront(m.A);
                Assert.IsTrue(o.Accepted, $"{m} was rejected: {o.Message}");
                Assert.IsFalse(o.CostBadge, $"{m} cost a badge");
            }
            foreach (var p in c.Suspects)
            {
                var chk = b.CheckAccusation(p.Id);
                Assert.AreEqual(p.Id == c.Incident.Culprit, chk.Ok, $"{p.Id}: {chk.Message}");
            }
        }

        /// <summary>Case 4: Maud is right and her clock is wrong. Confronting her costs a badge; linking the Town Hall clock clears her.</summary>
        [Test]
        public void FinaleTrapIsClearedByTheClockNotAConfrontation()
        {
            var c = CaseDef.FromJson(File.ReadAllText(Path.Combine(DataDir, "case4.json")));
            var b = new Board(c, Map(), autoPin: true);
            Assert.IsTrue(b.Confront("a_claim").Accepted);
            Assert.IsTrue(b.Unlocked.Contains("a_maud"));
            Assert.IsTrue(b.EstablishedConflicts.Any(k => k.Involves("a_maud")), "Maud should be in the red before the clock is fixed");
            var o = b.Confront("a_maud");
            Assert.IsTrue(o.CostBadge);
            Assert.IsFalse(b.Struck.Contains("a_maud"));
            Assert.AreEqual(c.CardById["a_maud"].Firm, o.Reply);
            Assert.IsNotNull(b.Link("t_coast", "t_hosking").CalibratedClock);
            Assert.IsFalse(b.EstablishedConflicts.Any(k => k.Involves("a_maud")), "fixing the Town Hall clock should clear Maud");
            Assert.AreEqual(b.Case.Incident.From - 10, b.IncidentFrom, "the incident window moves with its clock");
        }

        [TestCaseSource(nameof(CaseFiles))]
        public void ConfrontingATrueStatementCostsABadge(string file)
        {
            var c = CaseDef.FromJson(File.ReadAllText(file));
            var b = new Board(c, Map(), autoPin: true);
            foreach (var card in c.Cards.Where(x => x.IsTestimony && !x.IsFalse && b.Unlocked.Contains(x.Id)))
            {
                if (!b.CanConfront(card.Id, out _)) continue;
                var o = b.Confront(card.Id);
                Assert.IsTrue(o.CostBadge);
                Assert.IsFalse(b.Struck.Contains(card.Id));
            }
        }
    }
}
