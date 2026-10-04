using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>The game's colours. One place, so the whole look can be tuned together.</summary>
    public static class Pal
    {
        public static Color Hex(string hex, float a = 1)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            c.a = a;
            return c;
        }

        public static readonly Color Lamp = Hex("F2B65A");
        public static readonly Color Cork = Hex("B88A5A");
        public static readonly Color Paper = Hex("F1E6CF");
        public static readonly Color PaperWhite = Hex("FBF8EF");
        public static readonly Color Ink = Hex("1F2A3A");
        public static readonly Color InkSoft = Hex("3C4A5C");
        public static readonly Color InkFaint = Hex("7C8794");
        public static readonly Color Oxblood = Hex("8E2B2B");
        public static readonly Color Red = Hex("C23B2E");
        public static readonly Color RedBright = Hex("E2533F");
        public static readonly Color Brass = Hex("C9A24A");
        public static readonly Color Night = Hex("203B45");
        public static readonly Color Ribbon = Hex("2E4A6B");
        public static readonly Color Slack = Hex("6E8197");
        public static readonly Color Green = Hex("4F7A55");
        public static readonly Color Stamp = Hex("A8232C");
        public static readonly Color Blue = Hex("2F5D8A");

        public static Color CardColor(string kind)
        {
            switch (kind)
            {
                case "statement": return Hex("F3EAD3");
                case "receipt": return Hex("FBFAF3");
                case "ledger": return Hex("E2E8D2");
                case "call": return Hex("DBE5EC");
                case "ticket": return Hex("EBCBB2");
                case "photo": return Hex("F6F3EA");
                case "note": return Hex("F2E3A0");
                case "incident": return Hex("F4E9D4");
                default: return Paper;
            }
        }

        public static string KindLabel(string kind)
        {
            switch (kind)
            {
                case "statement": return "STATEMENT";
                case "receipt": return "RECEIPT";
                case "ledger": return "LEDGER";
                case "call": return "LOG";
                case "ticket": return "TICKET";
                case "photo": return "PHOTOGRAPH";
                case "note": return "LISTING";
                default: return kind.ToUpperInvariant();
            }
        }
    }

    /// <summary>Loads shared art from Resources once and hands out instances, with safe fallbacks.</summary>
    public static class Art
    {
        static readonly Dictionary<string, TMP_FontAsset> fonts = new Dictionary<string, TMP_FontAsset>();
        static readonly Dictionary<string, Material> baseMats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public const string Typewriter = "SpecialElite-Regular SDF";
        public const string Mono = "CourierPrime-Regular SDF";
        public const string MonoBold = "CourierPrime-Bold SDF";
        public const string Hand = "Caveat SDF";
        public const string Serif = "PlayfairDisplay SDF";
        public const string SerifItalic = "PlayfairDisplay-Italic SDF";
        public const string Display = "AbrilFatface-Regular SDF";
        public const string Sans = "IBMPlexSansCondensed-Medium SDF";
        public const string SansBold = "IBMPlexSansCondensed-SemiBold SDF";

        public static TMP_FontAsset Font(string name)
        {
            if (fonts.TryGetValue(name, out var f) && f != null) return f;
            f = Resources.Load<TMP_FontAsset>("Fonts/" + name);
            if (f == null)
            {
                Debug.LogWarning($"[Art] missing font {name}, using default");
                f = TMP_Settings.defaultFontAsset;
            }
            fonts[name] = f;
            return f;
        }

        public static Texture2D Tex(string name)
        {
            if (textures.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Textures/" + name);
            textures[name] = t;
            return t;
        }

        public static Sprite Sprite(string path)
        {
            if (sprites.TryGetValue(path, out var s)) return s;
            var tex = Resources.Load<Texture2D>(path);
            s = tex != null ? UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100) : null;
            sprites[path] = s;
            return s;
        }

        public static Texture2D Portrait(string id) => Resources.Load<Texture2D>("Portraits/" + id);

        public static Texture2D Photo(string id) => id == null ? null : Resources.Load<Texture2D>("Photos/" + id);

        public static GameObject Model(string name)
        {
            if (models.TryGetValue(name, out var m)) return m;
            m = Resources.Load<GameObject>("Models/" + name);
            if (m == null) Debug.LogWarning($"[Art] missing model {name}");
            models[name] = m;
            return m;
        }

        /// <summary>Instantiate a model (or null if it doesn't exist) with its materials swapped for ours.</summary>
        public static GameObject Spawn(string name, Transform parent)
        {
            var prefab = Model(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = RemapImported(mats[i]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return go;
        }

        static Material BaseMat(string name)
        {
            if (baseMats.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                var shader = Shader.Find(name.StartsWith("Unlit") ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                m = new Material(shader);
                Debug.LogWarning($"[Art] missing material asset {name}, built one at runtime");
            }
            baseMats[name] = m;
            return m;
        }

        /// <summary>A lit, opaque material (cached by colour/texture/smoothness).</summary>
        public static Material Lit(Color c, string tex = null, float smooth = 0.25f, float metal = 0f, Vector2? tiling = null, string normal = null)
        {
            string key = $"lit|{ColorUtility.ToHtmlStringRGBA(c)}|{tex}|{smooth:0.00}|{metal:0.00}|{tiling}|{normal}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(BaseMat(normal != null ? "LitNormal" : "Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (tex != null)
            {
                var t = Tex(tex);
                if (t != null) m.SetTexture("_BaseMap", t);
            }
            if (normal != null)
            {
                var n = Tex(normal);
                if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            }
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            if (tiling.HasValue && normal != null) m.SetTextureScale("_BumpMap", tiling.Value);
            cache[key] = m;
            return m;
        }

        public static Material Emissive(Color c, Color emission, float intensity = 1)
        {
            string key = $"emit|{ColorUtility.ToHtmlStringRGBA(c)}|{ColorUtility.ToHtmlStringRGBA(emission)}|{intensity}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(BaseMat("LitEmissive"));
            m.SetColor("_BaseColor", c);
            m.SetColor("_EmissionColor", emission * intensity);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            cache[key] = m;
            return m;
        }

        /// <summary>Unlit colour (optionally transparent, optionally textured). Not cached: callers often animate it.</summary>
        public static Material Unlit(Color c, bool transparent = false, string tex = null, Texture texture = null)
        {
            var m = new Material(BaseMat(transparent ? "UnlitTransparent" : "Unlit"));
            m.SetColor("_BaseColor", c);
            var t = texture != null ? texture : (tex != null ? Tex(tex) : null);
            if (t != null) m.SetTexture("_BaseMap", t);
            return m;
        }

        /// <summary>Shared unlit material for static colours (don't animate these).</summary>
        public static Material UnlitShared(Color c, bool transparent = false, string tex = null)
        {
            string key = $"unlit|{ColorUtility.ToHtmlStringRGBA(c)}|{transparent}|{tex}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = Unlit(c, transparent, tex);
            cache[key] = m;
            return m;
        }

        /// <summary>Destroy a generated hierarchy along with any procedural meshes it owns.</summary>
        public static void DestroyWithMeshes(Transform root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh != Shapes.QuadMesh && mf.sharedMesh.name == "strip") Object.Destroy(mf.sharedMesh);
            Object.Destroy(root.gameObject);
        }

        public static Material LitTransparent(Color c, string tex = null)
        {
            var m = new Material(BaseMat("LitTransparent"));
            m.SetColor("_BaseColor", c);
            if (tex != null && Tex(tex) != null) m.SetTexture("_BaseMap", Tex(tex));
            return m;
        }

        /// <summary>
        /// Imported FBX materials are named like "wood_oak" or "col_C9A24A" / "metal_C9A24A" /
        /// "glow_F2B65A"; swap them for shared URP materials.
        /// </summary>
        static Material RemapImported(Material src)
        {
            if (src == null) return Lit(Color.magenta);
            string n = src.name.Replace(" (Instance)", "");
            int dot = n.IndexOf('.');
            if (dot >= 0) n = n.Substring(0, dot);
            var parts = n.Split('_');
            string kind = parts[0];
            Color col = parts.Length > 1 && parts[1].Length >= 6 ? Pal.Hex(parts[1].Substring(0, 6)) : Color.gray;
            switch (kind)
            {
                case "metal": return Lit(col, null, 0.62f, 0.85f);
                case "brass": return Lit(Pal.Brass, "brushed", 0.58f, 0.9f);
                case "glow": return Emissive(col, col, 2.4f);
                case "glass": return Lit(col, null, 0.92f, 0.1f);
                case "wood": return Lit(Color.Lerp(parts.Length > 1 ? col : Pal.Hex("6B4528"), Color.white, 0.62f), "wood", 0.34f, 0, null, "wood_n");
                case "woodlight": return Lit(Pal.Hex("9C6B3F"), "wood", 0.3f);
                case "cork": return Lit(Pal.Hex("C79B68"), "cork", 0.05f, 0, new Vector2(3, 2), "cork_n");
                case "leather": return Lit(Color.Lerp(parts.Length > 1 ? col : Pal.Hex("3E5A45"), Color.white, 0.55f), "leather", 0.38f);
                case "paper": return Lit(parts.Length > 1 ? col : Pal.Paper, "paper", 0.1f);
                case "plastic": return Lit(col, null, 0.55f);
                case "ceramic": return Lit(col, null, 0.75f);
                case "fabric": return Lit(col, "paper", 0.05f);
                case "col":
                default: return Lit(col, null, 0.3f);
            }
        }
    }
}
