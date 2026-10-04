using System.Collections.Generic;
using System.Linq;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// The folded street map of Wrenhaven on the desk. Shows every street with its walking time,
    /// highlights the places in this case, and draws routes when you hover a card (or slot the
    /// incident). Hovering the map itself lifts it closer so the small print is readable.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        Stage stage;
        CaseDef c;
        Transform paper, routes, pawn;
        Vector2 size;
        Rect townBounds;
        readonly Dictionary<string, Vector2> pos = new Dictionary<string, Vector2>();
        readonly List<GameObject> minuteLabels = new List<GameObject>();
        BoxCollider col;
        float zoom, zoomTarget;
        Vector3 basePos;
        Quaternion baseRot;
        public bool Hovered;

        public static MapView Create(Stage stage, CaseDef c)
        {
            var go = new GameObject("Map");
            go.transform.SetParent(stage.DeskRoot, false);
            var m = go.AddComponent<MapView>();
            m.stage = stage;
            m.c = c;
            m.Build();
            return m;
        }

        // Must match ArtSource/town_map.py (paper 1.6 x 1.0, town -> paper transform).
        const float PaperAspect = 1.6f, MapS = 0.02793103448f, MapOX = 0.21344827586f, MapOY = 0.07f, TownMinX = 5.5f, TownMinY = 0.5f;

        Vector2 ToMap(float x, float y)
        {
            float px = MapOX + (x - TownMinX) * MapS;
            float py = MapOY + (y - TownMinY) * MapS;
            return new Vector2((px / PaperAspect - 0.5f) * size.x, (py - 0.5f) * size.y);
        }

        void Build()
        {
            var r = stage.Map;
            size = new Vector2(r.width - 0.15f, r.height - 0.1f);
            if (size.x / size.y > 1.6f) size.x = size.y * 1.6f; else size.y = size.x / 1.6f;
            basePos = new Vector3(r.center.x, r.center.y, -0.06f);
            baseRot = Quaternion.Euler(0, 0, -2.5f);
            transform.localPosition = basePos;
            transform.localRotation = baseRot;
            paper = new GameObject("paper").transform;
            paper.SetParent(transform, false);

            var town = Locations.Data;
            float minX = town.Locations.Min(l => l.X), maxX = town.Locations.Max(l => l.X);
            float minY = town.Locations.Min(l => l.Y), maxY = town.Locations.Max(l => l.Y);
            townBounds = Rect.MinMaxRect(minX - 2.5f, minY - 3.5f, maxX + 2.5f, maxY + 2.5f);
            foreach (var l in town.Locations) pos[l.Id] = ToMap(l.X, l.Y);

            var tex = Art.Tex("map_town");
            Shapes.Slab(paper, "sheet", size, 0.02f, Art.Lit(Pal.Hex("EFE5CC"), "paper", 0.08f), Vector3.zero);
            if (tex != null) Shapes.Quad(paper, "print", size, Art.Lit(Color.white, "map_town", 0.08f), new Vector3(0, 0, -0.021f));
            Shapes.Quad(paper, "shadow", size * 1.12f, Art.Unlit(new Color(0, 0, 0, 0.35f), true, "shadow"), new Vector3(0.1f, -0.12f, 0.01f));
            float z = -0.024f;
            if (tex == null)
            {
                // Procedural fallback: sea along the bottom.
                var sea = ToMap(0, 6.5f).y;
                Shapes.Quad(paper, "sea", new Vector2(size.x - 0.1f, sea + size.y / 2 - 0.05f), Art.Unlit(Pal.Hex("B7C7C9")), new Vector3(0, (-size.y / 2 + sea) / 2, z + 0.003f));
            }
            // Fold lines.
            Shapes.Quad(paper, "fold1", new Vector2(0.02f, size.y), Art.Unlit(new Color(0, 0, 0, 0.12f), true), new Vector3(-size.x / 6, 0, z + 0.002f));
            Shapes.Quad(paper, "fold2", new Vector2(0.02f, size.y), Art.Unlit(new Color(0, 0, 0, 0.12f), true), new Vector3(size.x / 6, 0, z + 0.002f));
            Shapes.Quad(paper, "fold3", new Vector2(size.x, 0.02f), Art.Unlit(new Color(0, 0, 0, 0.1f), true), new Vector3(0, 0, z + 0.002f));

            if (tex == null)
                Txt.Make(paper, "title", "WRENHAVEN · WALKING TIMES IN MINUTES", Art.SansBold, 0.13f, Pal.InkSoft, new Vector2(size.x - 0.4f, 0.22f),
                    TextAlignmentOptions.Left, new Vector3(0, size.y / 2 - 0.2f, z), false);

            var used = new HashSet<string>(c.Cards.Where(x => !x.Town).Select(x => x.Location)) { c.Incident.Location };
            var streetMat = Art.Unlit(new Color(0.18f, 0.2f, 0.25f, 0.75f), true);
            foreach (var s in town.Streets)
            {
                var a = pos[s.A];
                var b = pos[s.B];
                if (tex == null)
                    Shapes.StripObject(paper, "street", new[] { new Vector3(a.x, a.y, z + 0.001f), new Vector3(b.x, b.y, z + 0.001f) }, 0.05f, streetMat);
                var mid = (a + b) / 2;
                var lbl = Txt.Make(paper, "min", s.Minutes.ToString(), Art.SansBold, 0.12f, Pal.Ink, new Vector2(0.4f, 0.2f), TextAlignmentOptions.Center,
                    new Vector3(mid.x, mid.y, z - 0.004f), false);
                var bg = Shapes.Quad(lbl.transform, "bg", new Vector2(0.24f, 0.17f), Art.Unlit(new Color(0.96f, 0.93f, 0.85f, 0.95f), true), new Vector3(0, 0.0f, 0.003f));
                minuteLabels.Add(lbl.gameObject);
            }
            foreach (var l in town.Locations)
            {
                bool inCase = used.Contains(l.Id);
                var p = pos[l.Id];
                var col = Pal.Hex(l.Color);
                if (!inCase) col = Color.Lerp(col, new Color(0.7f, 0.68f, 0.62f), 0.65f);
                if (!inCase && tex != null) continue;
                Shapes.Quad(paper, "dot_" + l.Id, new Vector2(0.34f, 0.34f), Art.Unlit(col, true, "dot"), new Vector3(p.x, p.y, z - 0.006f));
                Shapes.Icon(paper, l.Icon, 0.18f, inCase ? Pal.PaperWhite : new Color(1, 1, 1, 0.6f), new Vector3(p.x, p.y, z - 0.008f));
                if (inCase)
                {
                    var t = Txt.Make(paper, "name_" + l.Id, l.Short, Art.SansBold, 0.15f, Pal.Ink, new Vector2(1.6f, 0.24f), TextAlignmentOptions.Center,
                        new Vector3(p.x, p.y + 0.27f, z - 0.01f), false);
                    t.outlineWidth = 0.25f;
                    t.outlineColor = new Color32(240, 230, 205, 255);
                }
                if (l.Id == c.Incident.Location)
                    Shapes.Icon(paper, "alert", 0.26f, Pal.Red, new Vector3(p.x + 0.25f, p.y - 0.2f, z - 0.012f));
            }
            routes = new GameObject("routes").transform;
            routes.SetParent(paper, false);

            col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 0.3f);
        }

        public bool ContainsDesk(Vector2 deskLocal)
        {
            var l = transform.InverseTransformPoint(stage.DeskRoot.TransformPoint(deskLocal));
            return Mathf.Abs(l.x) < size.x / 2 && Mathf.Abs(l.y) < size.y / 2;
        }

        public void Clear()
        {
            for (int i = routes.childCount - 1; i >= 0; i--) Art.DestroyWithMeshes(routes.GetChild(i));
        }

        /// <summary>Draw the shortest walk between two places with its total time.</summary>
        public void Route(string from, string to, Color color, string label = null, float width = 0.1f)
        {
            if (from == null || to == null || !pos.ContainsKey(from) || !pos.ContainsKey(to)) return;
            float z = -0.03f;
            if (from == to)
            {
                Shapes.Quad(routes, "here", new Vector2(0.62f, 0.62f), Art.Unlit(new Color(color.r, color.g, color.b, 0.5f), true, "dot"), new Vector3(pos[from].x, pos[from].y, z));
                return;
            }
            var path = Locations.Map.Route(from, to);
            var pts = path.Select(id => new Vector3(pos[id].x, pos[id].y, z)).ToList();
            Shapes.StripObject(routes, "route", pts, width, Art.Unlit(color, true));
            foreach (var p in pts) Shapes.Quad(routes, "joint", new Vector2(width * 1.1f, width * 1.1f), Art.Unlit(color, true, "dot"), p + new Vector3(0, 0, -0.001f));
            Shapes.Quad(routes, "end", new Vector2(0.5f, 0.5f), Art.Unlit(new Color(color.r, color.g, color.b, 0.55f), true, "dot"), pts[pts.Count - 1] + new Vector3(0, 0, 0.001f));
            if (label != null)
            {
                var mid = pts[pts.Count / 2];
                if (pts.Count % 2 == 0) mid = (pts[pts.Count / 2 - 1] + pts[pts.Count / 2]) / 2;
                var t = Txt.Make(routes, "routeLabel", label, Art.SansBold, 0.17f, Pal.PaperWhite, new Vector2(1.6f, 0.26f), TextAlignmentOptions.Center,
                    mid + new Vector3(0, 0.26f, -0.01f), false);
                Shapes.Quad(t.transform, "bg", new Vector2(Mathf.Max(0.6f, label.Length * 0.095f + 0.2f), 0.26f), Art.Unlit(color, true), new Vector3(0, 0, 0.004f));
            }
        }

        public void PawnAt(string loc, bool instant = false)
        {
            if (pawn == null)
            {
                pawn = new GameObject("pawn").transform;
                pawn.SetParent(paper, false);
                var model = Art.Spawn("pawn", pawn);
                if (model != null)
                {
                    model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    model.transform.localScale = Vector3.one * 11f;
                }
                else Shapes.Quad(pawn, "disc", new Vector2(0.4f, 0.4f), Art.Unlit(Pal.Red, true, "dot"), Vector3.zero);
            }
            if (!pos.TryGetValue(loc, out var p)) return;
            var target = new Vector3(p.x, p.y, -0.05f);
            if (instant) pawn.localPosition = target;
            else pawn.MoveLocal(target, 0.8f, Ease.InOutCubic);
        }

        float zoomScale = 2.05f, zoomLift = 4f;
        Vector2? zoomCorner;

        /// <summary>
        /// Lift the map toward the camera; scale and lift choose how big it ends up on screen. With a
        /// corner (viewport coords) the map's bottom-right corner is held there even while the camera moves.
        /// </summary>
        public void SetZoom(bool on, float scale = 2.05f, float lift = 4f, Vector2? corner = null)
        {
            zoomTarget = on ? 1 : 0;
            if (on) { zoomScale = scale; zoomLift = lift; zoomCorner = corner; }
        }

        void Update()
        {
            zoom = Mathf.Lerp(zoom, zoomTarget, 1 - Mathf.Exp(-Clock.Dt * 10f));
            // Lift toward the camera and slide up-left so the enlarged map stays on screen.
            float s = Mathf.Lerp(1f, zoomScale, zoom);
            var r = stage.Map;
            var view = stage.View;
            var grownCenter = new Vector3(view.xMax - size.x * s / 2 - 0.3f, Mathf.Min(r.center.y + (size.y * s - size.y) / 2 + 0.2f, view.yMax - size.y * s / 2 - 0.2f), -0.06f - zoom * zoomLift);
            if (zoomCorner.HasValue && transform.parent != null)
            {
                var cam = stage.Cam;
                var parent = transform.parent;
                var worldC = parent.TransformPoint(grownCenter);
                float depth = Vector3.Dot(worldC - cam.transform.position, cam.transform.forward);
                var corner = cam.ViewportToWorldPoint(new Vector3(zoomCorner.Value.x, zoomCorner.Value.y, depth));
                var half = parent.TransformVector(new Vector3(size.x * s / 2, size.y * s / 2, 0));
                var right = Vector3.Project(half, parent.right);
                var up = Vector3.Project(half, parent.up);
                grownCenter = parent.InverseTransformPoint(corner - right + up);
            }
            transform.localPosition = Vector3.Lerp(basePos, grownCenter, zoom);
            transform.localRotation = Quaternion.Slerp(baseRot, Quaternion.identity, zoom);
            transform.localScale = Vector3.one * s;
            bool showMinutes = zoom > 0.5f;
            if (minuteLabels.Count > 0 && minuteLabels[0].activeSelf != showMinutes)
                foreach (var m in minuteLabels) m.SetActive(showMinutes);
        }
    }
}
