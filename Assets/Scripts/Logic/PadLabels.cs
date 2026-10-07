using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AlibiCo.Logic
{
    /// <summary>Whose names are printed on a gamepad's buttons.</summary>
    public enum PadFamily { Xbox, PlayStation, Nintendo }

    /// <summary>
    /// The pad prompts are written with Xbox names in brackets ("[A]", "[LB]", "[Start]"), meaning the
    /// button in that *position*: [A] is the bottom face button. This names them as the pad in the
    /// player's hands prints them. A Switch Pro controller's bottom button is labelled B, so "[A]"
    /// becomes "[B]" there: the prompt names the button the player actually presses.
    /// </summary>
    public static class PadLabels
    {
        // Lower case. Vendor ids appear in browsers' gamepad names ("054c-0ce6-…", "Vendor: 054c").
        static readonly string[] PlayStationWords = { "dualshock", "dualsense", "playstation", "sony", "054c" };
        static readonly string[] NintendoWords = { "switchpro", "nintendo", "pro controller", "joy-con", "057e" };

        /// <summary>
        /// The family from whatever names the device has: the Input System layout ("DualSenseGamepadHID",
        /// "SwitchProControllerHID"), the manufacturer, the product, or a browser's gamepad id string.
        /// Anything unrecognised reads as Xbox, the layout the prompts are written in.
        /// </summary>
        public static PadFamily Detect(params string[] names)
        {
            if (names == null) return PadFamily.Xbox;
            foreach (var family in new[] { PadFamily.PlayStation, PadFamily.Nintendo })
            {
                var words = family == PadFamily.PlayStation ? PlayStationWords : NintendoWords;
                foreach (var n in names)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    var low = n.ToLowerInvariant();
                    foreach (var w in words) if (low.Contains(w)) return family;
                }
            }
            return PadFamily.Xbox;
        }

        static readonly Regex Token = new Regex(@"\[(A|B|X|Y|LB|RB|Start)\]", RegexOptions.CultureInvariant);

        /// <summary>The label for an Xbox-named button on this family's pad.</summary>
        public static string Name(string xbox, PadFamily family)
        {
            switch (family)
            {
                case PadFamily.PlayStation:
                    switch (xbox) { case "A": return "✕"; case "B": return "○"; case "X": return "□"; case "Y": return "△"; case "LB": return "L1"; case "RB": return "R1"; case "Start": return "Options"; }
                    break;
                case PadFamily.Nintendo:
                    switch (xbox) { case "A": return "B"; case "B": return "A"; case "X": return "Y"; case "Y": return "X"; case "LB": return "L"; case "RB": return "R"; case "Start": return "+"; }
                    break;
            }
            return xbox;
        }

        static readonly Dictionary<(string, PadFamily), string> cache = new Dictionary<(string, PadFamily), string>();

        /// <summary>Every "[A]"-style token in <paramref name="text"/>, renamed for the family (all at once, so A and B can swap).</summary>
        public static string Localize(string text, PadFamily family)
        {
            if (string.IsNullOrEmpty(text) || family == PadFamily.Xbox) return text;
            if (cache.TryGetValue((text, family), out var done)) return done;
            done = Token.Replace(text, m => "[" + Name(m.Groups[1].Value, family) + "]");
            if (cache.Count > 256) cache.Clear();
            cache[(text, family)] = done;
            return done;
        }

        /// <summary>True if the text still holds an Xbox token (a prompt that wasn't localized).</summary>
        public static bool HasXboxTokens(string text) => !string.IsNullOrEmpty(text) && Token.IsMatch(text);
    }
}
