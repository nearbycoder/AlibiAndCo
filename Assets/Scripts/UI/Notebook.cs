using System.Linq;
using System.Text;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlibiCo
{
    /// <summary>
    /// The Tab notebook: where the case stands (tray, unknowns, contradictions, open alibis), what's
    /// known about each clock, and every memo, question and witness reply so far, newest first.
    /// It reads the board when it opens; it never says more than the board already shows.
    /// </summary>
    public sealed class Notebook
    {
        readonly CanvasGroup group;
        readonly TextMeshProUGUI caseTitle, status, log;
        readonly ScrollRect scroll;

        static readonly Color Ink = Pal.Ink;
        // Rich-text <font> tags don't resolve here (fonts live in Resources/Fonts), so headings are bold Sans.
        const string Head = "<b><color=#8E2B2B><size=19><cspace=3>";
        const string HeadEnd = "</cspace></size></color></b>";

        public bool Open => group.gameObject.activeSelf && group.alpha > 0.01f;

        public Notebook()
        {
            var r = UiKit.Rect(UiKit.Root, "Notebook");
            r.Stretch();
            group = r.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            r.gameObject.SetActive(false);

            var shade = UiKit.Panel(r, "shade", new Color(0.02f, 0.02f, 0.03f, 0.55f), false);
            shade.rectTransform.Stretch();
            shade.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

            var panel = UiKit.Panel(r, "panel", UiKit.PanelPaper).rectTransform;
            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1320, 820));
            UiKit.FitInCanvas(panel);
            var margin = UiKit.Panel(panel, "margin", new Color(0.72f, 0.22f, 0.2f, 0.55f), false);
            margin.raycastTarget = false;
            margin.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(2, -24));

            var title = UiKit.Text(panel, "Notebook", Art.Display, 54, Ink);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, -22), new Vector2(420, 70));
            caseTitle = UiKit.Text(panel, "", Art.SerifItalic, 28, Pal.InkSoft, TextAlignmentOptions.BottomRight);
            caseTitle.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-48, -30), new Vector2(700, 50));
            var rule = UiKit.Panel(panel, "rule", new Color(0.12f, 0.16f, 0.22f, 0.5f), false);
            rule.raycastTarget = false;
            rule.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(14, -100), new Vector2(-124, 2));

            status = UiKit.Text(panel, "", Art.Sans, 22, Ink, TextAlignmentOptions.TopLeft);
            status.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, -122), new Vector2(560, -190));
            status.enableAutoSizing = true;
            status.fontSizeMin = 15;
            status.fontSizeMax = 22;
            status.lineSpacing = 4;
            status.paragraphSpacing = 6;

            var divider = UiKit.Panel(panel, "divider", new Color(0.12f, 0.16f, 0.22f, 0.25f), false);
            divider.raycastTarget = false;
            divider.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(664, -122), new Vector2(2, -190));

            var logHead = UiKit.Text(panel, Head + "MEMOS, QUESTIONS & REPLIES · NEWEST FIRST" + HeadEnd, Art.Sans, 19, Ink, TextAlignmentOptions.TopLeft);
            logHead.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(700, -122), new Vector2(-748, 30));

            var view = UiKit.Rect(panel, "view");
            view.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(700, -160), new Vector2(-748, -228));
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            log = UiKit.Text(view, "", Art.Typewriter, 19, Ink, TextAlignmentOptions.TopLeft);
            log.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-16, 100));
            log.lineSpacing = 2;
            log.paragraphSpacing = 14;
            log.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = log.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;

            var foot = UiKit.Text(panel, "<b>Tab</b> or <b>Esc</b> to close  ·  scroll for older notes", Art.Sans, 18, Pal.InkFaint, TextAlignmentOptions.BottomRight);
            foot.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-48, 26), new Vector2(700, 30));
        }

        public void Show(CaseSession s)
        {
            if (s == null) return;
            caseTitle.text = s.Case.Title;
            status.text = Status(s);
            log.text = Log(s);
            group.transform.SetAsLastSibling();
            UiKit.Fade(group, true, 0.2f);
            scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
            Sfx.Play("folder", 0.45f);
        }

        public void Hide()
        {
            if (group.gameObject.activeSelf) UiKit.Fade(group, false, 0.15f);
        }

        // ------------------------------------------------------------------ text

        static string Name(CaseDef c, string id) => c.PersonById.TryGetValue(id, out var p) ? p.Name : id;

        static string Status(CaseSession s)
        {
            var b = s.Board;
            var c = s.Case;
            var sb = new StringBuilder();
            const string ok = "<color=#4F7A55>✓</color>  ", open = "<color=#8E2B2B>•</color>  ";

            var inc = c.Incident;
            sb.Append(Head).Append("THE INCIDENT").Append(HeadEnd).Append('\n');
            sb.Append(inc.Title).Append(" at ").Append(Locations.Name(inc.Location)).Append(", ")
              .Append(inc.Duration).Append(" minutes, some time between ")
              .Append(TimeFmt.Format(b.IncidentFrom)).Append(" and ").Append(TimeFmt.Format(b.IncidentTo)).Append(".\n\n");

            sb.Append(Head).Append("WHERE THINGS STAND").Append(HeadEnd).Append('\n');
            int tray = b.TrayCards.Count(x => !x.IsUnknown);
            sb.Append(tray == 0 ? ok + "Every card is on the board." : $"{open}{tray} card{(tray == 1 ? " is" : "s are")} still in the tray.").Append('\n');

            foreach (var card in b.UnlockedCards.Where(x => x.IsUnknown && !b.Struck.Contains(x.Id)))
            {
                if (b.Confirmed.TryGetValue(card.Id, out var who))
                    sb.Append(ok).Append($"“{card.Title}” was {Name(c, who)}.\n");
                else
                {
                    var left = b.CandidatesLeft.TryGetValue(card.Id, out var l) ? l : card.Candidates;
                    string names = string.Join(" or ", left.Select(id => Name(c, id)));
                    string hunch = b.Hypotheses.TryGetValue(card.Id, out var h) && b.Pinned.Contains(card.Id) ? $" Pinned on {Name(c, h)} as a hunch." : "";
                    sb.Append(open).Append($"“{card.Title}”: could be {names}.{hunch}\n");
                }
            }

            var conflicts = b.EstablishedConflicts.ToList();
            foreach (var k in conflicts)
            {
                string who = Name(c, k.Lane);
                string from = Locations.Name(k.A.Location), to = Locations.Name(k.B.Location);
                if (k.Overlap) sb.Append(open).Append($"{who} is in two places at once: {from} and {to}.\n");
                else sb.Append(open).Append($"{who} can't get from {from} to {to}: a {k.Need} min walk with {Mathf.Max(0, k.Have)} min to do it.\n");
            }
            if (conflicts.Count == 0 && tray == 0) sb.Append(ok).Append("No contradictions on the board.\n");

            var openLanes = c.Suspects.Where(p => b.Fits.TryGetValue(p.Id, out var f) && f.Fits).Select(p => p.Name).ToList();
            sb.Append('\n').Append(Head).Append("ALIBIS").Append(HeadEnd).Append('\n');
            foreach (var p in c.Suspects)
            {
                bool fits = b.Fits.TryGetValue(p.Id, out var f) && f.Fits;
                sb.Append(fits ? open : ok).Append(p.Name).Append(fits ? ": <color=#8E2B2B>no alibi for the incident</color>" : ": covered").Append('\n');
            }

            sb.Append('\n').Append(Head).Append("CLOCKS").Append(HeadEnd).Append('\n');
            foreach (var k in c.Clocks.Where(k => !k.Reference))
            {
                if (!b.IsTrusted(k.Id))
                    sb.Append(open).Append($"<b>{k.Name}</b>: unchecked. Its times could be off.\n");
                else if (k.TrueOffset == 0)
                    sb.Append(ok).Append($"<b>{k.Name}</b>: checked, keeps good time.\n");
                else
                    sb.Append(ok).Append($"<b>{k.Name}</b>: runs {Mathf.Abs(k.TrueOffset)} min {(k.TrueOffset > 0 ? "fast" : "slow")}")
                      .Append(k.KnownAtStart ? " (known from an earlier case)." : ". Corrected.").Append('\n');
            }
            if (!c.Clocks.Any(k => !k.Reference)) sb.Append(ok).Append("Every time on the board is reliable.\n");

            sb.Append('\n').Append(Head).Append("NEXT").Append(HeadEnd).Append('\n');
            bool unknowns = b.UnlockedCards.Any(x => x.IsUnknown && !b.Struck.Contains(x.Id) && !b.Confirmed.ContainsKey(x.Id));
            if (tray > 0) sb.Append("Get the rest of the paper onto the board.");
            else if (conflicts.Count > 0) sb.Append("Something in the red is false, or a clock is lying. Confront a statement, or link two cards that are one moment.");
            else if (unknowns) sb.Append("Let the paper rule people out until the unknown card has one name left.");
            else if (openLanes.Count == 1) sb.Append($"One alibi is open. Drag the incident card onto that line.");
            else if (openLanes.Count > 1) sb.Append("More than one alibi is open. A clock you haven't checked may be moving cards.");
            else sb.Append("Everyone's covered, so somebody's cover is false. Look hard at the cards holding the alibis up.");
            return sb.ToString();
        }

        static string Log(CaseSession s)
        {
            var h = s.Memos.History;
            if (h.Count == 0) return "<color=#7C8794>Nothing yet.</color>";
            var sb = new StringBuilder();
            for (int i = h.Count - 1; i >= 0; i--)
            {
                var m = h[i];
                string label = m.Kind switch
                {
                    MemoKind.Connie => "MEMO · C. ALIBI",
                    MemoKind.Question => "<color=#8E2B2B>A QUESTION</color>",
                    MemoKind.Witness => "ADMITS IT · " + (m.Title ?? "").ToUpperInvariant(),
                    MemoKind.Firm => "<color=#8E2B2B>STANDS FIRM</color> · " + (m.Title ?? "").ToUpperInvariant(),
                    _ => (m.Title ?? "NOTE").ToUpperInvariant(),
                };
                sb.Append("<b><size=15><cspace=2><color=#3C4A5C>").Append(label).Append("</color></cspace></size></b>\n");
                bool spoken = m.Kind == MemoKind.Witness || m.Kind == MemoKind.Firm;
                bool quoted = m.Text.StartsWith("“") || m.Text.StartsWith("\"");
                sb.Append(spoken && !quoted ? "“" + m.Text + "”" : m.Text);
                if (i > 0) sb.Append("\n<size=10> </size>\n");
            }
            return sb.ToString();
        }
    }
}
