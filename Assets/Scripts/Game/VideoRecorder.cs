using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace AlibiCo
{
    /// <summary>
    /// Offline gameplay recorder. Game time is locked to the capture rate, every frame is piped to
    /// ffmpeg as raw RGBA, and AudioDirector logs what each voice plays so Tools/mix_recording.py
    /// can rebuild the soundtrack in sync afterwards. Draws its own cursor (the OS one isn't captured).
    /// </summary>
    public sealed class VideoRecorder : MonoBehaviour
    {
        public int Fps = 30;
        public int Frames { get; private set; }
        public float Seconds => Frames / (float)Fps;

        Process ffmpeg;
        Stream pipe;
        StreamWriter audioLog;
        int width, height;
        bool running;
        RectTransform cursor;
        Image cursorImg;

        public void Begin(string dir)
        {
            Directory.CreateDirectory(dir);
            width = Screen.width & ~1;
            height = Screen.height & ~1;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureFramerate = Fps;
            AudioListener.volume = 0;

            var psi = new ProcessStartInfo("ffmpeg",
                $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {width}x{height} -r {Fps} -i - " +
                $"-vf vflip -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p \"{Path.Combine(dir, "video.mp4")}\"")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            ffmpeg = Process.Start(psi);
            pipe = ffmpeg.StandardInput.BaseStream;
            audioLog = new StreamWriter(Path.Combine(dir, "audio.log"));
            audioLog.WriteLine($"# fps {Fps} master {Settings.Master.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            BuildCursor();
            running = true;
            StartCoroutine(Capture());
            Debug.Log($"[Record] {width}x{height} @ {Fps} -> {dir}");
        }

        public void End()
        {
            if (!running) return;
            running = false;
            audioLog.Flush();
            audioLog.Close();
            pipe.Flush();
            pipe.Close();
            ffmpeg.WaitForExit();
            Time.captureFramerate = 0;
            Debug.Log($"[Record] done: {Frames} frames, {Seconds:0.0}s");
        }

        IEnumerator Capture()
        {
            var wait = new WaitForEndOfFrame();
            while (running)
            {
                yield return wait;
                if (!running) yield break;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex.width < width || tex.height < height)
                {
                    Debug.LogWarning($"[Record] screen is {tex.width}x{tex.height}, expected {width}x{height}; frame skipped");
                    Destroy(tex);
                    continue;
                }
                if (tex.width != width || tex.height != height)
                {
                    var crop = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    crop.SetPixels(tex.GetPixels(0, 0, width, height));
                    Destroy(tex);
                    tex = crop;
                }
                var bytes = tex.GetRawTextureData();
                pipe.Write(bytes, 0, bytes.Length);
                Destroy(tex);
                Frames++;
                AudioDirector.I?.LogFrame(audioLog, Frames);
            }
        }

        // ------------------------------------------------------------------ cursor

        void BuildCursor()
        {
            var go = new GameObject("RecordCursor");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            cursor = UiKit.Rect(go.transform, "arrow");
            cursor.anchorMin = cursor.anchorMax = Vector2.zero;
            cursor.pivot = new Vector2(0.06f, 0.96f);
            float scale = Screen.height / 1080f;
            cursor.sizeDelta = new Vector2(34, 48) * scale;
            cursorImg = cursor.gameObject.AddComponent<Image>();
            cursorImg.sprite = ArrowSprite();
            cursorImg.raycastTarget = false;
        }

        void LateUpdate()
        {
            if (cursor == null || Mouse.current == null) return;
            cursor.anchoredPosition = Mouse.current.position.ReadValue();
            bool down = Mouse.current.leftButton.isPressed;
            float target = down ? 0.86f : 1f;
            float s = Mathf.Lerp(cursor.localScale.x, target, 1 - Mathf.Exp(-Clock.Dt * 25f));
            cursor.localScale = Vector3.one * s;
        }

        /// <summary>A classic arrow: cream fill, dark outline, antialiased from a distance field.</summary>
        static Sprite ArrowSprite()
        {
            const int w = 68, h = 96;
            Vector2[] poly =
            {
                new Vector2(4, 4), new Vector2(4, 74), new Vector2(21, 58), new Vector2(33, 87),
                new Vector2(45, 82), new Vector2(33, 54), new Vector2(56, 54),
            };
            var px = new Color32[w * h];
            var fill = new Color(0.97f, 0.94f, 0.86f);
            var ink = new Color(0.08f, 0.07f, 0.06f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = float.MaxValue;
                    bool inside = false;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                    {
                        var a = poly[j];
                        var b = poly[i];
                        var ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                        d = Mathf.Min(d, (a + ab * t - p).magnitude);
                        if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    }
                    float sd = inside ? -d : d;
                    const float outline = 2.4f;
                    float alpha = Mathf.Clamp01(outline + 0.5f - sd);
                    float fillK = Mathf.Clamp01(-sd - outline + 0.5f);
                    var c = Color.Lerp(ink, fill, fillK);
                    c.a = alpha;
                    px[(h - 1 - y) * w + x] = c;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.06f, 0.96f));
        }
    }
}
