using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AlibiCo
{
    /// <summary>Runtime-built uGUI: one overlay canvas, rounded panels, buttons, sliders, toggles.</summary>
    public static class UiKit
    {
        public static Canvas Canvas { get; private set; }
        public static RectTransform Root { get; private set; }
        static Sprite rounded, roundedSmall, white, softGlow, btnFace, btnRim, softShadow;

        public static readonly Color PanelDark = Pal.Hex("15191E", 0.92f);
        public static readonly Color PanelPaper = Pal.Hex("F1E8D4");
        public static readonly Color ButtonDark = Pal.Hex("232A31");
        public static readonly Color ButtonHover = Pal.Hex("33404B");

        static CanvasScaler scaler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()   // see Art.ResetStatics
        {
            Canvas = null; Root = null; scaler = null; ped = null;
            rounded = roundedSmall = white = softGlow = btnFace = btnRim = softShadow = null;
        }

        /// <summary>Text size setting: a smaller reference resolution makes every UI element larger.</summary>
        public static void ApplyScale()
        {
            if (scaler != null) scaler.referenceResolution = new Vector2(1920, 1080) / Settings.TextScale;
        }

        /// <summary>Big panels are laid out for 1920x1080; shrink them back to fit when the UI is enlarged.</summary>
        public static void FitInCanvas(RectTransform panel, float margin = 20f, float marginY = -1f)
        {
            var f = panel.gameObject.AddComponent<FitToCanvas>();
            f.Margin = margin;
            f.MarginY = marginY >= 0 ? marginY : margin;
        }

        public sealed class FitToCanvas : MonoBehaviour
        {
            public float Margin, MarginY;

            void LateUpdate()
            {
                var rt = (RectTransform)transform;
                var c = Root.rect.size;
                var r = rt.rect.size;
                if (r.x <= 0 || r.y <= 0) return;
                float s = Mathf.Min(1f, (c.x - 2 * Margin) / r.x, (c.y - 2 * MarginY) / r.y);
                rt.localScale = new Vector3(s, s, 1);
            }
        }

        public static void Init()
        {
            if (Canvas != null) return;
            var go = new GameObject("UI");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 0.5f;
            ApplyScale();
            go.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)go.transform;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                var module = es.AddComponent<InputSystemUIInputModule>();
                // A pad drives the UI through PadCursor's virtual mouse; the module's own pad
                // navigation would fire buttons a second time on A.
                module.move = null;
                module.submit = null;
                module.cancel = null;
                // Touches reach the UI through the same virtual mouse (PadCursor), so the module only
                // listens to mice and pens; with its default touch bindings a tap would press twice.
                var map = new UnityEngine.InputSystem.InputActionMap("UI");
                var point = map.AddAction("Point", UnityEngine.InputSystem.InputActionType.PassThrough, "<Mouse>/position");
                point.AddBinding("<Pen>/position");
                var click = map.AddAction("Click", UnityEngine.InputSystem.InputActionType.PassThrough, "<Mouse>/leftButton");
                click.AddBinding("<Pen>/tip");
                var right = map.AddAction("RightClick", UnityEngine.InputSystem.InputActionType.PassThrough, "<Mouse>/rightButton");
                var middle = map.AddAction("MiddleClick", UnityEngine.InputSystem.InputActionType.PassThrough, "<Mouse>/middleButton");
                var scroll = map.AddAction("ScrollWheel", UnityEngine.InputSystem.InputActionType.PassThrough, "<Mouse>/scroll");
                var asset = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputActionAsset>();
                asset.AddActionMap(map);
                module.actionsAsset = asset;
                module.point = UnityEngine.InputSystem.InputActionReference.Create(point);
                module.leftClick = UnityEngine.InputSystem.InputActionReference.Create(click);
                module.rightClick = UnityEngine.InputSystem.InputActionReference.Create(right);
                module.middleClick = UnityEngine.InputSystem.InputActionReference.Create(middle);
                module.scrollWheel = UnityEngine.InputSystem.InputActionReference.Create(scroll);
                module.move = null;
                module.submit = null;
                module.cancel = null;
                module.trackedDevicePosition = null;
                module.trackedDeviceOrientation = null;
            }
            rounded = MakeRounded(64, 18);
            roundedSmall = MakeRounded(32, 8);
            white = MakeRounded(8, 0);
            softGlow = MakeGlow(64);
            btnFace = MakeButtonFace(64, 12);
            btnRim = MakeRim(64, 12);
            softShadow = MakeSoftShadow(96, 28);
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

        /// <summary>A rounded face with the light baked in: brighter at the top, a little darker at the bottom.</summary>
        static Sprite MakeButtonFace(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    float t = (y + 0.5f) / size;                  // 0 bottom .. 1 top
                    float v = Mathf.Lerp(0.82f, 1.0f, Mathf.SmoothStep(0, 1, t));
                    byte c = (byte)(v * 255);
                    px[y * size + x] = new Color32(c, c, c, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        /// <summary>A thin rounded outline, brighter along the top edge: a bevel highlight laid over a button.</summary>
        static Sprite MakeRim(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = radius - Mathf.Sqrt(dx * dx + dy * dy);          // distance inside the edge
                    float edge = Mathf.Clamp01(d + 0.5f) * Mathf.Clamp01(2.0f - d);
                    float top = Mathf.Lerp(0.35f, 1f, (y + 0.5f) / size);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(edge * top * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        /// <summary>A blurred rounded rectangle for soft drop shadows (9-sliced, border = blur).</summary>
        static Sprite MakeSoftShadow(int size, int blur)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float inner = size / 2f - blur;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x + 0.5f - size / 2f) - inner);
                    float dy = Mathf.Max(0, Mathf.Abs(y + 0.5f - size / 2f) - inner);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / blur;
                    float a = Mathf.Clamp01(1 - d);
                    px[y * size + x] = new Color32(0, 0, 0, (byte)(a * a * (3 - 2 * a) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(blur + 2, blur + 2, blur + 2, blur + 2));
        }

        /// <summary>
        /// A soft shadow behind a floating panel. It's a sibling placed just before the panel and
        /// follows the panel's rect every frame, so callers can keep placing panels as usual.
        /// </summary>
        public static Image DropShadow(RectTransform target, float spread = 26f, float alpha = 0.55f, Vector2? offset = null)
        {
            var r = Rect(target.parent, target.name + "_shadow");
            r.SetSiblingIndex(target.GetSiblingIndex());
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = softShadow;
            img.type = Image.Type.Sliced;
            img.color = new Color(0, 0, 0, alpha);
            img.raycastTarget = false;
            r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // safe inside layout groups
            var f = r.gameObject.AddComponent<ShadowFollow>();
            f.Target = target;
            f.Spread = spread;
            f.Offset = offset ?? new Vector2(0, -10);
            return img;
        }

        public sealed class ShadowFollow : MonoBehaviour
        {
            public RectTransform Target;
            public float Spread;
            public Vector2 Offset;

            void LateUpdate()
            {
                if (Target == null) { Destroy(gameObject); return; }
                var r = (RectTransform)transform;
                r.anchorMin = Target.anchorMin;
                r.anchorMax = Target.anchorMax;
                r.pivot = Target.pivot;
                r.sizeDelta = Target.sizeDelta + new Vector2(Spread * 2, Spread * 2);
                // Grown by Spread on every side whatever the pivot.
                r.anchoredPosition = Target.anchoredPosition + Offset + (Target.pivot - new Vector2(0.5f, 0.5f)) * (Spread * 2);
                r.localScale = Target.localScale;
                r.localRotation = Target.localRotation;
                bool on = Target.gameObject.activeInHierarchy;
                if (gameObject.activeSelf != on) gameObject.SetActive(on);
            }
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

        /// <summary>Solid on the left, easing to clear on the right (for shading behind left-aligned text).</summary>
        public static Image FadeRight(Transform parent, string name, Color color, float solid = 0.55f)
        {
            const int w = 256;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w];
            for (int x = 0; x < w; x++)
            {
                float t = Mathf.InverseLerp(solid, 1, (x + 0.5f) / w);
                px[x] = new Color32(255, 255, 255, (byte)((1 - Mathf.SmoothStep(0, 1, t)) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var img = Rect(parent, name).gameObject.AddComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f));
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Solid at the bottom, fading to clear at the top, with soft left and right ends.</summary>
        public static Image FadeUp(Transform parent, string name, Color color)
        {
            const int w = 128, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 1 - Mathf.SmoothStep(0.45f, 1, (y + 0.5f) / h);   // solid for the lower half
                    float e = Mathf.SmoothStep(0, 1, Mathf.Min(x + 0.5f, w - x - 0.5f) / (w * 0.18f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(v * e * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var img = Rect(parent, name).gameObject.AddComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            img.color = color;
            img.raycastTarget = false;
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
            t.fontSize = size * Art.LetterScale(font);
            Lettering.Mark(t, font);
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>Button feel: eases toward a brighter colour and a slight lift on hover, dips on press.</summary>
        public sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            public Image Target;
            public Color Normal, Hot;
            public bool Interactable = true;
            bool over, down;
            float k;

            public void OnPointerEnter(PointerEventData e)
            {
                over = true;
                if (Interactable) Sfx.Play("ui_hover", 0.35f);
            }
            public void OnPointerExit(PointerEventData e) { over = false; down = false; }
            public void OnPointerDown(PointerEventData e) { down = Interactable; }
            public void OnPointerUp(PointerEventData e) { down = false; }
            void OnDisable() { over = down = false; k = 0; transform.localScale = Vector3.one; if (Target) Target.color = Normal; }

            void Update()
            {
                float goal = Interactable && over ? 1f : 0f;
                k = Mathf.MoveTowards(k, goal, Clock.Dt * 8f);
                float e = k * k * (3 - 2 * k);
                if (Target && Interactable) Target.color = Color.Lerp(Normal, Hot, e);
                float scale = 1f + 0.025f * e - (down ? 0.035f : 0f);
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * scale, 1 - Mathf.Exp(-Clock.Dt * 20f));
            }
        }

        /// <summary>
        /// A settings row that lights faintly under the pointer (mouse, pad or keys cursor, or a finger) with a
        /// soft tick, so whoever is steering can see which row they're on. Its backdrop also makes the whole row
        /// a target: a click on a toggle's label flips the toggle.
        /// </summary>
        public sealed class RowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Image Back;
            bool over;
            float k;

            public void OnPointerEnter(PointerEventData e) { over = true; Sfx.Play("ui_hover", 0.22f); }
            public void OnPointerExit(PointerEventData e) => over = false;
            void OnDisable() { over = false; k = 0; if (Back) Back.color = new Color(1, 1, 1, 0); }

            void Update()
            {
                k = Mathf.MoveTowards(k, over ? 1f : 0f, Clock.Dt * 8f);
                if (Back) Back.color = new Color(1, 1, 1, 0.065f * k * k * (3 - 2 * k));
            }
        }

        static void HoverRow(RectTransform row)
        {
            var back = Panel(row, "row_back", new Color(1, 1, 1, 0), true, true);
            back.rectTransform.Stretch();
            back.rectTransform.offsetMin = new Vector2(-12, -4);
            back.rectTransform.offsetMax = new Vector2(12, 4);
            back.transform.SetAsFirstSibling();
            row.gameObject.AddComponent<RowHover>().Back = back;
        }

        public static Button Button(Transform parent, string label, Action onClick, Color? bg = null, Color? fg = null,
            float fontSize = 30, string font = null, string name = null)
        {
            var img = Panel(parent, name ?? ("btn_" + label), bg ?? ButtonDark, true, true);
            img.sprite = btnFace;
            var drop = img.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0, 0, 0, 0.45f);
            drop.effectDistance = new Vector2(0, -3);
            var rim = Rect(img.transform, "rim");
            rim.Stretch();
            var rimImg = rim.gameObject.AddComponent<Image>();
            rimImg.sprite = btnRim;
            rimImg.type = Image.Type.Sliced;
            rimImg.color = new Color(1, 1, 1, 0.16f);
            rimImg.raycastTarget = false;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var h = img.gameObject.AddComponent<Hover>();
            h.Target = img;
            h.Normal = img.color;
            h.Hot = bg.HasValue ? Color.Lerp(bg.Value, Color.white, 0.12f) : ButtonHover;
            var t = Text(img.transform, label, font ?? Art.SansBold, fontSize, fg ?? Pal.Paper, TextAlignmentOptions.Center, "label");
            t.rectTransform.Stretch(6);
            t.characterSpacing = 1.5f;
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
            HoverRow(row);
            var sl = Rect(row, "slider");
            sl.Place(new Vector2(0.42f, 0.5f), new Vector2(0.84f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 26));
            var readout = Text(row, "", Art.SansBold, 24, Pal.Lamp, TextAlignmentOptions.Right, "readout");
            readout.rectTransform.Place(new Vector2(0.86f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
            readout.textWrappingMode = TextWrappingModes.NoWrap;
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
            readout.text = Mathf.RoundToInt(value * 100) + "%";
            s.onValueChanged.AddListener(v => { readout.text = Mathf.RoundToInt(v * 100) + "%"; onChange?.Invoke(v); });
            return s;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChange)
        {
            var row = Rect(parent, "toggle_" + label);
            HoverRow(row);
            var t = Text(row, label, Art.Sans, 28, Pal.Paper, TextAlignmentOptions.Left);
            t.rectTransform.Place(new Vector2(0, 0), new Vector2(0.75f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var box = Panel(row, "box", Pal.Hex("1E252C"), true, true);
            box.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(44, 44));
            var outline = Rect(box.transform, "outline");
            outline.Stretch();
            var ol = outline.gameObject.AddComponent<Image>();
            ol.sprite = btnRim;
            ol.type = Image.Type.Sliced;
            ol.color = new Color(1, 1, 1, 0.45f);
            ol.raycastTarget = false;
            var check = Panel(box.transform, "check", Pal.Lamp, true, true);
            check.rectTransform.Stretch(4);
            var tick = Rect(check.transform, "tick");
            tick.Stretch(5);
            var tickImg = tick.gameObject.AddComponent<Image>();
            tickImg.sprite = Art.Sprite("Icons/check");
            tickImg.color = Pal.Hex("1E1A14");
            tickImg.raycastTarget = false;
            var tg = row.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = box;
            tg.graphic = check;
            tg.isOn = value;
            // Off is an empty box, on a lit one with a tick: told apart by shape as well as colour.
            tick.gameObject.SetActive(value);
            tg.onValueChanged.AddListener(v => { tick.gameObject.SetActive(v); Sfx.Play("ui_click", 0.5f); onChange?.Invoke(v); });
            return tg;
        }

        /// <summary>
        /// A labelled slider that snaps to named notches (Low · Medium · High · Ultra): drag the handle, or
        /// click the track or a name. The chosen name is lit. Returns the slider (on the track inside the row).
        /// </summary>
        public static Slider Notches(Transform parent, string label, string[] names, int index, Action<int> onChange)
        {
            var row = Rect(parent, "notches_" + label);
            HoverRow(row);
            var t = Text(row, label, Art.Sans, 28, Pal.Paper, TextAlignmentOptions.Left);
            t.rectTransform.Place(new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var area = Rect(row, "area");
            area.Place(new Vector2(0.42f, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            // The track spans the notches' centres, inset by half a name's width at each end.
            int n = names.Length;
            float inset = 0.5f / n;
            var sl = Rect(area, "slider");
            // A thin track (the slider resets its fill to the track's full height, so the track is the bar).
            sl.Place(new Vector2(inset, 1), new Vector2(1 - inset, 1), new Vector2(0.5f, 1), new Vector2(0, -13), new Vector2(0, 12));
            var hitArea = Rect(sl, "hit");   // a taller, invisible target than the thin bar
            hitArea.Stretch();
            hitArea.offsetMin = new Vector2(-10, -14);
            hitArea.offsetMax = new Vector2(10, 14);
            hitArea.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var bg = Panel(sl, "bg", Pal.Hex("3A434C"), true, true);
            bg.rectTransform.Stretch();
            var fillArea = Rect(sl, "fillArea");
            fillArea.Stretch();
            var fill = Panel(fillArea, "fill", Pal.Lamp, true, true);
            fill.rectTransform.Stretch();
            var dots = new Image[n];
            for (int i = 0; i < n; i++)
            {
                var d = Panel(sl, "notch" + i, Pal.Hex("1E252C"), true, true);
                float x = n > 1 ? i / (float)(n - 1) : 0.5f;
                d.rectTransform.Place(new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
                d.raycastTarget = false;
                dots[i] = d;
            }
            var handleArea = Rect(sl, "handleArea");
            handleArea.Stretch();
            var handle = Panel(handleArea, "handle", Pal.Paper, true, true);
            handle.rectTransform.sizeDelta = new Vector2(26, 36);
            var labels = new TextMeshProUGUI[n];
            Slider s = null;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                var hit = Rect(area, "name_" + names[i]);
                hit.Place(new Vector2(i / (float)n, 0), new Vector2((i + 1) / (float)n, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 30));
                var img = hit.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0);   // a click target, not a picture
                var b = hit.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { if (s != null) s.value = k; });
                labels[i] = Text(hit, names[i], Art.SansBold, 19, Pal.Paper, TextAlignmentOptions.Center, "label");
                labels[i].rectTransform.Stretch();
                labels[i].characterSpacing = 1.5f;
            }
            s = sl.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.wholeNumbers = true;
            s.minValue = 0;
            s.maxValue = n - 1;
            void Paint(int v)
            {
                for (int i = 0; i < n; i++)
                {
                    labels[i].color = i == v ? Pal.Lamp : new Color(Pal.Paper.r, Pal.Paper.g, Pal.Paper.b, 0.55f);
                    dots[i].color = i <= v ? Pal.Hex("8A6A2E") : Pal.Hex("1E252C");
                }
            }
            s.SetValueWithoutNotify(Mathf.Clamp(index, 0, n - 1));
            Paint((int)s.value);
            s.onValueChanged.AddListener(v => { Paint((int)v); Sfx.Play("ui_click", 0.5f); onChange?.Invoke((int)v); });
            return s;
        }

        /// <summary>A labelled "‹ value ›" picker that cycles through choices.</summary>
        public static RectTransform Stepper(Transform parent, string label, string[] choices, int index, Action<int> onChange)
        {
            var row = Rect(parent, "stepper_" + label);
            HoverRow(row);
            var t = Text(row, label, Art.Sans, 28, Pal.Paper, TextAlignmentOptions.Left);
            t.rectTransform.Place(new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var box = Panel(row, "box", Pal.Hex("2A3138"), true, true);
            box.raycastTarget = false;
            box.rectTransform.Place(new Vector2(0.42f, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, -6));
            var value = Text(box.transform, "", Art.Sans, 26, Pal.Paper, TextAlignmentOptions.Center);
            value.rectTransform.Stretch();
            value.rectTransform.offsetMin = new Vector2(52, 0);
            value.rectTransform.offsetMax = new Vector2(-52, 0);
            value.textWrappingMode = TextWrappingModes.NoWrap;   // one line, a size smaller if it must
            value.enableAutoSizing = true;
            value.fontSizeMax = value.fontSize;
            value.fontSizeMin = value.fontSize * 0.7f;
            int i = Mathf.Clamp(index, 0, choices.Length - 1);
            void Show() => value.text = choices[i];
            void Step(int d)
            {
                if (choices.Length < 2) return;
                i = (i + d + choices.Length) % choices.Length;
                Show();
                onChange?.Invoke(i);
            }
            var prev = Button(box.transform, "‹", () => Step(-1), Pal.Hex("3A434C"), Pal.Paper, 30);
            ((RectTransform)prev.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(44, 38));
            var next = Button(box.transform, "›", () => Step(1), Pal.Hex("3A434C"), Pal.Paper, 30);
            ((RectTransform)next.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(44, 38));
            Show();
            return row;
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

        static readonly System.Collections.Generic.List<RaycastResult> uiHits = new System.Collections.Generic.List<RaycastResult>();
        static PointerEventData ped;

        /// <summary>Is the mouse over an interactive piece of UI? (Explicit raycast; works with simulated input too.)</summary>
        /// <summary>True if a click at this screen point would land on this object (or one of its children).</summary>
        public static bool TopHitIs(Vector2 screen, GameObject target)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screen };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(target.transform);
        }

        public static bool PointerOverUi()
        {
            var es = EventSystem.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (es == null || mouse == null) return false;
            if (ped == null || ped.currentInputModule == null) ped = new PointerEventData(es);
            ped.position = mouse.position.ReadValue();
            uiHits.Clear();
            es.RaycastAll(ped, uiHits);
            return uiHits.Count > 0;
        }
    }
}
