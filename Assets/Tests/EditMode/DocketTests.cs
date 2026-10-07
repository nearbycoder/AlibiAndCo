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
        public void CrimesDontRepeatWithinFifteenDays()
        {
            var map = Map();
            var start = new DateTime(2027, 3, 1);
            var titles = Enumerable.Range(0, 30).Select(i => Docket.Generate(start.AddDays(i), map).Case.Title).ToList();
            Assert.AreEqual(15, titles.Distinct().Count());
            for (int i = 0; i < titles.Count; i++)
                for (int j = i + 1; j < Math.Min(titles.Count, i + 15); j++)
                    Assert.AreNotEqual(titles[i], titles[j], $"day {i} and day {j} have the same crime");
            for (int i = 0; i < 30; i++)
                Assert.AreEqual(Docket.TitleFor(start.AddDays(i)), titles[i], "the drawer's title for a day must match its docket");
        }

        /// <summary>
        /// The words around the puzzle: the clock-day note is a prop from the wrong clock's own place
        /// (not an order pad at the pier), and the intro doesn't always name the culprit first.
        /// </summary>
        [Test]
        public void TheWordsFitTheDay()
        {
            var map = Map();
            var start = new DateTime(2026, 10, 1);
            int culpritFirst = 0, clockDays = 0;
            var notes = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 60; i++)
            {
                var c = Docket.Generate(start.AddDays(i), map).Case;
                string First(string name) => name.StartsWith("Capt. ") ? "Captain " + name.Split(' ').Last() : name.Split(' ')[0];
                var named = c.Suspects.OrderBy(p => c.Intro[1].IndexOf(First(p.Name), StringComparison.Ordinal)).First();
                if (named.Id == c.Incident.Culprit) culpritFirst++;
                if (!c.CardById.TryGetValue("t_clock", out var note)) continue;
                clockDays++;
                notes.Add(note.Title);
                if (note.Title == "Order pad note") Assert.AreEqual("cafe", note.Location, "only the café writes on an order pad");
                StringAssert.Contains(c.ClockById["k"].Phrase.Substring(4), note.Text);
            }
            Assert.Greater(notes.Count, 2, "every clock day's note is the same prop");
            Assert.Less(culpritFirst, 40, "the intro names the culprit first too often");
        }

        /// <summary>The drawer's seven days, today first, across a month and a year boundary.</summary>
        [Test]
        public void TheWeekOnFile()
        {
            var week = Docket.Week(new DateTime(2027, 1, 2, 23, 59, 0));
            Assert.AreEqual(7, week.Count);
            Assert.AreEqual(new DateTime(2027, 1, 2), week[0]);
            Assert.AreEqual(new DateTime(2026, 12, 27), week[6]);
            Assert.AreEqual(7, week.Select(Docket.IdFor).Distinct().Count());
            var march = Docket.Week(new DateTime(2028, 3, 1));
            Assert.AreEqual(new DateTime(2028, 2, 29), march[1], "a leap day is on file like any other");
            Assert.AreEqual(new DateTime(2027, 1, 8), Docket.OnFileUntil(new DateTime(2027, 1, 2)));
            Assert.IsNotNull(Docket.Generate(new DateTime(2028, 2, 29), Map()), "no docket for a leap day");
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

        [Test]
        public void TheShareLineSpoilsNothing()
        {
            var day = new DateTime(2026, 10, 7);
            Assert.AreEqual("Alibi & Co. Daily Docket, Wed 7 Oct 2026: " + Docket.TitleFor(day) + " ★★★ 2:41 (Clean · Unaided · Swift)",
                Docket.ShareLine(day, 3, 161.4f, true, true, true));
            Assert.AreEqual("Alibi & Co. Daily Docket, Wed 7 Oct 2026: " + Docket.TitleFor(day) + " ★★☆ 12:05 (Swift)",
                Docket.ShareLine(day, 2, 725f, false, false, true));
            StringAssert.EndsWith("☆☆☆ 0:00", Docket.ShareLine(day, -1, -3f, false, false, false));
            // No suspect's name, no card and no clock: only the day, the crime and how it went.
            var map = Map();
            for (int i = 0; i < 15; i++)
            {
                var d = day.AddDays(i);
                var c = Docket.Generate(d, map).Case;
                var line = Docket.ShareLine(d, 3, 100, true, true, true);
                foreach (var p in c.People)
                {
                    StringAssert.DoesNotContain(p.Name, line, $"{d:yyyy-MM-dd} names {p.Name}");
                    StringAssert.DoesNotContain(p.Name.Split(' ')[0], line.Replace(c.Title, ""), $"{d:yyyy-MM-dd} names {p.Name}");
                }
                foreach (var k in c.Clocks) StringAssert.DoesNotContain(k.Name, line);
            }
        }
    }
}
