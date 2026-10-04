using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlibiCo
{
    /// <summary>
    /// The physical set: a lamp-lit desk seen from above, with the cork case board on it, the
    /// card tray, the town map and the desk dressing. Builds camera, lights and post-processing.
    /// World units are decimetres; Y is up (toward the camera), +Z is "up the screen".
    /// </summary>
    public sealed class Stage : MonoBehaviour
    {
        public static Stage I { get; private set; }

        public Camera Cam { get; private set; }
        public Transform BoardRoot { get; private set; }   // local XY = board plane, local -Z = toward camera
        public Transform DeskRoot { get; private set; }    // same convention, at desk height
        public Light Lamp { get; private set; }
        public Volume Volume { get; private set; }

        /// <summary>Visible region at desk height, in world XZ.</summary>
        public Rect View { get; private set; }
        /// <summary>Cork board extents in BoardRoot local XY.</summary>
        public Rect Board { get; private set; }
        public Rect Tray { get; private set; }   // DeskRoot local
        public Rect Map { get; private set; }    // DeskRoot local
        public Rect Notes { get; private set; }  // DeskRoot local

        public const float BoardHeight = 0.16f;
        public const float DeskHeight = 0.0f;

        Vector3 camBasePos;
        Quaternion camBaseRot;
        Vector2 parallax;
        float shakeAmount, shakeTime;
        float lampBase;
        Transform lampShade;
        ParticleSystem dust;
        Bloom bloom;
        Vignette vignette;
        ColorAdjustments colorAdj;
        DepthOfField dof;
        public float Focus = 0; // 0 = board in focus, 1 = blurred backdrop (menus)
        public bool SuppressDof; // headless captures render UI through the camera, so skip the blur

        public static Stage Build()
        {
            var go = new GameObject("Stage");
            var s = go.AddComponent<Stage>();
            I = s;
            s.BuildCamera();
            s.ComputeLayout();
            s.BuildLights();
            s.BuildSet();
            s.BuildPost();
            return s;
        }

        void BuildCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = 28f;
            Cam.nearClipPlane = 1f;
            Cam.farClipPlane = 120f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Pal.Hex("0E1215");
            Cam.allowHDR = true;
            Cam.allowMSAA = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            camGo.AddComponent<AudioListener>();
            camBasePos = new Vector3(0, 27.5f, -1.9f);
            camBaseRot = Quaternion.Euler(86f, 0, 0);
            camGo.transform.SetPositionAndRotation(camBasePos, camBaseRot);
        }

        void ComputeLayout()
        {
            // Intersect the viewport corners with the desk plane to know what's on screen.
            var plane = new Plane(Vector3.up, new Vector3(0, BoardHeight, 0));
            Vector3 Hit(float x, float y)
            {
                var ray = Cam.ViewportPointToRay(new Vector3(x, y, 0));
                plane.Raycast(ray, out float d);
                return ray.GetPoint(d);
            }
            Cam.aspect = Mathf.Max(1.3f, (float)Screen.width / Mathf.Max(1, Screen.height));
            var bl = Hit(0, 0);
            var tr = Hit(1, 1);
            var tl = Hit(0, 1);
            var br = Hit(1, 0);
            float left = Mathf.Max(bl.x, tl.x), right = Mathf.Min(br.x, tr.x);
            View = Rect.MinMaxRect(left, bl.z, right, tr.z);

            // Board takes the top ~68% of the view, the desk strip below holds tray, notes and map.
            float margin = 0.35f;
            float boardTop = View.yMax - margin;
            float boardBottom = View.yMin + View.height * 0.318f;
            float boardHalfW = View.width * 0.5f - margin;
            var boardCenter = new Vector3(0, BoardHeight, (boardTop + boardBottom) * 0.5f);

            BoardRoot = new GameObject("BoardRoot").transform;
            BoardRoot.SetParent(transform, false);
            BoardRoot.SetPositionAndRotation(boardCenter, Quaternion.Euler(90, 0, 0));
            Board = new Rect(-boardHalfW, -(boardTop - boardBottom) * 0.5f, boardHalfW * 2, boardTop - boardBottom);

            DeskRoot = new GameObject("DeskRoot").transform;
            DeskRoot.SetParent(transform, false);
            DeskRoot.SetPositionAndRotation(new Vector3(0, DeskHeight + 0.02f, 0), Quaternion.Euler(90, 0, 0));
            float deskTop = boardBottom - 0.45f, deskBottom = View.yMin + 0.2f;
            float w = View.width;
            Notes = Rect.MinMaxRect(View.xMin + 0.3f, deskBottom, View.xMin + w * 0.17f, deskTop);
            Tray = Rect.MinMaxRect(View.xMin + w * 0.19f, deskBottom, View.xMin + w * 0.73f, deskTop);
            Map = Rect.MinMaxRect(View.xMin + w * 0.745f, deskBottom - 0.1f, View.xMax - 0.25f, deskTop + 0.15f);
        }

        void BuildLights()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Pal.Hex("5A6B7A") * 0.9f;
            RenderSettings.ambientEquatorColor = Pal.Hex("4A433B") * 0.9f;
            RenderSettings.ambientGroundColor = Pal.Hex("141210");
            RenderSettings.fog = false;

            var lampGo = new GameObject("Lamp Light");
            lampGo.transform.SetParent(transform, false);
            Lamp = lampGo.AddComponent<Light>();
            Lamp.type = LightType.Spot;
            Lamp.color = Pal.Hex("FFC77A");
            Lamp.range = 80;
            Lamp.spotAngle = 125;
            Lamp.innerSpotAngle = 70;
            Lamp.intensity = lampBase = 900f;
            Lamp.shadows = LightShadows.Soft;
            Lamp.shadowStrength = 0.82f;
            Lamp.shadowBias = 0.02f;
            Lamp.shadowNormalBias = 0.3f;
            lampGo.transform.position = new Vector3(View.xMin + 4.0f, 19f, View.yMax - 1.0f);
            lampGo.transform.LookAt(new Vector3(1.0f, 0, -0.5f));

            var moonGo = new GameObject("Window Light");
            moonGo.transform.SetParent(transform, false);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = Pal.Hex("7FA3C2");
            moon.intensity = 0.32f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.55f;
            moonGo.transform.rotation = Quaternion.Euler(58, -62, 0);
        }

        void BuildSet()
        {
            // Desk surface.
            var desk = Art.Spawn("desk", transform);
            if (desk == null)
            {
                desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                desk.name = "desk";
                desk.transform.SetParent(transform, false);
                desk.GetComponent<Renderer>().sharedMaterial = Art.Lit(Pal.Hex("5A3A22"), "wood", 0.35f, 0, new Vector2(2, 1), "wood_n");
                Destroy(desk.GetComponent<Collider>());
            }
            desk.transform.localPosition = new Vector3(0, -0.5f, 0);
            desk.transform.localScale = new Vector3(View.width + 12, 1, View.height + 12);
            if (desk.GetComponent<Renderer>() == null) desk.transform.localScale = Vector3.one * 10f;

            // Cork board with oak frame.
            var bw = Board.width; var bh = Board.height;
            var frame = Art.Spawn("board_frame", BoardRoot);
            if (frame != null)
            {
                frame.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                frame.transform.localScale = new Vector3(bw + 0.5f, 1, bh + 0.5f) * 0.1f;
                frame.transform.localPosition = new Vector3(0, 0, 0.0f);
            }
            else
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                f.name = "board_frame";
                f.transform.SetParent(BoardRoot, false);
                f.transform.localScale = new Vector3(bw + 0.5f, bh + 0.5f, BoardHeight * 2);
                f.transform.localPosition = new Vector3(0, 0, BoardHeight);
                f.GetComponent<Renderer>().sharedMaterial = Art.Lit(Pal.Hex("4A2E1A"), "wood", 0.42f);
                Destroy(f.GetComponent<Collider>());
            }
            var cork = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cork.name = "cork";
            cork.transform.SetParent(BoardRoot, false);
            cork.transform.localScale = new Vector3(bw, bh, 1);
            cork.transform.localPosition = new Vector3(0, 0, -0.005f);
            cork.GetComponent<Renderer>().sharedMaterial = Art.Lit(Pal.Hex("F2E2CC"), "cork", 0.04f, 0, new Vector2(bw / 6f, bh / 6f), "cork_n");
            cork.GetComponent<Renderer>().receiveShadows = true;
            Destroy(cork.GetComponent<Collider>());

            // Lamp model peeking in at the top-left corner, aligned with the light.
            var lamp = Art.Spawn("lamp", transform);
            if (lamp != null)
            {
                lamp.transform.position = new Vector3(View.xMin - 1.2f, 0, View.yMax + 0.6f);
                lamp.transform.rotation = Quaternion.Euler(0, 140, 0);
                lamp.transform.localScale = Vector3.one * 10f;
                lampShade = lamp.transform.Find("shade");
            }

            // Desk dressing around the edges.
            Prop("mug", new Vector3(View.xMax - 1.1f, 0, View.yMin + 1.0f), 25, 10f);
            Prop("phone", new Vector3(View.xMin - 0.6f, 0, View.yMin + 0.7f), -18, 10f);
            Prop("pencil", new Vector3(View.xMin + View.width * 0.17f, 0.05f, View.yMin + 0.45f), 78, 10f);
            Prop("magnifier", new Vector3(View.xMax - 0.6f, 0, View.yMax - View.height * 0.66f), -35, 10f);

            // Dust motes in the lamp beam.
            var dGo = new GameObject("Dust");
            dGo.transform.SetParent(transform, false);
            dGo.transform.position = new Vector3(View.xMin + View.width * 0.3f, 5, View.center.y + 1);
            dust = dGo.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.startLifetime = 9f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.maxParticles = 160;
            main.startColor = new Color(1f, 0.86f, 0.62f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = dust.emission; em.rateOverTime = 14;
            var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(View.width * 0.6f, 8, View.height * 0.7f);
            var noise = dust.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.2f;
            var col = dust.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var pr = dGo.GetComponent<ParticleSystemRenderer>();
            pr.material = Art.Unlit(new Color(1, 0.9f, 0.7f, 0.5f), true, "dot");
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        GameObject Prop(string model, Vector3 pos, float yaw, float scale)
        {
            var go = Art.Spawn(model, transform);
            if (go == null) return null;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        void BuildPost()
        {
            var go = new GameObject("Post");
            go.transform.SetParent(transform, false);
            Volume = go.AddComponent<Volume>();
            Volume.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            Volume.profile = p;

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(Pal.Hex("FFD9A0"));
            vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Pal.Hex("0A0806"));
            colorAdj = p.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.25f);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(-6f);
            colorAdj.colorFilter.Override(Pal.Hex("FFF4E6"));
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.7f);
            var lgg = p.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.98f, 1.0f, 1.04f, 0.0f));
            lgg.gain.Override(new Vector4(1.03f, 1.0f, 0.96f, 0.0f));
            dof = p.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(60f);
            dof.gaussianEnd.Override(80f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.active = false;
        }

        // ------------------------------------------------------------------ runtime life

        public void Shake(float amount = 0.08f, float time = 0.25f)
        {
            if (Settings.ReducedMotion) amount *= 0.3f;
            shakeAmount = Mathf.Max(shakeAmount, amount);
            shakeTime = Mathf.Max(shakeTime, time);
        }

        public void SetParallax(Vector2 viewport01)
        {
            if (Settings.ReducedMotion) { parallax = Vector2.zero; return; }
            parallax = (viewport01 - new Vector2(0.5f, 0.5f)) * 2f;
        }

        public void PushIn(float amount, float time = 0.8f)
        {
            float from = pushIn;
            Tween.Run((this, "push"), time, k => pushIn = Mathf.Lerp(from, amount, k), Ease.InOutCubic);
        }

        float pushIn;
        Vector3 pushTarget;
        public void SetPushTarget(Vector3 worldOnBoard) => pushTarget = worldOnBoard;

        void LateUpdate()
        {
            float t = Time.unscaledTime;
            // Lamp: gentle filament breathing plus the occasional flicker.
            float flicker = 1f + 0.015f * Mathf.Sin(t * 7.1f) + 0.01f * Mathf.Sin(t * 13.3f + 1.3f);
            if (Mathf.PerlinNoise(t * 0.7f, 3.3f) > 0.86f) flicker *= 0.92f + 0.08f * Mathf.PerlinNoise(t * 30f, 1f);
            if (Lamp) Lamp.intensity = lampBase * flicker * lampDim;

            var cam = Cam.transform;
            var smooth = Vector2.Lerp(parallaxNow, parallax, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 3f));
            parallaxNow = smooth;
            var pos = camBasePos + new Vector3(smooth.x * 0.35f, 0, smooth.y * 0.25f);
            if (pushIn > 0.001f)
            {
                var toward = (pushTarget + new Vector3(0, 8f, -0.8f)) - camBasePos;
                pos = Vector3.Lerp(pos, camBasePos + toward, pushIn);
            }
            if (shakeTime > 0)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float a = shakeAmount * Mathf.Clamp01(shakeTime / 0.25f);
                pos += new Vector3(Mathf.PerlinNoise(t * 40, 0) - 0.5f, 0, Mathf.PerlinNoise(0, t * 40) - 0.5f) * a * 2;
                if (shakeTime <= 0) shakeAmount = 0;
            }
            cam.SetPositionAndRotation(pos, camBaseRot);

            if (dof != null)
            {
                dof.active = Focus > 0.01f && !SuppressDof;
                dof.gaussianStart.value = Mathf.Lerp(60f, 18f, Focus);
                dof.gaussianEnd.value = Mathf.Lerp(80f, 26f, Focus);
            }
        }

        Vector2 parallaxNow;
        float lampDim = 1f;

        public void DimLamp(float to, float time)
        {
            float from = lampDim;
            Tween.Run((this, "dim"), time, k => lampDim = Mathf.Lerp(from, to, k), Ease.InOutSine);
        }

        public void SetLampInstant(float v) { Tween.Kill((this, "dim")); lampDim = v; }

        // ------------------------------------------------------------------ picking

        public bool MouseOnPlane(Vector2 screen, float height, out Vector3 world)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, height, 0));
            if (plane.Raycast(ray, out float d)) { world = ray.GetPoint(d); return true; }
            world = default;
            return false;
        }

        /// <summary>Mouse position in BoardRoot local XY (on the board surface).</summary>
        public Vector2 MouseBoard(Vector2 screen)
        {
            MouseOnPlane(screen, BoardHeight, out var w);
            var l = BoardRoot.InverseTransformPoint(w);
            return new Vector2(l.x, l.y);
        }

        public Vector2 MouseDesk(Vector2 screen)
        {
            MouseOnPlane(screen, DeskHeight, out var w);
            var l = DeskRoot.InverseTransformPoint(w);
            return new Vector2(l.x, l.y);
        }

        public Vector3 BoardToWorld(Vector2 p, float lift = 0) => BoardRoot.TransformPoint(new Vector3(p.x, p.y, -lift));
        public Vector3 DeskToWorld(Vector2 p, float lift = 0) => DeskRoot.TransformPoint(new Vector3(p.x, p.y, -lift));
        public Vector2 WorldToScreen(Vector3 w) => Cam.WorldToScreenPoint(w);
    }
}
