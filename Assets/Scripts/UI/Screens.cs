using System.Collections.Generic;
using System.Linq;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlibiCo
{
    /// <summary>All screen-space UI: title, case files, case intro, HUD, card actions, pause, settings, case closed.</summary>
    public sealed class Screens
    {
        readonly GameRoot root;
        CanvasGroup title, select, intro, hud, actions, pause, settings, closed, confirm, week;
        CaseSession hudSession;
        RectTransform actionsPanel;
        TextMeshProUGUI actionsTitle, actionsInfo;
        Button actionsConfront, actionsUnpin;
        CardView actionsCard;
        Notebook notebook;
        CanvasGroup helpGroup;
        RectTransform helpRect;
        TextMeshProUGUI helpText, helpKeys;
        string helpShown;

        static readonly Color Cream = Pal.Hex("F1E6CF");
        static readonly Color CreamDim = Pal.Hex("C9BFA8");

        public Screens(GameRoot root)
        {
            this.root = root;
            var ticker = new GameObject("ScreensTicker").AddComponent<ScreensTicker>();
            Object.DontDestroyOnLoad(ticker.gameObject);
            ticker.Screens = this;
        }

        CanvasGroup Group(string name)
        {
            var r = UiKit.Rect(UiKit.Root, name);
            r.Stretch();
            var g = r.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0;
            r.gameObject.SetActive(false);
            return g;
        }

        void Show(CanvasGroup g) => UiKit.Fade(g, true, 0.35f);
        void Hide(CanvasGroup g) { if (g != null && g.gameObject.activeSelf) UiKit.Fade(g, false, 0.25f); }

        void HideAllMenus()
        {
            Hide(title); Hide(select); Hide(intro); Hide(closed); Hide(pause); Hide(settings); Hide(confirm); Hide(week);
        }

        /// <summary>Running in a browser: no Quit, and no resolution picker.</summary>
        static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>Settings or a confirmation box is on screen.</summary>
        public bool AnyOverlayOpen =>
            (confirm != null && confirm.gameObject.activeSelf && confirm.alpha > 0.01f) ||
            (settings != null && settings.gameObject.activeSelf && settings.alpha > 0.01f) ||
            DocketWeekOpen;

        public bool CloseTopOverlay()
        {
            if (confirm != null && confirm.gameObject.activeSelf && confirm.alpha > 0.5f) { Hide(confirm); return true; }
            if (DocketWeekOpen) { Hide(week); return true; }
            if (notebook != null && notebook.Open) { notebook.Hide(); return true; }
            if (settings != null && settings.gameObject.activeSelf && settings.alpha > 0.5f) { Hide(settings); return true; }
            if (actions != null && actions.gameObject.activeSelf) { CaseSession.Current?.Deselect(); return true; }
            return false;
        }

        // ------------------------------------------------------------------ title

        public void ShowTitle()
        {
            HideAllMenus();
            if (title == null) BuildTitle();
            RefreshTitle();
            Show(title);
        }

        TextMeshProUGUI titleContinue;
        Button titleContinueBtn;

        void BuildTitle()
        {
            title = Group("Title");
            var shade = UiKit.FadeRight(title.transform, "shade", new Color(0.03f, 0.025f, 0.03f, 0.8f));
            shade.rectTransform.Place(new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);

            var col = UiKit.Rect(title.transform, "col");
            // A fixed 1080-tall column (the reference height) so a larger text size can shrink it to fit.
            col.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(1100, 1080));
            UiKit.FitInCanvas(col, 0, 0);

            var small = UiKit.Text(col, "A  WRENHAVEN  MYSTERY  ·  1986", Art.SansBold, 26, Pal.Lamp, TextAlignmentOptions.Left);
            small.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(4, -200), new Vector2(0, 40));
            small.characterSpacing = 6;
            var logo = UiKit.Text(col, "Alibi & Co.", Art.Display, 150, Cream, TextAlignmentOptions.TopLeft);
            logo.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(-6, -244), new Vector2(0, 200));
            logo.textWrappingMode = TextWrappingModes.NoWrap;
            var tag = UiKit.Text(col, "Pin the evidence to the timeline.\nThen find the alibi that can't be true.", Art.SerifItalic, 38, CreamDim, TextAlignmentOptions.Left);
            tag.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(4, -450), new Vector2(0, 120));

            var btns = UiKit.Rect(col, "buttons");
            btns.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -610), new Vector2(520, 360));
            var vl = btns.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 16; vl.childControlHeight = false; vl.childControlWidth = true; vl.childForceExpandHeight = false;

            titleContinueBtn = UiKit.Button(btns, "Continue", () =>
            {
                var s = SaveData.Current.inProgress;
                var c = s != null ? Cases.Get(s.caseId) : null;
                if (c != null) { Hide(title); root.StartCase(c, true); }
            }, Pal.Hex("8E2B2B"), Cream, 32);
            titleContinue = titleContinueBtn.GetComponentInChildren<TextMeshProUGUI>();
            Size(titleContinueBtn, 72);
            Size(UiKit.Button(btns, "Case Files", () => { root.ShowSelect(); }, Pal.Hex("2B3540"), Cream, 32), 72);
            Size(UiKit.Button(btns, "Settings", () => ShowSettings(), Pal.Hex("232A31"), CreamDim, 28), 60);
            if (!Web) Size(UiKit.Button(btns, "Quit", () => root.Quit(), Pal.Hex("232A31"), CreamDim, 28), 60);

            var foot = UiKit.Text(title.transform, "Mouse or gamepad  ·  Esc or Start pauses  ·  F11 fullscreen  ·  All art, music and sound generated for this game", Art.Sans, 20, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.Left);
            foot.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(150, 34), new Vector2(0, 30));
        }

        static void Size(Button b, float h) => ((RectTransform)b.transform).sizeDelta = new Vector2(0, h);

        void RefreshTitle()
        {
            var s = SaveData.Current.inProgress;
            var c = s != null ? Cases.Get(s.caseId) : null;
            titleContinueBtn.gameObject.SetActive(c != null);
            if (c != null) titleContinue.text = "Continue: " + c.Title;
        }

        // ------------------------------------------------------------------ case files

        public void ShowSelect()
        {
            HideAllMenus();
            if (select != null) Object.Destroy(select.gameObject);
            BuildSelect();
            Show(select);
        }

        /// <summary>
        /// The day moved on while the case files were up: redraw them in place for the new day, and
        /// the docket drawer too if it's open.
        /// </summary>
        public void RedrawSelect()
        {
            if (select == null) return;
            bool drawer = DocketWeekOpen;
            Object.Destroy(select.gameObject);
            BuildSelect();
            select.alpha = 1;
            Show(select);
            if (drawer) ShowDocketWeek(true);
        }

        void BuildSelect()
        {
            Cases.RefreshToday();   // a game left open past midnight moves on to the new day's docket
            select = Group("Select");
            var shade = UiKit.Panel(select.transform, "shade", new Color(0.02f, 0.02f, 0.03f, 0.72f), false);
            shade.rectTransform.Stretch();
            var top = UiKit.FadeUp(select.transform, "headShade", new Color(0.02f, 0.02f, 0.03f, 0.85f));
            top.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 300));
            top.rectTransform.localScale = new Vector3(1, -1, 1);   // solid at the top edge
            var head = UiKit.Text(select.transform, "Case Files", Art.Display, 84, Cream, TextAlignmentOptions.Center);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(0, 110));
            var sub = UiKit.Text(select.transform, "ALIBI & CO. · WRENHAVEN · AUTUMN 1986", Art.SansBold, 22, Pal.Lamp, TextAlignmentOptions.Center);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(0, 30));
            sub.characterSpacing = 5;

            var row = UiKit.Rect(select.transform, "row");
            // Three folders fill 1560 px; a fourth widens the row (FitInCanvas scales it back if needed).
            float rowWidth = Cases.All.Count > 4 ? 1840 : Cases.All.Count > 3 ? 1760 : 1560;
            row.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(rowWidth, 640));
            UiKit.FitInCanvas(row, 70, 205);   // keep clear of the heading
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 40; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true;

            for (int i = 0; i < Cases.All.Count; i++) Folder(row, i);

            var back = UiKit.Button(select.transform, "Back", () => root.ShowTitle(false), Pal.Hex("232A31"), CreamDim, 26);
            ((RectTransform)back.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-330, 44), new Vector2(240, 60));
            DocketButton(select.transform);
        }

        /// <summary>
        /// The Daily Docket, next to Back: a short generated case each day, proven airtight before
        /// it's offered (Logic/Docket). It opens once case 2 has taught clocks, and opens the drawer
        /// of this week's dockets.
        /// </summary>
        void DocketButton(Transform parent)
        {
            string label;
            bool open = Cases.DocketUnlocked;
            string head = "DAILY DOCKET  ·  " + Day(Cases.Today);
            if (!open) label = $"<size=60%>{head}</size>\n<size=80%>Opens when Case No. 2 is closed</size>";
            else
            {
                int week = Docket.Week(Cases.Today).Count(d => Cases.DocketRecord(d)?.solved == true);
                label = $"<size=60%>{head}  ·  {week} OF {Docket.DaysOnFile} THIS WEEK</size>\n{Docket.TitleFor(Cases.Today)}{DocketState(Cases.Today, true)}";
            }
            var b = UiKit.Button(parent, label, () => { if (open) { Sfx.Play("folder", 0.8f); ShowDocketWeek(); } },
                open ? Pal.Hex("8E2B2B") : Pal.Hex("3A3530"), open ? Cream : CreamDim, 26, null, "btn_docket");
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 36), new Vector2(620, 78));
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.lineSpacing = -12;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        static string Day(System.DateTime d) => d.ToString("ddd d MMM", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();

        /// <summary>"   ★★☆ 3:12", "   IN PROGRESS", or (in the drawer) what's left of a day's time on file.</summary>
        static string DocketState(System.DateTime day, bool onButton)
        {
            var rec = Cases.DocketRecord(day);
            var s = SaveData.Current.inProgress;
            if (rec != null && rec.solved) return $"   {Stars(rec.bestBadges)} <size=80%>{Clock(rec.bestTime)}</size>";
            if (s != null && s.caseId == Docket.IdFor(day)) return "   <size=70%>IN PROGRESS</size>";
            if (onButton) return "";
            if (day == Cases.Today) return "   <size=70%><color=#E8C27A>NEW TODAY</color></size>";
            return Docket.OnFileUntil(day) == Cases.Today ? "   <size=70%><color=#E8C27A>LAST DAY ON FILE</color></size>" : "   <size=70%><color=#B9AE98>NOT YET OPENED</color></size>";
        }

        /// <summary>The drawer is on screen (the case files' week of dockets).</summary>
        public bool DocketWeekOpen => week != null && week.gameObject.activeSelf && week.alpha > 0.01f;

        /// <summary>
        /// The docket drawer: the last seven days, today first, each with its crime and your result.
        /// A missed day can still be opened until it's a week old.
        /// </summary>
        public void ShowDocketWeek(bool instant = false)
        {
            if (week != null) Object.Destroy(week.gameObject);
            week = Group("DocketWeek");
            var shade = UiKit.Panel(week.transform, "shade", new Color(0.02f, 0.02f, 0.03f, 0.6f), false);
            shade.rectTransform.Stretch();
            var shut = shade.gameObject.AddComponent<Button>();
            shut.transition = Selectable.Transition.None;
            shut.onClick.AddListener(() => Hide(week));
            var paper = UiKit.Panel(week.transform, "paper", Pal.Hex("F2EAD6"));
            UiKit.DropShadow(paper.rectTransform, 40f, 0.65f, new Vector2(8, -18));
            paper.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 900));
            UiKit.FitInCanvas(paper.rectTransform, 20);
            paper.transform.localRotation = Quaternion.Euler(0, 0, 0.4f);
            var p = paper.rectTransform;
            var head = UiKit.Text(p, "ALIBI & CO.  ·  THE DAILY DOCKET", Art.SansBold, 22, Pal.Oxblood, TextAlignmentOptions.TopLeft);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -48), new Vector2(-120, 30));
            head.characterSpacing = 5;
            var t = UiKit.Text(p, "This week's dockets", Art.Display, 64, Pal.Ink, TextAlignmentOptions.TopLeft);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -84), new Vector2(-120, 86));
            var sub = UiKit.Text(p, "A new case comes in every day, and each one stays on file for a week. Missed one? It's still in the drawer.",
                Art.SerifItalic, 24, Pal.InkSoft, TextAlignmentOptions.TopLeft);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -174), new Vector2(-120, 64));

            var col = UiKit.Rect(p, "days");
            col.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -272), new Vector2(-120, 7 * 70));
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 10; vl.childControlHeight = false; vl.childControlWidth = true; vl.childForceExpandHeight = false;
            var days = Docket.Week(Cases.Today);
            for (int i = 0; i < days.Count; i++)
            {
                var day = days[i];
                string when = i == 0 ? "TODAY" : i == 1 ? "YESTERDAY" : Day(day);
                var rec = Cases.DocketRecord(day);
                bool done = rec != null && rec.solved;
                string label = $"<pos=2%><size=72%><cspace=2>{when}</cspace></size><pos=24%>{Docket.TitleFor(day)}<pos=64%>{DocketState(day, false).Trim()}";
                var b = UiKit.Button(col, label, () =>
                {
                    var c = Cases.DocketFor(day);
                    if (c == null) return;
                    Hide(week);
                    Sfx.Play("folder", 0.8f);
                    root.ShowIntro(c);
                }, i == 0 ? Pal.Hex("8E2B2B") : done ? Pal.Hex("2F4A3E") : Pal.Hex("2B3540"), Cream, 26, null, "docket_" + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                Size(b, 60);
                var lt = b.GetComponentInChildren<TextMeshProUGUI>();
                lt.alignment = TextAlignmentOptions.MidlineLeft;
                lt.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var close = UiKit.Button(p, "Close", () => Hide(week), Pal.Hex("2B3540"), Cream, 26);
            ((RectTransform)close.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 40), new Vector2(200, 60));
            var foot = UiKit.Text(p, Cases.DocketsClosed > 0 ? $"{Cases.DocketsClosed} closed in all." : "", Art.SerifItalic, 22, Pal.InkSoft, TextAlignmentOptions.MidlineLeft);
            foot.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(-340, 60));
            week.transform.SetAsLastSibling();
            if (instant) week.alpha = 1;
            Show(week);
        }

        /// <summary>A day's row in the drawer, as shown (for the autopilot), or null.</summary>
        public string DocketRowText(System.DateTime day)
        {
            if (week == null) return null;
            var row = week.transform.Find("paper/days/docket_" + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            return row != null ? row.GetComponentInChildren<TextMeshProUGUI>().text : null;
        }

        /// <summary>
        /// The screen centre of a named menu button a click would reach right now (not faded out or
        /// behind an overlay), or null. For the input tests, which then move a real cursor there.
        /// </summary>
        public Vector2? ButtonScreen(string name)
        {
            foreach (var b in UiKit.Root.GetComponentsInChildren<Button>())
            {
                if (b.name != name || !b.isActiveAndEnabled || !b.interactable) continue;
                var cg = b.GetComponentInParent<CanvasGroup>();
                if (cg != null && cg.alpha < 0.9f) continue;
                var corners = new Vector3[4];
                ((RectTransform)b.transform).GetWorldCorners(corners);
                var c = (Vector2)((corners[0] + corners[2]) / 2);
                if (UiKit.TopHitIs(c, b.gameObject)) return c;
            }
            return null;
        }

        /// <summary>Press a named button on the case files, the drawer or the title, as a click would (for the autopilot).</summary>
        public bool Press(string name)
        {
            foreach (var g in new[] { week, select, title })
            {
                if (g == null || !g.gameObject.activeSelf) continue;
                var b = g.GetComponentsInChildren<Button>().FirstOrDefault(x => x.name == name);
                if (b != null) { b.onClick.Invoke(); return true; }
            }
            return false;
        }

        void Folder(RectTransform parent, int index)
        {
            var c = Cases.All[index];
            var rec = SaveData.Current.Record(c.Id);
            bool unlocked = SaveData.Current.IsUnlocked(index);
            bool inProgress = SaveData.Current.inProgress != null && SaveData.Current.inProgress.caseId == c.Id;

            var folder = UiKit.Panel(parent, "folder_" + c.Id, unlocked ? Pal.Hex("D8B97E") : Pal.Hex("6C6253"));
            UiKit.DropShadow(folder.rectTransform, 30f, 0.6f, new Vector2(6, -14));
            var tab = UiKit.Panel(folder.transform, "tab", unlocked ? Pal.Hex("D8B97E") : Pal.Hex("6C6253"));
            tab.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(30, -18), new Vector2(200, 56));
            var tabText = UiKit.Text(tab.transform, $"CASE No. {index + 1}", Art.SansBold, 22, Pal.Ink, TextAlignmentOptions.Center);
            tabText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 8), Vector2.zero);

            var paper = UiKit.Panel(folder.transform, "paper", unlocked ? Pal.Hex("F3EBD8") : Pal.Hex("8A8274"), true, true);
            paper.rectTransform.Stretch(26);
            var inner = paper.rectTransform;

            // The file's front sheet, stacked top to bottom by a layout group so a long title or a
            // narrow folder (four or five to a row) pushes the lines below down instead of under.
            int n = Cases.All.Count;
            float k = n <= 3 ? 1f : n == 4 ? 0.88f : 0.74f;
            int pad = n <= 4 ? 34 : 24;
            var col = UiKit.Rect(inner, "sheet");
            col.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -30), new Vector2(-2 * pad, -150));
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            vl.spacing = 10 * k;
            void Line(string text, string font, float size, Color color, float spacing = 0, float gapAfter = 0)
            {
                var t = UiKit.Text(col, text, font, size * k, color, TextAlignmentOptions.TopLeft);
                t.characterSpacing = spacing;
                t.enableAutoSizing = false;
                if (gapAfter > 0) t.margin = new Vector4(0, 0, 0, gapAfter * k);
            }
            Line(c.Title, Art.Serif, 50, Pal.Ink, 0, 4);
            Line(c.Tagline, Art.SerifItalic, 28, Pal.InkSoft, 0, 6);
            Line((c.Date + "\n" + c.Weather).ToUpperInvariant(), Art.SansBold, 19, Pal.InkSoft, 3, 8);
            Line("SUSPECTS", Art.SansBold, 18, Pal.Oxblood);
            Line(string.Join("\n", c.Suspects.Select(p => "· " + p.Name)), Art.Typewriter, 25, Pal.Ink);

            string status;
            if (!unlocked) status = "<color=#3A2F25>LOCKED</color>\n<size=20>Close the previous case to open this file.</size>";
            // The CLOSED rubber stamp says it already: stars, best time and the seals earned so far.
            else if (rec.solved) status = $"{Stars(rec.bestBadges)}   <size=20>Best {Clock(rec.bestTime)}</size>\n<size={(n <= 4 ? 17 : 15)}>{Seals(rec.sealClean, rec.sealUnaided, rec.sealSwift, c.ParSeconds, false)}</size>";
            else if (inProgress) status = "<color=#8E2B2B>IN PROGRESS</color>\n<size=20>Pick up where you left off.</size>";
            else status = "<color=#8E2B2B>OPEN</color>\n<size=20>" + c.Lesson + "</size>";
            var st = UiKit.Text(inner, status, Art.SansBold, 28 * Mathf.Max(k, 0.85f), Pal.Ink, TextAlignmentOptions.BottomLeft);
            st.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(pad, 30), new Vector2(-2 * pad, 90));
            if (unlocked)
            {
                var clip = UiKit.Rect(folder.transform, "clip");
                clip.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-60, -6), new Vector2(44, 80));
                clip.localRotation = Quaternion.Euler(0, 0, -6);
                var ci = clip.gameObject.AddComponent<Image>();
                ci.sprite = Art.Sprite("Icons/clip");
                ci.color = Pal.Hex("8E969E");
                ci.raycastTarget = false;
            }
            if (rec.solved) Stamp(inner, "CLOSED", new Color(0.18f, 0.42f, 0.31f, 0.8f), new Vector2(-4, -455), -10f);

            if (unlocked)
            {
                var btn = folder.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                var hv = folder.gameObject.AddComponent<UiKit.Hover>();
                hv.Target = folder;
                hv.Normal = folder.color;
                hv.Hot = Pal.Hex("E6C98F");
                btn.onClick.AddListener(() => { Sfx.Play("folder", 0.8f); root.ShowIntro(c); });
            }
        }

        /// <summary>
        /// The press cutting sits under the write-up, so it only goes in if the text, once laid out,
        /// ends above it (the layout isn't ready on the frame the screen is built).
        /// </summary>
        static System.Collections.IEnumerator ShowCuttingIfRoom(CaseDef c, TextMeshProUGUI body, RectTransform cut, float room)
        {
            yield return null;
            yield return null;
            if (!body || !cut) yield break;
            body.ForceMeshUpdate();
            var ti = body.textInfo;
            float h = ti.lineCount > 0 ? ti.lineInfo[0].ascender - ti.lineInfo[ti.lineCount - 1].descender : float.MaxValue;
            cut.gameObject.SetActive(h <= room);
            Debug.Log($"[Intro] {c.Id}: write-up {ti.lineCount} lines, {h:0} of {room:0} px, cutting {(h <= room ? "shown" : "left out")}");
        }

        /// <summary>
        /// "✔ CLEAN  ✔ UNAIDED  SWIFT": the three seals, earned ones in green, missed ones greyed.
        /// Clean: no badge lost. Unaided: no hint. Swift: under the case's par time.
        /// </summary>
        public static string Seals(bool clean, bool unaided, bool swift, int parSeconds, bool withPar)
        {
            string One(bool got, string name) => got ? $"<color=#2F6B4F>✔ {name}</color>" : $"<color=#A79C88>{name}</color>";
            var line = One(clean, "CLEAN") + "   " + One(unaided, "UNAIDED");
            if (parSeconds > 0) line += "   " + One(swift, "SWIFT") + (withPar ? $"<color=#8A7A6A><size=80%>  under {Clock(parSeconds)}</size></color>" : "");
            return line;
        }

        public static string Stars(int n) => "<color=#B8862E>" + new string('★', n) + "</color><color=#9A9080>" + new string('☆', 3 - n) + "</color>";

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        // ------------------------------------------------------------------ intro

        public void ShowIntro(CaseDef c)
        {
            HideAllMenus();
            if (intro != null) Object.Destroy(intro.gameObject);
            intro = Group("Intro");
            var shade = UiKit.Panel(intro.transform, "shade", new Color(0.02f, 0.02f, 0.03f, 0.65f), false);
            shade.rectTransform.Stretch();
            var paper = UiKit.Panel(intro.transform, "paper", Pal.Hex("F2EAD6"));
            UiKit.DropShadow(paper.rectTransform, 40f, 0.65f, new Vector2(8, -18));
            paper.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 900));
            UiKit.FitInCanvas(paper.rectTransform, 20);
            paper.transform.localRotation = Quaternion.Euler(0, 0, -0.6f);
            var p = paper.rectTransform;
            int idx = Cases.IndexOf(c.Id) + 1;
            var head = UiKit.Text(p, Cases.IsDocket(c) ? "ALIBI & CO.  ·  THE DAILY DOCKET" : $"ALIBI & CO.  ·  CASE FILE No. {idx}", Art.SansBold, 22, Pal.Oxblood, TextAlignmentOptions.TopLeft);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -48), new Vector2(-120, 30));
            head.characterSpacing = 5;
            var t = UiKit.Text(p, c.Title, Art.Display, 76, Pal.Ink, TextAlignmentOptions.TopLeft);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -84), new Vector2(-120, 100));
            var d = UiKit.Text(p, (c.Date + "  ·  " + c.Weather).ToUpperInvariant(), Art.SansBold, 20, Pal.InkSoft, TextAlignmentOptions.TopLeft);
            d.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -186), new Vector2(-120, 30));
            d.characterSpacing = 3;
            var body = UiKit.Text(p, string.Join("\n\n", c.Intro), Art.Typewriter, 25, Pal.Ink, TextAlignmentOptions.TopLeft);
            // Ends 30 px short of the suspects column so a full line never runs into "SUSPECTS".
            body.rectTransform.Place(new Vector2(0, 1), new Vector2(0.62f, 1), new Vector2(0, 1), new Vector2(60, -236), new Vector2(-70, 520));
            body.lineSpacing = 8;
            var reveal = body.gameObject.AddComponent<TypeReveal>();
            reveal.Begin(body, 160f);

            // Case-file furniture: a paperclip on the top edge, a rubber stamp, and the Gazette's headline
            // as a press cutting when the write-up leaves room for it.
            var clip = UiKit.Rect(p, "clip");
            clip.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(34, 8), new Vector2(54, 96));
            clip.localRotation = Quaternion.Euler(0, 0, 8);
            var clipImg = clip.gameObject.AddComponent<Image>();
            clipImg.sprite = Art.Sprite("Icons/clip");
            clipImg.color = Pal.Hex("8E969E");
            clipImg.raycastTarget = false;
            var rec = SaveData.Current.Record(c.Id);
            bool resumeFile = SaveData.Current.inProgress != null && SaveData.Current.inProgress.caseId == c.Id;
            string stampText = rec.solved ? "CLOSED" : resumeFile ? "IN PROGRESS" : "OPEN FILE";
            var stampCol = rec.solved ? new Color(0.18f, 0.42f, 0.31f, 0.85f) : new Color(0.66f, 0.14f, 0.17f, 0.82f);
            Stamp(p, stampText, stampCol, new Vector2(-70, -64), -8f);
            var news = Art.Tex("news_" + c.Id);
            if (news != null)
            {
                var cut = UiKit.Rect(p, "cutting");
                cut.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(64, 146), new Vector2(560, 140));
                cut.localRotation = Quaternion.Euler(0, 0, 1.2f);
                UiKit.DropShadow(cut, 10f, 0.35f, new Vector2(3, -5));
                var raw = cut.gameObject.AddComponent<RawImage>();
                raw.texture = news;
                raw.uvRect = new Rect(0.03f, 0.64f, 0.94f, 0.335f);   // masthead, headline and standfirst
                raw.raycastTarget = false;
                cut.gameObject.SetActive(false);
                root.StartCoroutine(ShowCuttingIfRoom(c, body, cut, 900 - 146 - 140 - 236 - 24));
            }

            // Suspects column.
            var col = UiKit.Rect(p, "suspects");
            col.Place(new Vector2(0.62f, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -236), new Vector2(-80, 540));
            var ss = UiKit.Text(col, "SUSPECTS", Art.SansBold, 20, Pal.Oxblood, TextAlignmentOptions.TopLeft);
            ss.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 30));
            int i = 0;
            foreach (var person in c.Suspects)
            {
                float y = -42 - i * 128;
                var ph = PortraitImage(col, c, person.Id, 92);
                ph.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(92, 108));
                var n = UiKit.Text(col, person.Name, Art.Serif, 26, Pal.Ink, TextAlignmentOptions.TopLeft);
                n.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(108, y - 2), new Vector2(-108, 34));
                var bl = UiKit.Text(col, person.Blurb, Art.Sans, 18, Pal.InkSoft, TextAlignmentOptions.TopLeft);
                bl.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(108, y - 36), new Vector2(-108, 76));
                i++;
            }

            var btns = UiKit.Rect(p, "buttons");
            btns.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(60, 46), new Vector2(-120, 70));
            var hl = btns.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 18; hl.childControlWidth = false; hl.childForceExpandWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
            bool resume = SaveData.Current.inProgress != null && SaveData.Current.inProgress.caseId == c.Id;
            var open = UiKit.Button(btns, resume ? "Continue the board" : "Open the board", () => { Hide(intro); Sfx.Play("folder", 0.8f); root.StartCase(c, resume); }, Pal.Hex("8E2B2B"), Cream, 30);
            ((RectTransform)open.transform).sizeDelta = new Vector2(340, 70);
            if (resume)
            {
                var fresh = UiKit.Button(btns, "Start over", () => { Hide(intro); root.StartCase(c, false); }, Pal.Hex("2B3540"), Cream, 26);
                ((RectTransform)fresh.transform).sizeDelta = new Vector2(220, 70);
            }
            bool docket = Cases.IsDocket(c);
            var back = UiKit.Button(btns, docket ? "Back to the dockets" : "Back to files", () => { Hide(intro); root.ShowSelect(); if (docket) ShowDocketWeek(); }, Pal.Hex("2B3540"), Cream, 26);
            ((RectTransform)back.transform).sizeDelta = new Vector2(docket ? 300 : 240, 70);
            Show(intro);
            Sfx.Play("paper_slide", 0.5f);
        }

        /// <summary>A rubber stamp: spaced capitals in a ruled box, inked a little unevenly, at an angle.</summary>
        static void Stamp(RectTransform parent, string text, Color ink, Vector2 topRight, float angle)
        {
            var box = UiKit.Rect(parent, "stamp");
            var t = UiKit.Text(box, text, Art.SansBold, 34, ink, TextAlignmentOptions.Center);
            t.characterSpacing = 10;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.ForceMeshUpdate();
            float w = Mathf.Max(t.preferredWidth, text.Length * 26f) + 56, h = 70;
            box.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), topRight, new Vector2(w, h));
            box.localRotation = Quaternion.Euler(0, 0, angle);
            t.rectTransform.Stretch();
            const float k = 4f;
            foreach (var (min, max, pos, size) in new[] {
                (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, k)),
                (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, k)),
                (new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(k, 0)),
                (new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(k, 0)) })
            {
                var line = UiKit.Panel(box, "rule", ink, false);
                line.raycastTarget = false;
                line.rectTransform.anchorMin = min;
                line.rectTransform.anchorMax = max;
                line.rectTransform.pivot = new Vector2(min.x == max.x ? (min.x == 0 ? 0 : 1) : 0.5f, min.y == max.y ? (min.y == 0 ? 0 : 1) : 0.5f);
                line.rectTransform.anchoredPosition = pos;
                line.rectTransform.sizeDelta = size;
            }
        }

        public static Graphic PortraitImage(Transform parent, CaseDef c, string id, float w)
        {
            var frame = UiKit.Panel(parent, "portrait_" + id, Pal.Hex("FBF8EF"), true, true);
            var tex = Art.Portrait(id);
            var r = UiKit.Rect(frame.transform, "img");
            r.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(-12, w - 12));
            if (tex != null)
            {
                var ri = r.gameObject.AddComponent<RawImage>();
                ri.texture = tex;
            }
            else
            {
                var img = r.gameObject.AddComponent<Image>();
                img.color = Pal.Hex("3A4048");
                var person = c.PersonById.TryGetValue(id, out var pp) ? pp : null;
                var t = UiKit.Text(r, person != null ? Portraits.Initials(person.Name) : "?", Art.Serif, w * 0.38f, Pal.Hex("E9E1CC"), TextAlignmentOptions.Center);
                t.rectTransform.Stretch();
            }
            return frame;
        }

        // ------------------------------------------------------------------ HUD & card actions

        public void ShowHud(CaseSession s)
        {
            HideAllMenus();
            hudSession = s;
            if (hud == null) BuildHud();
            RefreshBadges(false);
            s.BadgeLost += () => RefreshBadges(true);
            s.ShowActions = ShowActions;
            s.HideActions = HideActions;
            Show(hud);
        }

        public void HideHud()
        {
            Hide(hud);
            notebook?.Hide();
            HideActions();
            hudSession = null;
        }

        void BuildHud()
        {
            hud = Group("HUD");
            var pill = UiKit.Panel(hud.transform, "pill", new Color(0.06f, 0.07f, 0.08f, 0.78f));
            UiKit.DropShadow(pill.rectTransform, 16f, 0.45f, new Vector2(0, -6));
            pill.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -14), new Vector2(290, 58));
            var hint = UiKit.Button(pill.transform, "Hint", () => hudSession?.Hint(), Pal.Hex("2B3540"), Cream, 22);
            ((RectTransform)hint.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(86, 42));
            var notes = UiKit.Button(pill.transform, "Notes", ToggleNotebook, Pal.Hex("2B3540"), Cream, 22);
            ((RectTransform)notes.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86, 42));
            var menu = UiKit.Button(pill.transform, "Menu", () => root.SetPaused(true), Pal.Hex("2B3540"), Cream, 22);
            ((RectTransform)menu.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(86, 42));

            // The controls strip: one contextual tip at a time, set on the board's dark lower frame so it
            // never covers the tray. Each tip retires once the player has used that gesture; the full
            // list of controls lives in the pause menu.
            var helpRoot = UiKit.Rect(hud.transform, "help");
            helpRoot.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(0, 64));
            helpRect = helpRoot;
            helpGroup = helpRoot.gameObject.AddComponent<CanvasGroup>();
            helpGroup.blocksRaycasts = false;
            helpGroup.alpha = 0;
            helpText = UiKit.Text(helpRoot, "", Art.Sans, 21, new Color(1, 0.95f, 0.85f, 0.92f), TextAlignmentOptions.Center);
            helpText.rectTransform.Place(new Vector2(0.18f, 0.5f), new Vector2(0.82f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 9), new Vector2(0, 30));
            helpText.enableAutoSizing = true;
            helpText.fontSizeMin = 14;
            helpText.fontSizeMax = 21;
            helpKeys = UiKit.Text(helpRoot, MouseKeys,
                Art.Sans, 16, new Color(1, 0.95f, 0.85f, 0.55f), TextAlignmentOptions.Center);
            helpKeys.rectTransform.Place(new Vector2(0.18f, 0.5f), new Vector2(0.82f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(0, 24));
            helpKeys.enableAutoSizing = true;
            helpKeys.fontSizeMin = 12;
            helpKeys.fontSizeMax = 16;

            BuildActions();
        }

        /// <summary>Tab / the Notes button: open the notebook on the current case, or close it.</summary>
        public void ToggleNotebook()
        {
            if (notebook == null) notebook = new Notebook();
            if (notebook.Open) { notebook.Hide(); return; }
            if (hudSession == null || hudSession.Solved) return;
            hudSession.Deselect();
            HideActions();
            notebook.Show(hudSession);
        }

        public bool NotebookOpen => notebook != null && notebook.Open;

        void RefreshBadges(bool animate)
        {
            var s = hudSession;
            if (s == null || s.View == null || s.View.Badges == null) return;
            int n = s.Badges;
            s.View.Badges.text = "<color=#C9A24A>" + new string('★', n) + "</color><color=#8A8170>" + new string('☆', 3 - n) + "</color>";
            if (animate)
            {
                s.View.Badges.transform.Punch(0.35f, 0.5f);
                Sfx.Play("badge_lost", 0.7f);
            }
        }

        public void Tick()
        {
            if (helpGroup != null) TickHelp();
            if (hudSession != null && hudSession.View != null && hudSession.View.Timer != null)
            {
                hudSession.View.Timer.text = Settings.ShowTimer ? Clock(hudSession.Elapsed) : "";
            }
            if (actionsCard != null && actions != null && actions.gameObject.activeSelf)
            {
                if (!actionsCard) { HideActions(); return; }
                PositionActions(Stage.I.WorldToScreen(actionsCard.transform.position));
            }
        }

        const string MouseKeys = "<b>Tab</b> notebook   ·   <b>H</b> hint   ·   <b>right-click</b> sends a card back   ·   <b>Esc</b> menu and controls";
        const string PadKeys = "<b>[Y]</b> notebook   ·   <b>[X]</b> hint   ·   <b>[B]</b> sends a card back   ·   <b>[Start]</b> menu and controls";

        void TickHelp()
        {
            string tip = hudSession != null && !hudSession.InputLocked && !NotebookOpen ? hudSession.CoachTip() : null;
            string keys = PadCursor.Active ? PadKeys : MouseKeys;
            if (helpKeys.text != keys) helpKeys.text = keys;
            if (tip != null && tip != helpShown && helpGroup.alpha < 0.05f) { helpShown = tip; helpText.text = tip; }
            bool show = tip != null && tip == helpShown;
            helpGroup.alpha = Mathf.MoveTowards(helpGroup.alpha, show ? 1f : 0f, AlibiCo.Clock.Dt * 2.5f);
            if (helpGroup.alpha <= 0 || Stage.I == null) return;
            // Centre it on the strip of frame between the bottom of the board and the top of the tray.
            var st = Stage.I;
            float yBoard = st.WorldToScreen(st.BoardToWorld(new Vector2(0, st.Board.yMin))).y;
            float yTray = st.WorldToScreen(st.DeskToWorld(new Vector2(0, st.Tray.yMax))).y;
            float scale = UiKit.Canvas != null ? UiKit.Canvas.scaleFactor : 1f;
            helpRect.anchoredPosition = new Vector2(0, (yBoard + yTray) * 0.5f / Mathf.Max(0.01f, scale));
        }

        void BuildActions()
        {
            actions = Group("Actions");
            actions.blocksRaycasts = true;
            var panel = UiKit.Panel(actions.transform, "panel", new Color(0.07f, 0.08f, 0.1f, 0.94f));
            UiKit.DropShadow(panel.rectTransform, 22f, 0.55f, new Vector2(0, -10));
            actionsPanel = panel.rectTransform;
            actionsPanel.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(400, 210));
            actionsTitle = UiKit.Text(actionsPanel, "", Art.SansBold, 22, Cream, TextAlignmentOptions.TopLeft);
            actionsTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(-36, 30));
            actionsInfo = UiKit.Text(actionsPanel, "", Art.Sans, 18, CreamDim, TextAlignmentOptions.TopLeft);
            actionsInfo.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(18, -46), new Vector2(-36, 60));
            actionsConfront = UiKit.Button(actionsPanel, "Confront", () =>
            {
                var s = CaseSession.Current;
                if (s != null && actionsCard != null) s.Confront(actionsCard);
            }, Pal.Hex("8E2B2B"), Cream, 24);
            ((RectTransform)actionsConfront.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(18, 76), new Vector2(-36, 52));
            actionsUnpin = UiKit.Button(actionsPanel, "Back to the tray", () =>
            {
                var s = CaseSession.Current;
                if (s != null && actionsCard != null) s.Unpin(actionsCard);
            }, Pal.Hex("2B3540"), Cream, 20);
            ((RectTransform)actionsUnpin.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(18, 16), new Vector2(-36, 46));
        }

        void ShowActions(CardView v, Vector2 screen)
        {
            var s = CaseSession.Current;
            if (s == null || v == null) return;
            actionsCard = v;
            var def = v.Def;
            bool pinned = s.Board.Pinned.Contains(def.Id);
            actionsTitle.text = (def.IsTestimony ? "Statement · " : "Record · ") + def.Title;
            string info;
            bool canConfront = s.Board.CanConfront(def.Id, out var reason);
            if (def.IsRecord) info = "Paper doesn't lie, but its clock might be wrong.";
            else if (s.Board.Struck.Contains(def.Id)) info = "Already struck off.";
            else info = canConfront ? "The board says this can't be true. Confront them?" : reason;
            actionsInfo.text = info;
            actionsConfront.gameObject.SetActive(def.IsTestimony && !s.Board.Struck.Contains(def.Id));
            UiKit.SetInteractable(actionsConfront, canConfront);
            actionsConfront.GetComponentInChildren<TextMeshProUGUI>().text = "Confront " + ShortName(def.Title);
            actionsUnpin.gameObject.SetActive(pinned);
            float h = 70 + 50 + (actionsConfront.gameObject.activeSelf ? 60 : 0) + (pinned ? 54 : 0);
            actionsPanel.sizeDelta = new Vector2(400, h);
            ((RectTransform)actionsConfront.transform).anchoredPosition = new Vector2(18, pinned ? 76 : 16);
            PositionActions(screen);
            actions.gameObject.SetActive(true);
            UiKit.Fade(actions, true, 0.15f);
        }

        static string ShortName(string title)
        {
            if (title.StartsWith("Capt.")) return "the Captain";
            var p = title.Split(' ');
            if (p[0] == "Mrs" || p[0] == "Mr") return title;
            return p[0];
        }

        void PositionActions(Vector2 screen)
        {
            var scale = UiKit.Root.localScale.x;
            var pos = screen / scale;
            var size = actionsPanel.sizeDelta;
            var canvasSize = UiKit.Root.sizeDelta;
            float x = Mathf.Clamp(pos.x, size.x / 2 + 10, canvasSize.x - size.x / 2 - 10);
            float y = pos.y + 48;
            if (y + size.y > canvasSize.y - 90) y = pos.y - 48 - size.y;
            y = Mathf.Clamp(y, 40, canvasSize.y - size.y - 10);
            actionsPanel.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>Screen position of the Confront button if the card panel is showing (for the input test).</summary>
        public Vector2? ConfrontButtonScreen()
        {
            if (actions == null || !actions.gameObject.activeSelf || actionsConfront == null || !actionsConfront.gameObject.activeInHierarchy) return null;
            var rt = (RectTransform)actionsConfront.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (Vector2)((corners[0] + corners[2]) / 2);
        }

        /// <summary>Screen centres of the selected card's action buttons (for the pad's jumps), if shown.</summary>
        public List<Vector2> ActionButtonsScreen()
        {
            var r = new List<Vector2>();
            if (actions == null || !actions.gameObject.activeSelf || actions.alpha < 0.5f) return r;
            foreach (var b in new[] { actionsConfront, actionsUnpin })
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4];
                ((RectTransform)b.transform).GetWorldCorners(corners);
                r.Add((corners[0] + corners[2]) / 2);
            }
            return r;
        }

        void HideActions()
        {
            actionsCard = null;
            if (actions != null && actions.gameObject.activeSelf) UiKit.Fade(actions, false, 0.12f);
        }

        // ------------------------------------------------------------------ pause & settings

        public void ShowPause()
        {
            if (pause == null) BuildPause();
            if (controlsBody != null) controlsBody.text = ControlsText(PadCursor.Active);
            notebook?.Hide();
            Show(pause);
            HideActions();
            Sfx.Play("folder", 0.5f);
        }

        public void HidePause() { Hide(pause); Hide(settings); }

        void BuildPause()
        {
            pause = Group("Pause");
            var shade = UiKit.Panel(pause.transform, "shade", new Color(0.02f, 0.02f, 0.03f, 0.6f), false);
            shade.rectTransform.Stretch();
            var panel = UiKit.Panel(pause.transform, "panel", new Color(0.08f, 0.09f, 0.11f, 0.96f));
            UiKit.DropShadow(panel.rectTransform, 36f, 0.6f, new Vector2(0, -16));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280, 0), new Vector2(520, 620));
            BuildControlsCard(pause.transform);
            var t = UiKit.Text(panel.transform, "Paused", Art.Display, 66, Cream, TextAlignmentOptions.Center);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(0, 90));
            var col = UiKit.Rect(panel.transform, "col");
            col.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(400, 440));
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 14; vl.childControlHeight = false; vl.childControlWidth = true; vl.childForceExpandHeight = false;
            Size(UiKit.Button(col, "Resume", () => root.SetPaused(false), Pal.Hex("8E2B2B"), Cream, 28), 64);
            Size(UiKit.Button(col, "Restart this case", () => Confirm("Start this case over? Your pins and badges reset.", () => { root.SetPaused(false); root.RestartCase(); }), Pal.Hex("2B3540"), Cream, 24), 56);
            Size(UiKit.Button(col, "Case files", () => { root.SetPaused(false); root.ShowSelect(); }, Pal.Hex("2B3540"), Cream, 24), 56);
            Size(UiKit.Button(col, "Settings", () => ShowSettings(), Pal.Hex("2B3540"), Cream, 24), 56);
            Size(UiKit.Button(col, "Title screen", () => { root.SetPaused(false); root.ShowTitle(false); }, Pal.Hex("2B3540"), Cream, 24), 56);
            if (!Web) Size(UiKit.Button(col, "Quit", () => root.Quit(), Pal.Hex("232A31"), CreamDim, 22), 50);
        }

        /// <summary>The full controls list, next to the pause menu (the board itself only shows one tip at a time).</summary>
        TextMeshProUGUI controlsBody;

        static string ControlsText(bool pad)
        {
            var rows = pad ? new[]
            {
                ("Left stick  ·  D-pad", "move the cursor (D-pad for fine steps)"),
                ("[LB]  [RB]", "jump to the previous / next card"),
                ("[A] on a card", "pin it; on a pinned statement, Confront"),
                ("Hold [A] and steer", "drag: onto a line, onto a card to link"),
                ("[B]", "send a pinned card back; back in menus"),
                ("Hold [A] on the incident", "accuse: drop it on the one line it fits"),
                ("[Y]  ·  [X]", "notebook  ·  Connie's hint"),
                ("[Start]", "pause, resume"),
                ("Move the mouse", "hands control back to the mouse"),
            } : new[]
            {
                ("Drag a card to the board", "pin it (or just click it)"),
                ("Hover a card", "read it in full; see the walk"),
                ("Drop a card on a card", "link: one moment, two clocks"),
                ("Click a pinned statement", "Confront, or send it back"),
                ("Right-click a pinned card", "back to the tray"),
                ("Drag the incident card", "accuse: the one line it fits"),
                ("Hover the town map", "zoom in on Wrenhaven"),
                ("Tab  ·  H or F1", "notebook  ·  Connie's hint"),
                ("Space or click", "skip a memo"),
                ("F11  ·  F12", "fullscreen  ·  screenshot"),
            };
            var sb = new System.Text.StringBuilder();
            foreach (var (k, v) in rows) sb.Append("<b>").Append(k).Append("</b>\n<color=#B9AE98>").Append(v).Append("</color>\n");
            return sb.ToString().TrimEnd();
        }

        void BuildControlsCard(Transform parent)
        {
            var card = UiKit.Panel(parent, "controls", new Color(0.08f, 0.09f, 0.11f, 0.96f));
            UiKit.DropShadow(card.rectTransform, 36f, 0.6f, new Vector2(0, -16));
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(290, 0), new Vector2(500, 620));
            var head = UiKit.Text(card.transform, "Controls", Art.Display, 44, Cream, TextAlignmentOptions.Center);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(0, 64));
            var body = UiKit.Text(card.transform, ControlsText(false), Art.Sans, 19, Cream, TextAlignmentOptions.TopLeft);
            controlsBody = body;
            body.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(-70, -140));
            body.lineSpacing = 2;
            body.enableAutoSizing = true;
            body.fontSizeMin = 12;
            body.fontSizeMax = 19;
        }

        public void ShowSettings()
        {
            if (settings == null) BuildSettings();
            settings.transform.SetAsLastSibling();
            Show(settings);
        }

        void BuildSettings()
        {
            settings = Group("Settings");
            var shade = UiKit.Panel(settings.transform, "shade", new Color(0.02f, 0.02f, 0.03f, 0.55f), false);
            shade.rectTransform.Stretch();
            var panel = UiKit.Panel(settings.transform, "panel", new Color(0.08f, 0.09f, 0.11f, 0.97f));
            UiKit.DropShadow(panel.rectTransform, 36f, 0.6f, new Vector2(0, -16));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 850));
            UiKit.FitInCanvas(panel.rectTransform);
            var t = UiKit.Text(panel.transform, "Settings", Art.Display, 60, Cream, TextAlignmentOptions.Center);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(0, 84));
            var col = UiKit.Rect(panel.transform, "col");
            col.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(680, 640));
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 16; vl.childControlHeight = false; vl.childControlWidth = true; vl.childForceExpandHeight = false;
            Row(UiKit.Slider(col, "Master volume", Settings.Master, v => Settings.Master = v));
            Row(UiKit.Slider(col, "Music", Settings.Music, v => Settings.Music = v));
            Row(UiKit.Slider(col, "Sound effects", Settings.Effects, v => { Settings.Effects = v; Sfx.Play("pin", 0.6f); }));
            if (!Web)   // in a browser the page decides the canvas size
            {
                var resChoices = Settings.ResolutionChoices();
                Row(UiKit.Stepper(col, "Resolution", resChoices.Select(Settings.ResolutionLabel).ToArray(),
                    resChoices.IndexOf(Settings.Resolution), i => Settings.Resolution = resChoices[i]));
            }
            Row(UiKit.Stepper(col, "Text size", Settings.TextSizeNames, Settings.TextSize, i => Settings.TextSize = i));
            Row(UiKit.Toggle(col, "Fullscreen", Settings.Fullscreen, v => Settings.Fullscreen = v));
            Row(UiKit.Toggle(col, "Reduced motion", Settings.ReducedMotion, v => Settings.ReducedMotion = v));
            Row(UiKit.Toggle(col, "Show case timer", Settings.ShowTimer, v => Settings.ShowTimer = v));
            var reset = UiKit.Button(col, "Erase all progress", () => Confirm("Erase every closed case and badge?", () => { SaveData.Reset(); if (root.Flow == Flow.Select) root.ShowSelect(); }), Pal.Hex("3A2526"), Pal.Hex("E8B4A8"), 20);
            Size(reset, 50);
            var close = UiKit.Button(panel.transform, "Done", () => { PlayerPrefs.Save(); Hide(settings); }, Pal.Hex("8E2B2B"), Cream, 26);
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(220, 60));
        }

        /// <summary>Row height for a settings control (a Slider lives on the bar inside its row, so size the row).</summary>
        static void Row(Component c) => ((RectTransform)(c is Slider ? c.transform.parent : c.transform)).sizeDelta = new Vector2(0, 52);

        void Confirm(string question, System.Action yes)
        {
            if (confirm != null) Object.Destroy(confirm.gameObject);
            confirm = Group("Confirm");
            var shade = UiKit.Panel(confirm.transform, "shade", new Color(0, 0, 0, 0.5f), false);
            shade.rectTransform.Stretch();
            var panel = UiKit.Panel(confirm.transform, "panel", new Color(0.1f, 0.11f, 0.13f, 0.98f));
            UiKit.DropShadow(panel.rectTransform, 30f, 0.6f, new Vector2(0, -14));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 260));
            var q = UiKit.Text(panel.transform, question, Art.Serif, 30, Cream, TextAlignmentOptions.Center);
            q.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(-60, 110));
            var y = UiKit.Button(panel.transform, "Yes", () => { Hide(confirm); yes(); }, Pal.Hex("8E2B2B"), Cream, 26);
            ((RectTransform)y.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-10, 30), new Vector2(200, 60));
            var n = UiKit.Button(panel.transform, "No", () => Hide(confirm), Pal.Hex("2B3540"), Cream, 26);
            ((RectTransform)n.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(10, 30), new Vector2(200, 60));
            confirm.transform.SetAsLastSibling();
            Show(confirm);
        }

        // ------------------------------------------------------------------ case closed

        public void ShowClosed(CaseSession s, bool firstClear)
        {
            HideAllMenus();
            if (closed != null) Object.Destroy(closed.gameObject);
            closed = Group("Closed");
            var c = s.Case;
            var shade = UiKit.Panel(closed.transform, "shade", new Color(0.02f, 0.02f, 0.03f, 0.62f), false);
            shade.rectTransform.Stretch();
            var paper = UiKit.Panel(closed.transform, "paper", Pal.Hex("F2EAD6"));
            UiKit.DropShadow(paper.rectTransform, 40f, 0.65f, new Vector2(8, -18));
            paper.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 820));
            UiKit.FitInCanvas(paper.rectTransform, 20);
            paper.transform.localRotation = Quaternion.Euler(0, 0, 0.5f);
            var p = paper.rectTransform;
            var culprit = c.PersonById[c.Incident.Culprit];

            var head = UiKit.Text(p, c.Title.ToUpperInvariant() + "  ·  THE CULPRIT", Art.SansBold, 22, Pal.Oxblood, TextAlignmentOptions.TopLeft);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -50), new Vector2(-120, 30));
            head.characterSpacing = 5;
            var ph = PortraitImage(p, c, culprit.Id, 170);
            ph.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -100), new Vector2(170, 200));
            ph.transform.localRotation = Quaternion.Euler(0, 0, -3);
            var name = UiKit.Text(p, culprit.Name, Art.Display, 62, Pal.Ink, TextAlignmentOptions.TopLeft);
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(270, -160), new Vector2(-330, 80));
            var stats = UiKit.Text(p, $"{Stars(s.Badges)}    {Clock(s.Elapsed)}" + (s.UsedHints ? "    <size=20><color=#8A7A6A>with Connie's help</color></size>" : "") + (firstClear ? "" : "    <size=20><color=#8A7A6A>replay</color></size>"),
                Art.SansBold, 36, Pal.Ink, TextAlignmentOptions.TopLeft);
            stats.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(272, -242), new Vector2(-330, 50));
            var badgeNote = UiKit.Text(p, s.Badges == 3 ? "A clean case. Not a single wrong accusation." : (s.Badges == 2 ? "One false step along the way." : "Got there in the end. Connie would've been quicker."),
                Art.SerifItalic, 24, Pal.InkSoft, TextAlignmentOptions.TopLeft);
            badgeNote.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(272, -286), new Vector2(-330, 36));
            var seals = UiKit.Text(p, Seals(s.SealClean, s.SealUnaided, s.SealSwift, c.ParSeconds, true), Art.SansBold, 21, Pal.Ink, TextAlignmentOptions.TopLeft);
            seals.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(272, -318), new Vector2(-330, 30));
            seals.characterSpacing = 3;

            var body = UiKit.Text(p, string.Join("\n\n", c.Epilogue), Art.Typewriter, 24, Pal.Ink, TextAlignmentOptions.TopLeft);
            body.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -366), new Vector2(-120, 318));
            body.lineSpacing = 6;
            body.gameObject.AddComponent<TypeReveal>().Begin(body, 120f, 1.2f);

            var stampRect = UiKit.Rect(p, "stamp");
            stampRect.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-230, -96), new Vector2(400, 104));
            stampRect.localRotation = Quaternion.Euler(0, 0, 9);
            var frame = UiKit.Panel(stampRect, "frame", new Color(0.66f, 0.14f, 0.17f, 0.9f), true, true);
            frame.rectTransform.Stretch();
            var inner = UiKit.Panel(frame.transform, "inner", Pal.Hex("F2EAD6"), true, true);
            inner.rectTransform.Stretch(8);
            var st = UiKit.Text(inner.transform, "CASE CLOSED", Art.Display, 52, new Color(0.66f, 0.14f, 0.17f), TextAlignmentOptions.Center);
            st.rectTransform.Stretch();
            st.textWrappingMode = TextWrappingModes.NoWrap;
            stampRect.localScale = Vector3.one * 3f;
            var cg = stampRect.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            Tween.Run((stampRect, "slam"), 0.25f, k => { stampRect.localScale = Vector3.one * Mathf.Lerp(3f, 1f, k); cg.alpha = k; }, Ease.InCubic, () =>
            {
                Sfx.Play("stamp_big", 1f);
                Stage.I.Shake(0.15f, 0.3f);
                stampRect.Punch(0.08f, 0.3f);
            }, 0.6f);

            var btns = UiKit.Rect(p, "buttons");
            btns.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(60, 46), new Vector2(-120, 70));
            var hl = btns.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 18; hl.childControlWidth = false; hl.childForceExpandWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
            int idx = Cases.IndexOf(c.Id);
            bool docket = Cases.IsDocket(c);
            bool last = docket || idx + 1 >= Cases.All.Count;
            var next = UiKit.Button(btns, docket ? "Back to the dockets" : last ? "Back to the case files" : "Next case: " + Cases.All[idx + 1].Title,
                () => { Hide(closed); if (last) { root.ShowSelect(); if (docket) ShowDocketWeek(); } else root.NextCase(c); }, Pal.Hex("8E2B2B"), Cream, 28);
            ((RectTransform)next.transform).sizeDelta = new Vector2(last ? 380 : 470, 70);
            var replay = UiKit.Button(btns, "Replay this case", () => { Hide(closed); root.StartCase(c, false); }, Pal.Hex("2B3540"), Cream, 24);
            ((RectTransform)replay.transform).sizeDelta = new Vector2(260, 70);
            if (!last)
            {
                var files = UiKit.Button(btns, "Case files", () => { Hide(closed); root.ShowSelect(); }, Pal.Hex("2B3540"), Cream, 24);
                ((RectTransform)files.transform).sizeDelta = new Vector2(200, 70);
            }
            if (docket)
            {
                Docket.TryParseId(c.Id, out var day);
                var fin = UiKit.Text(p, day >= Cases.Today ? "A new docket comes in tomorrow." : $"Seven days on file. {Docket.Week(Cases.Today).Count(d => Cases.DocketRecord(d)?.solved == true)} of this week's closed.",
                    Art.SerifItalic, 24, Pal.Oxblood, TextAlignmentOptions.MidlineRight);
                fin.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 46), new Vector2(-840, 70));
            }
            else if (last)
            {
                string[] words = { "None", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
                string n = Cases.All.Count < words.Length ? words[Cases.All.Count] : Cases.All.Count.ToString();
                var fin = UiKit.Text(p, $"{n} for {n.ToLowerInvariant()}. Wrenhaven sleeps a little easier.", Art.SerifItalic, 24, Pal.Oxblood, TextAlignmentOptions.MidlineRight);
                fin.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 46), new Vector2(-840, 70));
            }
            Show(closed);
            AudioDirector.I.PlayMusic("music_closed", 1.5f);
        }
    }

    /// <summary>Per-frame UI upkeep (timer, action panel tracking its card).</summary>
    public sealed class ScreensTicker : MonoBehaviour
    {
        public Screens Screens;
        void Update() => Screens?.Tick();
    }

    /// <summary>Typewriter reveal for a UI text block.</summary>
    public sealed class TypeReveal : MonoBehaviour
    {
        TextMeshProUGUI t;
        float cps, time, delay;
        int last;

        public void Begin(TextMeshProUGUI text, float charsPerSecond, float startDelay = 0.3f)
        {
            t = text;
            cps = charsPerSecond;
            delay = startDelay;
            t.maxVisibleCharacters = 0;
        }

        void Update()
        {
            if (t == null) return;
            if (delay > 0) { delay -= AlibiCo.Clock.Dt; return; }
            time += AlibiCo.Clock.Dt;
            int n = Mathf.FloorToInt(time * cps);
            if (n != last && n / 4 != last / 4 && n < t.text.Length) Sfx.Play("type", 0.12f, 1, 0.15f);
            last = n;
            t.maxVisibleCharacters = n;
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) time = 999;
            if (n > t.text.Length + 5) enabled = false;
        }
    }
}
