using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlibiCo
{
    /// <summary>
    /// The browser page around the game (Assets/WebGLTemplates/Alibi). At launch it says what kind of
    /// device it's on (-alibiTouch: touch-first, a phone or tablet; -alibiCompact: a phone-sized
    /// screen; -alibiSafeMode: the last visit ended without the page closing, likely out of memory).
    /// On a touchscreen it draws its own thumb-sized buttons beside the picture (Menu or Back, Notes,
    /// Hint, Fit) and sends them here through GameRoot.TouchCommand; the game tells it which ones fit
    /// the moment (Plugins/WebGL/WebPage.jslib) and, while they show, hides its own Hint / Notes / Menu
    /// pill. Nothing here does anything outside a browser.
    /// </summary>
    public static class WebPage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void AlibiTouchState(string state);
#endif
        /// <summary>A phone or tablet: a coarse pointer and no fine one (the page decides).</summary>
        public static bool TouchFirst { get; private set; }
        /// <summary>A phone-sized screen (its short side under 500 CSS pixels).</summary>
        public static bool Compact { get; private set; }
        /// <summary>The last visit ended mid-game without the page closing: start on the lightest settings.</summary>
        public static bool SafeMode { get; private set; }
        /// <summary>The page's touch buttons are showing (the last input was a touch).</summary>
        public static bool RailShown { get; private set; }

        static string told;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { TouchFirst = Compact = SafeMode = RailShown = false; told = null; }   // see Art.ResetStatics

        public static void ReadArgs(string[] args)
        {
            TouchFirst = args.Contains("-alibiTouch");
            Compact = args.Contains("-alibiCompact");
            SafeMode = args.Contains("-alibiSafeMode");
            if (TouchFirst || Compact || SafeMode)
                Debug.Log($"[Touch] page says: {(TouchFirst ? "touch-first" : "pointer-first")}{(Compact ? ", phone-sized" : "")}{(SafeMode ? ", safe mode after an unexpected end" : "")}");
        }

        /// <summary>From GameRoot.TouchUi: the page showed ("1") or hid ("0") its touch buttons.</summary>
        public static void SetRail(bool on)
        {
            if (on == RailShown) return;
            RailShown = on;
            Debug.Log("[Touch] page buttons " + (on ? "shown" : "hidden"));
        }

        /// <summary>
        /// Which touch buttons fit now: "title", "back" (a menu or screen with somewhere to go back to),
        /// "board" or "board zoomed" (Menu, Notes, Hint and, zoomed in, Fit), or "none". Sent on change.
        /// </summary>
        public static void Tell(string state)
        {
            if (state == told) return;
            told = state;
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            Debug.Log("[Touch] controls: " + state);
#if UNITY_WEBGL && !UNITY_EDITOR
            AlibiTouchState(state);
#endif
        }

        /// <summary>
        /// For Tools/mobile-check.mjs: every button a tap would reach now, and the cards on the desk and
        /// board, as "name@x,y" in screen pixels (origin bottom left).
        /// </summary>
        public static string Targets()
        {
            var list = new List<string>();
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.isActiveAndEnabled || !s.interactable) continue;
                var cg = s.GetComponentInParent<CanvasGroup>();
                if (cg != null && (cg.alpha < 0.5f || !cg.interactable || !cg.blocksRaycasts)) continue;
                var corners = new Vector3[4];
                ((RectTransform)s.transform).GetWorldCorners(corners);
                var c = (Vector2)((corners[0] + corners[2]) / 2);
                if (c.x < 0 || c.y < 0 || c.x > Screen.width || c.y > Screen.height || !UiKit.TopHitIs(c, s.gameObject)) continue;
                var label = s.GetComponentInChildren<TextMeshProUGUI>();
                string name = (label != null && !string.IsNullOrWhiteSpace(label.text) ? label.text : s.name).Replace(";", ",").Replace("@", " ").Replace("\n", " ");
                list.Add($"{name}@{c.x:0},{c.y:0}");
            }
            var root = GameRoot.I;
            if (root != null && root.Flow == Flow.Playing && root.Session != null)
                foreach (var p in root.Session.CursorTargets()) list.Add($"card@{p.x:0},{p.y:0}");
            return $"{Screen.width}x{Screen.height};" + string.Join(";", list);
        }
    }
}
