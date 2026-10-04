using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    public enum MemoKind { Connie, Witness, Question, Firm, Notice }

    /// <summary>
    /// Connie's typed memos, witness replies and open questions, typed onto paper slips in the
    /// desk's notes corner. New slips slide in over the old ones; the old ones slide away.
    /// </summary>
    public sealed class MemoDesk : MonoBehaviour
    {
        public sealed class Memo
        {
            public MemoKind Kind;
            public string Title, Text;

            /// <summary>"kind|title|text" for the save file.</summary>
            public string Pack() => (int)Kind + "|" + (Title ?? "") + "|" + Text;

            public static Memo Unpack(string s)
            {
                var p = s.Split(new[] { '|' }, 3);
                if (p.Length < 3 || !int.TryParse(p[0], out int k)) return new Memo { Kind = MemoKind.Notice, Text = s };
                return new Memo { Kind = (MemoKind)k, Title = p[1].Length > 0 ? p[1] : null, Text = p[2] };
            }
        }

        readonly Queue<Memo> queue = new Queue<Memo>();
        Stage stage;
        Transform current;
        TextMeshPro body;
        int visible, total;
        float typeTimer, holdTimer;
        bool typing;
        public bool Busy => typing || queue.Count > 0;
        /// <summary>Every memo posted this case, oldest first; the notebook reads it.</summary>
        public readonly List<Memo> History = new List<Memo>();

        public static MemoDesk Create(Stage stage)
        {
            var go = new GameObject("Memos");
            go.transform.SetParent(stage.DeskRoot, false);
            var m = go.AddComponent<MemoDesk>();
            m.stage = stage;
            return m;
        }

        public void Post(MemoKind kind, string title, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var m = new Memo { Kind = kind, Title = title, Text = text };
            queue.Enqueue(m);
            History.Add(m);
            if (!typing && (current == null || holdTimer > 2.2f)) Next();
        }

        public void Clear()
        {
            queue.Clear();
            if (current) Destroy(current.gameObject);
            current = null;
            typing = false;
        }

        public void Skip()
        {
            if (typing) { visible = total; body.maxVisibleCharacters = total; typing = false; holdTimer = 3f; }
            else if (queue.Count > 0) Next();
        }

        public bool Contains(Vector2 deskLocal) => stage.Notes.Contains(deskLocal);

        void Next()
        {
            if (queue.Count == 0) return;
            var m = queue.Dequeue();
            if (current != null)
            {
                var old = current;
                var p = old.localPosition;
                old.MoveLocal(p + new Vector3(-6f, -0.6f, 0), 0.45f, Ease.InCubic, () => { if (old) Destroy(old.gameObject); });
            }
            current = BuildSlip(m);
            var target = current.localPosition;
            current.localPosition = target + new Vector3(-6.5f, 0.4f, -0.4f);
            current.MoveLocal(target, 0.5f, Ease.OutCubic);
            Sfx.Play("paper_slide", 0.55f);
            typing = true;
            visible = 0;
            total = body.textInfo != null ? body.text.Length : 0;
            body.maxVisibleCharacters = 0;
            typeTimer = -0.35f;
            AudioDirector.I?.Duck(0.7f, 2.5f);
        }

        Transform BuildSlip(Memo m)
        {
            var r = stage.Notes;
            var root = new GameObject("memo").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(r.center.x, r.center.y, -0.05f);
            root.localRotation = Quaternion.Euler(0, 0, Random.Range(-2.5f, 1.5f));
            var size = new Vector2(r.width - 0.1f, r.height - 0.15f);
            Color paper, ink;
            string head, font;
            float textSize;
            switch (m.Kind)
            {
                case MemoKind.Witness:
                case MemoKind.Firm:
                    paper = Pal.Hex("F3EAD3"); ink = Pal.Hex("1E365A"); font = Art.Hand; textSize = 0.3f;
                    head = (m.Kind == MemoKind.Firm ? "STANDS FIRM · " : "ADMITS IT · ") + (m.Title ?? "").ToUpperInvariant();
                    break;
                case MemoKind.Question:
                    paper = Pal.Hex("EFE9DC"); ink = Pal.Ink; font = Art.Typewriter; textSize = 0.205f;
                    head = "A QUESTION";
                    break;
                case MemoKind.Notice:
                    paper = Pal.Hex("E9E4D8"); ink = Pal.Ink; font = Art.Typewriter; textSize = 0.205f;
                    head = m.Title ?? "NOTE";
                    break;
                default:
                    paper = Pal.Hex("F4EBC8"); ink = Pal.Ink; font = Art.Typewriter; textSize = 0.21f;
                    head = "MEMO · C. ALIBI";
                    break;
            }
            Shapes.Slab(root, "paper", size, 0.02f, Art.Lit(paper, "paper", 0.1f), Vector3.zero);
            Shapes.Quad(root, "shadow", size * 1.18f, Art.Unlit(new Color(0, 0, 0, 0.3f), true, "shadow"), new Vector3(0.08f, -0.1f, 0.01f));
            var headCol = m.Kind == MemoKind.Firm || m.Kind == MemoKind.Question ? Pal.Oxblood : Pal.InkSoft;
            Txt.Make(root, "head", head, Art.SansBold, 0.15f, headCol, new Vector2(size.x - 0.4f, 0.3f), TextAlignmentOptions.Left,
                new Vector3(0, size.y / 2 - 0.3f, -0.03f), false).Fit(0.1f);
            Shapes.Quad(root, "rule", new Vector2(size.x - 0.4f, 0.015f), Art.Unlit(new Color(0.2f, 0.2f, 0.25f, 0.35f), true), new Vector3(0, size.y / 2 - 0.5f, -0.03f));
            body = Txt.Make(root, "body", m.Text, font, textSize, ink, new Vector2(size.x - 0.45f, size.y - 0.85f), TextAlignmentOptions.TopLeft,
                new Vector3(0, -0.2f, -0.03f));
            body.Fit(textSize * 0.62f);
            if (m.Kind == MemoKind.Witness || m.Kind == MemoKind.Firm) body.lineSpacing = -14;
            // Paper clip.
            Shapes.Icon(root, "clip", 0.55f, Pal.Hex("9AA3AB"), new Vector3(-size.x / 2 + 0.55f, size.y / 2 - 0.02f, -0.06f), 8);
            body.ForceMeshUpdate();
            return root;
        }

        void Update()
        {
            if (typing && body != null)
            {
                typeTimer += Time.unscaledDeltaTime;
                float cps = 70f;
                int target = Mathf.Min(total, Mathf.FloorToInt(typeTimer * cps));
                if (target > visible)
                {
                    if ((target / 3) != (visible / 3)) Sfx.Play("type", 0.22f, 1f, 0.12f);
                    visible = target;
                    body.maxVisibleCharacters = visible;
                }
                if (visible >= total)
                {
                    typing = false;
                    holdTimer = 0;
                    Sfx.Play("type_bell", 0.18f);
                }
            }
            else
            {
                holdTimer += Time.unscaledDeltaTime;
                // Let each memo be read before the next one replaces it.
                if (queue.Count > 0 && holdTimer > 2.2f) Next();
            }
        }
    }
}
