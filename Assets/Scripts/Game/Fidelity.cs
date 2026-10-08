using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlibiCo
{
    /// <summary>
    /// Graphics fidelity: one setting, four steps, applied live. High is the look the game always had
    /// (4× MSAA with SMAA, soft lamp shadows, ambient occlusion, bloom and film grain). Low and Medium
    /// shed the costly parts for weak GPUs without touching the board's text (the picture is never
    /// rendered below the window's size, so chip times stay sharp); Ultra supersamples the whole picture
    /// and raises the occlusion, bloom, shadow and particle detail.
    /// </summary>
    public static class Fidelity
    {
        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };
        public const int Default = 2;

        /// <summary>What each step does, in a line (the settings panel shows it under the slider).</summary>
        public static readonly string[] Blurbs =
        {
            "For older or integrated graphics: hard lamp shadows, no ambient occlusion or bloom, light anti-aliasing.",
            "Soft shadows, half-resolution ambient occlusion, bloom and 2× anti-aliasing. Light on laptops.",
            "The full look: soft lamp shadows, ambient occlusion, bloom, film grain and 4× anti-aliasing.",
            "Supersampled for the crispest print, finer shadows and occlusion, richer bloom and denser dust. Needs a strong GPU.",
        };

        /// <summary>The step in force (-1 until the first Apply).</summary>
        public static int Level { get; private set; } = -1;

        /// <summary>Particle bursts and the lamp's dust scale with the step.</summary>
        public static float ParticleScale => Level switch { 0 => 0.5f, 1 => 0.75f, 3 => 2f, _ => 1f };

        /// <summary>Ultra's supersampling, capped so the picture is never drawn above about 8.3 megapixels (4K).</summary>
        public static float UltraScale(int w, int h)
        {
            const float most = 3840f * 2160f;
            float s = Mathf.Sqrt(most / Mathf.Max(1f, (float)w * h));
            return Mathf.Clamp(Mathf.Floor(s * 20f) / 20f, 1f, 1.5f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Level = -1; asset = null; ssao = null; ssaoSettings = null; }   // see Art.ResetStatics

        static UniversalRenderPipelineAsset asset;
        static ScriptableRendererFeature ssao;
        static object ssaoSettings;
        static int scaledFor;

        /// <summary>
        /// The pipeline asset to change. In the Editor it's a copy, so a play session never rewrites the
        /// project's asset; a player's copy of the asset lives only in memory anyway.
        /// </summary>
        static UniversalRenderPipelineAsset Asset()
        {
            if (asset != null) return asset;
            var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (cur == null) return null;
            if (Application.isEditor)
            {
                cur = Object.Instantiate(cur);
                QualitySettings.renderPipeline = cur;
            }
            asset = cur;
            // The ambient occlusion feature on the renderer (its settings are internal to URP, so they're
            // reached by name; if a URP update renames them, the feature is only switched on and off).
            try
            {
                var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(asset) as ScriptableRendererData[];
                if (list != null)
                    foreach (var data in list)
                        if (data != null)
                            foreach (var f in data.rendererFeatures)
                                if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") ssao = f;
                if (ssao != null && !Application.isEditor)
                    ssaoSettings = ssao.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ssao);
            }
            catch (System.Exception e) { Debug.LogWarning("[Fidelity] ambient occlusion settings unreachable: " + e.Message); }
            return asset;
        }

        static void SetField(object target, string name, int value)
        {
            var f = target?.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return;
            if (f.FieldType.IsEnum) f.SetValue(target, System.Enum.ToObject(f.FieldType, value));
            else if (f.FieldType == typeof(bool)) f.SetValue(target, value != 0);
            else if (f.FieldType == typeof(int)) f.SetValue(target, value);
        }

        /// <summary>Apply a step to the pipeline, the camera, the lights, the post stack and the particles.</summary>
        public static void Apply(int level)
        {
            level = Mathf.Clamp(level, 0, Names.Length - 1);
            Level = level;
            scaledFor = 0;
            var urp = Asset();
            var stage = Stage.I;
            var notes = new List<string>();
            if (urp != null)
            {
                urp.msaaSampleCount = level switch { 0 => 1, 1 => 2, _ => 4 };
                urp.renderScale = level == 3 ? UltraScale(Screen.width, Screen.height) : 1f;
                scaledFor = Screen.width * 10000 + Screen.height;
                urp.upscalingFilter = UpscalingFilterSelection.Auto;
                // Ultra keeps the HDR picture in 64-bit colour: smoother lamp falloff and vignette, no banding.
                urp.hdrColorBufferPrecision = level == 3 ? HDRColorBufferPrecision._64Bits : HDRColorBufferPrecision._32Bits;
                urp.mainLightShadowmapResolution = level switch { 0 => 1024, 1 => 1024, 2 => 2048, _ => 4096 };
                urp.additionalLightsShadowmapResolution = level switch { 0 => 2048, 1 => 2048, _ => 4096 };
                notes.Add($"MSAA {urp.msaaSampleCount}x");
                notes.Add($"scale {urp.renderScale:0.00}");
                notes.Add(level == 3 ? "64-bit HDR" : "32-bit HDR");
            }
            if (ssao != null && !Application.isEditor)   // the Editor's renderer is the project's asset: leave it be
            {
                ssao.SetActive(level > 0);
                if (ssaoSettings != null && level > 0)
                {
                    // Samples: 0 High (12), 1 Medium (8), 2 Low (4). Blur: 0 bilateral, 1 Gaussian, 2 Kawase.
                    SetField(ssaoSettings, "Downsample", level == 1 ? 1 : 0);
                    SetField(ssaoSettings, "Samples", level switch { 1 => 2, 2 => 1, _ => 0 });
                    SetField(ssaoSettings, "BlurQuality", level == 1 ? 1 : 0);
                    SetField(ssaoSettings, "NormalSamples", level == 3 ? 2 : 1);
                }
                notes.Add((level == 0 ? "AO off" : level == 1 ? "AO half-res" : level == 2 ? "AO" : "AO high") + (ssaoSettings == null ? " (its settings unreachable: on/off only)" : ""));
            }
            // The project forces anisotropic filtering on (High); Ultra raises its floor, Medium leaves it to each texture.
            QualitySettings.anisotropicFiltering = level switch { 0 => AnisotropicFiltering.Disable, 1 => AnisotropicFiltering.Enable, _ => AnisotropicFiltering.ForceEnable };
            // Forced on, every texture gets at least 9× (Unity's own floor); Ultra gives every texture 16×.
            Texture.SetGlobalAnisotropicFilteringLimits(level == 3 ? 16 : 9, 16);
            stage?.ApplyFidelity(level);
            Debug.Log($"[Fidelity] {Names[level]}: {string.Join(", ", notes)}{(stage != null ? ", " + stage.FidelityNote : "")}");
        }

        /// <summary>Ultra's supersampling follows the window's size (a bigger window gets less of it).</summary>
        public static void Tick()
        {
            if (Level != 3 || asset == null) return;
            int key = Screen.width * 10000 + Screen.height;
            if (key == scaledFor) return;
            scaledFor = key;
            asset.renderScale = UltraScale(Screen.width, Screen.height);
        }
    }
}
