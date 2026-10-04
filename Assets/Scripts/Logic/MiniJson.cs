using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AlibiCo.Logic
{
    /// <summary>
    /// Minimal JSON reader (objects, arrays, strings, numbers, bools, null) with no engine
    /// dependencies, so the case data loads identically in Unity and in the validator.
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWs();
            var v = p.Value();
            p.SkipWs();
            if (!p.End) throw p.Error("trailing characters");
            return v;
        }

        sealed class Parser
        {
            readonly string s;
            int i;
            public Parser(string text) { s = text; }
            public bool End => i >= s.Length;

            public FormatException Error(string msg)
            {
                int line = 1, col = 1;
                for (int k = 0; k < i && k < s.Length; k++)
                {
                    if (s[k] == '\n') { line++; col = 1; } else col++;
                }
                return new FormatException($"JSON {msg} at line {line}, column {col}");
            }

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') { i++; continue; }
                    // Allow // line comments so data files can carry designer notes.
                    if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                    {
                        while (i < s.Length && s[i] != '\n') i++;
                        continue;
                    }
                    break;
                }
            }

            public object Value()
            {
                if (End) throw Error("unexpected end");
                char c = s[i];
                switch (c)
                {
                    case '{': return Obj();
                    case '[': return Arr();
                    case '"': return Str();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return Num();
                        throw Error($"unexpected '{c}'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error($"expected {word}");
                i += word.Length;
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                i++;
                SkipWs();
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (s[i] != '"') throw Error("expected key");
                    var k = Str();
                    SkipWs();
                    if (s[i] != ':') throw Error("expected ':'");
                    i++;
                    SkipWs();
                    d[k] = Value();
                    SkipWs();
                    if (s[i] == ',') { i++; SkipWs(); if (s[i] == '}') { i++; return d; } continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw Error("expected ',' or '}'");
                }
            }

            List<object> Arr()
            {
                var l = new List<object>();
                i++;
                SkipWs();
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    SkipWs();
                    l.Add(Value());
                    SkipWs();
                    if (s[i] == ',') { i++; SkipWs(); if (s[i] == ']') { i++; return l; } continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw Error("expected ',' or ']'");
                }
            }

            string Str()
            {
                i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (End) throw Error("unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw Error($"bad escape \\{e}");
                    }
                }
            }

            object Num()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length && "0123456789.eE+-".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Typed accessors over MiniJson's loose object graph.</summary>
    public static class J
    {
        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;

        public static string Str(Dictionary<string, object> d, string key, string fallback = null) =>
            d != null && d.TryGetValue(key, out var v) && v is string s ? s : fallback;

        public static int Int(Dictionary<string, object> d, string key, int fallback = 0) =>
            d != null && d.TryGetValue(key, out var v) && v is double n ? (int)Math.Round(n) : fallback;

        public static float Float(Dictionary<string, object> d, string key, float fallback = 0) =>
            d != null && d.TryGetValue(key, out var v) && v is double n ? (float)n : fallback;

        public static bool Bool(Dictionary<string, object> d, string key, bool fallback = false) =>
            d != null && d.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        public static List<object> Arr(Dictionary<string, object> d, string key) =>
            d != null && d.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        public static List<string> Strs(Dictionary<string, object> d, string key)
        {
            var r = new List<string>();
            if (d == null || !d.TryGetValue(key, out var v)) return r;
            if (v is string one) { r.Add(one); return r; }
            if (v is List<object> l)
                foreach (var x in l)
                    if (x is string s) r.Add(s);
            return r;
        }

        public static IEnumerable<Dictionary<string, object>> Objs(Dictionary<string, object> d, string key)
        {
            foreach (var x in Arr(d, key))
                if (x is Dictionary<string, object> o) yield return o;
        }
    }
}
