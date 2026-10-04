using System.Collections.Generic;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>Small procedural mesh and quad helpers (all in local XY, facing local -Z).</summary>
    public static class Shapes
    {
        static Mesh quad;

        public static Mesh QuadMesh
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "quad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                quad.RecalculateBounds();
                return quad;
            }
        }

        public static MeshRenderer Quad(Transform parent, string name, Vector2 size, Material mat, Vector3 pos, float rotZ = 0, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            return r;
        }

        static readonly Dictionary<string, Material> iconMats = new Dictionary<string, Material>();

        /// <summary>
        /// The project enters play mode without a domain reload, so statics outlive a play session
        /// while the objects they cache are destroyed with it. Start every session empty.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            iconMats.Clear();
            quad = null;
        }

        /// <summary>A tinted icon quad from Resources/Icons.</summary>
        public static MeshRenderer Icon(Transform parent, string icon, float size, Color color, Vector3 pos, float rotZ = 0)
        {
            string key = icon + "|" + ColorUtility.ToHtmlStringRGBA(color);
            if (!iconMats.TryGetValue(key, out var m) || m == null)
            {
                var tex = Resources.Load<Texture2D>("Icons/" + icon);
                m = Art.Unlit(color, true, null, tex);
                iconMats[key] = m;
            }
            return Quad(parent, "icon_" + icon, new Vector2(size, size), m, pos, rotZ);
        }

        /// <summary>A thin slab (paper card) with real thickness, its back face at pos.z, rising toward the camera (-Z).</summary>
        public static MeshRenderer Slab(Transform parent, string name, Vector2 size, float thickness, Material mat, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos + new Vector3(0, 0, -thickness * 0.5f);
            go.transform.localScale = new Vector3(size.x, size.y, thickness);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            return r;
        }

        /// <summary>A flat strip mesh along a polyline (for ribbons and strings), width in local units.</summary>
        public static Mesh Strip(IList<Vector3> pts, float width, float uvPerUnit = 1f)
        {
            var m = new Mesh { name = "strip" };
            int n = pts.Count;
            if (n < 2) return m;
            var v = new Vector3[n * 2];
            var uv = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];
            float dist = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = i < n - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                if (i > 0 && i < n - 1) dir = (pts[i + 1] - pts[i - 1]);
                var nrm = new Vector3(-dir.y, dir.x, 0).normalized * width * 0.5f;
                if (i > 0) dist += Vector3.Distance(pts[i], pts[i - 1]);
                v[i * 2] = pts[i] - nrm;
                v[i * 2 + 1] = pts[i] + nrm;
                uv[i * 2] = new Vector2(dist * uvPerUnit, 0);
                uv[i * 2 + 1] = new Vector2(dist * uvPerUnit, 1);
                if (i < n - 1)
                {
                    int t = i * 6, a = i * 2;
                    tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 2;
                    tris[t + 3] = a + 1; tris[t + 4] = a + 3; tris[t + 5] = a + 2;
                }
            }
            m.vertices = v;
            m.uv = uv;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static MeshRenderer StripObject(Transform parent, string name, IList<Vector3> pts, float width, Material mat, float uvPerUnit = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Strip(pts, width, uvPerUnit);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        /// <summary>A sagging string between two points (catenary-ish), for red string and leader lines.</summary>
        public static List<Vector3> Sag(Vector3 a, Vector3 b, float sag, int segments = 12)
        {
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                var p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4 * t * (1 - t);
                pts.Add(p);
            }
            return pts;
        }
    }
}
