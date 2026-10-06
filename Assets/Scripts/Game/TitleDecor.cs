using System.Collections.Generic;
using System.Linq;
using AlibiCo.Logic;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// The board behind the menus: evidence from the first three cases pinned up at odd angles with red
    /// string between them, a few polaroids. Pure decoration; removed when a case opens.
    /// </summary>
    public sealed class TitleDecor : MonoBehaviour
    {
        public static TitleDecor Build(Stage stage)
        {
            var go = new GameObject("TitleDecor");
            go.transform.SetParent(stage.transform, false);
            var d = go.AddComponent<TitleDecor>();
            d.Make(stage);
            return d;
        }

        void Make(Stage stage)
        {
            var rnd = new System.Random(1986);
            var b = stage.Board;
            // The menu column covers the left ~40% of the screen, so the wall is composed on the right:
            // a loose ring of evidence round the suspects, joined by string. The left gets only a few
            // small, dim cards so the logo has calm ground.
            // Composed from the first three files (the cover art); later files bring back the same faces.
            var files = Cases.All.Take(3).ToList();
            var picks = new List<(CardDef card, CaseDef c)>();
            foreach (var c in files)
                foreach (var card in c.Cards)
                    if (card.StartsAvailable && !card.Town) picks.Add((card, c));
            for (int i = picks.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (picks[i], picks[j]) = (picks[j], picks[i]); }

            float x0 = Mathf.Lerp(b.xMin, b.xMax, 0.44f), x1 = b.xMax - 1.2f;
            var anchors = new List<Vector3>();
            var slots = new List<(Vector2 pos, bool chip, float scale)>();
            // Two rows of cards on the right half, staggered.
            for (int i = 0; i < 7; i++)
            {
                float u = (i + 0.3f + (float)rnd.NextDouble() * 0.4f) / 7f;
                float yRow = i % 2 == 0 ? b.yMax - 1.5f : b.yMin + 1.5f;
                slots.Add((new Vector2(Mathf.Lerp(x0, x1, u), yRow + (float)(rnd.NextDouble() - 0.5) * 0.8f), i % 3 == 1, i % 3 == 1 ? 1.5f : 0.86f));
            }
            // A few small chips down the far left, low and quiet.
            for (int i = 0; i < 3; i++)
                slots.Add((new Vector2(b.xMin + 1.6f + i * 2.2f, b.yMin + 1.2f + (i % 2) * 0.5f), true, 1.15f));
            for (int i = 0; i < slots.Count && i < picks.Count; i++)
            {
                var (card, c) = picks[i];
                var (pos, chip, scale) = slots[i];
                var v = CardView.Create(card, c, transform);
                v.SetCompact(chip, false);
                v.SetTimes(card.From, card.To, null);
                v.EnableCollider(false);
                v.transform.position = stage.BoardToWorld(pos, 0.05f + i * 0.08f);
                v.transform.rotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, (float)(rnd.NextDouble() - 0.5) * 14f);
                v.transform.localScale = Vector3.one * scale;
                if (rnd.NextDouble() < 0.25) v.Stamp(rnd.NextDouble() < 0.5 ? "FALSE" : "?", Pal.Stamp, false);
                var size = chip ? CardView.ChipSize : CardView.FullSize;
                var pin = v.transform.position + v.transform.rotation * new Vector3(0, size.y * scale / 2 - 0.15f, -0.2f);
                if (pos.x > x0 - 0.5f) anchors.Add(pin);
                BoardView.Pin(transform, pin + Vector3.up * 0.05f, Pal.Hex(rnd.NextDouble() < 0.5 ? "B8402F" : "C9A24A"), 0.12f);
            }
            // Every suspect's polaroid in a band across the middle of the right half.
            var who = new List<(string id, CaseDef c)>();
            foreach (var c in files)
                foreach (var p in c.Suspects) who.Add((p.Id, c));
            int n = who.Count;
            for (int i = 0; i < n; i++)
            {
                var (id, c) = who[i];
                var pr = Portraits.Make(transform, c, id, new Vector2(1.5f, 1.8f), false);
                float u = (i + 0.5f) / n;
                var local = new Vector2(Mathf.Lerp(x0 - 0.4f, x1 + 0.2f, u), (b.yMin + b.yMax) / 2 + 0.1f + (i % 2 == 0 ? 0.95f : -0.95f));
                pr.position = stage.BoardToWorld(local, 1.4f + i * 0.05f);
                pr.rotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, (float)(rnd.NextDouble() - 0.5) * 12f);
                var pin = pr.position + pr.rotation * new Vector3(0, 0.75f, -0.1f);
                anchors.Add(pin);
                BoardView.Pin(transform, pin + Vector3.up * 0.05f, Pal.Hex("B8402F"), 0.12f);
            }
            // Red string: each portrait to a couple of the cards, so the lines read as deliberate links.
            var mat = Art.Lit(Pal.Hex("A3272A"), null, 0.3f);
            int cards = anchors.Count - n;
            for (int i = 0; i < n; i++)
            {
                var a = anchors[cards + i];
                // One string per suspect to the nearest card above or below, plus a few suspect-to-suspect links.
                if (cards > 0)
                {
                    Vector3 best = anchors[0];
                    float bd = float.MaxValue;
                    for (int k = 0; k < cards; k++)
                    {
                        float d = Mathf.Abs(anchors[k].x - a.x) + (i % 2 == 0 ? (anchors[k].z < a.z ? 99 : 0) : (anchors[k].z > a.z ? 99 : 0));
                        if (d < bd) { bd = d; best = anchors[k]; }
                    }
                    String(a, best, mat);
                }
                if (i + 2 < n && i % 3 == 0) String(a, anchors[cards + i + 2], mat);
            }
        }

        void String(Vector3 a, Vector3 c2, Material mat)
        {
            var local = new List<Vector3>();
            for (int k = 0; k <= 12; k++)
            {
                float t = k / 12f;
                var p = Vector3.Lerp(a, c2, t);
                p.y = Mathf.Max(a.y, c2.y) + 0.15f + Mathf.Sin(t * Mathf.PI) * 0.1f;
                local.Add(new Vector3(p.x, p.z, -p.y));
            }
            var go = new GameObject("string");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = Shapes.Strip(local, 0.05f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
