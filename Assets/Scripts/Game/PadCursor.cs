using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AlibiCo
{
    /// <summary>
    /// Gamepad and keyboard play. The game is built around a mouse, so a pad (or the keyboard)
    /// drives a virtual mouse device: everything that reads Mouse.current (dragging, hovering, the
    /// UI) just works. The left stick or the arrow keys move a software cursor, A or Enter is the
    /// left button, LB/RB or Q/E jump to the previous/next card (or button, in menus), B or
    /// Backspace sends a card back on the board and means "back" elsewhere, X asks for a hint, Y
    /// opens the notebook and Start pauses (the keyboard keeps H, Tab and Esc for those). Touching
    /// the real mouse hands control back.
    /// A touchscreen drives the same virtual mouse: the finger is the pointer and the left button,
    /// so a tap clicks and a drag drags. A press held still reads a card (the hover card) without
    /// clicking it, and nothing is hovered once the finger lifts. No cursor is drawn for touch.
    /// </summary>
    public sealed class PadCursor : MonoBehaviour
    {
        public enum Pointer { Mouse, Pad, Keys, Touch }

        public static PadCursor I { get; private set; }
        /// <summary>The pad or the keyboard is the active pointer (the cursor is drawn).</summary>
        public static bool Active => I != null && I.active;
        /// <summary>What's steering right now, for the prompts.</summary>
        public static Pointer Using => !Active ? Pointer.Mouse : I.touch ? Pointer.Touch : I.keys ? Pointer.Keys : Pointer.Pad;
        /// <summary>
        /// Touch is driving and no finger is down: nothing should count as hovered. It follows the
        /// virtual mouse's button, not the finger, because the board sees the button a frame later.
        /// </summary>
        public static bool FingerUp => Active && I.touch && !I.touchDown && I.tapPhase == 0 && I.virtualMouse != null
                                       && !I.virtualMouse.leftButton.isPressed && !I.virtualMouse.leftButton.wasReleasedThisFrame;
        /// <summary>A press held this long without moving reads the card instead of clicking it.</summary>
        public const float HoldToRead = 0.5f;
        /// <summary>Automation only: the shared desktop's real pointer mustn't take over mid-test.</summary>
        public static bool IgnoreRealMouse;

        Mouse virtualMouse, realMouse;
        Vector2 pos;
        bool active, keys, touch;
        bool touchDown, touchMoved, heldToRead;
        float touchHeld, touchQuiet;
        int touchFrames;
        Vector2 touchStart;
        int tapPhase;   // a tap that began and ended between two frames, replayed: 1 move there, 2 press, then release
        bool rightPulse;
        Vector2 arrowsHeld;
        float arrowsTime;
        RectTransform cursor;
        float hideTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; IgnoreRealMouse = false; }   // see Art.ResetStatics

        public static void Create(GameObject host)
        {
            if (I == null) I = host.AddComponent<PadCursor>();
        }

        public Vector2 Position => pos;

        void OnDestroy()
        {
            if (virtualMouse != null && virtualMouse.added) InputSystem.RemoveDevice(virtualMouse);
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (realMouse == null || !realMouse.added) realMouse = InputSystem.devices.OfType<Mouse>().FirstOrDefault(m => m != virtualMouse);

            // A finger on any touchscreen: the touch pointer takes over (see UpdateTouch).
            UnityEngine.InputSystem.Controls.TouchControl finger = null;
            foreach (var d in InputSystem.devices)
                if (d is Touchscreen ts && (ts.primaryTouch.press.isPressed || ts.primaryTouch.press.wasReleasedThisFrame)) { finger = ts.primaryTouch; break; }
            if (finger != null && !touch) { touch = true; keys = false; touchDown = false; SetActive(true); }
            if (touchQuiet > 0) touchQuiet -= Clock.Dt;

            // The real mouse moved or clicked: it's in charge again. (Not while a finger is on the
            // glass or has just lifted: a browser may follow a tap with mouse events of its own.)
            if (active && !IgnoreRealMouse && !(touch && (touchDown || touchQuiet > 0 || tapPhase != 0)) && realMouse != null && (realMouse.delta.ReadValue().sqrMagnitude > 4f || realMouse.leftButton.wasPressedThisFrame || realMouse.rightButton.wasPressedThisFrame))
                SetActive(false);

            // Any connected pad will do: the one being used this frame, else the current one.
            Gamepad pad = null;
            foreach (var g in Gamepad.all)
                if (g.leftStick.ReadValue().sqrMagnitude > 0.04f || g.allControls.OfType<UnityEngine.InputSystem.Controls.ButtonControl>().Any(b => b.isPressed || b.wasReleasedThisFrame)) { pad = g; break; }
            if (pad == null) pad = Gamepad.current;
            var kb = Keyboard.current;
            var arrows = Vector2.zero;
            if (kb != null)
                arrows = new Vector2((kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0), (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0));
            bool padTouched = pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.allControls.OfType<UnityEngine.InputSystem.Controls.ButtonControl>().Any(b => b.wasPressedThisFrame));
            bool keysTouched = kb != null && (arrows != Vector2.zero || kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame ||
                                              kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame);
            if (padTouched) { keys = false; touch = false; SetActive(true); }
            else if (keysTouched) { keys = true; touch = false; SetActive(true); }
            if (!active) { UpdateCursor(); return; }

            float dt = Clock.Dt;
            var root = GameRoot.I;
            bool left = false;
            if (touch) left = UpdateTouch(finger, dt);
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                // Fine control near the centre, quick across the screen at full tilt.
                float mag = Mathf.Clamp01((stick.magnitude - 0.12f) / 0.88f);
                if (mag > 0)
                {
                    float speed = Screen.height * Mathf.Lerp(0.18f, 1.25f, mag * mag);
                    pos += stick.normalized * speed * dt;
                }
                var dpad = pad.dpad.ReadValue();
                if (dpad.sqrMagnitude > 0.1f) pos += dpad.normalized * Screen.height * 0.25f * dt;

                if (pad.rightShoulder.wasPressedThisFrame) Jump(+1);
                if (pad.leftShoulder.wasPressedThisFrame) Jump(-1);
                if (root != null)
                {
                    if (pad.startButton.wasPressedThisFrame) root.BackOrPause(true);
                    if (pad.buttonEast.wasPressedThisFrame && !root.PadBack()) rightPulse = true;
                    if (pad.buttonNorth.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame) root.PadNotebook();
                    if (pad.buttonWest.wasPressedThisFrame) root.PadHint();
                }
                left |= pad.buttonSouth.isPressed;
            }
            if (kb != null)
            {
                // The arrows start slow for fine steps and speed up the longer they're held; turning resets that.
                if (arrows != arrowsHeld) { arrowsHeld = arrows; arrowsTime = 0; }
                if (arrows != Vector2.zero)
                {
                    arrowsTime += dt;
                    float speed = Screen.height * Mathf.Lerp(0.2f, 1.1f, Mathf.Clamp01((arrowsTime - 0.15f) / 0.9f));
                    pos += arrows.normalized * speed * dt;
                }
                if (kb.eKey.wasPressedThisFrame) Jump(+1);
                if (kb.qKey.wasPressedThisFrame) Jump(-1);
                if ((kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) && root != null && !root.PadBack()) rightPulse = true;
                left |= kb.enterKey.isPressed || kb.numpadEnterKey.isPressed;
            }
            pos = new Vector2(Mathf.Clamp(pos.x, 1, Screen.width - 2), Mathf.Clamp(pos.y, 1, Screen.height - 2));

            Send(left, rightPulse);
            rightPulse = false;
            UpdateCursor();
        }

        /// <summary>The finger's position and press, a frame late on the way down so the game sees it arrive first.</summary>
        bool UpdateTouch(UnityEngine.InputSystem.Controls.TouchControl finger, float dt)
        {
            bool down = finger != null && finger.press.isPressed;
            // Replaying a tap that came and went between two frames: press now, release next frame.
            if (tapPhase == 1) { tapPhase = 2; return true; }
            if (tapPhase == 2) { tapPhase = 0; return false; }
            if (down && !touchDown)
            {
                touchDown = true;
                touchMoved = false;
                heldToRead = false;
                touchHeld = 0;
                touchFrames = 0;
                pos = touchStart = finger.position.ReadValue();
                return false;
            }
            if (down)
            {
                pos = finger.position.ReadValue();
                touchHeld += dt;
                touchFrames++;
                if ((pos - touchStart).magnitude > TouchSlop) touchMoved = true;
                return true;
            }
            if (touchDown)
            {
                // Lifted. A long, still press was for reading: it doesn't click what's under it. It
                // has to have been seen down for a few frames too, so at a crawling frame rate a
                // quick tap that spans two slow frames is still a tap.
                touchDown = false;
                touchQuiet = 0.6f;
                if (finger != null) pos = finger.position.ReadValue();
                if (!touchMoved && touchHeld >= HoldToRead && touchFrames >= 4)
                {
                    heldToRead = true;
                    Debug.Log($"[Touch] held still {touchHeld:0.00}s ({touchFrames} frames): read, not clicked");
                }
                return false;
            }
            if (finger != null && finger.press.wasReleasedThisFrame)
            {
                // Down and up since the last frame: replay it as move, press, release.
                pos = finger.position.ReadValue();
                tapPhase = 1;
                heldToRead = false;
                touchQuiet = 0.6f;
            }
            return false;
        }

        /// <summary>
        /// The press just released was a finger held still to read a card, so it mustn't click.
        /// Asked once, by the board, when it sees the release.
        /// </summary>
        public static bool TakeHeldToRead()
        {
            if (I == null || !I.heldToRead) return false;
            I.heldToRead = false;
            return true;
        }

        /// <summary>How far a finger may wander before a press becomes a drag (screen pixels).</summary>
        public static float TouchSlop => Mathf.Max(12f, UnityEngine.Screen.height * 0.012f);

        void SetActive(bool on)
        {
            if (on == active) return;
            active = on;
            if (on)
            {
                if (virtualMouse == null || !virtualMouse.added) virtualMouse = InputSystem.AddDevice<Mouse>("PadCursor");
                // Start where the real pointer was, so nothing jumps.
                pos = realMouse != null ? realMouse.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
                tapPhase = 0;
                virtualMouse.MakeCurrent();
                Cursor.visible = false;
            }
            else
            {
                if (virtualMouse != null && virtualMouse.added) Send(false, false);
                touch = touchDown = heldToRead = false;
                tapPhase = 0;
                realMouse?.MakeCurrent();
                Cursor.visible = true;
            }
        }

        void Send(bool left, bool right)
        {
            if (virtualMouse == null || !virtualMouse.added) return;
            var st = new MouseState { position = pos };
            if (left) st = st.WithButton(MouseButton.Left, true);
            if (right) st = st.WithButton(MouseButton.Right, true);
            InputSystem.QueueStateEvent(virtualMouse, st);
            if (Mouse.current != virtualMouse) virtualMouse.MakeCurrent();
        }

        // ------------------------------------------------------------------ jumping between targets

        /// <summary>LB/RB (Q/E): the previous/next card on the desk and board, or button in a menu.</summary>
        void Jump(int dir)
        {
            var targets = Targets();
            if (targets.Count == 0) return;
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                float d = (targets[i] - pos).sqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }
            // Already on a target: step from it; otherwise go to the nearest one first.
            int next = best < 40f * 40f ? (nearest + dir + targets.Count) % targets.Count : nearest;
            pos = targets[next];
            Sfx.Play("paper_touch", 0.2f, 1.2f);
        }

        /// <summary>Screen points to jump between, in reading order.</summary>
        public static List<Vector2> Targets()
        {
            var root = GameRoot.I;
            var list = new List<Vector2>();
            bool board = root != null && root.Flow == Flow.Playing && !GameRoot.Paused && root.Session != null && !root.Screens.AnyOverlayOpen && !root.Screens.NotebookOpen;
            if (board)
            {
                // The actions panel, if a card is selected, comes first: Confront / Back to the tray.
                list.AddRange(root.Screens.ActionButtonsScreen());
                list.AddRange(root.Session.CursorTargets());
                return list;
            }
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.isActiveAndEnabled || !s.interactable) continue;
                var cg = s.GetComponentInParent<CanvasGroup>();
                if (cg != null && (cg.alpha < 0.5f || !cg.interactable || !cg.blocksRaycasts)) continue;
                var rt = (RectTransform)s.transform;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                var c = (Vector2)((corners[0] + corners[2]) / 2);
                if (c.x < 0 || c.y < 0 || c.x > Screen.width || c.y > Screen.height) continue;
                if (!UiKit.TopHitIs(c, s.gameObject)) continue;   // hidden behind an overlay
                list.Add(c);
            }
            // Reading order: top to bottom, then left to right.
            list.Sort((a, b) => Mathf.Abs(a.y - b.y) > 24 ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
            return list;
        }

        // ------------------------------------------------------------------ drawn cursor

        void UpdateCursor()
        {
            if (cursor == null) BuildCursor();
            bool show = active && !touch;
            if (cursor.gameObject.activeSelf != show) cursor.gameObject.SetActive(show);
            if (!show) return;
            cursor.SetAsLastSibling();
            float scale = UiKit.Canvas != null ? UiKit.Canvas.scaleFactor : 1f;
            cursor.anchoredPosition = pos / Mathf.Max(0.01f, scale);
            var m = Mouse.current;
            float s = m != null && m.leftButton.isPressed ? 0.82f : 1f;
            cursor.localScale = Vector3.Lerp(cursor.localScale, Vector3.one * s, 1 - Mathf.Exp(-Clock.Dt * 20f));
        }

        void BuildCursor()
        {
            var parent = UiKit.Root;
            var go = new GameObject("PadCursor", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            cursor = (RectTransform)go.transform;
            cursor.anchorMin = cursor.anchorMax = Vector2.zero;
            cursor.pivot = new Vector2(0.1f, 0.94f);   // the arrow's tip
            cursor.sizeDelta = new Vector2(44, 44);
            var img = go.AddComponent<Image>();
            img.sprite = MakeArrow(64);
            img.raycastTarget = false;
            go.SetActive(false);
        }

        /// <summary>A cream arrow with a dark outline, drawn once.</summary>
        static Sprite MakeArrow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            // Arrow polygon in unit space (y down from the tip at top-left).
            var poly = new[] { new Vector2(0.1f, 0.06f), new Vector2(0.1f, 0.86f), new Vector2(0.3f, 0.68f), new Vector2(0.45f, 0.96f),
                               new Vector2(0.58f, 0.9f), new Vector2(0.44f, 0.62f), new Vector2(0.72f, 0.62f) };
            float Dist(Vector2 p)
            {
                bool inside = false;
                float d = float.MaxValue;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                {
                    var a = poly[j]; var b = poly[i];
                    if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    d = Mathf.Min(d, (a + ab * t - p).magnitude);
                }
                return inside ? -d : d;
            }
            var cream = new Color(0.96f, 0.92f, 0.82f);
            var ink = new Color(0.08f, 0.07f, 0.07f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, 1 - (y + 0.5f) / size);
                    float d = Dist(p) * size;   // in pixels; negative inside
                    float fill = Mathf.Clamp01(-d + 0.5f - 2.2f);
                    float edge = Mathf.Clamp01(-d + 0.5f);
                    var c = Color.Lerp(ink, cream, fill);
                    c.a = edge;
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.1f, 0.94f), 100);
        }
    }
}
