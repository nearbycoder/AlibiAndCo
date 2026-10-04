using System.Collections.Generic;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    public enum GlowKind { None, Hover, Conflict, LinkTarget, Confirmed, Selected, Incident }

    /// <summary>
    /// One physical evidence card. Two faces share the object: the full card (tray, hover, drag)
    /// and the compact chip it shrinks to when pinned on the board.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public static readonly Vector2 FullSize = new Vector2(4.1f, 2.65f);
        public static readonly Vector2 ChipSize = new Vector2(1.5f, 0.72f);
        const float Thick = 0.03f;

        public CardDef Def;
        public CaseDef Case;
        public string Id => Def.Id;
        public Transform Body { get; private set; }
        public bool Compact { get; private set; }
        public bool Struck { get; private set; }
        public bool Hypothesis { get; private set; }
        public bool IsIncident;

        Transform full, chip, faceRow;
        MeshRenderer glow, softShadow, chipPaper, fullPaper, newTag;
        TextMeshPro chipTime, chipLine, chipWho, fullTime, stamp, chipStampMark, fullClock;
        MeshRenderer chipClockIcon, chipHypIcon;
        BoxCollider col;
        readonly List<(string who, Transform root, MeshRenderer cross)> faces = new List<(string, Transform, MeshRenderer)>();
        readonly List<TextMeshPro> allText = new List<TextMeshPro>();
        Material glowMat, shadowMat;
        Color glowColor = Color.clear;
        float glowAlpha, glowTarget, pulse;
        float lift, liftTarget;
        GlowKind glowKind;
        GameObject newTagRoot;
        Transform conflictMark, conflictBadge;
        Material conflictMat;
        bool inConflict;

        public float LiftTarget { get => liftTarget; set => liftTarget = value; }
        public float CurrentLift => lift;

        // ------------------------------------------------------------------ construction

        public static CardView Create(CardDef def, CaseDef c, Transform parent)
        {
            var go = new GameObject("card_" + def.Id);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var v = go.AddComponent<CardView>();
            v.Def = def;
            v.Case = c;
            v.Build();
            return v;
        }

        public static CardView CreateIncident(CaseDef c, Transform parent)
        {
            var def = new CardDef
            {
                Id = "incident", Kind = "incident", Title = c.Incident.Title, Text = c.Incident.Text,
                Source = "record", SourceName = "Case file", Location = c.Incident.Location, Clock = c.Incident.Clock,
                From = c.Incident.From, To = c.Incident.To,
            };
            var go = new GameObject("card_incident");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var v = go.AddComponent<CardView>();
            v.Def = def;
            v.Case = c;
            v.IsIncident = true;
            v.Build();
            return v;
        }

        Color Ink => Def.IsTestimony ? Pal.Hex("1E365A") : Pal.Ink;

        static string Shape(string kind)
        {
            switch (kind)
            {
                case "receipt": return "receipt";
                case "ledger": return "ledger";
                case "call": return "slip";
                case "ticket": return "ticket";
                case "photo": return "photo";
                case "note": return "cutting";
                default: return "index";
            }
        }

        /// <summary>The paper itself: the Blender silhouette for this kind of card, or a plain slab.</summary>
        MeshRenderer Paper(Transform parent, Vector2 size, bool chipSize, Material mat)
        {
            var go = Art.Spawn($"card_{Shape(IsIncident ? "incident" : Def.Kind)}_{(chipSize ? "chip" : "full")}", parent);
            if (go == null) return Shapes.Slab(parent, "paper", size, Thick, mat, Vector3.zero);
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            go.transform.localPosition = Vector3.zero;
            var r = go.GetComponentInChildren<MeshRenderer>();
            r.sharedMaterial = mat;
            return r;
        }

        void Build()
        {
            Body = new GameObject("body").transform;
            Body.SetParent(transform, false);

            shadowMat = Art.Unlit(new Color(0, 0, 0, 0.35f), true, "shadow");
            softShadow = Shapes.Quad(transform, "softShadow", FullSize * 1.25f, shadowMat, new Vector3(0, 0, 0.005f));
            glowMat = Art.Unlit(Color.clear, true, "softrect");
            glow = Shapes.Quad(Body, "glow", FullSize * 1.3f, glowMat, new Vector3(0, 0, 0.004f));

            full = new GameObject("full").transform;
            full.SetParent(Body, false);
            chip = new GameObject("chip").transform;
            chip.SetParent(Body, false);
            BuildFull();
            BuildChip();

            var stampGo = new GameObject("stampRoot");
            stampGo.transform.SetParent(Body, false);
            stamp = AlibiCo.Txt.Make(stampGo.transform, "stamp", "", Art.Display, 0.62f, Pal.Stamp, new Vector2(5, 1.2f),
                TextAlignmentOptions.Center, new Vector3(0, 0, -Thick - 0.03f), false);
            stamp.transform.localRotation = Quaternion.Euler(0, 0, 11);
            var stampMat = new Material(stamp.fontSharedMaterial);
            var grunge = Art.Tex("grunge");
            if (grunge != null) stampMat.SetTexture("_FaceTex", grunge);
            stamp.fontSharedMaterial = stampMat;
            stamp.gameObject.SetActive(false);

            col = gameObject.AddComponent<BoxCollider>();
            ApplyCompact(false);
        }

        void BuildFull()
        {
            var size = FullSize;
            var paperColor = Pal.CardColor(Def.Kind);
            fullPaper = Paper(full, size, false, Art.Lit(paperColor, "paper", 0.12f));
            float z = -Thick - 0.004f;
            float left = -size.x / 2 + 0.24f, right = size.x / 2 - 0.24f, top = size.y / 2 - 0.16f;
            var kindCol = Pal.InkFaint;

            if (IsIncident)
            {
                // Red border band for the incident.
                Shapes.Quad(full, "band", new Vector2(size.x, 0.48f), Art.Unlit(Pal.Hex("8E2B2B")), new Vector3(0, size.y / 2 - 0.24f, z + 0.002f));
                Txt(full, "kind", "INCIDENT · " + Case.Incident.Short.ToUpperInvariant(), AlibiCo.Art.SansBold, 0.19f, Pal.PaperWhite,
                    new Vector2(size.x - 0.5f, 0.4f), TextAlignmentOptions.Left, new Vector3(0.05f, size.y / 2 - 0.25f, z));
                Shapes.Icon(full, "alert", 0.26f, Pal.PaperWhite, new Vector3(left - 0.02f, size.y / 2 - 0.24f, z));
            }
            else
            {
                Shapes.Icon(full, Def.Kind == "note" ? "note" : Def.Kind, 0.28f, kindCol, new Vector3(left + 0.1f, top - 0.08f, z));
                var label = Pal.KindLabel(Def.Kind) + (Def.IsTestimony ? "" : "  ·  RECORD");
                Txt(full, "kind", label, AlibiCo.Art.SansBold, 0.16f, kindCol, new Vector2(2.6f, 0.3f), TextAlignmentOptions.Left,
                    new Vector3(left + 0.32f + 1.3f, top - 0.09f, z));
            }

            bool photo = Def.Kind == "photo";
            float textLeft = left;
            float textWidth = size.x - 0.48f;
            if (photo)
            {
                // Polaroid print on the left.
                var tex = AlibiCo.Art.Photo(Def.Image);
                var printPos = new Vector3(left + 0.75f, -0.12f, z + 0.001f);
                Shapes.Quad(full, "printFrame", new Vector2(1.55f, 1.75f), Art.Unlit(Pal.Hex("F7F4EA")), printPos);
                var img = Shapes.Quad(full, "print", new Vector2(1.36f, 1.36f),
                    tex != null ? Art.Unlit(Color.white, false, null, tex) : Art.Unlit(Pal.Hex("2C3238")), printPos + new Vector3(0, 0.13f, -0.002f));
                if (tex == null)
                    Txt(full, "printLabel", "PRINT", AlibiCo.Art.Sans, 0.14f, Pal.Hex("8A949E"), new Vector2(1.2f, 0.3f), TextAlignmentOptions.Center, printPos + new Vector3(0, 0.13f, -0.004f));
                textLeft = left + 1.62f;
                textWidth = size.x - 0.48f - 1.62f;
            }

            int faceCount = Def.IsUnknown ? Def.Candidates.Count : (Def.Subjects.Count > 1 || (Def.IsTestimony && Def.Source != (Def.Subjects.Count > 0 ? Def.Subjects[0] : "")) ? Def.Subjects.Count : 0);
            if (Def.Town) faceCount = 0;
            float faceW = 0.5f;
            float facesWidth = faceCount * (faceW + 0.06f);

            // Title.
            var title = IsIncident ? "" : Def.Title;
            Txt(full, "title", title, AlibiCo.Art.Serif, 0.3f, Pal.Ink, new Vector2(textWidth - facesWidth, 0.42f), TextAlignmentOptions.Left,
                new Vector3(textLeft + (textWidth - facesWidth) / 2, top - 0.47f, z)).Fit(0.2f);
            if (!IsIncident && !string.IsNullOrEmpty(Def.SourceName))
                Txt(full, "source", Def.SourceName, AlibiCo.Art.Sans, 0.15f, Pal.InkSoft, new Vector2(textWidth - facesWidth, 0.26f), TextAlignmentOptions.Left,
                    new Vector3(textLeft + (textWidth - facesWidth) / 2, top - 0.75f, z)).Fit(0.11f);

            // Faces row (top-right): who this card is about, or who it could be.
            if (faceCount > 0)
            {
                faceRow = new GameObject("faces").transform;
                faceRow.SetParent(full, false);
                var ids = Def.IsUnknown ? Def.Candidates : Def.Subjects;
                for (int i = 0; i < ids.Count; i++)
                {
                    float x = right - faceW / 2 - (ids.Count - 1 - i) * (faceW + 0.06f);
                    var root = new GameObject("face_" + ids[i]).transform;
                    root.SetParent(faceRow, false);
                    root.localPosition = new Vector3(x, top - 0.42f, z);
                    Portraits.Make(root, Case, ids[i], new Vector2(faceW, 0.6f), true);
                    var cross = Shapes.Icon(root, "cross", 0.5f, new Color(Pal.Red.r, Pal.Red.g, Pal.Red.b, 0.92f), new Vector3(0, 0.03f, -0.01f));
                    cross.gameObject.SetActive(false);
                    faces.Add((ids[i], root, cross));
                }
            }

            // Body text.
            bool hand = Def.IsTestimony && !IsIncident;
            if (hand) Shapes.Quad(full, "ruling", new Vector2(size.x - 0.1f, size.y - 0.95f), Art.Unlit(new Color(1, 1, 1, 0.75f), true, "ruled"), new Vector3(0, -0.2f, z + 0.002f));
            float bodyTop = top - 0.98f, bodyBottom = -size.y / 2 + 0.62f;
            var body = Txt(full, "text", Def.Text, hand ? AlibiCo.Art.Hand : AlibiCo.Art.Mono, hand ? 0.29f : 0.19f, Ink,
                new Vector2(textWidth, bodyTop - bodyBottom), TextAlignmentOptions.TopLeft,
                new Vector3(textLeft + textWidth / 2, (bodyTop + bodyBottom) / 2, z));
            body.Fit(hand ? 0.2f : 0.13f);
            body.lineSpacing = hand ? -18 : 0;

            // Footer: time, place, clock.
            Shapes.Quad(full, "rule", new Vector2(size.x - 0.4f, 0.012f), Art.Unlit(new Color(0.12f, 0.16f, 0.22f, 0.35f), true), new Vector3(0, -size.y / 2 + 0.56f, z));
            fullTime = Txt(full, "time", "", AlibiCo.Art.MonoBold, 0.36f, Pal.Ink, new Vector2(2.4f, 0.46f), TextAlignmentOptions.Left,
                new Vector3(left + 1.2f, -size.y / 2 + 0.3f, z), false);

            var loc = Stage.I != null ? Locations.Get(Def.Location) : null;
            if (!Def.Town || IsIncident)
            {
                var locCol = loc != null ? Pal.Hex(loc.Color) : Pal.InkSoft;
                Shapes.Icon(full, loc != null ? loc.Icon : "pin", 0.26f, Darken(locCol, 0.75f), new Vector3(right - 1.45f, -size.y / 2 + 0.3f, z));
                Txt(full, "place", loc != null ? loc.Short : Def.Location, AlibiCo.Art.SansBold, 0.19f, Darken(locCol, 0.7f), new Vector2(1.3f, 0.3f), TextAlignmentOptions.Left,
                    new Vector3(right - 1.25f + 0.65f, -size.y / 2 + 0.3f, z)).Fit(0.13f);
            }
            var clock = Case.ClockById[Def.Clock];
            if (!clock.Reference)
            {
                fullClock = Txt(full, "clock", "", AlibiCo.Art.Sans, 0.13f, Pal.InkSoft, new Vector2(size.x - 0.48f, 0.25f), TextAlignmentOptions.Right,
                    new Vector3(0, -size.y / 2 + 0.68f, z), false);
                fullClock.Fit(0.09f);
            }

            var tag = new GameObject("newTag").transform;
            tag.SetParent(full, false);
            tag.localPosition = new Vector3(right - 0.2f, size.y / 2 + 0.02f, z - 0.002f);
            tag.localRotation = Quaternion.Euler(0, 0, -8);
            Shapes.Quad(tag, "bg", new Vector2(0.66f, 0.28f), Art.Unlit(Pal.Red), Vector3.zero);
            AlibiCo.Txt.Make(tag, "newText", "NEW", AlibiCo.Art.SansBold, 0.17f, Pal.PaperWhite, new Vector2(0.66f, 0.28f), TextAlignmentOptions.Center, new Vector3(0, 0, -0.01f), false);
            newTag = tag.GetComponentInChildren<MeshRenderer>();
            newTagRoot = tag.gameObject;
            newTagRoot.SetActive(false);
        }

        void BuildChip()
        {
            var size = ChipSize;
            var paperColor = Pal.CardColor(Def.Kind);
            chipPaper = Paper(chip, size, true, Art.Lit(paperColor, "paper", 0.12f));
            float z = -Thick - 0.004f;
            if (IsIncident)
            {
                Shapes.Quad(chip, "band", new Vector2(size.x, 0.2f), Art.Unlit(Pal.Hex("8E2B2B")), new Vector3(0, size.y / 2 - 0.1f, z + 0.002f));
                AlibiCo.Txt.Make(chip, "head", "THE INCIDENT", AlibiCo.Art.SansBold, 0.11f, Pal.PaperWhite, new Vector2(size.x - 0.1f, 0.18f), TextAlignmentOptions.Center,
                    new Vector3(0, size.y / 2 - 0.1f, z), false);
                chipTime = Txt(chip, "time", "", AlibiCo.Art.MonoBold, 0.17f, Pal.Oxblood, new Vector2(size.x - 0.12f, 0.24f), TextAlignmentOptions.Center,
                    new Vector3(0, 0.02f, z), false);
                chipTime.Fit(0.1f);
                chipLine = Txt(chip, "place", $"{Case.Incident.Duration} min at {Locations.Short(Case.Incident.Location)}", AlibiCo.Art.SansBold, 0.12f, Pal.Ink,
                    new Vector2(size.x - 0.12f, 0.2f), TextAlignmentOptions.Center, new Vector3(0, -size.y / 2 + 0.14f, z), false);
                chipLine.Fit(0.08f);
                return;
            }
            var loc = Locations.Get(Def.Location);
            var locCol = loc != null ? Pal.Hex(loc.Color) : Pal.InkSoft;
            if (!Def.Town)
                Shapes.Quad(chip, "stripe", new Vector2(0.09f, size.y), Art.Unlit(locCol), new Vector3(-size.x / 2 + 0.045f, 0, z + 0.001f));
            chipTime = Txt(chip, "time", "", AlibiCo.Art.MonoBold, 0.25f, Pal.Ink, new Vector2(size.x - 0.32f, 0.32f), TextAlignmentOptions.Left,
                new Vector3(0.02f, size.y / 2 - 0.2f, z), false);
            chipTime.Fit(0.15f);
            string place = Def.Town ? (Def.Kind == "statement" ? "heard at " + (loc != null ? loc.Short : "") : "") : (loc != null ? loc.Short : Def.Location);
            chipLine = Txt(chip, "place", place, AlibiCo.Art.SansBold, 0.165f, Darken(locCol, 0.65f), new Vector2(size.x - 0.24f, 0.2f), TextAlignmentOptions.Left,
                new Vector3(0.04f, -0.03f, z), false);
            chipLine.Fit(0.1f);
            chipWho = Txt(chip, "who", ShortSource(), AlibiCo.Art.Sans, 0.148f, Pal.InkSoft, new Vector2(size.x - 0.24f, 0.2f), TextAlignmentOptions.Left,
                new Vector3(0.04f, -size.y / 2 + 0.13f, z), false);
            chipWho.Fit(0.09f);
            Shapes.Icon(chip, Def.Kind, 0.2f, Pal.InkFaint, new Vector3(size.x / 2 - 0.15f, size.y / 2 - 0.14f, z));
            if (!Case.ClockById[Def.Clock].Reference)
            {
                chipClockIcon = Shapes.Icon(chip, "clock", 0.17f, Pal.Red, new Vector3(size.x / 2 - 0.15f, -size.y / 2 + 0.13f, z));
                chipStampMark = Txt(chip, "clockMark", "?", AlibiCo.Art.SansBold, 0.15f, Pal.Red, new Vector2(0.3f, 0.2f), TextAlignmentOptions.Right,
                    new Vector3(size.x / 2 - 0.36f, -size.y / 2 + 0.13f, z), false);
            }
            chipHypIcon = Shapes.Icon(chip, "question", 0.26f, Pal.Blue, new Vector3(size.x / 2 - 0.16f, -0.02f, z - 0.002f));
            chipHypIcon.gameObject.SetActive(false);

            // Contradiction marker: a red border and an alert badge that pulse while the card is in the red.
            conflictMark = new GameObject("conflict").transform;
            conflictMark.SetParent(chip, false);
            conflictMat = Art.Unlit(Pal.RedBright, true);
            float bw = 0.045f;
            Shapes.Quad(conflictMark, "top", new Vector2(size.x + bw, bw), conflictMat, new Vector3(0, size.y / 2, z - 0.003f));
            Shapes.Quad(conflictMark, "bottom", new Vector2(size.x + bw, bw), conflictMat, new Vector3(0, -size.y / 2, z - 0.003f));
            Shapes.Quad(conflictMark, "left", new Vector2(bw, size.y + bw), conflictMat, new Vector3(-size.x / 2, 0, z - 0.003f));
            Shapes.Quad(conflictMark, "right", new Vector2(bw, size.y + bw), conflictMat, new Vector3(size.x / 2, 0, z - 0.003f));
            var badge = new GameObject("badge").transform;
            badge.SetParent(conflictMark, false);
            badge.localPosition = new Vector3(size.x / 2 - 0.02f, size.y / 2 - 0.02f, z - 0.006f);
            Shapes.Quad(badge, "disc", new Vector2(0.3f, 0.3f), Art.Unlit(Pal.Red, true, "dot"), Vector3.zero);
            Shapes.Icon(badge, "alert", 0.17f, Pal.PaperWhite, new Vector3(0, 0.005f, -0.002f));
            conflictBadge = badge;
            conflictMark.gameObject.SetActive(false);
        }

        string ShortSource()
        {
            if (Def.IsTestimony)
            {
                var n = Def.Title ?? "";
                int sp = n.IndexOf(' ');
                string first = sp > 0 ? n.Substring(0, sp) : n;
                if (first == "Capt." || first == "Mrs" || first == "Mr") first = n;
                return "“" + first + "”";
            }
            return Def.Title;
        }

        TextMeshPro Txt(Transform parent, string name, string text, string font, float size, Color color, Vector2 box, TextAlignmentOptions align, Vector3 pos, bool wrap = true)
        {
            var t = AlibiCo.Txt.Make(parent, name, text, font, size, color, box, align, pos, wrap);
            allText.Add(t);
            return t;
        }

        static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1);

        // ------------------------------------------------------------------ state

        public void SetCompact(bool compact, bool animate = true)
        {
            if (Compact == compact && animate) return;
            if (!animate) { ApplyCompact(compact); return; }
            var from = Body.localScale;
            Tween.Run((this, "compact"), 0.12f, k => Body.localScale = Vector3.Lerp(from, new Vector3(0.6f, 0.6f, 1), k), Ease.InCubic, () =>
            {
                ApplyCompact(compact);
                Body.localScale = new Vector3(compact ? 1.5f : 0.6f, compact ? 1.5f : 0.6f, 1);
                Tween.Run((this, "compact"), 0.32f, k => Body.localScale = Vector3.LerpUnclamped(Body.localScale, Vector3.one, k), Ease.OutBack);
            });
        }

        void ApplyCompact(bool compact)
        {
            Compact = compact;
            full.gameObject.SetActive(!compact);
            chip.gameObject.SetActive(compact);
            var size = compact ? ChipSize : FullSize;
            col.size = new Vector3(size.x, size.y, 0.2f);
            col.center = new Vector3(0, 0, -0.05f);
            glow.transform.localScale = new Vector3(size.x * 1.38f + 0.3f, size.y * 1.5f + 0.3f, 1);
            softShadow.transform.localScale = new Vector3(size.x * 1.35f + 0.2f, size.y * 1.5f + 0.2f, 1);
            stamp.SetSize(compact ? 0.3f : 0.62f);
            stamp.rectTransform.sizeDelta = new Vector2(size.x * 1.2f, size.y);
        }

        public Vector2 Size => Compact ? ChipSize : FullSize;

        public void SetTimes(int from, int to, Board board)
        {
            int corr = board != null ? board.Correction(Def.Clock) : 0;
            bool instant = from == to;
            string t = instant ? TimeFmt.Format(from) : TimeFmt.Format(from) + "–" + TimeFmt.Format(to);
            if (chipTime != null)
            {
                chipTime.text = t;
                if (!IsIncident)
                {
                    chipTime.SetSize(instant ? 0.27f : 0.19f);
                    chipTime.fontSizeMax = chipTime.fontSize;
                }
            }
            if (fullTime != null) fullTime.text = t;
            var clock = Case.ClockById[Def.Clock];
            bool trusted = clock.Reference || (board != null && board.IsTrusted(Def.Clock));
            if (fullClock != null)
            {
                fullClock.text = trusted
                    ? $"<color=#4F7A55>✔</color> {clock.Name}{(corr != 0 ? $": {(corr > 0 ? "slow" : "fast")} {Mathf.Abs(corr)} min · printed <s>{TimeFmt.Format(Def.From)}</s>" : "")}"
                    : $"<color=#C23B2E>?</color> {clock.Name}: untested";
            }
            if (chipStampMark != null)
            {
                chipStampMark.text = trusted ? (corr != 0 ? (corr > 0 ? "+" : "−") + Mathf.Abs(corr) : "✓") : "?";
                chipStampMark.color = trusted ? Pal.Green : Pal.Red;
                chipClockIcon.sharedMaterial = IconTint(chipClockIcon, trusted ? Pal.Green : Pal.Red);
            }
        }

        static Material IconTint(MeshRenderer r, Color c)
        {
            var m = new Material(r.sharedMaterial);
            m.SetColor("_BaseColor", c);
            return m;
        }

        public void SetStruck(bool struck, string label, bool animate)
        {
            if (Struck == struck) return;
            Struck = struck;
            float dim = struck ? 0.55f : 1f;
            foreach (var t in allText)
            {
                var c = t.color;
                t.color = new Color(c.r, c.g, c.b, struck ? 0.45f : 1f);
            }
            var pc = Pal.CardColor(Def.Kind);
            var mat = Art.Lit(new Color(pc.r * dim + 0.2f, pc.g * dim + 0.2f, pc.b * dim + 0.2f), "paper", 0.1f);
            chipPaper.sharedMaterial = mat;
            fullPaper.sharedMaterial = mat;
            if (struck) Stamp(label, Pal.Stamp, animate);
            else stamp.gameObject.SetActive(false);
        }

        public void Stamp(string label, Color color, bool animate)
        {
            stamp.gameObject.SetActive(true);
            stamp.text = label;
            stamp.color = color;
            if (!animate) { stamp.transform.localScale = Vector3.one; return; }
            stamp.transform.localScale = Vector3.one * 2.4f;
            stamp.alpha = 0;
            Tween.Run((this, "stamp"), 0.22f, k =>
            {
                stamp.transform.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, k);
                stamp.alpha = k;
            }, Ease.InCubic, () => { Body.Punch(0.06f, 0.25f); });
        }

        public void ClearStamp() => stamp.gameObject.SetActive(false);

        public void SetHypothesis(bool h)
        {
            Hypothesis = h;
            if (chipHypIcon) chipHypIcon.gameObject.SetActive(h);
        }

        public void SetNew(bool isNew)
        {
            if (newTagRoot) newTagRoot.SetActive(isNew);
        }

        /// <summary>Cross out the faces of candidates who've been ruled out.</summary>
        public void SetCandidates(ICollection<string> stillPossible, bool animate)
        {
            foreach (var f in faces)
            {
                bool out_ = stillPossible != null && !stillPossible.Contains(f.who);
                bool was = f.cross.gameObject.activeSelf;
                if (out_ == was) continue;
                f.cross.gameObject.SetActive(out_);
                if (out_ && animate)
                {
                    var tr = f.cross.transform;
                    tr.localScale = Vector3.one * 0.01f;
                    Tween.Run((tr, "x"), 0.3f, k => tr.localScale = Vector3.one * 0.5f * k, Ease.OutBack);
                    Sfx.Play("pencil", 0.6f);
                }
            }
        }

        public void SetConflict(bool on)
        {
            if (conflictMark == null) return;
            if (on && !inConflict && conflictBadge != null) conflictBadge.Punch(0.6f, 0.45f);
            inConflict = on;
            conflictMark.gameObject.SetActive(on);
        }

        public void SetGlow(GlowKind kind)
        {
            glowKind = kind;
            switch (kind)
            {
                case GlowKind.Hover: glowColor = new Color(1f, 0.85f, 0.5f); glowTarget = 0.55f; break;
                case GlowKind.Conflict: glowColor = Pal.RedBright; glowTarget = 0.85f; break;
                case GlowKind.LinkTarget: glowColor = new Color(0.45f, 0.75f, 1f); glowTarget = 1f; break;
                case GlowKind.Confirmed: glowColor = new Color(0.5f, 0.9f, 0.6f); glowTarget = 0.8f; break;
                case GlowKind.Selected: glowColor = new Color(1f, 0.92f, 0.65f); glowTarget = 0.95f; break;
                case GlowKind.Incident: glowColor = Pal.RedBright; glowTarget = 0.5f; break;
                default: glowTarget = 0; break;
            }
        }

        public GlowKind CurrentGlow => glowKind;

        public void EnableCollider(bool on) => col.enabled = on;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            lift = Mathf.Lerp(lift, liftTarget, 1 - Mathf.Exp(-dt * 14f));
            Body.localPosition = new Vector3(Body.localPosition.x, Body.localPosition.y, -lift);
            // Soft shadow grows and fades with height, offset away from the lamp (top-left).
            var s = Mathf.Clamp01(lift / 3f);
            softShadow.transform.localPosition = new Vector3(0.25f * s + 0.03f, -0.35f * s - 0.03f, 0.005f);
            var baseScale = Compact ? ChipSize : FullSize;
            softShadow.transform.localScale = new Vector3(baseScale.x * (1.25f + 0.25f * s) + 0.2f, baseScale.y * (1.35f + 0.3f * s) + 0.2f, 1);
            shadowMat.SetColor("_BaseColor", new Color(0, 0, 0, Mathf.Lerp(0.38f, 0.22f, s) * (Compact ? 0.8f : 1f)));

            pulse += dt;
            float a = glowTarget;
            if (glowKind == GlowKind.Conflict) a *= 0.65f + 0.35f * Mathf.Sin(pulse * 5.5f);
            if (glowKind == GlowKind.LinkTarget) a *= 0.75f + 0.25f * Mathf.Sin(pulse * 9f);
            glowAlpha = Mathf.Lerp(glowAlpha, a, 1 - Mathf.Exp(-dt * 12f));
            glowMat.SetColor("_BaseColor", new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha));
            if (inConflict && conflictMat != null)
            {
                float k = 0.55f + 0.45f * Mathf.Sin(pulse * 6f);
                conflictMat.SetColor("_BaseColor", new Color(Pal.RedBright.r, Pal.RedBright.g, Pal.RedBright.b, 0.5f + 0.5f * k));
            }
        }
    }
}
