using System.Collections.Generic;
using System.IO;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>The town (places + walking times), loaded once from Resources/Data/town.json.</summary>
    public static class Locations
    {
        static TownData data;
        static TownMap map;

        public static TownData Data { get { Load(); return data; } }
        public static TownMap Map { get { Load(); return map; } }

        static void Load()
        {
            if (data != null) return;
            var txt = Resources.Load<TextAsset>("Data/town");
            data = TownData.FromJson(txt.text);
            map = new TownMap(data);
        }

        public static Location Get(string id)
        {
            if (id == null) return null;
            return Data.ById.TryGetValue(id, out var l) ? l : null;
        }

        public static string Name(string id) => Get(id)?.Name ?? id;
        public static string Short(string id) => Get(id)?.Short ?? id;
    }

    /// <summary>Every case file (Data/case1.json, case2.json, …), in order.</summary>
    public static class Cases
    {
        static List<CaseDef> all;

        public static List<CaseDef> All
        {
            get
            {
                if (all != null) return all;
                all = new List<CaseDef>();
                for (int i = 1; i <= 9; i++)
                {
                    var t = Resources.Load<TextAsset>("Data/case" + i);
                    if (t == null) break;
                    all.Add(CaseDef.FromJson(t.text));
                }
                return all;
            }
        }

        /// <summary>A handwritten case, or a Daily Docket by its id ("docket-2026-10-06").</summary>
        public static CaseDef Get(string id)
        {
            if (Docket.TryParseId(id, out var date)) return DocketFor(date);
            return All.Find(c => c.Id == id);
        }

        public static int IndexOf(string id) => All.FindIndex(c => c.Id == id);

        // ------------------------------------------------------------------ the Daily Docket

        static readonly Dictionary<string, CaseDef> dockets = new Dictionary<string, CaseDef>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all = null; dockets.Clear(); ClockShift = System.TimeSpan.Zero; Today = System.DateTime.Now.Date; TodayFixed = false; }   // see Art.ResetStatics

        public static bool IsDocket(CaseDef c) => c != null && Docket.IsDocket(c.Id);

        /// <summary>The docket for a day, generated and proven airtight on first use (null if none could be).</summary>
        public static CaseDef DocketFor(System.DateTime date)
        {
            var id = Docket.IdFor(date);
            if (dockets.TryGetValue(id, out var c)) return c;
            var t0 = Time.realtimeSinceStartup;
            var r = Docket.Generate(date, Locations.Map);
            c = r?.Case;
            dockets[id] = c;
            Debug.Log(r == null ? $"[Docket] {id}: no airtight variation" :
                $"[Docket] {id} \"{c.Title}\": variation {r.Attempt + 1}, {(r.ClockDay ? "clock day" : "plain")}, proven airtight in {(Time.realtimeSinceStartup - t0) * 1000:0} ms");
            return c;
        }

        /// <summary>
        /// The local time, which decides the day's docket. Tests can start it at a chosen moment
        /// (-alibiClockAt yyyy-MM-ddTHH:mm:ss), after which it runs forward in real time.
        /// </summary>
        public static System.DateTime Now => System.DateTime.Now + ClockShift;
        public static System.TimeSpan ClockShift;

        /// <summary>The local date decides the day's docket (-alibiDocketDate yyyy-MM-dd overrides it for tests).</summary>
        public static System.DateTime Today = System.DateTime.Now.Date;
        public static bool TodayFixed;

        /// <summary>
        /// Called whenever the case files open, and every second while they're on screen, so a game
        /// left running past midnight gets the new day's docket. True if the day moved on.
        /// </summary>
        public static bool RefreshToday()
        {
            if (TodayFixed || Now.Date == Today) return false;
            Today = Now.Date;
            return true;
        }
        public static CaseDef TodaysDocket => DocketFor(Today);

        /// <summary>The docket opens once case 2 has taught clocks.</summary>
        public static bool DocketUnlocked => SaveData.UnlockAll || (All.Count > 1 && SaveData.Current.Record(All[1].Id).solved);

        public static int DocketsClosed => SaveData.Current.cases.FindAll(r => r.solved && Docket.IsDocket(r.id)).Count;

        /// <summary>The save's record for a day's docket, or null if it has never been opened.</summary>
        public static SaveData.CaseRecord DocketRecord(System.DateTime day) => SaveData.Current.cases.Find(r => r.id == Docket.IdFor(day));

        /// <summary>Days in a row with a docket closed, up to today (Docket.Run).</summary>
        public static int DocketRun => Docket.Run(Today, d => DocketRecord(d)?.solved == true);

        /// <summary>"4 days in a row." once a run is two days or more, else empty.</summary>
        public static string DocketRunLine { get { int n = DocketRun; return n >= 2 ? $"{n} days in a row." : ""; } }
    }

    /// <summary>Polaroid portraits (rendered in Blender), with a typographic fallback.</summary>
    public static class Portraits
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => mats.Clear();   // see Art.ResetStatics

        public static Transform Make(Transform parent, CaseDef c, string personId, Vector2 size, bool small)
        {
            var root = new GameObject("portrait_" + personId).transform;
            root.SetParent(parent, false);
            var frameMat = Art.Lit(Pal.Hex("F4F1E8"), "paper", 0.1f);
            Shapes.Slab(root, "frame", size, 0.02f, frameMat, Vector3.zero);
            float border = size.x * 0.07f;
            var img = new Vector2(size.x - border * 2, size.x - border * 2);
            var imgPos = new Vector3(0, size.y / 2 - border - img.y / 2, -0.024f);
            Shapes.Quad(root, "photo", img, Mat(personId), imgPos);
            var person = c != null && c.PersonById.TryGetValue(personId, out var p) ? p : null;
            if (Art.Portrait(personId) == null)
            {
                var initials = person != null ? Initials(person.Name) : personId.Substring(0, 1).ToUpperInvariant();
                Txt.Make(root, "initials", initials, Art.Serif, img.y * 0.42f, Pal.Hex("E9E1CC"), img, TextAlignmentOptions.Center, imgPos + new Vector3(0, 0, -0.004f), false);
            }
            if (!small && person != null)
            {
                Txt.Make(root, "name", person.Name, Art.Hand, size.x * 0.13f, Pal.Ink, new Vector2(size.x * 0.95f, (size.y - size.x) * 0.9f),
                    TextAlignmentOptions.Center, new Vector3(0, -size.y / 2 + (size.y - size.x) * 0.5f, -0.024f), false).Fit(size.x * 0.08f);
            }
            return root;
        }

        public static Material Mat(string personId)
        {
            if (mats.TryGetValue(personId, out var m) && m != null) return m;
            var tex = Art.Portrait(personId);
            m = tex != null ? Art.Unlit(Color.white, false, null, tex) : Art.Unlit(Fallback(personId));
            mats[personId] = m;
            return m;
        }

        static Color Fallback(string id)
        {
            int h = id.GetHashCode();
            float hue = (h & 0xFF) / 255f;
            return Color.HSVToRGB(hue, 0.25f, 0.32f);
        }

        public static string Initials(string name)
        {
            var parts = name.Replace("Capt. ", "").Split(' ');
            string r = "";
            foreach (var p in parts) if (p.Length > 0 && char.IsLetter(p[0])) r += p[0];
            return r.Length > 2 ? r.Substring(0, 2) : r;
        }

        public static string FirstName(CaseDef c, string id)
        {
            if (c == null || !c.PersonById.TryGetValue(id, out var p)) return id;
            var n = p.Name.Replace("Capt. ", "Captain ");
            if (n.StartsWith("Captain ")) return "Captain " + n.Split(' ')[^1];
            return n.Split(' ')[0];
        }
    }

    /// <summary>Player settings (PlayerPrefs).</summary>
    public static class Settings
    {
        public static float Master { get => PlayerPrefs.GetFloat("vol_master", 0.85f); set { PlayerPrefs.SetFloat("vol_master", value); Apply(); } }
        public static float Music { get => PlayerPrefs.GetFloat("vol_music", 0.7f); set { PlayerPrefs.SetFloat("vol_music", value); Apply(); } }
        public static float Effects { get => PlayerPrefs.GetFloat("vol_sfx", 0.85f); set { PlayerPrefs.SetFloat("vol_sfx", value); Apply(); } }
        public static bool ReducedMotion { get => PlayerPrefs.GetInt("reduced_motion", 0) == 1; set => PlayerPrefs.SetInt("reduced_motion", value ? 1 : 0); }
        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set { Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; PlayerPrefs.SetInt("fullscreen", value ? 1 : 0); }
        }
        public static bool ShowTimer { get => PlayerPrefs.GetInt("show_timer", 1) == 1; set => PlayerPrefs.SetInt("show_timer", value ? 1 : 0); }

        /// <summary>
        /// Plain lettering: memos, statements, records, the notebook and the case files in a plain sans
        /// instead of the typewriter, handwriting and Courier (see Art.Lettered). Automated runs keep their
        /// choice in memory (-alibiPlainText, or a test's click), never in the prefs file.
        /// </summary>
        public static bool PlainText
        {
            get => SaveData.Volatile ? plainForRun : PlainTextFlag || PlayerPrefs.GetInt("plain_text", 0) == 1;
            set
            {
                if (SaveData.Volatile) plainForRun = value; else PlayerPrefs.SetInt("plain_text", value ? 1 : 0);
                Art.Reletter();
                GameRoot.I?.TextSizeChanged();   // the open board is rebuilt in place once the menus close
            }
        }

        /// <summary>-alibiPlainText on the command line.</summary>
        public static bool PlainTextFlag;
        static bool plainForRun;

        /// <summary>An automated run starts on a blank save: its lettering is the flag's, whatever the prefs say.</summary>
        public static void PlainTextForRun()
        {
            plainForRun = PlainTextFlag;
            Art.Reletter();
        }

        // ------------------------------------------------------------------ text size

        public static readonly string[] TextSizeNames = { "Normal", "Large", "Larger" };
        static readonly float[] TextScales = { 1f, 1.15f, 1.3f };

        /// <summary>
        /// Index into TextSizeNames. Until the player picks one, small windows (under 900 px tall, such as
        /// 720p laptops and handhelds) start at Large. Automated runs always use Normal so captures are comparable.
        /// </summary>
        public static int TextSize
        {
            get => Mathf.Clamp(TextSizeOverride >= 0 ? TextSizeOverride : SaveData.Volatile ? 0 : PlayerPrefs.GetInt("text_size", Screen.height < 900 ? 1 : 0), 0, TextScales.Length - 1);
            set { PlayerPrefs.SetInt("text_size", value); UiKit.ApplyScale(); GameRoot.I?.TextSizeChanged(); }
        }

        /// <summary>-alibiTextSize n on the command line (for capturing the larger sizes).</summary>
        public static int TextSizeOverride = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { TextSizeOverride = FidelityOverride = -1; fidelityForRun = AlibiCo.Fidelity.Default; PlainTextFlag = plainForRun = false; }   // see Art.ResetStatics

        // ------------------------------------------------------------------ graphics fidelity

        /// <summary>
        /// Index into AlibiCo.Fidelity.Names (Low, Medium, High, Ultra), applied live. Automated runs use
        /// High unless -alibiFidelity n says otherwise, and keep a change in memory, never in the prefs.
        /// </summary>
        public static int GraphicsFidelity
        {
            get => Mathf.Clamp(FidelityOverride >= 0 ? FidelityOverride : SaveData.Volatile ? fidelityForRun : PlayerPrefs.GetInt("fidelity", AlibiCo.Fidelity.Default), 0, AlibiCo.Fidelity.Names.Length - 1);
            set
            {
                FidelityOverride = -1;
                if (SaveData.Volatile) fidelityForRun = value; else PlayerPrefs.SetInt("fidelity", value);
                AlibiCo.Fidelity.Apply(value);
            }
        }

        /// <summary>-alibiFidelity n on the command line.</summary>
        public static int FidelityOverride = -1;
        static int fidelityForRun = AlibiCo.Fidelity.Default;

        /// <summary>Scales the screen-space UI and the hovered-card inspector.</summary>
        public static float TextScale => TextScales[TextSize];

        // ------------------------------------------------------------------ resolution

        /// <summary>"desktop" (the display's own size) or "WIDTHxHEIGHT".</summary>
        public static string Resolution
        {
            get => PlayerPrefs.GetString("resolution", "desktop");
            set { PlayerPrefs.SetString("resolution", value); ApplyResolution(); }
        }

        public static Vector2Int DesktopSize => new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);

        /// <summary>
        /// "desktop" first, then every distinct display mode of at least 1280x720, largest first. Some
        /// platforms (Wayland) report only the current mode, so common 16:9 sizes that fit are added.
        /// </summary>
        public static List<string> ResolutionChoices()
        {
            var desk = DesktopSize;
            var sizes = new List<Vector2Int>();
            foreach (var r in Screen.resolutions) sizes.Add(new Vector2Int(r.width, r.height));
            foreach (var h in new[] { 720, 900, 1080, 1440, 2160 }) sizes.Add(new Vector2Int(h * 16 / 9, h));
            var list = new List<string> { "desktop" };
            var seen = new HashSet<Vector2Int>();
            sizes.Sort((a, b) => b.x * b.y - a.x * a.y);
            foreach (var v in sizes)
            {
                if (v.x < 1280 || v.y < 720 || v.x > desk.x || v.y > desk.y || v == desk || !seen.Add(v)) continue;
                list.Add(v.x + "x" + v.y);
            }
            var cur = Resolution;
            if (!list.Contains(cur)) list.Insert(1, cur);
            return list;
        }

        public static string ResolutionLabel(string choice)
        {
            if (choice == "desktop") { var d = DesktopSize; return $"Desktop · {d.x} × {d.y}"; }
            return choice.Replace("x", " × ");
        }

        /// <summary>Apply the saved choice, or the given one without saving it (-alibiResolution for testing).</summary>
        public static void ApplyResolution(string choice = null)
        {
            if (Application.isEditor) return;
            var size = DesktopSize;
            var p = (choice ?? Resolution).Split('x');
            if (p.Length == 2 && int.TryParse(p[0], out int w) && int.TryParse(p[1], out int h)) size = new Vector2Int(w, h);
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
        }

        public static void Apply()
        {
            AudioListener.volume = Master;
            AudioDirector.I?.ApplyVolumes();
        }
    }

    /// <summary>Progress per case plus an in-progress board snapshot, saved as JSON.</summary>
    [System.Serializable]
    public sealed class SaveData
    {
        [System.Serializable]
        public sealed class CaseRecord
        {
            public string id;
            public bool solved;
            public int bestBadges;
            public float bestTime;
            public int plays;
            // Seals, best ever: no badge lost, no hint asked, under the case's par time.
            public bool sealClean, sealUnaided, sealSwift;
        }

        [System.Serializable]
        public sealed class Snapshot
        {
            public string caseId;
            public List<string> unlocked = new List<string>();
            public List<string> pinned = new List<string>();
            public List<string> calibrated = new List<string>();
            public List<string> struck = new List<string>();
            public List<string> fired = new List<string>();
            public List<string> confirmedKeys = new List<string>();
            public List<string> confirmedVals = new List<string>();
            public List<string> hypKeys = new List<string>();
            public List<string> hypVals = new List<string>();
            public List<string> linkA = new List<string>();
            public List<string> linkB = new List<string>();
            public List<string> seenMemos = new List<string>();
            public List<string> memoLog = new List<string>();
            public List<string> seenCards = new List<string>();
            public int mistakes;
            public float elapsed;
            public bool usedHints;
        }

        public List<CaseRecord> cases = new List<CaseRecord>();
        /// <summary>The board played most recently: the title's Continue.</summary>
        public Snapshot inProgress;
        /// <summary>
        /// Every other board left part-way, one per case or docket, oldest first. Opening one case
        /// shelves the board you were on rather than losing it. (Saves from before round 7 have none.)
        /// </summary>
        public List<Snapshot> shelved = new List<Snapshot>();

        /// <summary>The board left part-way on this case or docket, or null.</summary>
        public Snapshot BoardFor(string caseId)
        {
            if (inProgress != null && inProgress.caseId == caseId) return inProgress;
            return shelved.Find(s => s.caseId == caseId);
        }

        /// <summary>This board is the one being played: it becomes Continue, and the one before it goes on the shelf.</summary>
        public void Keep(Snapshot board)
        {
            shelved.RemoveAll(s => s.caseId == board.caseId);
            if (inProgress != null && inProgress.caseId != board.caseId) shelved.Add(inProgress);
            inProgress = board;
        }

        /// <summary>A case's board is finished with (solved, or started over). Continue moves to the next most recent board.</summary>
        public void Drop(string caseId)
        {
            shelved.RemoveAll(s => s.caseId == caseId);
            if (inProgress != null && inProgress.caseId == caseId)
            {
                inProgress = null;
                if (shelved.Count > 0) { inProgress = shelved[shelved.Count - 1]; shelved.RemoveAt(shelved.Count - 1); }
            }
        }

        /// <summary>
        /// JsonUtility can't write a null board, so "none" comes back as an empty one: read it as none.
        /// A save from before round 7 has no shelf at all.
        /// </summary>
        void Tidy()
        {
            if (shelved == null) shelved = new List<Snapshot>();
            shelved.RemoveAll(s => s == null || string.IsNullOrEmpty(s.caseId));
            if (inProgress != null && string.IsNullOrEmpty(inProgress.caseId)) inProgress = null;
        }

        /// <summary>Automated runs start from a clean desk.</summary>
        public void DropAllBoards()
        {
            inProgress = null;
            shelved.Clear();
        }

        /// <summary>
        /// A shelved docket goes once its day leaves the drawer (it can't be opened from there any
        /// more). The most recent board stays, so Continue still finds it. True if anything went.
        /// </summary>
        public bool DropDocketsBefore(System.DateTime oldestOnFile)
        {
            return shelved.RemoveAll(s => Docket.TryParseId(s.caseId, out var day) && day < oldestOnFile.Date) > 0;
        }
        public bool seenIntro;
        /// <summary>Gestures the player has used at least once; the controls strip stops teaching them.</summary>
        public List<string> learned = new List<string>();

        public static bool Learned(string what) => Current.learned.Contains(what);

        public static void Learn(string what)
        {
            if (Learned(what)) return;
            Current.learned.Add(what);
            Write();
        }

        static SaveData current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()   // see Art.ResetStatics
        {
            current = null;
            Volatile = false;
            LoadedFrom = SafeFile.Source.None;
            UnlockAll = false;
        }
        static string PathOnDisk => System.IO.Path.Combine(Application.persistentDataPath, "alibi_save.json");

        static bool Parses(string json)
        {
            if (!SafeFile.IsCompleteJsonObject(json)) return false;
            try { return JsonUtility.FromJson<SaveData>(json) != null; }
            catch (System.Exception) { return false; }
        }

        public static SaveData Current
        {
            get
            {
                if (current != null) return current;
                // The save is written crash-safe (Logic/SafeFile): an unreadable file is moved aside
                // and the previous save is loaded instead, rather than starting blank and overwriting it.
                var text = SafeFile.Read(PathOnDisk, Parses, out var source, m => Debug.LogWarning("[Save] " + m));
                if (text != null) current = JsonUtility.FromJson<SaveData>(text);
                LoadedFrom = source;
                if (source == SafeFile.Source.Backup) Debug.LogWarning("[Save] recovered progress from the backup save");
                if (current == null) current = new SaveData();
                current.Tidy();
                return current;
            }
        }

        /// <summary>Automated runs play on a blank in-memory save so they never touch the player's progress.</summary>
        public static void UseVolatile()
        {
            Volatile = true;
            current = new SaveData();
        }

        public static bool Volatile { get; private set; }

        /// <summary>Where the save came from on launch (the backup means the main file was unreadable).</summary>
        public static SafeFile.Source LoadedFrom { get; private set; }

        public static string Folder => Application.persistentDataPath;

        public static void Write()
        {
            if (Volatile) return;
            if (Application.platform == RuntimePlatform.WebGLPlayer) PlayerPrefs.Save();   // settings live in IndexedDB too
            try { SafeFile.Write(PathOnDisk, JsonUtility.ToJson(Current, true)); }
            catch (System.Exception e) { Debug.LogWarning("[Save] couldn't write save: " + e.Message); }
        }

        public static void Reset()
        {
            current = new SaveData();
            Write();
        }

        public CaseRecord Record(string id)
        {
            var r = cases.Find(c => c.id == id);
            if (r == null) { r = new CaseRecord { id = id }; cases.Add(r); }
            return r;
        }

        public bool IsUnlocked(int index) => index == 0 || UnlockAll || Record(Cases.All[index - 1].Id).solved;

        /// <summary>Set by the -alibiUnlockAll command-line flag (testing and capture).</summary>
        public static bool UnlockAll;
    }
}
