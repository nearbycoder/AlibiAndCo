using System.Collections.Generic;
using AlibiCo.Logic;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// The board behind the menus: evidence from all three cases pinned up at odd angles with red
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
            var picks = new List<(CardDef card, CaseDef c)>();
            foreach (var c in Cases.All)
                foreach (var card in c.Cards)
                    if (card.StartsAvailable && !card.Town) picks.Add((card, c));
            // Shuffle and keep a handful.
            for (int i = picks.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (picks[i], picks[j]) = (picks[j], picks[i]); }
            var anchors = new List<Vector3>();
            int n = Mathf.Min(14, picks.Count);
            for (int i = 0; i < n; i++)
            {
                var (card, c) = picks[i];
                var v = CardView.Create(card, c, transform);
                bool chip = rnd.NextDouble() < 0.55;
                v.SetCompact(chip, false);
                v.SetTimes(card.From, card.To, null);
                v.EnableCollider(false);
                float u = (i % 5 + (float)rnd.NextDouble() * 0.6f) / 5f;
                float w = (i / 5 + (float)rnd.NextDouble() * 0.5f) / 3f;
                var local = new Vector2(Mathf.Lerp(b.xMin + 2.2f, b.xMax - 2.2f, u), Mathf.Lerp(b.yMin + 1.6f, b.yMax - 1.4f, w));
                v.transform.position = stage.BoardToWorld(local, 0.05f + i * 0.08f);
                v.transform.rotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, (float)(rnd.NextDouble() - 0.5) * 18f);
                v.transform.localScale = Vector3.one * (chip ? 1.6f : 0.82f);
                if (rnd.NextDouble() < 0.3) v.Stamp(rnd.NextDouble() < 0.5 ? "FALSE" : "?", Pal.Stamp, false);
                var pinLocal = v.transform.position + v.transform.rotation * new Vector3(0, (chip ? CardView.ChipSize.y * 1.6f : CardView.FullSize.y * 0.82f) / 2 - 0.15f, -0.2f);
                anchors.Add(pinLocal);
                BoardView.Pin(transform, pinLocal + Vector3.up * 0.05f, Pal.Hex(rnd.NextDouble() < 0.5 ? "B8402F" : "C9A24A"), 0.12f);
            }
            // Suspect polaroids.
            string[] who = { "rook", "marlow", "bram", "nell", "ines", "cole" };
            for (int i = 0; i < who.Length; i++)
            {
                var c = Cases.All.Find(cc => cc.PersonById.ContainsKey(who[i]));
                var pr = Portraits.Make(transform, c, who[i], new Vector2(1.6f, 1.9f), false);
                var local = new Vector2(Mathf.Lerp(b.xMin + 1.4f, b.xMax - 1.4f, (i + 0.3f) / who.Length), (i % 2 == 0) ? b.yMax - 1.4f : b.yMin + 1.5f);
                pr.position = stage.BoardToWorld(local, 1.4f + i * 0.05f);
                pr.rotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, (float)(rnd.NextDouble() - 0.5) * 14f);
                var pin = pr.position + pr.rotation * new Vector3(0, 0.8f, -0.1f);
                anchors.Add(pin);
                BoardView.Pin(transform, pin + Vector3.up * 0.05f, Pal.Hex("B8402F"), 0.12f);
            }
            // Red string zig-zagging between pins.
            var mat = Art.Lit(Pal.Hex("A3272A"), null, 0.3f);
            for (int i = 0; i + 1 < anchors.Count; i += 1)
            {
                if (rnd.NextDouble() < 0.35) continue;
                var a = anchors[i];
                var c2 = anchors[(i * 7 + 3) % anchors.Count];
                var pts = new List<Vector3>();
                for (int k = 0; k <= 12; k++)
                {
                    float t = k / 12f;
                    var p = Vector3.Lerp(a, c2, t);
                    p.y = Mathf.Max(a.y, c2.y) + 0.15f + Mathf.Sin(t * Mathf.PI) * 0.1f;
                    pts.Add(p);
                }
                var go = new GameObject("string");
                go.transform.SetParent(transform, false);
                var local = new List<Vector3>();
                foreach (var p in pts) local.Add(new Vector3(p.x, p.z, -p.y));
                var rot = Quaternion.Euler(90, 0, 0);
                go.transform.rotation = rot;
                var m = Shapes.Strip(local, 0.05f);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
            }
        }
    }
}
