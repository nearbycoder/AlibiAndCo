using System;
using System.Collections.Generic;
using System.Globalization;

namespace AlibiCo.Logic
{
    public static class TimeFmt
    {
        /// <summary>"21:05" -> minutes since midnight.</summary>
        public static int Parse(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm)) throw new FormatException("empty time");
            var parts = hhmm.Split(':');
            return int.Parse(parts[0], CultureInfo.InvariantCulture) * 60 + int.Parse(parts[1], CultureInfo.InvariantCulture);
        }

        public static string Format(int minutes)
        {
            minutes = ((minutes % 1440) + 1440) % 1440;
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        public static string Span(int minutes) => minutes == 1 ? "1 min" : $"{minutes} min";
    }

    public sealed class Location
    {
        public string Id, Name, Short, Icon, Color, Blurb;
        public float X, Y;
    }

    public sealed class Street
    {
        public string A, B;
        public int Minutes;
    }

    /// <summary>The shared town of Wrenhaven: places, streets and walking times.</summary>
    public sealed class TownData
    {
        public string Name;
        public readonly List<Location> Locations = new List<Location>();
        public readonly List<Street> Streets = new List<Street>();
        public readonly Dictionary<string, Location> ById = new Dictionary<string, Location>();

        public static TownData FromJson(string json)
        {
            var root = J.Obj(MiniJson.Parse(json));
            var t = new TownData { Name = J.Str(root, "name", "Wrenhaven") };
            foreach (var o in J.Objs(root, "locations"))
            {
                var l = new Location
                {
                    Id = J.Str(o, "id"),
                    Name = J.Str(o, "name"),
                    Short = J.Str(o, "short", J.Str(o, "name")),
                    Icon = J.Str(o, "icon", ""),
                    Color = J.Str(o, "color", "#888888"),
                    Blurb = J.Str(o, "blurb", ""),
                    X = J.Float(o, "x"),
                    Y = J.Float(o, "y"),
                };
                t.Locations.Add(l);
                t.ById[l.Id] = l;
            }
            foreach (var o in J.Objs(root, "streets"))
                t.Streets.Add(new Street { A = J.Str(o, "a"), B = J.Str(o, "b"), Minutes = J.Int(o, "min") });
            return t;
        }
    }

    public sealed class Person
    {
        public string Id, Name, Role, Blurb, Portrait;
        public bool IsSuspect => Role == "suspect";
    }

    public sealed class ClockDef
    {
        public string Id, Name, Note;
        /// <summary>How the clock reads mid-sentence ("the Town Hall clock"), if the name alone doesn't.</summary>
        public string Phrase;
        public bool Reference;
        /// <summary>Shown time = true time + offset (a fast clock has a positive offset).</summary>
        public int TrueOffset;
        /// <summary>Offset already known when the case opens (learned in an earlier case).</summary>
        public bool KnownAtStart;

        /// <summary>"the Lantern's bar clock", "the press camera's date-back": the clock inside a sentence.</summary>
        public string InSentence => Phrase ?? (Name.StartsWith("The ", StringComparison.Ordinal) ? "the " + Name.Substring(4) : "the " + Name);
    }

    public enum Truth { True, Lie, Mistaken }

    public sealed class CardDef
    {
        public string Id, Kind, Title, Text, Source, SourceName, Location, Clock, Event, Image;
        /// <summary>Times as printed on the card, on <see cref="Clock"/>.</summary>
        public int From, To;
        public readonly List<string> Subjects = new List<string>();
        public readonly List<string> Candidates = new List<string>();
        public bool Town;
        public Truth Truth;
        public string TrueSubject;
        public bool StartsAvailable;
        public string Reply, Firm;
        public readonly List<string> UnlocksOnStrike = new List<string>();
        public readonly List<string> UnlocksOnConfirm = new List<string>();

        public bool IsRecord => Source == "record";
        public bool IsTestimony => !IsRecord;
        public bool IsUnknown => !Town && Subjects.Count == 0;
        public bool IsInstant => From == To;
        public bool IsFalse => Truth != Truth.True;
    }

    public enum TriggerKind { Conflict, Calibrate, Strike, Confirm }

    public sealed class TriggerDef
    {
        public TriggerKind Kind;
        public string A, B;   // card ids for Conflict / Strike / Confirm; clock id in A for Calibrate
        public readonly List<string> Unlocks = new List<string>();
        public string Question, Memo;
        public string Key => Kind + ":" + A + (B != null ? "|" + B : "");
    }

    public sealed class IncidentDef
    {
        public string Title, Text, Location, Clock, Culprit, Short;
        public int From, To, Duration;
    }

    public sealed class Stop
    {
        public string Location;
        public int From, To;
    }

    public sealed class MemoDef
    {
        public string When, Text;
    }

    public sealed class CaseDef
    {
        public string Id, Title, Tagline, Date, Weather, Lesson;
        public int SpanFrom, SpanTo;
        /// <summary>Par time in seconds for the "Swift" seal ("par": "m:ss"); 0 when the case has none.</summary>
        public int ParSeconds;
        public readonly List<string> Intro = new List<string>();
        public readonly List<string> Epilogue = new List<string>();
        public readonly List<Person> People = new List<Person>();
        public readonly List<ClockDef> Clocks = new List<ClockDef>();
        public readonly List<CardDef> Cards = new List<CardDef>();
        public readonly List<TriggerDef> Triggers = new List<TriggerDef>();
        public readonly List<MemoDef> Memos = new List<MemoDef>();
        public readonly Dictionary<string, List<Stop>> Itineraries = new Dictionary<string, List<Stop>>();
        public readonly List<string> ReconstructionLines = new List<string>();
        public IncidentDef Incident;
        public bool HasTownLane;

        public readonly Dictionary<string, CardDef> CardById = new Dictionary<string, CardDef>();
        public readonly Dictionary<string, Person> PersonById = new Dictionary<string, Person>();
        public readonly Dictionary<string, ClockDef> ClockById = new Dictionary<string, ClockDef>();

        public IEnumerable<Person> Suspects
        {
            get { foreach (var p in People) if (p.IsSuspect) yield return p; }
        }

        public static CaseDef FromJson(string json)
        {
            var root = J.Obj(MiniJson.Parse(json));
            var c = new CaseDef
            {
                Id = J.Str(root, "id"),
                Title = J.Str(root, "title"),
                Tagline = J.Str(root, "tagline", ""),
                Date = J.Str(root, "date", ""),
                Weather = J.Str(root, "weather", ""),
                Lesson = J.Str(root, "lesson", ""),
                SpanFrom = TimeFmt.Parse(J.Str(root, "spanFrom")),
                SpanTo = TimeFmt.Parse(J.Str(root, "spanTo")),
            };
            var par = J.Str(root, "par");
            if (!string.IsNullOrEmpty(par)) c.ParSeconds = TimeFmt.Parse(par);   // "m:ss" parses as minutes*60 + seconds
            c.Intro.AddRange(J.Strs(root, "intro"));
            c.Epilogue.AddRange(J.Strs(root, "epilogue"));
            c.ReconstructionLines.AddRange(J.Strs(root, "reconstruction"));

            foreach (var o in J.Objs(root, "people"))
            {
                var p = new Person
                {
                    Id = J.Str(o, "id"),
                    Name = J.Str(o, "name"),
                    Role = J.Str(o, "role", "suspect"),
                    Blurb = J.Str(o, "blurb", ""),
                    Portrait = J.Str(o, "portrait", J.Str(o, "id")),
                };
                c.People.Add(p);
                c.PersonById[p.Id] = p;
            }

            // Every case has the reference clock implicitly.
            var refClock = new ClockDef { Id = "ref", Name = "Reliable time", Reference = true };
            c.Clocks.Add(refClock);
            c.ClockById["ref"] = refClock;
            foreach (var o in J.Objs(root, "clocks"))
            {
                var k = new ClockDef
                {
                    Id = J.Str(o, "id"),
                    Name = J.Str(o, "name"),
                    Note = J.Str(o, "note", ""),
                    Phrase = J.Str(o, "phrase", null),
                    Reference = J.Bool(o, "reference"),
                    TrueOffset = J.Int(o, "offset"),
                    KnownAtStart = J.Bool(o, "known"),
                };
                if (k.Id == "ref") { refClock.Name = k.Name; continue; }
                c.Clocks.Add(k);
                c.ClockById[k.Id] = k;
            }

            foreach (var o in J.Objs(root, "cards"))
            {
                var card = new CardDef
                {
                    Id = J.Str(o, "id"),
                    Kind = J.Str(o, "kind", "statement"),
                    Title = J.Str(o, "title", ""),
                    Text = J.Str(o, "text", ""),
                    Source = J.Str(o, "source", "record"),
                    SourceName = J.Str(o, "sourceName", ""),
                    Location = J.Str(o, "location"),
                    Clock = J.Str(o, "clock", "ref"),
                    Event = J.Str(o, "event"),
                    Image = J.Str(o, "image"),
                    Town = J.Bool(o, "town"),
                    TrueSubject = J.Str(o, "trueSubject"),
                    StartsAvailable = J.Bool(o, "start"),
                    Reply = J.Str(o, "reply", ""),
                    Firm = J.Str(o, "firm", ""),
                };
                var at = J.Str(o, "at");
                if (at != null) card.From = card.To = TimeFmt.Parse(at);
                else
                {
                    card.From = TimeFmt.Parse(J.Str(o, "from"));
                    card.To = TimeFmt.Parse(J.Str(o, "to"));
                }
                card.Subjects.AddRange(J.Strs(o, "subjects"));
                card.Candidates.AddRange(J.Strs(o, "candidates"));
                switch (J.Str(o, "truth", "true"))
                {
                    case "lie": card.Truth = Truth.Lie; break;
                    case "mistaken": card.Truth = Truth.Mistaken; break;
                    default: card.Truth = Truth.True; break;
                }
                card.UnlocksOnStrike.AddRange(J.Strs(o, "unlocks"));
                card.UnlocksOnConfirm.AddRange(J.Strs(o, "unlocksOnConfirm"));
                if (card.Town) c.HasTownLane = true;
                c.Cards.Add(card);
                c.CardById[card.Id] = card;
            }

            foreach (var o in J.Objs(root, "triggers"))
            {
                var t = new TriggerDef
                {
                    A = J.Str(o, "a"),
                    B = J.Str(o, "b"),
                    Question = J.Str(o, "question"),
                    Memo = J.Str(o, "memo"),
                };
                switch (J.Str(o, "on"))
                {
                    case "conflict": t.Kind = TriggerKind.Conflict; break;
                    case "calibrate": t.Kind = TriggerKind.Calibrate; break;
                    case "strike": t.Kind = TriggerKind.Strike; break;
                    case "confirm": t.Kind = TriggerKind.Confirm; break;
                    default: throw new FormatException($"trigger 'on' must be conflict/calibrate/strike/confirm in {c.Id}");
                }
                t.Unlocks.AddRange(J.Strs(o, "unlocks"));
                c.Triggers.Add(t);
            }

            foreach (var o in J.Objs(root, "memos"))
                c.Memos.Add(new MemoDef { When = J.Str(o, "when"), Text = J.Str(o, "text") });

            var inc = J.Obj(root["incident"]);
            c.Incident = new IncidentDef
            {
                Title = J.Str(inc, "title"),
                Short = J.Str(inc, "short", J.Str(inc, "title")),
                Text = J.Str(inc, "text"),
                Location = J.Str(inc, "location"),
                Clock = J.Str(inc, "clock", "ref"),
                Culprit = J.Str(inc, "culprit"),
                From = TimeFmt.Parse(J.Str(inc, "from")),
                To = TimeFmt.Parse(J.Str(inc, "to")),
                Duration = J.Int(inc, "duration"),
            };

            if (root.TryGetValue("truth", out var truthObj) && truthObj is Dictionary<string, object> truth)
            {
                foreach (var kv in truth)
                {
                    var stops = new List<Stop>();
                    if (kv.Value is List<object> l)
                        foreach (var x in l)
                        {
                            var so = J.Obj(x);
                            stops.Add(new Stop
                            {
                                Location = J.Str(so, "at"),
                                From = TimeFmt.Parse(J.Str(so, "from")),
                                To = TimeFmt.Parse(J.Str(so, "to")),
                            });
                        }
                    c.Itineraries[kv.Key] = stops;
                }
            }
            return c;
        }
    }
}
