using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AlibiCo
{
    /// <summary>Runtime-built uGUI: one overlay canvas, rounded panels, buttons, sliders, toggles.</summary>
    public static class UiKit
    {
        public static Canvas Canvas { get; private set; }
        public static RectTransform Root { get; private set; }
        static Sprite rounded, roundedSmall, white, softGlow;

        public static readonly Color PanelDark = Pal.Hex("15191E", 0.92f);
        public static readonly Color PanelPaper = Pal.Hex("F1E8D4");
        public static readonly Color ButtonDark = Pal.Hex("232A31");
        public static readonly Color ButtonHover = Pal.Hex("33404B");

        public static void Init()
        {
            if (Canvas != null) return;
            var go = new GameObject("UI");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)go.transform;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            rounded = MakeRounded(64, 18);
            roundedSmall = MakeRounded(32, 8);
            white = MakeRounded(8, 0);
            softGlow = MakeGlow(64);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = radius == 0 ? 1 : Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            int b = Mathf.Max(1, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(this RectTransform r, float inset = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        public static Image Panel(Transform parent, string name, Color color, bool round = true, bool small = false)
        {
            var r = Rect(parent, name);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = round ? (small ? roundedSmall : rounded) : white;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Image Glow(Transform parent, Color color)
        {
            var r = Rect(parent, "glow");
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = softGlow;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, string font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, string name = "text")
        {
            var r = Rect(parent, name);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Art.Font(font);
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            public Image Target;
            public Color Normal, Hot;
            public bool Interactable = true;
            Vector3 baseScale = Vector3.one;
            public void OnPointerEnter(PointerEventData e)
            {
                if (!Interactable) return;
                if (Target) Target.color = Hot;
                transform.localScale = baseScale * 1.03f;
                Sfx.Play("ui_hover", 0.35f);
            }
            public void OnPointerExit(PointerEventData e)
            {
                if (Target) Target.color = Normal;
                transform.localScale = baseScale;
            }
            public void OnPointerDown(PointerEventData e) { if (Interactable) transform.localScale = baseScale * 0.97f; }
            public void OnPointerUp(PointerEventData e) { transform.localScale = baseScale; }
        }

        public static Button Button(Transform parent, string label, Action onClick, Color? bg = null, Color? fg = null,
            float fontSize = 30, string font = null, string name = null)
        {
            var img = Panel(parent, name ?? ("btn_" + label), bg ?? ButtonDark, true, true);
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var h = img.gameObject.AddComponent<Hover>();
            h.Target = img;
            h.Normal = img.color;
            h.Hot = bg.HasValue ? Color.Lerp(bg.Value, Color.white, 0.12f) : ButtonHover;
            var t = Text(img.transform, label, font ?? Art.SansBold, fontSize, fg ?? Pal.Paper, TextAlignmentOptions.Center, "label");
            t.rectTransform.Stretch(6);
            b.onClick.AddListener(() =>
            {
                if (!h.Interactable) return;
                Sfx.Play("ui_click", 0.6f);
                onClick?.Invoke();
            });
            return b;
        }

        public static void SetInteractable(Button b, bool on)
        {
            var h = b.GetComponent<Hover>();
            if (h) h.Interactable = on;
            b.interactable = on;
            var img = b.GetComponent<Image>();
            var c = img.color;
            img.color = new Color(c.r, c.g, c.b, on ? 1f : 0.45f);
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            if (t) t.alpha = on ? 1f : 0.55f;
        }

        public static Slider Slider(Transform parent, string label, float value, Action<float> onChange)
        {
            var row = Rect(parent, "slider_" + label);
            var t = Text(row, label, Art.Sans, 28, Pal.Paper, TextAlignmentOptions.Left);
            t.rectTransform.Place(new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var sl = Rect(row, "slider");
            sl.Place(new Vector2(0.42f, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 26));
            var bg = Panel(sl, "bg", Pal.Hex("3A434C"), true, true);
            bg.rectTransform.Stretch();
            var fillArea = Rect(sl, "fillArea");
            fillArea.Stretch();
            var fill = Panel(fillArea, "fill", Pal.Lamp, true, true);
            fill.rectTransform.Stretch();
            var handleArea = Rect(sl, "handleArea");
            handleArea.Stretch();
            var handle = Panel(handleArea, "handle", Pal.Paper, true, true);
            handle.rectTransform.sizeDelta = new Vector2(30, 40);
            var s = sl.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = 0;
            s.maxValue = 1;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChange)
        {
            var row = Rect(parent, "toggle_" + label);
            var t = Text(row, label, Art.Sans, 28, Pal.Paper, TextAlignmentOptions.Left);
            t.rectTransform.Place(new Vector2(0, 0), new Vector2(0.75f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var box = Panel(row, "box", Pal.Hex("3A434C"), true, true);
            box.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(44, 44));
            var check = Panel(box.transform, "check", Pal.Lamp, true, true);
            check.rectTransform.Stretch(9);
            var tg = row.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = box;
            tg.graphic = check;
            tg.isOn = value;
            tg.onValueChanged.AddListener(v => { Sfx.Play("ui_click", 0.5f); onChange?.Invoke(v); });
            return tg;
        }

        /// <summary>Fade a CanvasGroup in or out.</summary>
        public static void Fade(CanvasGroup g, bool show, float time = 0.25f, Action done = null)
        {
            if (g == null) return;
            float from = g.alpha, to = show ? 1 : 0;
            if (show) g.gameObject.SetActive(true);
            g.blocksRaycasts = show;
            g.interactable = show;
            Tween.Run((g, "fade"), time, k => { if (g) g.alpha = Mathf.Lerp(from, to, k); }, Ease.OutCubic, () =>
            {
                if (!show && g) g.gameObject.SetActive(false);
                done?.Invoke();
            });
        }

        public static bool PointerOverUi()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
