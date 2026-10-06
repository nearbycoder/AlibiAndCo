using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AlibiCo.Logic
{
    /// <summary>
    /// The Daily Docket: a short generated case for each calendar day. Three of Wrenhaven's
    /// regulars, a small crime at one of the town's places, and three stories: the culprit's, which
    /// a record breaks; an innocent's, which is a lie about something else, broken by a record and
    /// then backed by a witness; and an honest one. On some days the honest story turns red because
    /// of a wrong clock, and a link (not a confrontation) clears it.
    ///
    /// The generator writes an ordinary case JSON, so the game, the validator and the tests load it
    /// exactly like a handwritten case. A day's docket is only offered once
    /// <see cref="CaseValidator"/> has proven it airtight; if a variation fails, the next is tried.
    /// Everything is driven by a seeded generator of its own (not System.Random), so a date gives
    /// the same docket in the game, in the browser build and in the .NET validator.
    /// </summary>
    public static class Docket
    {
        public const string Prefix = "docket-";
        public const int MaxAttempts = 40;

        public static string IdFor(DateTime date) => Prefix + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static bool IsDocket(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryParseId(string id, out DateTime date)
        {
            date = default;
            return IsDocket(id) && DateTime.TryParseExact(id.Substring(Prefix.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        public sealed class Result
        {
            public CaseDef Case;
            public string Json;
            public int Attempt;
            public bool ClockDay;
            public ValidationReport Report;
        }

        /// <summary>Today's (or any day's) docket, proven airtight; null only if every variation failed.</summary>
        public static Result Generate(DateTime date, TownMap map)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var rng = new Rng(Seed(date, attempt));
                var json = Build(rng, date, map, out bool clockDay);
                if (json == null) continue;
                var c = CaseDef.FromJson(json);
                var report = CaseValidator.Validate(c, map);
                if (!report.Ok || report.Solution == null) continue;
                // A clock day must really have its trap: the honest story in the red until the link.
                if (clockDay && !report.Traps.Contains("h_claim")) continue;
                return new Result { Case = c, Json = json, Attempt = attempt, ClockDay = clockDay, Report = report };
            }
            return null;
        }

        /// <summary>The crimes come round on a fixed rota, so a crime never returns sooner than
        /// thirteen days later; the people, places and times around it change every day.</summary>
        static Crime CrimeFor(DateTime date)
        {
            var order = new Rng(0xC0FFEEUL).Shuffle(Crimes);
            int day = (int)(date.Date - new DateTime(2000, 1, 1)).TotalDays;
            return order[((day % order.Count) + order.Count) % order.Count];
        }

        static ulong Seed(DateTime d, int attempt) => (ulong)(d.Year * 10000 + d.Month * 100 + d.Day) * 0x9E3779B97F4A7C15UL ^ (ulong)(attempt + 1) * 0xBF58476D1CE4E5B9UL;

        /// <summary>SplitMix64: small, fast and identical on every runtime.</summary>
        sealed class Rng
        {
            ulong s;
            public Rng(ulong seed) { s = seed; }
            ulong NextU()
            {
                s += 0x9E3779B97F4A7C15UL;
                ulong z = s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
            public int Next(int n) => n <= 1 ? 0 : (int)(NextU() % (ulong)n);
            public bool Chance(int percent) => Next(100) < percent;
            /// <summary>A random item, or default when the list is empty (that draw doesn't fit).</summary>
            public T Pick<T>(IList<T> list) => list.Count == 0 ? default : list[Next(list.Count)];
            public List<T> Shuffle<T>(IEnumerable<T> items)
            {
                var l = items.ToList();
                for (int i = l.Count - 1; i > 0; i--) { int j = Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
                return l;
            }
        }

        // ------------------------------------------------------------------ the town's people and places

        sealed class Regular
        {
            public string Id, Name, Blurb;
            public string First => Name.StartsWith("Capt. ") ? "Captain " + Name.Split(' ').Last() : Name.Split(' ')[0];
            /// <summary>"A. TRELAWNEY", "CAPT. A. ROOK": how a till roll or a ledger writes the name.</summary>
            public string OnPaper
            {
                get
                {
                    var parts = Name.Split(' ');
                    var title = parts[0] == "Capt." ? "CAPT. " : "";
                    var given = parts[0] == "Capt." ? parts[1] : parts[0];
                    return (title + given[0] + ". " + parts.Last()).ToUpperInvariant();
                }
            }
        }

        static readonly Regular[] Regulars =
        {
            new Regular { Id = "agnes", Name = "Agnes Trelawney", Blurb = "WI treasurer and the Bake-Off's eternal runner-up. Never forgets a slight." },
            new Regular { Id = "bram", Name = "Bram Okafor", Blurb = "Maud's apprentice. Up at four for the bread, out most evenings anyway." },
            new Regular { Id = "clem", Name = "Clem Hollis", Blurb = "Caterer and chess fiend. Knows every kitchen door in town." },
            new Regular { Id = "marlow", Name = "Marlow Quint", Blurb = "Yacht Club treasurer. Pressed suit, frayed cuffs, a bookie who knows his name." },
            new Regular { Id = "ines", Name = "Ines Delacroix", Blurb = "Breton skipper, wintering her yacht at the Club. Keeps her own counsel." },
            new Regular { Id = "rolf", Name = "Rolf Abernethy", Blurb = "Retired boathouse steward. Everybody trusts Rolf." },
            new Regular { Id = "nell", Name = "Nell Garrow", Blurb = "The keeper's daughter, apprentice gardener at the Park. Restless." },
            new Regular { Id = "elias", Name = "Elias Garrow", Blurb = "Keeper of Wren Point. A steady bell-ringer and a steadier darts player." },
            new Regular { Id = "cole", Name = "Bertram Cole", Blurb = "Fisherman, and doorman at the Grand on Saturdays. Holds a grudge." },
            new Regular { Id = "rook", Name = "Capt. Augustus Rook", Blurb = "Owner of the Merry Wren. Likes to be seen, and to be thanked." },
        };

        /// <summary>What each place can say about a person: what they'd be doing there, the paper it
        /// leaves, who'd vouch for them, and (for a few) a clock that could be wrong.</summary>
        sealed class Place
        {
            public string Id, At, Noun, Doing;
            public string RecordKind, RecordTitle, RecordSource;
            public string[] RecordLines;   // {name} is the person as written on paper
            public string Witness, WitnessRole, WitnessSaw;
            public string ClockName;       // null: this place's paper is timed by a reliable clock
        }

        static readonly Place[] Places =
        {
            new Place { Id = "boathouse", At = "at the Yacht Club boathouse", Noun = "the boathouse", Doing = "varnishing a dinghy in the Yacht Club boathouse",
                RecordKind = "ledger", RecordTitle = "Slipway log", RecordSource = "Yacht Club Boathouse · steward's log",
                RecordLines = new[] { "Launch key signed out: {name}.", "Locker 9 opened, signed {name}." },
                Witness = "Kit Nancarrow", WitnessRole = "Barman at the Yacht Club", WitnessSaw = "Sanding the same dinghy all evening. I lent the sandpaper." },
            new Place { Id = "cafe", At = "at the Harbour Café", Noun = "the café", Doing = "over a pot of tea at the Harbour Café",
                RecordKind = "receipt", RecordTitle = "Till receipt", RecordSource = "Harbour Café",
                RecordLines = new[] { "1 × pot of tea\n1 × slice of parkin\nON THE SLATE: {name}", "2 × toasted teacake\nON THE SLATE: {name}" },
                Witness = "Morwenna Tregear", WitnessRole = "Behind the counter at the Harbour Café", WitnessSaw = "Window table. Three pots of tea and the crossword.",
                ClockName = "Harbour Café till clock" },
            new Place { Id = "pier", At = "on the pier", Noun = "the pier", Doing = "fishing off the end of the pier",
                RecordKind = "ticket", RecordTitle = "Turnstile ticket", RecordSource = "The Pier · evening turnstile",
                RecordLines = new[] { "Admit one, angler. Season card No. 31, {name}." },
                Witness = "Wilf Couch", WitnessRole = "Night watchman on the pier", WitnessSaw = "Sat on the end bench with a rod and caught nothing. As usual.",
                ClockName = "pier turnstile clock" },
            new Place { Id = "lantern", At = "in the Lantern", Noun = "the Lantern", Doing = "having a quiet half by the fire in the Lantern",
                RecordKind = "receipt", RecordTitle = "Bar tab", RecordSource = "The Lantern",
                RecordLines = new[] { "{name}\n2 × bitter\nSettled at the bar.", "{name}\n1 × sweet sherry, 1 × crisps\nSettled at the bar." },
                Witness = "Sid Barrow", WitnessRole = "Landlord of the Lantern", WitnessSaw = "Sat at the end of the bar and never shifted." },
            new Place { Id = "bandstand", At = "at the bandstand", Noun = "the bandstand", Doing = "listening to the brass band rehearse at the bandstand",
                RecordKind = "call", RecordTitle = "Exchange log", RecordSource = "Wrenhaven Telephone Exchange",
                RecordLines = new[] { "Kiosk by the bandstand → Polgarth 214.\nCaller gave the name {name}." },
                Witness = "Percy Hambly", WitnessRole = "Bandmaster, Wrenhaven Silver Band", WitnessSaw = "Front row of the deckchairs, tapping along. Every number." },
            new Place { Id = "hotel", At = "at the Grand", Noun = "the Grand", Doing = "at the bar of the Grand",
                RecordKind = "ledger", RecordTitle = "Night porter's ledger", RecordSource = "The Grand Hotel",
                RecordLines = new[] { "Cloakroom ticket 14 collected: {name}.", "Bar bill signed: {name}." },
                Witness = "Arthur Vosper", WitnessRole = "Night porter at the Grand", WitnessSaw = "In the corner of the bar with the evening paper. Didn't stir.",
                ClockName = "Grand's lobby clock" },
            new Place { Id = "cliff", At = "on the cliff path", Noun = "the cliff path", Doing = "walking the cliff path",
                RecordKind = "ledger", RecordTitle = "Coastguard log", RecordSource = "Coastguard hut, Wren Point road",
                RecordLines = new[] { "Walker passed the hut heading for the Point. Gave the name {name}." },
                Witness = "Len Tonkin", WitnessRole = "Coastguard on watch", WitnessSaw = "Up and down past my hut like a sentry. Waved every time." },
            new Place { Id = "hardware", At = "at Fenwick's", Noun = "Fenwick's", Doing = "at Fenwick's late counter",
                RecordKind = "receipt", RecordTitle = "Till receipt", RecordSource = "Fenwick's Hardware · night counter",
                RecordLines = new[] { "1 × box of tacks\n1 × tin of putty\nON ACCOUNT: {name}", "1 × paraffin, 1 pint\nON ACCOUNT: {name}" },
                Witness = "Gerald Fenwick", WitnessRole = "Runs the night counter at Fenwick's", WitnessSaw = "Talked my ear off about drill bits the whole time.",
                ClockName = "Fenwick's till clock" },
            new Place { Id = "bakery", At = "at Penhallow's", Noun = "Penhallow's", Doing = "helping with tomorrow's bread at Penhallow's",
                RecordKind = "receipt", RecordTitle = "Bakery till receipt", RecordSource = "Penhallow's Bakery",
                RecordLines = new[] { "6 × day-old rolls\nON ACCOUNT: {name}", "1 × Cornish pasty\nPAID: {name}" },
                Witness = "Maud Penhallow", WitnessRole = "Penhallow's Bakery", WitnessSaw = "Floured to the elbows the whole time. I'd know.",
                ClockName = "bakery wall clock" },
            new Place { Id = "townhall", At = "at the Town Hall", Noun = "the Town Hall", Doing = "at the Parish Council meeting in the Town Hall",
                RecordKind = "ledger", RecordTitle = "Door book", RecordSource = "Town Hall · caretaker's door book",
                RecordLines = new[] { "Signed in at the front desk: {name}.", "Key to the committee room returned: {name}." },
                Witness = "Mr Hosking", WitnessRole = "Town Hall caretaker", WitnessSaw = "Second row, objecting to the car park. Twice." },
            new Place { Id = "depot", At = "at the bus depot", Noun = "the depot", Doing = "waiting for the Polgarth bus at the depot",
                RecordKind = "ticket", RecordTitle = "Bus ticket", RecordSource = "Route 4 · Wrenhaven depot",
                RecordLines = new[] { "Season ticket in the name of {name}, clipped by the conductor." },
                Witness = "Reg Bolitho", WitnessRole = "Inspector at the bus depot", WitnessSaw = "On the bench under the timetable, grumbling about the Polgarth bus.",
                ClockName = "depot clock" },
            new Place { Id = "cinema", At = "at the Odeon", Noun = "the Odeon", Doing = "watching the picture at the Odeon",
                RecordKind = "ticket", RecordTitle = "Ticket stub", RecordSource = "The Odeon · Screen 1",
                RecordLines = new[] { "Admit one, stalls. Booked in the name of {name}." },
                Witness = "Joan Pascoe", WitnessRole = "Usherette at the Odeon", WitnessSaw = "Row F, on the aisle. I showed them in myself.",
                ClockName = "Odeon box-office clock" },
            new Place { Id = "church", At = "at St Brigid's", Noun = "St Brigid's", Doing = "at bell practice at St Brigid's",
                RecordKind = "ledger", RecordTitle = "Vestry book", RecordSource = "St Brigid's Church",
                RecordLines = new[] { "Tower key signed for: {name}.", "Hymn books returned: {name}." },
                Witness = "Edna Rowe", WitnessRole = "Verger at St Brigid's", WitnessSaw = "On the tenor bell all practice, and not one rope dropped." },
            new Place { Id = "station", At = "at the station", Noun = "the station", Doing = "meeting the last train at the station",
                RecordKind = "ticket", RecordTitle = "Platform ticket", RecordSource = "Wrenhaven Station · booking office",
                RecordLines = new[] { "Platform ticket, 5p. Bought by {name}." },
                Witness = "Stan Curnow", WitnessRole = "Booking clerk at Wrenhaven Station", WitnessSaw = "On the platform the whole time. The train was late, as ever." },
            new Place { Id = "glasshouse", At = "at the Park Glasshouse", Noun = "the glasshouse", Doing = "among the palms at the Park Glasshouse",
                RecordKind = "ledger", RecordTitle = "Gate book", RecordSource = "Park Glasshouse · night gate",
                RecordLines = new[] { "Late entry signed: {name}." },
                Witness = "Old Penrose", WitnessRole = "Winds the glasshouse clock", WitnessSaw = "Pottering about the palms the whole time. Asked about the bananas." },
        };

        /// <summary>A small crime at one place. {from} {to} {d} are filled in.</summary>
        sealed class Crime
        {
            public string Place, Title, Short, Tagline, Text, Found, Act, Ending;
        }

        static readonly Crime[] Crimes =
        {
            new Crime { Place = "bakery", Title = "The Proving Drawer", Short = "Float gone", Tagline = "Somebody needed the float more than Maud did.",
                Text = "The bakery's cash float went from the proving drawer at Penhallow's. Counted at {from}, gone at {to}. Whoever took it needed {d} minutes inside.",
                Found = "Maud Penhallow counted the float at {from} and went upstairs. At {to} the proving drawer was empty and the back door was on the latch.",
                Act = "the float goes into a coat pocket", Ending = "{F} paid it back by Friday, every penny, and Maud took the spare key off its nail for good." },
            new Crime { Place = "cafe", Title = "The Lifeboat Tin", Short = "Tin emptied", Tagline = "Pennies for the lifeboat, gone.",
                Text = "The lifeboat collecting tin on the Harbour Café counter was emptied between {from} and {to}. Prising the lid took {d} minutes.",
                Found = "The café's lifeboat tin was heavy at {from}. At {to} it rattled with one button and a bus ticket.",
                Act = "the lifeboat tin is prised open", Ending = "{F} owned up and pushed twice the money through the café letterbox next morning. The lifeboat crew think it was an anonymous donor." },
            new Crime { Place = "lantern", Title = "The Darts Shield", Short = "Shield taken", Tagline = "Somebody wanted to win it the easy way.",
                Text = "The darts league shield vanished from behind the Lantern's bar between {from} and {to}. Unhooking it took {d} minutes.",
                Found = "Sid Barrow polished the league shield at {from}. At {to} there was a clean square of wallpaper where it used to hang.",
                Act = "the shield comes off its hooks", Ending = "The shield turned up in {F}'s shed, wrapped in a tablecloth. The league has banned {F} from the oche until spring." },
            new Crime { Place = "hotel", Title = "The Cloakroom Brooch", Short = "Brooch taken", Tagline = "A guest's garnet brooch and a careless pin.",
                Text = "A guest's garnet brooch was taken from a coat in the Grand's cloakroom between {from} and {to}. Unpinning it took {d} minutes.",
                Found = "Mrs Vane hung her coat in the Grand's cloakroom at {from}. At {to} the brooch on its lapel was gone.",
                Act = "the brooch comes off the lapel", Ending = "{F} said it was meant as a joke. Mrs Vane didn't laugh, but she got her brooch back, polished." },
            new Crime { Place = "hardware", Title = "The Night Till", Short = "Till forced", Tagline = "Fenwick's never locks the back door.",
                Text = "The spare till at Fenwick's was forced between {from} and {to}, while Gerald was in the stockroom. It took {d} minutes.",
                Found = "Gerald Fenwick cashed up the spare till at {from} and went to the stockroom. At {to} its drawer was hanging open.",
                Act = "the spare till drawer is jemmied", Ending = "{F} had run up a debt on account and panicked. Gerald has taken it out in shelf-stacking." },
            new Crime { Place = "townhall", Title = "The Tombola Prizes", Short = "Prizes gone", Tagline = "Bath salts, a ham, and somebody's nerve.",
                Text = "The tombola prizes for Saturday's fête vanished from the Town Hall committee room between {from} and {to}. Bagging them up took {d} minutes.",
                Found = "Mr Hosking locked the tombola prizes in the committee room at {from}. At {to} there was nothing left but the raffle drum.",
                Act = "the prizes go into a shopping bag", Ending = "{F} wanted the ham. The rest came back in the same bag. The fête went ahead, a ham short." },
            new Crime { Place = "cinema", Title = "The Missing Reel", Short = "Reel taken", Tagline = "Saturday's picture, one reel short.",
                Text = "The third reel of Saturday's picture went from the Odeon's projection box between {from} and {to}. Finding the right can took {d} minutes.",
                Found = "The projectionist checked the reels at {from}. At {to} reel three of the Saturday picture was missing.",
                Act = "reel three goes under a coat", Ending = "{F} had been paid by Polgarth's cinema to make Wrenhaven's premiere a flop. The reel was back in time; the money wasn't." },
            new Crime { Place = "church", Title = "The Harvest Box", Short = "Box forced", Tagline = "Thou shalt not, and somebody did.",
                Text = "The harvest collection box at St Brigid's was forced between {from} and {to}. The lock took {d} minutes.",
                Found = "Edna Rowe tidied the hymn books beside the harvest box at {from}. At {to} the box's little lock lay on the floor.",
                Act = "the harvest box gives way", Ending = "{F} confessed in the vestry before the vicar had finished asking. The money went back, with interest, and a long sermon." },
            new Crime { Place = "boathouse", Title = "The Racing Sail", Short = "Sail slashed", Tagline = "Somebody doesn't want the Mouette racing.",
                Text = "A new racing sail was slashed in the Yacht Club boathouse between {from} and {to}. The damage took {d} minutes.",
                Found = "The steward folded the new mainsail at {from}. At {to} it had a cut in it a yard long.",
                Act = "a knife goes through the mainsail", Ending = "{F} had money on the other boat. The Club has stopped the bet and sent {F} the sailmaker's bill." },
            new Crime { Place = "station", Title = "The Parcels Office", Short = "Parcel opened", Tagline = "Somebody couldn't wait for Christmas.",
                Text = "A parcel addressed to the Mayor was opened in the station's parcels office between {from} and {to}. Unwrapping it took {d} minutes.",
                Found = "Stan Curnow logged the Mayor's parcel at {from}. At {to} its string was cut and its paper was loose.",
                Act = "the Mayor's parcel is opened", Ending = "{F} only wanted to know whether the Mayor had really bought a new chain. The Mayor has asked for it to be kept quiet." },
            new Crime { Place = "glasshouse", Title = "The First Bananas", Short = "Fruit picked", Tagline = "The Park's first bunch, gone green.",
                Text = "The banana palm's first bunch was cut down in the Park Glasshouse between {from} and {to}. Getting at it took {d} minutes.",
                Found = "Old Penrose admired the first bunch at {from}. At {to} there was only a cut stalk.",
                Act = "the bunch comes down", Ending = "{F} wanted them for the Harvest show. They were still green. Old Penrose has forgiven {F}, mostly." },
            new Crime { Place = "pier", Title = "The Fortune Teller", Short = "Machine robbed", Tagline = "She didn't see it coming.",
                Text = "The fortune-telling machine on the pier was emptied of pennies between {from} and {to}. Picking the lock took {d} minutes.",
                Found = "Wilf Couch tapped the fortune teller's glass at {from}. At {to} its coin box was open.",
                Act = "the coin box comes open", Ending = "{F}'s excuse: the machine had been swallowing pennies for years. The pier has fitted a better lock." },
            new Crime { Place = "bandstand", Title = "The Band Parts", Short = "Music taken", Tagline = "No music, no march.",
                Text = "The silver band's music for the Armistice parade went from the bandstand locker between {from} and {to}. It took {d} minutes.",
                Found = "Percy Hambly locked the band parts away at {from}. At {to} the locker was open and empty.",
                Act = "the band parts go under an arm", Ending = "{F} wanted the band to play something else for once. They played the march, from memory." },
        };

        static readonly string[] Flourishes = { "Never left.", "Ask anyone.", "Didn't stir all evening.", "You can check.", "Where else would I be?" };
        static readonly string[] Excuses =
        {
            "I didn't want it getting about.", "It's nobody's business where I spend my evenings.",
            "I'd promised I'd stop going.", "I owe money and I'd rather it stayed quiet.", "I didn't want a fuss, that's all.",
        };
        static readonly string[] Notes =
        {
            "Connie's note on the docket: “Small crimes still leave paper.”",
            "Connie's note on the docket: “Another one for the drawer, partner.”",
            "Connie's note on the docket: “Short and sweet. Same time tomorrow?”",
            "Connie's note on the docket: “The paper had it all along. It usually does.”",
        };
        static readonly string[] Weather = { "Drizzle · 9°C", "Clear and cold · 4°C", "Sea fret · 7°C", "Blustery · 10°C", "Still and mild · 12°C", "Hard frost · −2°C" };

        // ------------------------------------------------------------------ generation

        /// <summary>One variation of a day's docket as case JSON, or null if this draw doesn't fit.</summary>
        static string Build(Rng rng, DateTime date, TownMap map, out bool clockDay)
        {
            clockDay = rng.Chance(45);
            int W(string a, string b) => map.Minutes(a, b);

            var crime = CrimeFor(date);
            string scene = crime.Place;
            var cast = rng.Shuffle(Regulars).Take(3).ToList();
            Regular culprit = cast[0], liar = cast[1], honest = cast[2];

            int S = 19 * 60 + 30 * rng.Next(4);           // the board opens 19:00–20:30
            int w0 = S + 40 + 5 * rng.Next(4);
            int L = 40 + 5 * rng.Next(4);
            int w1 = w0 + L;
            int d = 4 + rng.Next(4);
            int spanTo = S + 165;

            var cards = new List<Dictionary<string, object>>();
            var triggers = new List<Dictionary<string, object>>();
            var truth = new Dictionary<string, object>();
            var clocks = new List<Dictionary<string, object>>();

            var others = Places.Where(p => p.Id != scene).ToList();

            // ---- the culprit: claims A all evening, really slipped out to the scene, and a record at P2 catches it.
            var A = rng.Pick(others.Where(p => W(p.Id, scene) >= 4 && W(p.Id, scene) <= 14).ToList());
            if (A == null) return null;
            var P2 = rng.Pick(others.Where(p => p.Id != A.Id && W(scene, p.Id) >= 4 && W(scene, p.Id) <= 14).ToList());
            if (P2 == null) return null;
            int cFrom = Floor5(w0 - 25 - 5 * rng.Next(3)), cTo = Ceil5(w1 + 15 + 5 * rng.Next(3));
            int ts = w0 + rng.Next(L - d);
            int leave = Floor5(ts - W(A.Id, scene) - rng.Next(3));
            if (leave < cFrom + 10) return null;
            int te = ts + d + rng.Next(2);
            int r = te + W(scene, P2.Id) + 1 + rng.Next(6);
            if (r > cTo - 3) return null;
            truth[culprit.Id] = Stops((A.Id, cFrom, leave), (scene, ts, te), (P2.Id, r - 1, r + 12));
            var cRec = Record("c_rec", P2, culprit, r, rng);
            cards.Add(Statement("c_claim", culprit, culprit.Name, culprit.First + ", in their own words",
                $"“I was {A.Doing} from {Say(cFrom)} till {Say(cTo)}. {rng.Pick(Flourishes)}”", A.Id, cFrom, cTo, true,
                lie: $"“Fine. I left {A.Noun} at {Say(leave)}. I went for a walk. Needed some air. That's not a crime.”", unlocks: "c_true"));
            cards.Add(cRec);
            cards.Add(Statement("c_true", culprit, culprit.Name + " (again)", culprit.First + ", second statement",
                $"“I was {A.Doing} from {Say(cFrom)} till {Say(leave)}. Then I walked about a bit. That's the truth.”", A.Id, cFrom, leave, false,
                firm: $"“That's the truth this time. {Cap(Say(leave))}, give or take.”"));
            triggers.Add(Conflict("c_claim", "c_rec", $"{culprit.First} swears to {A.Noun} until {F(cTo)}, but the {Lower((string)cRec["title"])} puts {culprit.First} {P2.At} at {F(r)}. Which is it?"));

            // ---- the liar: claims B, was really at Q (a record shows it), and a witness there covers the whole window.
            // Every story gets its own places, so two people's paper never turns up at the same spot.
            var Q = rng.Pick(others.Where(p => p.Id != A.Id && p.Id != P2.Id).ToList());
            var B = rng.Pick(others.Where(p => p.Id != Q.Id && p.Id != A.Id && p.Id != P2.Id).ToList());
            int lFrom = Floor5(w0 - 20 - 5 * rng.Next(3)), lTo = Ceil5(w1 + 10 + 5 * rng.Next(3));
            int wa = Floor5(w0 - 15 - 5 * rng.Next(2)), wb = Ceil5(w1 + 10 + 5 * rng.Next(2));
            int q = Math.Max(lFrom, wa) + 3 + rng.Next(Math.Max(1, Math.Min(lTo, wb) - Math.Max(lFrom, wa) - 6));
            truth[liar.Id] = Stops((Q.Id, wa - 5, wb + 5));
            var lRec = Record("l_rec", Q, liar, q, rng);
            cards.Add(Statement("l_claim", liar, liar.Name, liar.First + ", in their own words",
                $"“I was {B.Doing} from {Say(lFrom)} till {Say(lTo)}. {rng.Pick(Flourishes)}”", B.Id, lFrom, lTo, true,
                lie: $"“…All right. I wasn't {B.At}. I was {Q.At}. {rng.Pick(Excuses)} Ask {Q.Witness}.”", unlocks: "l_wit"));
            cards.Add(lRec);
            cards.Add(Witness("l_wit", liar, Q, wa, wb));
            triggers.Add(Conflict("l_claim", "l_rec", $"{liar.First} says {B.Noun} all evening, so why has the {Lower((string)lRec["title"])} got {liar.First} {Q.At} at {F(q)}?"));

            // ---- the honest one, with paper to match; on a clock day that paper is on a wrong clock.
            var used = new HashSet<string> { A.Id, P2.Id, Q.Id, B.Id };
            var C = rng.Pick(others.Where(p => !used.Contains(p.Id)).ToList());
            used.Add(C.Id);
            int hFrom = Floor5(w0 - 15 - 5 * rng.Next(3)), hTo = Ceil5(w1 + 10 + 5 * rng.Next(3));
            cards.Add(Statement("h_claim", honest, honest.Name, honest.First + ", in their own words",
                $"“I was {C.Doing} from {Say(hFrom)} till {Say(hTo)}. {rng.Pick(Flourishes)}”", C.Id, hFrom, hTo, true,
                firm: $"“I've told you. {Cap(C.Noun)}, {Say(hFrom)} till {Say(hTo)}. I don't know what else to say.”"));
            int par = 300;
            if (!clockDay)
            {
                int h = hFrom + 5 + rng.Next(hTo - hFrom - 10);
                truth[honest.Id] = Stops((C.Id, hFrom - 5, hTo + 5));
                cards.Add(Record("h_rec", C, honest, h, rng));
            }
            else
            {
                var K = rng.Pick(others.Where(p => p.ClockName != null && !used.Contains(p.Id) && W(p.Id, C.Id) <= 9).ToList());
                if (K == null) return null;
                bool before = rng.Chance(55);
                int trueAt, offset;
                if (before)
                {
                    // At K first, then C: the K clock runs fast, so its paper looks like it overlaps the story.
                    trueAt = hFrom - W(K.Id, C.Id) - 2 - rng.Next(3);
                    offset = hFrom - trueAt + 1 + rng.Next(5);
                    if (trueAt < S + 5) return null;
                    truth[honest.Id] = Stops((K.Id, trueAt - 10, trueAt + 2), (C.Id, hFrom, hTo + 5));
                }
                else
                {
                    // C first, then K: the K clock runs slow.
                    trueAt = hTo + W(C.Id, K.Id) + 2 + rng.Next(3);
                    offset = -(trueAt - hTo + 1 + rng.Next(5));
                    if (trueAt + 2 > spanTo) return null;
                    truth[honest.Id] = Stops((C.Id, hFrom - 5, hTo), (K.Id, trueAt - 2, trueAt + 10));
                }
                int dip = S + 8 + rng.Next(25);
                if (dip + offset < S + 2 || dip + offset > spanTo - 2) return null;
                clocks.Add(new Dictionary<string, object> { ["id"] = "k", ["name"] = Cap(K.ClockName), ["offset"] = offset });
                var hRec = Record("h_rec", K, honest, trueAt + offset, rng);
                hRec["clock"] = "k";
                cards.Add(hRec);
                cards.Add(new Dictionary<string, object>
                {
                    ["id"] = "t_board", ["kind"] = "ledger", ["start"] = true, ["town"] = true, ["title"] = "Electricity Board log",
                    ["source"] = "record", ["sourceName"] = "South Western Electricity Board · Wrenhaven substation",
                    ["text"] = "Supply to the harbour district dipped for twenty seconds. Logged at the substation.",
                    ["location"] = "townhall", ["at"] = F(dip), ["event"] = "dip",
                });
                cards.Add(new Dictionary<string, object>
                {
                    ["id"] = "t_clock", ["kind"] = "note", ["start"] = true, ["town"] = true, ["title"] = "Order pad note",
                    ["source"] = "record", ["sourceName"] = Cap(K.Noun),
                    ["text"] = $"“Lights flickered, the {K.ClockName} said {F(dip + offset)}.” Written on the back of the order pad.",
                    ["location"] = K.Id, ["at"] = F(dip + offset), ["clock"] = "k", ["event"] = "dip",
                });
                string side = before ? $"says {C.Noun} from {F(hFrom)}" : $"says {C.Noun} until {F(hTo)}";
                triggers.Add(Conflict("h_claim", "h_rec", $"The {Lower((string)hRec["title"])} from {K.Noun} has {honest.First} there at {F(trueAt + offset)}, but {honest.First} {side}. Is {honest.First} lying, or is that clock?"));
                triggers.Add(new Dictionary<string, object>
                {
                    ["on"] = "calibrate", ["a"] = "k",
                    ["memo"] = $"The {K.ClockName} runs {Math.Abs(offset)} min {(offset > 0 ? "fast" : "slow")}. Once it's put right, {honest.First}'s story holds.",
                });
                par = 420;
            }

            // ---- the case file around it.
            var d86 = new DateTime(1986, date.Month, date.Month == 2 && date.Day == 29 ? 28 : date.Day);
            string Fill(string t) => t.Replace("{from}", F(w0)).Replace("{to}", F(w1)).Replace("{d}", d.ToString(CultureInfo.InvariantCulture)).Replace("{F}", culprit.First);
            var people = rng.Shuffle(cast).Select(p => (object)new Dictionary<string, object> { ["id"] = p.Id, ["name"] = p.Name, ["role"] = "suspect", ["blurb"] = p.Blurb }).ToList();
            var memos = new List<object>
            {
                Memo("start", clockDay
                    ? "Today's docket, partner. Three stories, one false where it matters, and a clock somewhere that isn't. Not everything red is a lie."
                    : "Today's docket, partner. A small one: three stories, and one of them is false where it matters."),
                Memo("firstConflict", clockDay
                    ? "Red means something's wrong, not someone. Check whose clock timed it before you confront anybody."
                    : "Red: two things can't both be true, and paper doesn't lie. Click the statement and confront them."),
                Memo("firstStrike", "A lie isn't a confession. Who still had the time to do it?"),
            };
            var root = new Dictionary<string, object>
            {
                ["id"] = IdFor(date),
                ["title"] = crime.Title,
                ["tagline"] = crime.Tagline,
                ["date"] = d86.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture),
                ["weather"] = rng.Pick(Weather),
                ["lesson"] = clockDay ? "Today's docket: mind the clocks." : "Today's docket: three stories.",
                ["spanFrom"] = F(S),
                ["spanTo"] = F(spanTo),
                ["par"] = $"{par / 60}:{par % 60:00}",
                ["intro"] = new List<object>
                {
                    Fill(crime.Found) + $" Whoever did it needed {d} minutes there.",
                    $"Three regulars were out and about that evening: {cast[0].First}, {cast[1].First} and {cast[2].First}. Each of them has a story.",
                },
                ["people"] = people,
                ["clocks"] = clocks.Cast<object>().ToList(),
                ["incident"] = new Dictionary<string, object>
                {
                    ["title"] = crime.Title, ["short"] = crime.Short, ["text"] = Fill(crime.Text), ["location"] = scene,
                    ["from"] = F(w0), ["to"] = F(w1), ["duration"] = d, ["culprit"] = culprit.Id,
                },
                ["cards"] = rng.Shuffle(cards).Cast<object>().ToList(),
                ["triggers"] = triggers.Cast<object>().ToList(),
                ["memos"] = memos,
                ["truth"] = truth,
                ["reconstruction"] = new List<object>
                {
                    $"{F(leave)}. {culprit.First} leaves {A.Noun}.",
                    $"{F(ts)}. {culprit.First} reaches the scene. {Cap(crime.Act)}.",
                    $"{F(r)}. {culprit.First} turns up {P2.At}, and the {Lower((string)cRec["title"])} says so. Not quite an alibi.",
                },
                ["epilogue"] = new List<object> { Fill(crime.Ending), rng.Pick(Notes) },
            };
            return Json(root);
        }

        // ------------------------------------------------------------------ card builders

        static Dictionary<string, object> Statement(string id, Regular who, string title, string sourceName, string text, string place, int from, int to,
                                                    bool start, string lie = null, string firm = null, string unlocks = null)
        {
            var c = new Dictionary<string, object>
            {
                ["id"] = id, ["kind"] = "statement", ["title"] = title, ["source"] = who.Id, ["sourceName"] = sourceName, ["text"] = text,
                ["subjects"] = new List<object> { who.Id }, ["location"] = place, ["from"] = F(from), ["to"] = F(to),
            };
            if (start) c["start"] = true;
            if (lie != null) { c["truth"] = "lie"; c["reply"] = lie; }
            if (firm != null) c["firm"] = firm;
            if (unlocks != null) c["unlocks"] = new List<object> { unlocks };
            return c;
        }

        static Dictionary<string, object> Witness(string id, Regular about, Place at, int from, int to) => new Dictionary<string, object>
        {
            ["id"] = id, ["kind"] = "statement", ["title"] = at.Witness, ["source"] = "witness", ["sourceName"] = at.WitnessRole,
            ["text"] = $"“{about.First} was here from {Say(from)} till {Say(to)}. {at.WitnessSaw}”",
            ["subjects"] = new List<object> { about.Id }, ["location"] = at.Id, ["from"] = F(from), ["to"] = F(to),
            ["firm"] = $"“I'd swear to it. {Cap(Say(from))} till {Say(to)}.”",
        };

        static Dictionary<string, object> Record(string id, Place at, Regular who, int time, Rng rng) => new Dictionary<string, object>
        {
            ["id"] = id, ["kind"] = at.RecordKind, ["start"] = true, ["title"] = at.RecordTitle, ["source"] = "record", ["sourceName"] = at.RecordSource,
            ["text"] = rng.Pick(at.RecordLines).Replace("{name}", who.OnPaper),
            ["subjects"] = new List<object> { who.Id }, ["location"] = at.Id, ["at"] = F(time),
        };

        static Dictionary<string, object> Conflict(string a, string b, string question) =>
            new Dictionary<string, object> { ["on"] = "conflict", ["a"] = a, ["b"] = b, ["question"] = question };

        static Dictionary<string, object> Memo(string when, string text) => new Dictionary<string, object> { ["when"] = when, ["text"] = text };

        static List<object> Stops(params (string at, int from, int to)[] stops) =>
            stops.Select(s => (object)new Dictionary<string, object> { ["at"] = s.at, ["from"] = F(s.from), ["to"] = F(s.to) }).ToList();

        // ------------------------------------------------------------------ words and numbers

        static string F(int minutes) => TimeFmt.Format(minutes);
        static int Floor5(int m) => m - ((m % 5) + 5) % 5;
        static int Ceil5(int m) => Floor5(m + 4);
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        static readonly string[] Hours = { "twelve", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven" };

        /// <summary>How a Wrenhaven regular says a time: "half eight", "twenty to nine", "ten o'clock".</summary>
        public static string Say(int minutes)
        {
            int h = minutes / 60 % 24, m = minutes % 60;
            string H(int hour) => Hours[hour % 12];
            switch (m)
            {
                case 0: return H(h) + " o'clock";
                case 15: return "quarter past " + H(h);
                case 30: return "half " + H(h);
                case 45: return "quarter to " + H(h + 1);
            }
            if (m % 5 != 0) return F(minutes);
            string[] words = { "", "five", "ten", "", "twenty", "twenty-five" };
            return m < 30 ? words[m / 5] + " past " + H(h) : words[(60 - m) / 5] + " to " + H(h + 1);
        }

        static string Json(object o)
        {
            var sb = new StringBuilder();
            Write(sb, o);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object o)
        {
            switch (o)
            {
                case null: sb.Append("null"); break;
                case string s:
                    sb.Append('"');
                    foreach (char ch in s)
                    {
                        if (ch == '"') sb.Append("\\\"");
                        else if (ch == '\\') sb.Append("\\\\");
                        else if (ch == '\n') sb.Append("\\n");
                        else if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                    }
                    sb.Append('"');
                    break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> d:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in d)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, kv.Key);
                        sb.Append(':');
                        Write(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case IEnumerable<object> list:
                    sb.Append('[');
                    bool f = true;
                    foreach (var x in list) { if (!f) sb.Append(','); f = false; Write(sb, x); }
                    sb.Append(']');
                    break;
                default: throw new InvalidOperationException("can't write " + o.GetType());
            }
        }
    }
}
