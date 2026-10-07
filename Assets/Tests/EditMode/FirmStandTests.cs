using System;
using System.IO;
using System.Linq;
using AlibiCo.Logic;
using NUnit.Framework;

namespace AlibiCo.Tests
{
    /// <summary>
    /// When an honest witness stands firm, Connie says what made their story red (Board.WhyFirm):
    /// the clock nobody has checked yet, by name. Checked on the traps of cases 2, 4 and 5 and on
    /// a clock-day docket.
    /// </summary>
    public class FirmStandTests
    {
        static string DataDir => Path.Combine(UnityEngine.Application.dataPath, "Resources", "Data");

        static TownMap Map() => new TownMap(TownData.FromJson(File.ReadAllText(Path.Combine(DataDir, "town.json"))));

        static CaseDef Load(string id) => CaseDef.FromJson(File.ReadAllText(Path.Combine(DataDir, id + ".json")));

        /// <summary>Confront these lies first, then play the shortest solution until the card can be confronted, and confront it.</summary>
        static Board RedAndConfronted(CaseDef c, string cardId, params string[] liesFirst)
        {
            var b = new Board(c, Map(), autoPin: true);
            foreach (var id in liesFirst) Assert.IsTrue(b.Confront(id).Accepted, $"{c.Id}: {id} should be a lie");
            foreach (var m in Solver.ShortestSolution(b.Clone()))
            {
                if (b.CanConfront(cardId, out _)) break;
                if (m.Kind == "link") b.Link(m.A, m.B); else b.Confront(m.A);
            }
            Assert.IsTrue(b.CanConfront(cardId, out var why), $"{c.Id}: {cardId} never turned red ({why})");
            var o = b.Confront(cardId);
            Assert.IsTrue(o.CostBadge, $"{c.Id}: {cardId} should stand firm");
            return b;
        }

        static void AssertNames(string text, string clock)
        {
            StringAssert.Contains("stood firm", text);
            StringAssert.Contains(clock, text);
            StringAssert.DoesNotContain("the The ", text);
        }

        [Test]
        public void CaseTwoBlamesTheLanternClock()
        {
            var c = Load("case2");
            AssertNames(RedAndConfronted(c, "i_sid").WhyFirm("i_sid"), "the Lantern's bar clock");
        }

        [Test]
        public void CaseFourBlamesTheTownHallClock()
        {
            var c = Load("case4");
            AssertNames(RedAndConfronted(c, "a_maud", "a_claim").WhyFirm("a_maud"), "the Town Hall clock");
        }

        [Test]
        public void CaseFiveBlamesTheGlasshouseClock()
        {
            var c = Load("case5");
            AssertNames(RedAndConfronted(c, "n_penrose").WhyFirm("n_penrose"), "the Glasshouse clock");
        }

        /// <summary>On a clock day the honest story's own clock is fine; the card against it isn't.</summary>
        [Test]
        public void ClockDayBlamesTheOtherCardsClock()
        {
            var map = Map();
            for (var date = new DateTime(2026, 10, 1); date < new DateTime(2026, 11, 1); date = date.AddDays(1))
            {
                var d = Docket.Generate(date, map);
                if (!d.ClockDay) continue;
                var text = RedAndConfronted(d.Case, "h_claim").WhyFirm("h_claim");
                AssertNames(text, d.Case.ClockById["k"].InSentence);
                StringAssert.Contains("The card against them", text);
                StringAssert.DoesNotContain("the Pier", text);   // "the pier turnstile clock", not the capitalised label
            }
        }

        [Test]
        public void ClockNamesReadInsideASentence()
        {
            Assert.AreEqual("the press camera's date-back", Load("case3").ClockById["camera"].InSentence);
            Assert.AreEqual("the Lantern's bar clock", Load("case3").ClockById["lantern"].InSentence);
            Assert.AreEqual("the Town Hall clock", Load("case4").ClockById["hall"].InSentence);
        }
    }
}
