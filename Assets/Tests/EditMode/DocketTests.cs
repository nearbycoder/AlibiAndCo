using System;
using System.IO;
using System.Linq;
using AlibiCo.Logic;
using NUnit.Framework;

namespace AlibiCo.Tests
{
    /// <summary>
    /// The Daily Docket generator (Logic/Docket): every day gets a case the validator proves
    /// airtight, a date always gives the same case, and the clock days really have their trap.
    /// The validator console sweeps far more days (Tools/validate.sh --docket 1000).
    /// </summary>
    public class DocketTests
    {
        static TownMap Map() => new TownMap(TownData.FromJson(File.ReadAllText(
            Path.Combine(UnityEngine.Application.dataPath, "Resources", "Data", "town.json"))));

        [Test]
        public void SixtyDaysAreAllAirtight()
        {
            var map = Map();
            var start = new DateTime(2026, 10, 1);
            int clockDays = 0;
            for (int i = 0; i < 60; i++)
            {
                var date = start.AddDays(i);
                var d = Docket.Generate(date, map);
                Assert.IsNotNull(d, $"no airtight docket for {date:yyyy-MM-dd}");
                Assert.IsTrue(d.Report.Ok, string.Join("\n", d.Report.Errors));
                Assert.AreEqual(Docket.IdFor(date), d.Case.Id);
                Assert.AreEqual(3, d.Case.Suspects.Count());
                Assert.IsNotNull(d.Report.Solution);
                if (d.ClockDay)
                {
                    clockDays++;
                    Assert.Contains("h_claim", d.Report.Traps.ToList(), "a clock day's honest story never turns red");
                    Assert.IsTrue(d.Report.Solution.Any(m => m.Kind == "link"), "a clock day is solved without a link");
                }
            }
            Assert.That(clockDays, Is.InRange(5, 55), "clock days should be a share of the dockets, not none or all");
        }

        [Test]
        public void TheSameDateGivesTheSameDocket()
        {
            var map = Map();
            var date = new DateTime(2026, 12, 25);
            Assert.AreEqual(Docket.Generate(date, map).Json, Docket.Generate(date, map).Json);
            Assert.AreNotEqual(Docket.Generate(date, map).Json, Docket.Generate(date.AddDays(1), map).Json);
        }

        [Test]
        public void CrimesDontRepeatWithinThirteenDays()
        {
            var map = Map();
            var start = new DateTime(2027, 3, 1);
            var titles = Enumerable.Range(0, 26).Select(i => Docket.Generate(start.AddDays(i), map).Case.Title).ToList();
            for (int i = 0; i < titles.Count; i++)
                for (int j = i + 1; j < Math.Min(titles.Count, i + 13); j++)
                    Assert.AreNotEqual(titles[i], titles[j], $"day {i} and day {j} have the same crime");
        }

        [Test]
        public void IdsRoundTrip()
        {
            var date = new DateTime(2026, 2, 28);
            Assert.IsTrue(Docket.TryParseId(Docket.IdFor(date), out var back));
            Assert.AreEqual(date, back);
            Assert.IsFalse(Docket.TryParseId("case3", out _));
            Assert.IsFalse(Docket.TryParseId("docket-yesterday", out _));
        }

        [Test]
        public void SpokenTimes()
        {
            Assert.AreEqual("half eight", Docket.Say(20 * 60 + 30));
            Assert.AreEqual("twenty to nine", Docket.Say(20 * 60 + 40));
            Assert.AreEqual("quarter past ten", Docket.Say(22 * 60 + 15));
            Assert.AreEqual("nine o'clock", Docket.Say(21 * 60));
            Assert.AreEqual("twenty-five to eight", Docket.Say(19 * 60 + 35));
        }
    }
}
