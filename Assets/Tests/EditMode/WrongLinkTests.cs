using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlibiCo.Logic;
using NUnit.Framework;

namespace AlibiCo.Tests
{
    /// <summary>
    /// After a wrong link, Connie says why it couldn't have worked (Board.WhyNotLinked), by the clocks of
    /// the two cards linked: both already right, one clock twice, neither checked, or one unchecked clock
    /// and two different moments. Every wrong pair in cases 2–5 and a month of dockets is tried, at the
    /// start of the case and with its clocks mended, and the line must never name a third card.
    /// </summary>
    public class WrongLinkTests
    {
        static string DataDir => Path.Combine(UnityEngine.Application.dataPath, "Resources", "Data");

        static TownMap Map() => new TownMap(TownData.FromJson(File.ReadAllText(Path.Combine(DataDir, "town.json"))));

        static CaseDef Load(string id) => CaseDef.FromJson(File.ReadAllText(Path.Combine(DataDir, id + ".json")));

        /// <summary>A board with every card out, and optionally every link of the solution made.</summary>
        static Board Board(CaseDef c, TownMap map, bool mended)
        {
            var b = new Board(c, map, autoPin: true);
            if (mended)
                foreach (var m in Solver.ShortestSolution(b.Clone()))
                    if (m.Kind == "link") b.Link(m.A, m.B); else b.Confront(m.A);
            foreach (var card in c.Cards) b.Unlocked.Add(card.Id);
            b.Refresh();
            return b;
        }

        /// <summary>Tries every wrong pair on this board; returns which kinds of explanation came up.</summary>
        static HashSet<string> CheckAll(CaseDef c, Board start)
        {
            var kinds = new HashSet<string>();
            var cards = c.Cards.ToList();
            for (int i = 0; i < cards.Count; i++)
                for (int j = i + 1; j < cards.Count; j++)
                {
                    var x = cards[i];
                    var y = cards[j];
                    if (x.Event != null && x.Event == y.Event) continue;
                    var b = start.Clone();
                    Assert.IsTrue(b.Link(x.Id, y.Id).CostBadge, $"{c.Id}: {x.Id} + {y.Id} should be a wrong link");
                    var text = b.WhyNotLinked(x.Id, y.Id);
                    string where = $"{c.Id}: {x.Id} + {y.Id}: {text}";
                    StringAssert.DoesNotContain("the The ", text, where);
                    foreach (var other in c.Cards.Where(o => o.Id != x.Id && o.Id != y.Id && o.Title != x.Title && o.Title != y.Title))
                        StringAssert.DoesNotContain("“" + other.Title + "”", text, where + " names " + other.Id);
                    bool tx = b.IsTrusted(x.Clock), ty = b.IsTrusted(y.Clock);
                    if (tx && ty) { StringAssert.Contains("already trust", text, where); kinds.Add("trusted"); }
                    else if (x.Clock == y.Clock) { StringAssert.Contains(c.ClockById[x.Clock].InSentence, text, where); StringAssert.Contains("itself", text, where); kinds.Add("same"); }
                    else if (!tx && !ty) { StringAssert.Contains(c.ClockById[x.Clock].InSentence, text, where); StringAssert.Contains(c.ClockById[y.Clock].InSentence, text, where); kinds.Add("neither"); }
                    else { StringAssert.Contains(c.ClockById[tx ? y.Clock : x.Clock].InSentence, text, where); StringAssert.Contains("aren't the same moment", text, where); kinds.Add("one"); }
                }
            return kinds;
        }

        [Test]
        public void EveryWrongLinkInTheCasesIsExplained()
        {
            var map = Map();
            var seen = new HashSet<string>();
            foreach (var id in new[] { "case2", "case3", "case4", "case5" })
            {
                var c = Load(id);
                seen.UnionWith(CheckAll(c, Board(c, map, false)));
                seen.UnionWith(CheckAll(c, Board(c, map, true)));
            }
            // Case 5's chain of clocks has two unchecked clocks at once; every case has trusted pairs.
            CollectionAssert.IsSubsetOf(new[] { "trusted", "same", "neither", "one" }, seen.ToList(), "kinds seen: " + string.Join(",", seen));
        }

        [Test]
        public void EveryWrongLinkInAMonthOfDocketsIsExplained()
        {
            var map = Map();
            for (var date = new DateTime(2026, 10, 1); date < new DateTime(2026, 11, 1); date = date.AddDays(1))
            {
                var d = Docket.Generate(date, map);
                CheckAll(d.Case, Board(d.Case, map, false));
            }
        }

        [Test]
        public void StatementsAreNamedAsStatements()
        {
            var c = Load("case4");
            var b = Board(c, Map(), false);
            var text = b.WhyNotLinked("a_maud", "t_coast");
            StringAssert.Contains("'s statement", text);
            StringAssert.Contains("the Town Hall clock", text);
        }
    }
}
