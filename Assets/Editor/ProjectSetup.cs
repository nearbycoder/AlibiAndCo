using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace AlibiCo.EditorTools
{
    /// <summary>
    /// One-shot project wiring: base URP materials (so their shader variants ship), TextMeshPro
    /// font assets for every TTF in Assets/Fonts, URP quality/shadow settings, the Main scene and
    /// player settings. Menu: Alibi & Co/Setup Project, or -executeMethod AlibiCo.EditorTools.ProjectSetup.Apply
    /// </summary>
    public static class ProjectSetup
    {
        const string MatDir = "Assets/Resources/Materials";
        const string FontOutDir = "Assets/Resources/Fonts";
        const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "¡£¥§©«®°±²³´·¹»¼½¾¿ÀÁÂÄÅÆÇÈÉÊËÌÍÎÏÑÒÓÔÖ×ØÙÚÛÜßàáâäåæçèéêëìíîïñòóôöøùúûüÿ" +
            "–—‘’‚“”„†•…‰′″€™←↑→↓★☆✓✔✗✘●○■□▲▼◆◇";

        [MenuItem("Alibi & Co/Setup Project")]
        public static void Apply()
        {
            try
            {
                Materials();
                Fonts();
                Rendering();
                Scene();
                Player();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ProjectSetup] done");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ materials

        static void Materials()
        {
            Directory.CreateDirectory(MatDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Ensure("Lit", lit, m => { m.SetFloat("_Smoothness", 0.3f); });
            Ensure("LitNormal", lit, m =>
            {
                var n = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/cork_n.png");
                if (n != null) m.SetTexture("_BumpMap", n);
                m.EnableKeyword("_NORMALMAP");
            });
            Ensure("LitEmissive", lit, m =>
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            Ensure("LitTransparent", lit, Transparent);
            Ensure("Unlit", unlit, m => { });
            Ensure("UnlitTransparent", unlit, Transparent);
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_AlphaClip", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Material Ensure(string name, Shader shader, Action<Material> setup)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ fonts

        static void Fonts()
        {
            Directory.CreateDirectory(FontOutDir);
            var created = new Dictionary<string, TMP_FontAsset>();
            foreach (var ttf in Directory.GetFiles("Assets/Fonts", "*.ttf"))
            {
                var name = Path.GetFileNameWithoutExtension(ttf);
                var outPath = $"{FontOutDir}/{name} SDF.asset";
                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
                if (existing != null) { created[name] = existing; continue; }
                var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
                if (font == null) { Debug.LogError("[ProjectSetup] can't load font " + ttf); continue; }
                bool symbols = name.StartsWith("DejaVu");
                int size = name.StartsWith("Abril") || name.StartsWith("Playfair") ? 96 : 84;
                var fa = TMP_FontAsset.CreateFontAsset(font, size, 10, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                fa.name = name + " SDF";
                AssetDatabase.CreateAsset(fa, outPath);
                fa.atlasTextures[0].name = name + " SDF Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = name + " SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.TryAddCharacters(symbols ? "★☆✓✔✗✘●○■□▲▼◆◇←↑→↓•…" : Charset, out var missing);
                if (!string.IsNullOrEmpty(missing) && !symbols) Debug.Log($"[ProjectSetup] {name}: {missing.Length} chars not in font (fallback will cover)");
                EditorUtility.SetDirty(fa);
                created[name] = fa;
            }
            if (created.TryGetValue("DejaVuSans", out var dejavu))
            {
                foreach (var kv in created)
                {
                    if (kv.Key == "DejaVuSans") continue;
                    var fa = kv.Value;
                    if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    if (!fa.fallbackFontAssetTable.Contains(dejavu)) fa.fallbackFontAssetTable.Add(dejavu);
                    EditorUtility.SetDirty(fa);
                }
                var settings = Resources.Load<TMP_Settings>("TMP Settings");
                if (settings != null)
                {
                    var so = new SerializedObject(settings);
                    var fallbacks = so.FindProperty("m_fallbackFontAssets");
                    if (fallbacks != null)
                    {
                        fallbacks.arraySize = 1;
                        fallbacks.GetArrayElementAtIndex(0).objectReferenceValue = dejavu;
                    }
                    if (created.TryGetValue("IBMPlexSansCondensed-Medium", out var sans))
                    {
                        var def = so.FindProperty("m_defaultFontAsset");
                        if (def != null) def.objectReferenceValue = sans;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }
        }

        // ------------------------------------------------------------------ rendering

        static void Rendering()
        {
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pc == null) { Debug.LogError("[ProjectSetup] PC_RPAsset missing"); return; }
            var so = new SerializedObject(pc);
            void SetInt(string p, int v) { var x = so.FindProperty(p); if (x != null) x.intValue = v; else Debug.LogWarning("[ProjectSetup] no property " + p); }
            void SetFloat(string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; else Debug.LogWarning("[ProjectSetup] no property " + p); }
            void SetBool(string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; else Debug.LogWarning("[ProjectSetup] no property " + p); }
            SetBool("m_SupportsHDR", true);
            SetInt("m_MSAA", 4);
            SetFloat("m_RenderScale", 1f);
            SetBool("m_MainLightShadowsSupported", true);
            SetInt("m_MainLightShadowmapResolution", 2048);
            SetInt("m_AdditionalLightsRenderingMode", 1);
            SetBool("m_AdditionalLightShadowsSupported", true);
            SetInt("m_AdditionalLightsShadowmapResolution", 4096);
            SetInt("m_AdditionalLightsShadowResolutionTierLow", 1024);
            SetInt("m_AdditionalLightsShadowResolutionTierMedium", 2048);
            SetInt("m_AdditionalLightsShadowResolutionTierHigh", 4096);
            SetFloat("m_ShadowDistance", 70f);
            SetInt("m_ShadowCascadeCount", 1);
            SetBool("m_SoftShadowsSupported", true);
            SetInt("m_SoftShadowQuality", 3);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pc);

            GraphicsSettings.defaultRenderPipeline = pc;
            var names = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pc;
            }
            int high = Array.FindLastIndex(names, n => n.Contains("PC") || n.Contains("High") || n.Contains("Ultra"));
            QualitySettings.SetQualityLevel(high >= 0 ? high : current, false);

            // Gentle SSAO on the PC renderer.
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer != null)
            {
                foreach (var f in renderer.rendererFeatures.Where(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
                {
                    var fs = new SerializedObject(f);
                    void S(string p, float v)
                    {
                        var x = fs.FindProperty("m_Settings." + p);
                        if (x == null) return;
                        if (x.propertyType == SerializedPropertyType.Float) x.floatValue = v;
                        else if (x.propertyType == SerializedPropertyType.Boolean) x.boolValue = v > 0;
                        else x.intValue = (int)v;
                    }
                    S("Intensity", 0.8f);
                    S("Radius", 0.6f);
                    S("DirectLightingStrength", 0.25f);
                    S("Falloff", 80f);
                    fs.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(f);
                }
            }
        }

        // ------------------------------------------------------------------ scene & player

        static void Scene()
        {
            const string path = "Assets/Scenes/Main.unity";
            if (!File.Exists(path))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
        }

        static void Player()
        {
            PlayerSettings.productName = "Alibi & Co.";
            PlayerSettings.companyName = "AlibiAndCo";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.visibleInBackground = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/app_icon.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }

    /// <summary>Import rules for generated textures, icons, portraits, audio and Blender models.</summary>
    public sealed class AlibiImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/Resources/")) return;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            if (p.Contains("/Textures/"))
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                if (Path.GetFileNameWithoutExtension(p).EndsWith("_n")) ti.textureType = TextureImporterType.NormalMap;
                string n = Path.GetFileNameWithoutExtension(p);
                if (n == "shadow" || n == "dot" || n == "app_icon" || n.StartsWith("map")) ti.wrapMode = TextureWrapMode.Clamp;
                if (n.StartsWith("map")) { ti.maxTextureSize = 4096; ti.textureCompression = TextureImporterCompression.CompressedHQ; }
                ti.alphaIsTransparency = true;
            }
            if (p.Contains("/Icons/") || p.Contains("/Portraits/") || p.Contains("/Photos/"))
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }

        void OnPreprocessAudio()
        {
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            bool longClip = n.StartsWith("music") || n.StartsWith("amb");
            s.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = longClip ? 0.7f : 0.85f;
            ai.defaultSampleSettings = s;
            ai.forceToMono = false;
            ai.loadInBackground = longClip;
        }

        void OnPreprocessModel()
        {
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.importBlendShapes = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
        }
    }
}
