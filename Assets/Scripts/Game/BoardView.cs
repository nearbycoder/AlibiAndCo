using System.Collections.Generic;
using System.Linq;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// Draws one case's board: lanes with portraits, the time ruler, the incident window, the
    /// travel ribbons between pinned cards, contradiction hatching and each suspect's alibi lock.
    /// All geometry lives under Stage.BoardRoot in local XY (local -Z is toward the camera).
    /// </summary>
    public sealed class BoardView
    {
        public sealed class Lane
        {
            public string Id;
            public Person Person;
            public float Top, Bottom, Track;
            public bool IsTown;
            public MeshRenderer Strip;
            public Material StripMat;
            public Color StripColor;
            public Transform LockRoot;
            public MeshRenderer LockIcon;
            public TextMeshPro LockLabel, LockNote;
            public bool? LastOpen;
            public float Height => Top - Bottom;
        }

        public sealed class ChipPlace
        {
            public string CardId, Lane;
            public Vector2 Pos;        // chip centre (local)
            public float MarkerX, MarkerX2;
            public bool Interval;
            public int Order;
        }

        readonly Stage stage;
        readonly CaseDef c;
        readonly Transform root;
        readonly Transform dynamicRoot;
        readonly Transform previewRoot;
        public readonly List<Lane> Lanes = new List<Lane>();
        public readonly Dictionary<string, Lane> LaneById = new Dictionary<string, Lane>();
        public float X0, X1, HeaderBottom;
        public Rect Area;
        public Vector2 IncidentSlot;
        public TextMeshPro Badges, Timer;
        /// <summary>Chips grow when lanes are roomy (fewer people on the board).</summary>
        public float ChipScale = 1f;
        /// <summary>Space between a lane's track line and the bottom of its chips (room for the markers).</summary>
        const float ChipGap = 0.13f;
        public Vector2 ChipSize => CardView.ChipSize * ChipScale;
        /// <summary>The TOWN lane is shorter; its chips sit centred on it, never taller than it.</summary>
        public float TownChipScale = 1f;
        public float ScaleFor(string laneId) => laneId == Board.TownLane ? TownChipScale : ChipScale;
        Transform clockLegend;
        readonly Dictionary<string, TextMeshPro> clockRows = new Dictionary<string, TextMeshPro>();
        Material ribbonMat, slackMat, hatchMat, redMat, markerShadowMat, stringMat, leaderMat;

        const float ZStrip = -0.012f, ZGrid = -0.016f, ZRibbon = -0.022f, ZMarker = -0.028f, ZLabel = -0.034f;

        /// <summary>The text size setting, applied to the board's own labels as well as the chips.</summary>
        static float TextScale => Settings.TextScale;

        public BoardView(Stage stage, CaseDef c)
        {
            this.stage = stage;
            this.c = c;
            root = new GameObject("BoardView_" + c.Id).transform;
            root.SetParent(stage.BoardRoot, false);
            dynamicRoot = new GameObject("dynamic").transform;
            dynamicRoot.SetParent(root, false);
            previewRoot = new GameObject("preview").transform;
            previewRoot.SetParent(root, false);
            ribbonMat = Art.Unlit(Pal.Ribbon, true);
            slackMat = Art.Unlit(new Color(Pal.Slack.r, Pal.Slack.g, Pal.Slack.b, 0.75f), true, "dash");
            hatchMat = Art.Unlit(new Color(Pal.Red.r, Pal.Red.g, Pal.Red.b, 0.95f), true, "hatch");
            redMat = Art.Unlit(Pal.Red, true);
            stringMat = Art.Lit(Pal.Hex("A3272A"), null, 0.3f);
            leaderMat = Art.Unlit(new Color(0.12f, 0.12f, 0.14f, 0.55f), true);
            markerShadowMat = Art.Unlit(new Color(0, 0, 0, 0.25f), true, "dot");
            Build();
        }

        public void Destroy() { if (root) Object.Destroy(root.gameObject); }

        public float TimeToX(float minutes) => Mathf.Lerp(X0, X1, (minutes - c.SpanFrom) / (float)(c.SpanTo - c.SpanFrom));
        public float XToTime(float x) => Mathf.Lerp(c.SpanFrom, c.SpanTo, Mathf.InverseLerp(X0, X1, x));
        public float MinutesToWidth(float m) => (X1 - X0) * m / (c.SpanTo - c.SpanFrom);

        // ------------------------------------------------------------------ static layout

        void Build()
        {
            var b = stage.Board;
            Area = b;
            const float leftCol = 2.55f, rightCol = 1.55f, header = 1.45f;
            X0 = b.xMin + leftCol + 0.35f;
            X1 = b.xMax - rightCol - 0.2f;
            HeaderBottom = b.yMax - header;
            IncidentSlot = new Vector2(b.xMin + leftCol * 0.5f + 0.12f, b.yMax - header * 0.5f + 0.02f);

            // Title block (top-left, above the timeline).
            Txt.Make(root, "title", c.Title, Art.Serif, 0.46f, Pal.Ink, new Vector2(7.5f, 0.6f), TextAlignmentOptions.Left,
                new Vector3(X0 + 3.75f - 0.1f, b.yMax - 0.43f, ZLabel), false);
            Txt.Make(root, "date", (c.Date + "   ·   " + c.Weather).ToUpperInvariant(), Art.SansBold, 0.15f, Pal.InkSoft, new Vector2(7.5f, 0.3f), TextAlignmentOptions.Left,
                new Vector3(X0 + 3.75f - 0.1f, b.yMax - 0.84f, ZLabel), false);
            // Title paper label behind the text.
            Shapes.Slab(root, "titleCard", new Vector2(6.4f, 1.0f), 0.02f, Art.Lit(Pal.Hex("EFE6D2"), "paper", 0.1f), new Vector3(X0 + 3.0f, b.yMax - 0.62f, 0f)).transform.localRotation = Quaternion.Euler(0, 0, 0.6f);
            Pin(root, new Vector3(X0 + 0.15f, b.yMax - 0.25f, -0.05f), Pal.Hex("B33A2E"));
            Badges = Txt.Make(root, "badges", "★★★", Art.Sans, 0.3f, Pal.Brass, new Vector2(1.4f, 0.4f), TextAlignmentOptions.Right,
                new Vector3(X0 + 5.35f, b.yMax - 0.4f, ZLabel), false);
            Timer = Txt.Make(root, "timer", "0:00", Art.MonoBold, 0.2f, Pal.InkSoft, new Vector2(1.4f, 0.3f), TextAlignmentOptions.Right,
                new Vector3(X0 + 5.35f, b.yMax - 0.82f, ZLabel), false);

            // Lanes.
            var ids = new List<(string id, Person p, bool town)>();
            foreach (var p in c.People) ids.Add((p.Id, p, false));
            if (c.HasTownLane) ids.Add((Board.TownLane, null, true));
            float total = ids.Sum(x => x.town ? 0.62f : 1f);
            float avail = HeaderBottom - (b.yMin + 0.22f);
            float unit = avail / total;
            float y = HeaderBottom;
            int idx = 0;
            foreach (var (id, p, town) in ids)
            {
                float h = unit * (town ? 0.62f : 1f);
                var lane = new Lane { Id = id, Person = p, IsTown = town, Top = y, Bottom = y - h };
                lane.Track = lane.Bottom + Mathf.Min(0.36f, h * 0.24f);
                Lanes.Add(lane);
                LaneById[id] = lane;
                BuildLane(lane, idx++);
                y -= h;
            }

            // Chips grow when lanes are roomy and with the text size setting, but never past one row's
            // height above the track (so a crowded lane still has somewhere to put them).
            var personLane = Lanes.FirstOrDefault(l => !l.IsTown);
            if (personLane != null)
            {
                float roomy = Mathf.Clamp((personLane.Height - 0.42f) / 1.15f, 1.0f, 1.4f);
                float fits = (personLane.Top - 0.06f - (personLane.Track + ChipGap)) / CardView.ChipSize.y;
                ChipScale = Mathf.Max(roomy * TextScale, Mathf.Min(fits, 1.15f * TextScale));
                ChipScale = Mathf.Max(1f, Mathf.Min(ChipScale, Mathf.Max(roomy, fits), 1.7f));
            }
            var townLane = Lanes.FirstOrDefault(l => l.IsTown);
            TownChipScale = ChipScale;
            if (townLane != null)
            {
                TownChipScale = Mathf.Min(ChipScale, Mathf.Max(0.9f, (townLane.Height - 0.1f) / CardView.ChipSize.y));
            }

            BuildRuler();
            BuildIncidentBand();
            BuildClockLegend();
        }

        void BuildLane(Lane lane, int idx)
        {
            var b = stage.Board;
            float h = lane.Height;
            // Paper strip the cards are pinned along.
            lane.StripColor = lane.IsTown ? Pal.Hex("E4DCCB") : (idx % 2 == 0 ? Pal.Hex("EFE7D6") : Pal.Hex("E9E0CD"));
            lane.StripMat = Art.Lit(lane.StripColor, "paper", 0.08f);
            float left = b.xMin + 0.12f, right = b.xMax - 0.12f;
            var strip = Shapes.Slab(root, "lane_" + lane.Id, new Vector2(right - left, h - 0.12f), 0.012f, lane.StripMat,
                new Vector3((left + right) / 2, (lane.Top + lane.Bottom) / 2, ZStrip + 0.012f));
            lane.Strip = strip;
            strip.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // Index-tab panel for the name column, with a ruled edge where the timeline starts.
            float tabRight = X0 - 0.38f;
            var tabCol = lane.IsTown ? Pal.Hex("D9CFBB") : Pal.Hex("DED0B2");
            Shapes.Quad(root, "laneTab", new Vector2(tabRight - left, h - 0.12f), Art.Lit(tabCol, "paper", 0.06f),
                new Vector3((left + tabRight) / 2, (lane.Top + lane.Bottom) / 2, ZStrip - 0.001f));
            Line(root, new Vector3(tabRight, lane.Bottom + 0.06f, ZStrip - 0.002f), new Vector3(tabRight, lane.Top - 0.06f, ZStrip - 0.002f), 0.018f,
                new Color(0.55f, 0.18f, 0.16f, 0.5f));
            Pin(root, new Vector3(left + 0.12f, lane.Top - 0.16f, ZStrip - 0.04f), Pal.Hex("8A8F96"), 0.07f);
            Pin(root, new Vector3(right - 0.12f, lane.Top - 0.16f, ZStrip - 0.04f), Pal.Hex("8A8F96"), 0.07f);

            // Track line with minute ticks.
            Line(root, new Vector3(X0, lane.Track, ZGrid), new Vector3(X1, lane.Track, ZGrid), 0.022f, new Color(0.1f, 0.12f, 0.16f, 0.65f));
            for (int m = Mathf.CeilToInt(c.SpanFrom / 5f) * 5; m <= c.SpanTo; m += 5)
            {
                bool quarter = m % 15 == 0;
                float x = TimeToX(m);
                Line(root, new Vector3(x, lane.Track - (quarter ? 0.1f : 0.05f), ZGrid), new Vector3(x, lane.Track + (quarter ? 0.1f : 0.05f), ZGrid),
                    quarter ? 0.02f : 0.012f, new Color(0.1f, 0.12f, 0.16f, quarter ? 0.55f : 0.35f));
                if (quarter && !lane.IsTown)
                    Line(root, new Vector3(x, lane.Track + 0.12f, ZGrid + 0.002f), new Vector3(x, lane.Top - 0.1f, ZGrid + 0.002f), 0.01f, new Color(0.25f, 0.3f, 0.4f, 0.12f));
            }

            // Who.
            float colLeft = b.xMin + 0.25f;
            if (lane.IsTown)
            {
                Shapes.Icon(root, "bell", 0.42f, Pal.InkSoft, new Vector3(colLeft + 0.4f, (lane.Top + lane.Bottom) / 2 + 0.05f, ZLabel));
                Txt.Make(root, "townName", "TOWN", Art.SansBold, 0.22f * TextScale, Pal.Ink, new Vector2(1.6f, 0.3f), TextAlignmentOptions.Left,
                    new Vector3(colLeft + 0.75f + 0.8f, (lane.Top + lane.Bottom) / 2 + 0.12f, ZLabel), false);
                Txt.Make(root, "townNote", "radio, clocks, power", Art.Hand, 0.24f * TextScale, Pal.InkSoft, new Vector2(1.7f, 0.3f), TextAlignmentOptions.Left,
                    new Vector3(colLeft + 0.75f + 0.85f, (lane.Top + lane.Bottom) / 2 - 0.16f, ZLabel), false).Fit(0.2f);
                return;
            }
            float ph = Mathf.Min(h - 0.2f, 1.3f);
            var portraitSize = new Vector2(ph * 0.84f, ph);
            var pr = Portraits.Make(root, c, lane.Id, portraitSize, false);
            pr.localPosition = new Vector3(colLeft + portraitSize.x / 2 + 0.05f, (lane.Top + lane.Bottom) / 2, -0.03f);
            pr.localRotation = Quaternion.Euler(0, 0, (idx % 2 == 0 ? -2.2f : 1.8f));
            Pin(root, pr.localPosition + new Vector3(0, portraitSize.y / 2 - 0.1f, -0.06f), Pal.Hex("B8402F"), 0.08f);
            float tx = colLeft + portraitSize.x + 0.2f;
            float tw = X0 - 0.45f - tx;
            var role = Txt.Make(root, "role", lane.Person.IsSuspect ? "SUSPECT" : "WITNESS", Art.SansBold, 0.12f * TextScale, lane.Person.IsSuspect ? Pal.Oxblood : Pal.InkSoft,
                new Vector2(tw, 0.2f), TextAlignmentOptions.Left, new Vector3(tx + tw / 2, lane.Top - 0.3f, ZLabel), false);
            role.characterSpacing = 8;
            var nm = Txt.Make(root, "name", lane.Person.Name.Replace("Capt. ", "Capt.\u00A0").Replace(" ", "\n").Replace("\u00A0", " "), Art.Serif, 0.21f * TextScale, Pal.Ink,
                new Vector2(tw + 0.05f, 0.78f), TextAlignmentOptions.TopLeft, new Vector3(tx + tw / 2, lane.Top - 0.82f, ZLabel), true);
            nm.lineSpacing = -18;
            nm.fontStyle = FontStyles.Bold;
            nm.Fit(0.17f);

            if (lane.Person.IsSuspect) BuildLock(lane);
        }

        void BuildLock(Lane lane)
        {
            var b = stage.Board;
            lane.LockRoot = new GameObject("lock_" + lane.Id).transform;
            lane.LockRoot.SetParent(root, false);
            lane.LockRoot.localPosition = new Vector3(X1 + 0.35f + (b.xMax - X1 - 0.35f) / 2, (lane.Top + lane.Bottom) / 2 + 0.05f, ZLabel);
            Shapes.Slab(lane.LockRoot, "tag", new Vector2(1.36f, 1.12f), 0.015f, Art.Lit(Pal.Hex("EEE6D2"), "paper", 0.1f), new Vector3(0, 0, 0.03f));
            lane.LockIcon = Shapes.Icon(lane.LockRoot, "lock", 0.46f, Pal.Green, new Vector3(0, 0.24f, -0.01f));
            lane.LockLabel = Txt.Make(lane.LockRoot, "label", "COVERED", Art.SansBold, 0.17f * TextScale, Pal.Green, new Vector2(1.3f, 0.26f), TextAlignmentOptions.Center,
                new Vector3(0, -0.12f, -0.01f), false).Fit(0.14f);
            lane.LockLabel.characterSpacing = 6;
            lane.LockNote = Txt.Make(lane.LockRoot, "note", "", Art.Sans, 0.13f * TextScale, Pal.Ink, new Vector2(1.3f, 0.22f), TextAlignmentOptions.Center,
                new Vector3(0, -0.34f, -0.01f), false).Fit(0.11f);
        }

        void BuildRuler()
        {
            float y = HeaderBottom + 0.2f;
            // A strip of paper under the times so they read against the cork.
            var strip = Shapes.Slab(root, "rulerStrip", new Vector2(X1 - X0 + 1.1f, 0.5f), 0.01f, Art.Lit(Pal.Hex("EDE5D3"), "paper", 0.08f),
                new Vector3((X0 + X1) / 2, y - 0.02f, ZStrip + 0.01f));
            strip.transform.localRotation = Quaternion.Euler(0, 0, -0.25f);
            for (int m = Mathf.CeilToInt(c.SpanFrom / 15f) * 15; m <= c.SpanTo; m += 15)
            {
                float x = TimeToX(m);
                bool hour = m % 60 == 0;
                Txt.Make(root, "t" + m, TimeFmt.Format(m), hour ? Art.MonoBold : Art.Mono, (hour ? 0.24f : 0.19f) * Mathf.Min(TextScale, 1.2f), hour ? Pal.Ink : Pal.InkSoft,
                    new Vector2(1.2f, 0.3f), TextAlignmentOptions.Center, new Vector3(x, y + 0.04f, ZLabel), false);
                Line(root, new Vector3(x, y - 0.18f, ZGrid), new Vector3(x, y - 0.06f, ZGrid), 0.02f, new Color(0.1f, 0.12f, 0.16f, 0.6f));
            }
        }

        Transform incidentRoot;
        TextMeshPro incidentLabel;
        int bandFrom;

        /// <summary>
        /// The red band for the incident window, drawn at the printed times. If the crime itself was
        /// timed by an untrusted clock, <see cref="RefreshIncident"/> slides it when that clock is corrected.
        /// </summary>
        void BuildIncidentBand()
        {
            var suspects = Lanes.Where(l => l.Person != null && l.Person.IsSuspect).ToList();
            if (suspects.Count == 0) return;
            incidentRoot = new GameObject("incident").transform;
            incidentRoot.SetParent(root, false);
            bandFrom = c.Incident.From;
            float top = suspects.Max(l => l.Top) - 0.04f, bottom = suspects.Min(l => l.Bottom) + 0.04f;
            float x0 = TimeToX(c.Incident.From), x1 = TimeToX(c.Incident.To);
            Shapes.Quad(incidentRoot, "incidentBand", new Vector2(x1 - x0, top - bottom),
                Art.Unlit(new Color(0.75f, 0.12f, 0.1f, 0.11f), true), new Vector3((x0 + x1) / 2, (top + bottom) / 2, ZGrid - 0.001f));
            Line(incidentRoot, new Vector3(x0, bottom, ZGrid - 0.002f), new Vector3(x0, top + 0.2f, ZGrid - 0.002f), 0.025f, new Color(0.7f, 0.15f, 0.12f, 0.55f));
            Line(incidentRoot, new Vector3(x1, bottom, ZGrid - 0.002f), new Vector3(x1, top + 0.2f, ZGrid - 0.002f), 0.025f, new Color(0.7f, 0.15f, 0.12f, 0.55f));
            incidentLabel = Txt.Make(incidentRoot, "incidentLabel", IncidentLabel(null),
                Art.SansBold, 0.14f * TextScale, Pal.Oxblood, new Vector2(Mathf.Max(6.5f, x1 - x0), 0.25f * TextScale), TextAlignmentOptions.Center,
                new Vector3((x0 + x1) / 2, top - 0.16f, ZLabel), false);
            incidentLabel.Fit(0.1f);
        }

        string IncidentLabel(Board board)
        {
            var inc = c.Incident;
            int from = board != null ? board.IncidentFrom : inc.From, to = board != null ? board.IncidentTo : inc.To;
            string where = $"{inc.Duration} MIN AT {Locations.Short(inc.Location).ToUpperInvariant()}";
            var clock = c.ClockById[inc.Clock];
            string when = $"INCIDENT WINDOW  {TimeFmt.Format(from)}–{TimeFmt.Format(to)}";
            if (clock.Reference) return when + "  ·  " + where;
            bool trusted = board != null && board.IsTrusted(inc.Clock);
            return trusted
                ? when + " (CORRECTED)  ·  " + where
                : when + " ON " + clock.InSentence.ToUpperInvariant() + " ?";
        }

        /// <summary>Slide the incident band to the board's current window (after its clock is corrected).</summary>
        public void RefreshIncident(Board board, bool animate)
        {
            if (incidentRoot == null) return;
            incidentLabel.text = IncidentLabel(board);
            int from = board.IncidentFrom;
            if (from == bandFrom) return;
            var target = incidentRoot.localPosition + new Vector3(TimeToX(from) - TimeToX(bandFrom), 0, 0);
            bandFrom = from;
            if (animate) incidentRoot.MoveLocal(target, 1.25f, Ease.InOutCubic);
            else incidentRoot.localPosition = target;
        }

        void BuildClockLegend()
        {
            var clocks = c.Clocks.Where(k => !k.Reference).ToList();
            if (clocks.Count == 0) return;
            var b = stage.Board;
            clockLegend = new GameObject("clocks").transform;
            clockLegend.SetParent(root, false);
            clockLegend.localPosition = new Vector3(b.xMax - 7.4f, b.yMax - 0.5f, ZLabel);   // clear of the HUD pill at every text size
            // The legend sits just above the ruler, so only its type grows with the text size.
            float ts = Mathf.Min(TextScale, 1.15f), step = 0.29f;
            float h = 0.34f + clocks.Count * step;
            Shapes.Slab(clockLegend, "card", new Vector2(4.6f, h), 0.015f, Art.Lit(Pal.Hex("E6E0D0"), "paper", 0.1f), new Vector3(0, -h / 2 + 0.22f, 0.03f))
                .transform.localRotation = Quaternion.Euler(0, 0, -0.8f);
            Txt.Make(clockLegend, "head", "CLOCKS IN THIS CASE", Art.SansBold, 0.12f * ts, Pal.InkSoft, new Vector2(4.2f, 0.2f), TextAlignmentOptions.Left, new Vector3(0, 0.06f, 0), false);
            for (int i = 0; i < clocks.Count; i++)
            {
                var row = Txt.Make(clockLegend, "clock_" + clocks[i].Id, "", Art.Sans, 0.15f * ts, Pal.Ink, new Vector2(4.2f, 0.26f), TextAlignmentOptions.Left,
                    new Vector3(0, -0.22f - i * step, 0), false);
                row.Fit(0.1f);
                clockRows[clocks[i].Id] = row;
            }
        }

        public void RefreshClocks(Board board)
        {
            foreach (var kv in clockRows)
            {
                var k = c.ClockById[kv.Key];
                bool known = board.IsTrusted(k.Id);
                int corr = board.Correction(k.Id);
                kv.Value.text = known
                    ? $"<color=#4F7A55>✔</color>  {k.Name}  <color=#4F7A55>{(corr == 0 ? "right" : (corr > 0 ? $"{corr} min slow" : $"{-corr} min fast"))}</color>"
                    : $"<color=#C23B2E>?</color>  {k.Name}  <color=#8A6A5A>untested</color>";
            }
        }

        // ------------------------------------------------------------------ small builders

        public static void Pin(Transform parent, Vector3 pos, Color color, float r = 0.1f)
        {
            var go = Art.Spawn("pin", parent);
            if (go != null)
            {
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(-90, Random.Range(0f, 360f), 0);
                go.transform.localScale = Vector3.one * (r / 0.0062f);
                foreach (var rend in go.GetComponentsInChildren<MeshRenderer>())
                    if (rend.gameObject.name.Contains("head"))
                        rend.sharedMaterial = Art.Lit(color, null, 0.6f);
                return;
            }
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(s.GetComponent<Collider>());
            s.name = "pin";
            s.transform.SetParent(parent, false);
            s.transform.localPosition = pos;
            s.transform.localScale = new Vector3(r * 2, r * 2, r * 1.2f);
            s.GetComponent<Renderer>().sharedMaterial = Art.Lit(color, null, 0.6f);
        }

        static MeshRenderer Line(Transform parent, Vector3 a, Vector3 b, float width, Color color)
        {
            return Shapes.StripObject(parent, "line", new[] { a, b }, width, Art.UnlitShared(color, true));
        }

        // ------------------------------------------------------------------ dynamic

        public Lane LaneAt(Vector2 local)
        {
            if (local.x < stage.Board.xMin || local.x > stage.Board.xMax) return null;
            foreach (var l in Lanes) if (local.y <= l.Top && local.y >= l.Bottom) return l;
            return null;
        }

        public bool OnBoard(Vector2 local) => stage.Board.Contains(local);

        /// <summary>
        /// Lay out chips for every lane, in time order. A chip sits over its moment when there's room;
        /// otherwise it takes a free row, or slides right and keeps a leader line to its true time.
        /// Returns chip centres keyed by "card@lane", each with a stacking order.
        /// </summary>
        public Dictionary<string, ChipPlace> LayoutChips(Board board, IEnumerable<(CardDef card, string lane)> placed)
        {
            var result = new Dictionary<string, ChipPlace>();
            var byLane = placed.GroupBy(p => p.lane);
            const float gap = 0.06f;
            foreach (var g in byLane)
            {
                if (!LaneById.TryGetValue(g.Key, out var lane)) continue;
                var size = CardView.ChipSize * ScaleFor(lane.Id);
                float w = size.x, h = size.y;
                var items = g.Select(p => (p.card, from: board.BoardFrom(p.card), to: board.BoardTo(p.card)))
                    .OrderBy(x => x.from).ThenBy(x => x.to).ThenBy(x => x.card.Id).ToList();
                float chipBase = lane.IsTown ? (lane.Top + lane.Bottom) / 2 + 0.02f : lane.Track + ChipGap + h / 2;
                int rows = lane.IsTown ? 1 : Mathf.Clamp(Mathf.FloorToInt((lane.Top - 0.06f - (lane.Track + ChipGap)) / (h + 0.05f)), 1, 3);
                float rowStep = h + 0.05f;
                var rowEnds = new float[rows];
                for (int r = 0; r < rows; r++) rowEnds[r] = float.MinValue;
                int order = 0;
                foreach (var it in items)
                {
                    bool interval = it.to > it.from;
                    float mx = TimeToX(it.from), mx2 = TimeToX(it.to);
                    float desired = interval ? mx + w / 2 - 0.1f : mx;
                    desired = Mathf.Clamp(desired, X0 - 0.3f + w / 2, X1 + 0.3f - w / 2);
                    int bestRow = 0;
                    float bestX = 0, bestCost = float.MaxValue;
                    for (int r = 0; r < rows; r++)
                    {
                        float x = Mathf.Max(desired, rowEnds[r] + gap + w / 2);
                        float cost = (x - desired) + r * 0.35f;
                        if (cost < bestCost) { bestCost = cost; bestRow = r; bestX = x; }
                    }
                    rowEnds[bestRow] = bestX + w / 2;
                    var pos = new Vector2(bestX, chipBase + bestRow * rowStep);
                    result[it.card.Id + "@" + lane.Id] = new ChipPlace { CardId = it.card.Id, Lane = lane.Id, Pos = pos, MarkerX = mx, MarkerX2 = mx2, Interval = interval, Order = order++ };
                }
            }
            return result;
        }

        /// <summary>Rebuild markers, leader lines, ribbons and contradiction marks from the board.</summary>
        public void RefreshDynamic(Board board, Dictionary<string, ChipPlace> chips, ICollection<string> struck, string hoverCard)
        {
            for (int i = dynamicRoot.childCount - 1; i >= 0; i--) Art.DestroyWithMeshes(dynamicRoot.GetChild(i));
            var map = board.Map;

            // Markers & leaders for every placed chip (including struck, drawn faint).
            foreach (var ch in chips.Values)
            {
                if (!LaneById.TryGetValue(ch.Lane, out var lane)) continue;
                var card = c.CardById.TryGetValue(ch.CardId, out var cd) ? cd : null;
                if (card == null) continue;
                bool isStruck = struck.Contains(ch.CardId);
                var loc = Locations.Get(card.Location);
                var col = loc != null ? Pal.Hex(loc.Color) : Pal.InkSoft;
                float alpha = isStruck ? 0.28f : 1f;
                if (ch.Interval)
                {
                    var bar = Shapes.Quad(dynamicRoot, "bar", new Vector2(Mathf.Max(0.05f, ch.MarkerX2 - ch.MarkerX), 0.13f),
                        Art.UnlitShared(new Color(col.r, col.g, col.b, 0.75f * alpha), true), new Vector3((ch.MarkerX + ch.MarkerX2) / 2, lane.Track, ZMarker));
                    if (isStruck) Line(dynamicRoot, new Vector3(ch.MarkerX, lane.Track, ZMarker - 0.002f), new Vector3(ch.MarkerX2, lane.Track, ZMarker - 0.002f), 0.025f, new Color(0.3f, 0.3f, 0.3f, 0.6f));
                }
                else
                {
                    Shapes.Quad(dynamicRoot, "dotShadow", new Vector2(0.26f, 0.26f), markerShadowMat, new Vector3(ch.MarkerX + 0.02f, lane.Track - 0.02f, ZMarker + 0.001f));
                    Shapes.Quad(dynamicRoot, "dot", new Vector2(0.19f, 0.19f), Art.UnlitShared(new Color(col.r * 0.85f, col.g * 0.85f, col.b * 0.85f, alpha), true, "dot"),
                        new Vector3(ch.MarkerX, lane.Track, ZMarker));
                    Shapes.Quad(dynamicRoot, "dotCore", new Vector2(0.11f, 0.11f), Art.UnlitShared(new Color(col.r * 0.7f, col.g * 0.7f, col.b * 0.7f, alpha), false),
                        new Vector3(ch.MarkerX, lane.Track, ZMarker - 0.001f));
                }
                if (!lane.IsTown)
                {
                    float bottom = ch.Pos.y - ChipSize.y / 2;
                    float lx = Mathf.Clamp(ch.MarkerX, ch.Pos.x - ChipSize.x / 2 + 0.08f, ch.Pos.x + ChipSize.x / 2 - 0.08f);
                    Line(dynamicRoot, new Vector3(lx, bottom, ZRibbon + 0.004f), new Vector3(ch.MarkerX, lane.Track + 0.06f, ZRibbon + 0.004f), 0.018f,
                        new Color(0.12f, 0.12f, 0.14f, isStruck ? 0.15f : 0.45f));
                }
            }

            // Ribbons: walk from the event that ends latest so far to the next one that starts after it.
            foreach (var lane in Lanes)
            {
                if (lane.IsTown || !board.Lanes.TryGetValue(lane.Id, out var evs) || evs.Count < 2) continue;
                var frontier = evs[0];
                for (int i = 1; i < evs.Count; i++)
                {
                    var next = evs[i];
                    if (next.From >= frontier.To) DrawRibbon(lane, frontier, next, map, frontier.Hypothesis || next.Hypothesis);
                    if (next.To > frontier.To || next.From >= frontier.To) frontier = next;
                }
            }

            // Overlap contradictions ("two places at once").
            var drawn = new HashSet<string>();
            foreach (var k in board.Conflicts)
            {
                if (!k.Overlap || !LaneById.TryGetValue(k.Lane, out var lane)) continue;
                float o0 = Mathf.Max(k.A.From, k.B.From), o1 = Mathf.Min(k.A.To, k.B.To);
                string key = k.Lane + o0 + "-" + o1;
                if (!drawn.Add(key)) continue;
                float x0 = TimeToX(o0), x1 = Mathf.Max(TimeToX(o1), x0 + 0.12f);
                var hatch = Shapes.Quad(dynamicRoot, "overlap", new Vector2(x1 - x0, 0.34f), hatchMat, new Vector3((x0 + x1) / 2, lane.Track, ZRibbon - 0.004f));
                SetTiling(hatch, new Vector2((x1 - x0) / 0.34f, 1));
                if (k.Established) Pill(dynamicRoot, "TWO PLACES AT ONCE", x1 + 0.12f, lane.Track - 0.22f, Pal.Red, 0.13f, "alert");
                else Label(dynamicRoot, "if it were them…", x1 + 1.05f, lane.Track - 0.22f, Pal.Red, 0.14f, true, 2.0f);
            }
        }

        void DrawRibbon(Lane lane, LaneEvent a, LaneEvent b, TownMap map, bool hyp)
        {
            int need = map.Minutes(a.Location, b.Location);
            int have = b.From - a.To;
            float y = lane.Track;
            float xa = TimeToX(a.To), xb = TimeToX(b.From);
            if (need == 0)
            {
                if (xb - xa > 0.15f)
                    Shapes.StripObject(dynamicRoot, "stay", new[] { new Vector3(xa, y, ZRibbon), new Vector3(xb, y, ZRibbon) }, 0.035f,
                        Art.UnlitShared(new Color(Pal.Slack.r, Pal.Slack.g, Pal.Slack.b, 0.35f), true));
                return;
            }
            float xNeed = TimeToX(a.To + need);
            bool bad = need > have;
            var walkCol = bad ? Pal.Red : Pal.Ribbon;
            if (hyp) walkCol = new Color(walkCol.r, walkCol.g, walkCol.b, 0.55f);
            float walkEnd = bad ? xb : xNeed;
            if (walkEnd > xa)
                Shapes.StripObject(dynamicRoot, "walk", new[] { new Vector3(xa, y, ZRibbon), new Vector3(walkEnd, y, ZRibbon) }, 0.1f, Art.UnlitShared(walkCol, true));
            if (!bad && xb - xNeed > 0.06f)
            {
                var s = Shapes.StripObject(dynamicRoot, "slack", new[] { new Vector3(xNeed, y, ZRibbon), new Vector3(xb, y, ZRibbon) }, 0.05f, slackMat, 1f / 0.18f);
            }
            if (bad)
            {
                var hatch = Shapes.Quad(dynamicRoot, "short", new Vector2(Mathf.Max(0.08f, xNeed - xb), 0.22f), hatchMat, new Vector3((xb + xNeed) / 2, y, ZRibbon - 0.003f));
                SetTiling(hatch, new Vector2(Mathf.Max(0.3f, (xNeed - xb) / 0.22f), 1));
                // Tear mark at the next card's start.
                Shapes.Icon(dynamicRoot, "alert", 0.24f, Pal.Red, new Vector3(xb, y + 0.26f, ZLabel));
            }
            // Label: walker + minutes (and the shortfall if impossible).
            float mid = (xa + Mathf.Max(xNeed, xb)) / 2;
            string text = bad ? $"{need} min walk · only {Mathf.Max(0, have)}" : $"{need} min";
            float lw = bad ? 2.3f : 0.9f;
            if (bad)
                Pill(dynamicRoot, text.ToUpperInvariant(), mid - 1.1f, y - 0.22f, hyp ? new Color(Pal.Red.r, Pal.Red.g, Pal.Red.b, 0.7f) : Pal.Red, 0.13f, "walk");
            else if (xb - xa > 0.75f)
            {
                Shapes.Icon(dynamicRoot, "walk", 0.18f, Pal.Ribbon, new Vector3(mid - lw / 2 + 0.02f, y - 0.22f, ZLabel));
                Label(dynamicRoot, text, mid + 0.12f, y - 0.22f, Pal.Ribbon, 0.13f, false, lw);
            }
        }

        static readonly Dictionary<(Material, Vector2), Material> tiled = new Dictionary<(Material, Vector2), Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => tiled.Clear();   // see Art.ResetStatics

        static void SetTiling(MeshRenderer r, Vector2 tiling)
        {
            tiling = new Vector2(Mathf.Round(tiling.x * 4) / 4, Mathf.Round(tiling.y * 4) / 4);
            var key = (r.sharedMaterial, tiling);
            if (!tiled.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(r.sharedMaterial);
                m.SetTextureScale("_BaseMap", tiling);
                tiled[key] = m;
            }
            r.sharedMaterial = m;
        }

        /// <summary>A solid tag with white type, left edge at x: readable over ribbons, lines and lane edges.</summary>
        static TextMeshPro Pill(Transform parent, string text, float x, float y, Color bg, float size, string icon = null)
        {
            size *= Mathf.Min(TextScale, 1.2f);
            float pad = size * 0.6f, iconW = icon != null ? size * 1.35f : 0f, h = size * 1.85f;
            var t = Txt.Make(parent, "pill", text, Art.SansBold, size, Color.white, new Vector2(6f, h), TextAlignmentOptions.Left,
                new Vector3(0, y, ZLabel - 0.004f), false);
            t.characterSpacing = 4;
            float textW = t.GetPreferredValues(text).x;
            float w = pad + iconW + textW + pad;
            t.rectTransform.sizeDelta = new Vector2(textW + 0.02f, h);
            t.transform.localPosition = new Vector3(x + pad + iconW + textW / 2 + 0.01f, y, ZLabel - 0.004f);
            Shapes.Quad(parent, "pillBg", new Vector2(w, h), Art.UnlitShared(bg), new Vector3(x + w / 2, y, ZLabel - 0.002f));
            if (icon != null) Shapes.Icon(parent, icon, size * 1.15f, Color.white, new Vector3(x + pad + iconW * 0.4f, y, ZLabel - 0.005f));
            return t;
        }

        static TextMeshPro Label(Transform parent, string text, float x, float y, Color col, float size, bool bold, float width = 3f)
        {
            float k = Mathf.Min(TextScale, 1.2f);
            return Txt.Make(parent, "label", text, bold ? Art.SansBold : Art.Sans, size * k, col, new Vector2(width * k, 0.25f * k), TextAlignmentOptions.Center,
                new Vector3(x, y, ZLabel), false);
        }

        /// <summary>Update each suspect's lock. Returns lanes whose lock just broke open.</summary>
        public List<string> RefreshLocks(Board board, bool animate)
        {
            var opened = new List<string>();
            foreach (var lane in Lanes)
            {
                if (lane.LockRoot == null || !board.Fits.TryGetValue(lane.Id, out var fit)) continue;
                bool empty = !board.Lanes.TryGetValue(lane.Id, out var evs) || !evs.Exists(e => !e.Hypothesis);
                bool open = fit.Fits;
                var col = empty ? Pal.InkFaint : (open ? Pal.Red : Pal.Green);
                lane.LockIcon.sharedMaterial = Art.Unlit(col, true, null, Resources.Load<Texture2D>("Icons/" + (empty ? "question" : open ? "unlock" : "lock")));
                lane.LockLabel.text = empty ? "NO STORY" : open ? "OPEN" : "COVERED";
                lane.LockLabel.color = col;
                lane.LockNote.text = empty ? "pin some cards" : open
                    ? (fit.Spare == 0 ? "not a minute to spare" : $"{fit.Spare} min to spare")
                    : "couldn't reach it";
                bool? state = empty ? (bool?)null : open;
                if (lane.LastOpen.HasValue && state.HasValue && lane.LastOpen.Value != state.Value && animate)
                {
                    lane.LockRoot.Punch(0.35f, 0.45f);
                    if (open) opened.Add(lane.Id);
                }
                if (state.HasValue) lane.LastOpen = state;
            }
            return opened;
        }

        // ------------------------------------------------------------------ previews

        public void ClearPreview()
        {
            for (int i = previewRoot.childCount - 1; i >= 0; i--) Art.DestroyWithMeshes(previewRoot.GetChild(i));
            foreach (var l in Lanes) l.Strip.sharedMaterial = l.StripMat;
        }

        public void HighlightLane(string laneId, Color tint)
        {
            foreach (var l in Lanes)
                l.Strip.sharedMaterial = l.Id == laneId ? Art.Lit(Color.Lerp(l.StripColor, tint, 0.55f), "paper", 0.08f) : l.StripMat;
        }

        /// <summary>Where a card would land: a ghost marker on the lane's track.</summary>
        public void PreviewMarker(string laneId, int from, int to, Color col)
        {
            if (!LaneById.TryGetValue(laneId, out var lane)) return;
            float x0 = TimeToX(from), x1 = TimeToX(to);
            if (to > from)
                Shapes.Quad(previewRoot, "ghost", new Vector2(x1 - x0, 0.2f), Art.Unlit(new Color(col.r, col.g, col.b, 0.45f), true), new Vector3((x0 + x1) / 2, lane.Track, ZLabel));
            else
                Shapes.Quad(previewRoot, "ghost", new Vector2(0.34f, 0.34f), Art.Unlit(new Color(col.r, col.g, col.b, 0.7f), true, "dot"), new Vector3(x0, lane.Track, ZLabel));
        }

        /// <summary>Show the incident slotted into a lane (or why it can't be).</summary>
        public void PreviewIncident(Board board, string laneId)
        {
            ClearPreview();
            if (!LaneById.TryGetValue(laneId, out var lane) || lane.LockRoot == null) return;
            if (!board.Fits.TryGetValue(laneId, out var fit)) return;
            var inc = c.Incident;
            var map = board.Map;
            if (fit.Fits)
            {
                HighlightLane(laneId, Pal.Red);
                int s = fit.EarliestStart;
                float x0 = TimeToX(s), x1 = TimeToX(s + inc.Duration);
                var block = Shapes.Quad(previewRoot, "inc", new Vector2(Mathf.Max(0.1f, x1 - x0), 0.36f), Art.Unlit(new Color(0.78f, 0.15f, 0.12f, 0.85f), true), new Vector3((x0 + x1) / 2, lane.Track, ZLabel));
                Label(previewRoot, $"{Locations.Short(inc.Location)} {TimeFmt.Format(s)}" + (fit.Spare > 0 ? $"  (+{fit.Spare})" : ""), (x0 + x1) / 2, lane.Track + 0.34f, Pal.Oxblood, 0.14f, true, 2.4f);
                // Routes to and from the scene.
                var evs = board.Lanes[laneId].Where(e => !e.Hypothesis).ToList();
                var before = evs.Where(e => e.To <= s).OrderByDescending(e => e.To).FirstOrDefault();
                var after = evs.Where(e => e.From >= s + inc.Duration).OrderBy(e => e.From).FirstOrDefault();
                if (before != null && before.Location != inc.Location)
                {
                    int need = map.Minutes(before.Location, inc.Location);
                    float a = TimeToX(before.To), bx = TimeToX(before.To + need);
                    Shapes.StripObject(previewRoot, "to", new[] { new Vector3(a, lane.Track + 0.12f, ZLabel), new Vector3(bx, lane.Track + 0.12f, ZLabel) }, 0.07f, Art.Unlit(new Color(0.75f, 0.2f, 0.15f, 0.9f), true));
                }
                if (after != null && after.Location != inc.Location)
                {
                    int need = map.Minutes(inc.Location, after.Location);
                    float a = TimeToX(s + inc.Duration), bx = TimeToX(s + inc.Duration + need);
                    Shapes.StripObject(previewRoot, "from", new[] { new Vector3(a, lane.Track + 0.12f, ZLabel), new Vector3(bx, lane.Track + 0.12f, ZLabel) }, 0.07f, Art.Unlit(new Color(0.75f, 0.2f, 0.15f, 0.9f), true));
                }
            }
            else
            {
                HighlightLane(laneId, Pal.Green);
                float x0 = TimeToX(board.IncidentFrom), x1 = TimeToX(board.IncidentTo);
                Shapes.Quad(previewRoot, "covered", new Vector2(x1 - x0, 0.3f), Art.Unlit(new Color(0.3f, 0.5f, 0.35f, 0.35f), true, "hatch"), new Vector3((x0 + x1) / 2, lane.Track, ZLabel));
                Label(previewRoot, "COVERED: couldn't have been there", (x0 + x1) / 2, lane.Track + 0.34f, Pal.Green, 0.14f, true, 4f);
            }
        }

        /// <summary>Red string between the echoes of a multi-person card.</summary>
        public void DrawString(Vector2 a, Vector2 b)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                var p = Vector2.Lerp(a, b, t);
                p.x += Mathf.Sin(t * Mathf.PI) * 0.18f;
                pts.Add(new Vector3(p.x, p.y, -0.12f - Mathf.Sin(t * Mathf.PI) * 0.05f));
            }
            Shapes.StripObject(dynamicRoot, "string", pts, 0.035f, stringMat);
        }

        public Vector3 LocalToWorld(Vector2 p, float lift = 0) => stage.BoardToWorld(p, lift);
    }
}
