using TMPro;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>World-space TextMeshPro helpers. Sizes are em heights in the parent's local units.</summary>
    public static class Txt
    {
        // TextMeshPro (3D) renders fontSize 10 at roughly one local unit per em.
        const float FontSizePerUnit = 10f;

        public static TextMeshPro Make(Transform parent, string name, string text, string font, float size, Color color,
            Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Vector3? pos = null, bool wrap = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos ?? Vector3.zero;
            var t = go.AddComponent<TextMeshPro>();
            t.font = Art.Font(font);
            t.fontSize = size * FontSizePerUnit;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = box;
            t.richText = true;
            t.text = text;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return t;
        }

        /// <summary>Shrink-to-fit between minSize and size.</summary>
        public static TextMeshPro Fit(this TextMeshPro t, float minSize)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = minSize * FontSizePerUnit;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static void SetSize(this TextMeshPro t, float size) => t.fontSize = size * FontSizePerUnit;
    }
}
