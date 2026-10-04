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

    /// <summary>All three cases, in order.</summary>
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

        public static CaseDef Get(string id) => All.Find(c => c.Id == id);
        public static int IndexOf(string id) => All.FindIndex(c => c.Id == id);
    }

    /// <summary>Polaroid portraits (rendered in Blender), with a typographic fallback.</summary>
    public static class Portraits
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

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
        }

        public List<CaseRecord> cases = new List<CaseRecord>();
        public Snapshot inProgress;
        public bool seenIntro;

        static SaveData current;
        static string PathOnDisk => System.IO.Path.Combine(Application.persistentDataPath, "alibi_save.json");

        public static SaveData Current
        {
            get
            {
                if (current != null) return current;
                try
                {
                    if (File.Exists(PathOnDisk)) current = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOnDisk));
                }
                catch (System.Exception e) { Debug.LogWarning("[Save] couldn't read save: " + e.Message); }
                if (current == null) current = new SaveData();
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

        public static void Write()
        {
            if (Volatile) return;
            try { File.WriteAllText(PathOnDisk, JsonUtility.ToJson(Current, true)); }
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
