using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    public enum MemoKind { Connie, Witness, Question, Firm, Notice }

    /// <summary>
    /// Connie's typed memos, witness replies and open questions, typed onto paper slips in the
    /// desk's notes corner. New slips slide in over the old ones; the old ones slide away. While
    /// others are waiting, a slip stays long enough to be read (Logic.Reading) and wears a MORE tag
    /// saying how to see the next one now.
    /// </summary>
    public sealed class MemoDesk : MonoBehaviour
    {
        public sealed class Memo
        {
            public MemoKind Kind;
            public string Title, Text;
            /// <summary>Set for a question that the board may answer before it reaches the desk; it's passed over then (never saved).</summary>
            public System.Func<bool> Stale;

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
        Transform moreTag, moreTab;
        TextMeshPro moreText, headText;
        float headWidth, slipWidth;
        int moreCount = -1;
        PadCursor.Pointer morePointer;
        AlibiCo.Logic.PadFamily moreFamily;
        /// <summary>Questions passed over because the board had already answered them (the tests read it).</summary>
        public int PassedOver { get; private set; }
        /// <summary>Memos waiting behind the one on the desk.</summary>
        public int Waiting => queue.Count;
        /// <summary>The MORE tag's text, or "" when nothing is waiting (the tests read it).</summary>
        public string MoreShown => moreTag != null && moreTag.gameObject.activeSelf ? moreText.text : "";
        /// <summary>True once the memo on the desk has been given its time to read.</summary>
        bool ReadEnough => !typing && AlibiCo.Logic.Reading.Read(total, typeTimer, holdTimer);
        public bool Busy => typing || queue.Count > 0;
        /// <summary>The memo on the desk right now (the last one dealt from the queue).</summary>
        public Memo Showing { get; private set; }
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

        public void Post(MemoKind kind, string title, string text, System.Func<bool> stale = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var m = new Memo { Kind = kind, Title = title, Text = text, Stale = stale };
            queue.Enqueue(m);
            History.Add(m);
            if (current == null || ReadEnough) Next();
        }

        public void Clear()
        {
            queue.Clear();
            if (current) Destroy(current.gameObject);
            current = null;
            typing = false;
        }

        /// <summary>Space, or a click off the cards: finish typing the memo, or if it's typed, show the next one.</summary>
        public void Skip()
        {
            if (typing) { visible = total; body.maxVisibleCharacters = total; typing = false; holdTimer = 0; }
            else if (queue.Count > 0) Next();
        }

        /// <summary>Put a memo back on the desk as it was, already typed (the board was rebuilt).</summary>
        public void Reshow(Memo m)
        {
            if (m == null) return;
            if (current != null) Destroy(current.gameObject);
            current = BuildSlip(m);
            typing = false;
            typeTimer = holdTimer = AlibiCo.Logic.Reading.MaxOnDesk;   // already read
        }

        public bool Contains(Vector2 deskLocal) => stage.Notes.Contains(deskLocal);

        void Next()
        {
            // A question the board has answered in the meantime stays in the notebook, not on the desk.
            while (queue.Count > 0 && queue.Peek().Stale != null && queue.Peek().Stale()) { queue.Dequeue(); PassedOver++; }
            if (queue.Count == 0) return;
            var m = queue.Dequeue();
            Showing = m;
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
            Sfx.Play("paper_slide", 0.3f);   // every memo: keep it under the pins and stamps
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
            // Text size setting: the slip's type grows with it and shrinks back to fit a long memo.
            float baseSize = textSize;
            textSize *= Settings.TextScale;
            Shapes.Slab(root, "paper", size, 0.02f, Art.Lit(paper, "paper", 0.1f), Vector3.zero);
            // A memo pad sheet: two older sheets peeking out underneath and the agency's letterhead at the foot.
            for (int i = 1; i <= 2; i++)
                Shapes.Slab(root, "under", size, 0.012f, Art.Lit(Color.Lerp(paper, Pal.Hex("8C8270"), 0.12f * i), "paper", 0.1f),
                    new Vector3(0.05f * i, -0.06f * i, 0.016f * i)).transform.localRotation = Quaternion.Euler(0, 0, 1.6f * i);
            if (m.Kind == MemoKind.Connie || m.Kind == MemoKind.Question || m.Kind == MemoKind.Notice)
            {
                Txt.Make(root, "letterhead", "ALIBI & CO.  ·  PRIVATE ENQUIRIES  ·  4 QUAY STREET, WRENHAVEN", Art.SansBold, 0.085f,
                    new Color(0.45f, 0.18f, 0.16f, 0.7f), new Vector2(size.x - 0.4f, 0.16f), TextAlignmentOptions.Center,
                    new Vector3(0, -size.y / 2 + 0.22f, -0.03f), false).characterSpacing = 4;
                Shapes.Quad(root, "footRule", new Vector2(size.x - 0.4f, 0.012f), Art.UnlitShared(new Color(0.55f, 0.2f, 0.18f, 0.45f), true),
                    new Vector3(0, -size.y / 2 + 0.36f, -0.03f));
            }
            Shapes.Quad(root, "shadow", size * 1.18f, Art.Unlit(new Color(0, 0, 0, 0.3f), true, "shadow"), new Vector3(0.08f, -0.1f, 0.01f));
            var headCol = m.Kind == MemoKind.Firm || m.Kind == MemoKind.Question ? Pal.Oxblood : Pal.InkSoft;
            var headLine = Txt.Make(root, "head", head, Art.SansBold, 0.15f * Mathf.Min(Settings.TextScale, 1.2f), headCol, new Vector2(size.x - 0.4f, 0.3f), TextAlignmentOptions.Left,
                new Vector3(0, size.y / 2 - 0.3f, -0.03f), false).Fit(0.1f);
            Shapes.Quad(root, "rule", new Vector2(size.x - 0.4f, 0.015f), Art.Unlit(new Color(0.2f, 0.2f, 0.25f, 0.35f), true), new Vector3(0, size.y / 2 - 0.5f, -0.03f));
            body = Txt.Make(root, "body", m.Text, font, textSize, ink, new Vector2(size.x - 0.45f, size.y - 1.15f), TextAlignmentOptions.TopLeft,
                new Vector3(0, -0.05f, -0.03f));   // stops above the letterhead
            body.Fit(baseSize * 0.62f);
            if ((m.Kind == MemoKind.Witness || m.Kind == MemoKind.Firm) && !Settings.PlainText) body.lineSpacing = -14;
            // Paper clip.
            Shapes.Icon(root, "clip", 0.55f, Pal.Hex("9AA3AB"), new Vector3(-size.x / 2 + 0.55f, size.y / 2 - 0.02f, -0.06f), 8);
            body.ForceMeshUpdate();
            BuildMoreTag(root, size, headLine);
            return root;
        }

        /// <summary>A small oxblood label at the right of the slip's heading while memos are waiting: how many, and how to see the next.</summary>
        void BuildMoreTag(Transform slip, Vector2 size, TextMeshPro head)
        {
            float k = Mathf.Min(Settings.TextScale, 1.2f);
            headText = head;
            headWidth = head.rectTransform.sizeDelta.x;
            slipWidth = size.x;
            moreTag = new GameObject("more").transform;
            moreTag.SetParent(slip, false);
            moreTag.localPosition = new Vector3(0, head.transform.localPosition.y, -0.035f);
            moreTab = Shapes.Slab(moreTag, "tab", Vector2.one, 0.01f, Art.Lit(Pal.Oxblood, "paper", 0.1f), Vector3.zero).transform;
            moreText = Txt.Make(moreTag, "text", "", Art.SansBold, 0.12f * k, Pal.Hex("F3EAD3"), new Vector2(size.x * 0.6f, 0.3f * k),
                TextAlignmentOptions.Center, new Vector3(0, 0, -0.02f), false);
            moreText.characterSpacing = 2;
            moreCount = -1;
            RefreshMoreTag();
        }

        static string HowToSeeNext(PadCursor.Pointer p) => p switch
        {
            PadCursor.Pointer.Keys => "SPACE",
            PadCursor.Pointer.Pad => PadCursor.Label("[A] ON IT"),
            PadCursor.Pointer.Touch => "TAP IT",
            _ => "CLICK IT",
        };

        void RefreshMoreTag()
        {
            if (moreTag == null) return;
            var pointer = PadCursor.Using;
            var family = PadCursor.PadFamily;
            if (queue.Count == moreCount && pointer == morePointer && family == moreFamily) return;
            moreCount = queue.Count; morePointer = pointer; moreFamily = family;
            moreTag.gameObject.SetActive(moreCount > 0);
            float tagWidth = 0;
            if (moreCount > 0)
            {
                moreText.text = $"{moreCount} MORE  ·  {HowToSeeNext(pointer)}";
                var pref = moreText.GetPreferredValues(moreText.text);
                tagWidth = pref.x + 0.2f;
                float h = pref.y + 0.08f;
                moreTab.localScale = new Vector3(tagWidth, h, 0.01f);
                moreTag.localPosition = new Vector3(slipWidth / 2 - 0.2f - tagWidth / 2, moreTag.localPosition.y, moreTag.localPosition.z);
            }
            // The heading gives way to the label (and shrinks to fit if it must).
            if (headText != null)
            {
                float w = Mathf.Max(0.6f, headWidth - (tagWidth > 0 ? tagWidth + 0.15f : 0));
                headText.rectTransform.sizeDelta = new Vector2(w, headText.rectTransform.sizeDelta.y);
                var p = headText.transform.localPosition;
                headText.transform.localPosition = new Vector3((w - headWidth) / 2, p.y, p.z);   // keep its left edge
            }
        }

        void Update()
        {
            if (typing && body != null)
            {
                typeTimer += Clock.Dt;
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
                typeTimer += Clock.Dt;
                holdTimer += Clock.Dt;
                // Let each memo be read before the next one replaces it.
                if (queue.Count > 0 && ReadEnough) Next();
            }
            RefreshMoreTag();
        }
    }
}
