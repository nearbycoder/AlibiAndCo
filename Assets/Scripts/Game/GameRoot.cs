using System;
using System.Collections;
using System.Linq;
using AlibiCo.Logic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlibiCo
{
    public enum Flow { Boot, Title, Select, Intro, Playing, Closing, Closed }

    /// <summary>
    /// Bootstraps everything at runtime (the scene only needs to exist) and runs the screen flow:
    /// title → case files → case intro → board → reconstruction → case closed.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot I { get; private set; }
        public static bool Paused { get; private set; }
        /// <summary>
        /// The window (in a browser, the page) has the player's attention. The case timer only runs
        /// while it does, so alt-tabbing away never costs the Swift seal.
        /// </summary>
        public static bool Attended { get; private set; } = true;
        /// <summary>
        /// Attention has only just come back: this frame's time mostly passed away (one frame at the
        /// background rate can be a tenth of a second or more), so the case timer leaves it out.
        /// </summary>
        public static bool JustBack => Time.frameCount <= backFrame + 1;
        static int backFrame = -10;
        /// <summary>Automated runs keep their timing whatever the desktop's focus (they usually run unfocused).</summary>
        public static bool TimerIgnoresFocus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Paused = false; Time.timeScale = 1; Attended = true; TimerIgnoresFocus = false; backFrame = -10; }   // see Art.ResetStatics

        void OnApplicationFocus(bool focused) => SetAttended(focused, "window focus");
        void OnApplicationPause(bool paused) => SetAttended(!paused && Application.isFocused, "application pause");
        /// <summary>From the web page (Assets/WebGLTemplates/Alibi): "1" when it's visible and focused, else "0".</summary>
        public void PageAttention(string on) => SetAttended(on == "1", "page focus");

        void SetAttended(bool on, string why)
        {
            if (on == Attended) return;
            Attended = on;
            if (on) backFrame = Time.frameCount;
            Debug.Log($"[Focus] {(on ? "attended" : "away")} ({why}){(Session != null ? $", case timer at {Session.Elapsed:0.0}s" : "")}");
            ApplyFrameRate();
        }

        /// <summary>
        /// Frames a second while nobody's looking: plenty for memos typing and music fades, and it
        /// spares a laptop's battery and fans. Sound runs on its own thread either way.
        /// </summary>
        public const int AwayFrameRate = 10;

        /// <summary>
        /// Full speed (vsync, capped at 120) while attended; a slow, unsynced trickle while away.
        /// Automated runs stay at full speed and keep their own pacing (the recorder sets its own).
        /// </summary>
        static void ApplyFrameRate(bool force = false)
        {
            if (TimerIgnoresFocus && !force) return;
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            bool rest = !Attended && !TimerIgnoresFocus;
            // A frame-rate cap only applies with vsync off.
            QualitySettings.vSyncCount = rest ? 0 : 1;
            // In a browser, requestAnimationFrame paces the frames (-1); a cap switches it to a timer.
            Application.targetFrameRate = rest ? AwayFrameRate : web ? -1 : 120;
        }

        public Stage Stage { get; private set; }
        public Screens Screens { get; private set; }
        public Flow Flow { get; private set; } = Flow.Boot;
        public CaseSession Session { get; private set; }
        TitleDecor decor;

        void ShowDecor(bool on)
        {
            if (on && decor == null) decor = TitleDecor.Build(Stage);
            if (!on && decor != null) { Destroy(decor.gameObject); decor = null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("GameRoot");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            I = this;
            ApplyFrameRate(true);
            foreach (var cam in FindObjectsByType<Camera>()) Destroy(cam.gameObject);
            foreach (var l in FindObjectsByType<Light>()) Destroy(l.gameObject);

            var args = Environment.GetCommandLineArgs();
            SaveData.UnlockAll = args.Contains("-alibiUnlockAll");
            Settings.PlainTextFlag = args.Contains("-alibiPlainText");
            int dateArg = Array.IndexOf(args, "-alibiDocketDate");
            if (dateArg >= 0 && dateArg + 1 < args.Length && DateTime.TryParseExact(args[dateArg + 1], "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var docketDate))
            {
                Cases.Today = docketDate;
                Cases.TodayFixed = true;
            }
            int clockArg = Array.IndexOf(args, "-alibiClockAt");
            if (clockArg >= 0 && clockArg + 1 < args.Length && DateTime.TryParseExact(args[clockArg + 1], "yyyy-MM-ddTHH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var clockAt))
            {
                Cases.ClockShift = clockAt - DateTime.Now;
                Cases.Today = Cases.Now.Date;
            }

            AudioDirector.Build();
            UiKit.Init();
            PadCursor.Create(gameObject);
            Stage = Stage.Build();
            int fidArg = Array.IndexOf(args, "-alibiFidelity");
            if (fidArg >= 0 && fidArg + 1 < args.Length && int.TryParse(args[fidArg + 1], out var fid)) Settings.FidelityOverride = fid;
            Fidelity.Apply(Settings.GraphicsFidelity);
            Screens = new Screens(this);
            Settings.Apply();
            AudioDirector.I.StartAmbience();
        }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int caseArg = Array.IndexOf(args, "-alibiCase");
            int autoArg = Array.IndexOf(args, "-alibiAutoplay");
            int capArg = Array.IndexOf(args, "-alibiCapture");
            int inputArg = Array.IndexOf(args, "-alibiInputTest");
            int padArg = Array.IndexOf(args, "-alibiPadTest");
            int midnightArg = Array.IndexOf(args, "-alibiMidnightTest");
            int keysArg = Array.IndexOf(args, "-alibiKeysTest");
            int shareArg = Array.IndexOf(args, "-alibiShareCheck");
            int recordArg = Array.IndexOf(args, "-alibiRecord");
            int focusArg = Array.IndexOf(args, "-alibiFocusTest");
            int touchArg = Array.IndexOf(args, "-alibiTouchTest");
            int boardsArg = Array.IndexOf(args, "-alibiBoardsTest");
            int hintArg = Array.IndexOf(args, "-alibiHintTour");
            int memoArg = Array.IndexOf(args, "-alibiMemoTest");
            int benchArg = Array.IndexOf(args, "-alibiFidelityBench");
            int tourArg = Array.IndexOf(args, "-alibiScreensTour");
            bool automated = inputArg >= 0 || padArg >= 0 || keysArg >= 0 || shareArg >= 0 || midnightArg >= 0 || autoArg >= 0 || capArg >= 0 || recordArg >= 0 || focusArg >= 0 || touchArg >= 0 || boardsArg >= 0 || hintArg >= 0 || memoArg >= 0 || benchArg >= 0 || tourArg >= 0;
            TimerIgnoresFocus = automated && focusArg < 0;
            ApplyFrameRate(true);   // focus may have gone before this was known
            int textArg = Array.IndexOf(args, "-alibiTextSize");
            if (textArg >= 0 && textArg + 1 < args.Length && int.TryParse(args[textArg + 1], out var ts)) Settings.TextSizeOverride = ts;
            // Automated runs don't overwrite the desktop's clipboard (a browser's belongs to the test).
            Clipboard.Private = automated && Application.platform != RuntimePlatform.WebGLPlayer && !args.Contains("-alibiClipboardCheck");
            if (automated) { SaveData.UseVolatile(); Settings.PlainTextForRun(); Fidelity.Apply(Settings.GraphicsFidelity); }
            // A saved resolution choice; automated runs keep the size they were launched with.
            else if (PlayerPrefs.HasKey("resolution") && Application.platform != RuntimePlatform.WebGLPlayer) Settings.ApplyResolution();
            int resArg = Array.IndexOf(args, "-alibiResolution");
            if (resArg >= 0 && resArg + 1 < args.Length) Settings.ApplyResolution(args[resArg + 1]);
            UiKit.ApplyScale();
            yield return null;
            int saveCheckArg = Array.IndexOf(args, "-alibiSaveCheck");
            if (saveCheckArg >= 0)
            {
                string dir = saveCheckArg + 1 < args.Length && !args[saveCheckArg + 1].StartsWith("-") ? args[saveCheckArg + 1] : "Captures/save-check";
                StartCoroutine(SaveCheck(dir));
                yield break;
            }
            if (recordArg >= 0)
            {
                string dir = recordArg + 1 < args.Length && !args[recordArg + 1].StartsWith("-") ? args[recordArg + 1] : "/tmp/alibi-record";
                int casesArg = Array.IndexOf(args, "-alibiRecordCases");
                int maxCases = casesArg >= 0 && casesArg + 1 < args.Length && int.TryParse(args[casesArg + 1], out var n) ? n : 99;
                gameObject.AddComponent<Showcase>().Run(dir, maxCases, args.Contains("-alibiTrailer"));
                yield break;
            }
            if (padArg >= 0)
            {
                string dir = padArg + 1 < args.Length && !args[padArg + 1].StartsWith("-") ? args[padArg + 1] : "Captures/pad-test";
                gameObject.AddComponent<AutoPilot>().Run(dir, true, false, true);
                yield break;
            }
            if (tourArg >= 0)
            {
                string dir = tourArg + 1 < args.Length && !args[tourArg + 1].StartsWith("-") ? args[tourArg + 1] : "Captures/screens-tour";
                gameObject.AddComponent<AutoPilot>().RunScreensTour(dir);
                yield break;
            }
            if (benchArg >= 0)
            {
                string dir = benchArg + 1 < args.Length && !args[benchArg + 1].StartsWith("-") ? args[benchArg + 1] : "Captures/fidelity-bench";
                gameObject.AddComponent<AutoPilot>().RunFidelityBench(dir);
                yield break;
            }
            if (memoArg >= 0)
            {
                string dir = memoArg + 1 < args.Length && !args[memoArg + 1].StartsWith("-") ? args[memoArg + 1] : "Captures/memo-test";
                gameObject.AddComponent<AutoPilot>().RunMemo(dir);
                yield break;
            }
            if (hintArg >= 0)
            {
                string dir = hintArg + 1 < args.Length && !args[hintArg + 1].StartsWith("-") ? args[hintArg + 1] : "Captures/hint-tour";
                gameObject.AddComponent<AutoPilot>().RunHintTour(dir);
                yield break;
            }
            if (shareArg >= 0)
            {
                string dir = shareArg + 1 < args.Length && !args[shareArg + 1].StartsWith("-") ? args[shareArg + 1] : "Captures/share-check";
                gameObject.AddComponent<AutoPilot>().RunShareCheck(dir);
                yield break;
            }
            if (keysArg >= 0)
            {
                string dir = keysArg + 1 < args.Length && !args[keysArg + 1].StartsWith("-") ? args[keysArg + 1] : "Captures/keys-test";
                gameObject.AddComponent<AutoPilot>().Run(dir, true, false, false, true);
                yield break;
            }
            if (touchArg >= 0)
            {
                string dir = touchArg + 1 < args.Length && !args[touchArg + 1].StartsWith("-") ? args[touchArg + 1] : "Captures/touch-test";
                gameObject.AddComponent<AutoPilot>().RunTouch(dir, args.Contains("-alibiTouchReal"));
                yield break;
            }
            if (focusArg >= 0)
            {
                string dir = focusArg + 1 < args.Length && !args[focusArg + 1].StartsWith("-") ? args[focusArg + 1] : "Captures/focus-test";
                gameObject.AddComponent<AutoPilot>().RunFocus(dir, args.Contains("-alibiFocusReal"));
                yield break;
            }
            if (boardsArg >= 0)
            {
                string dir = boardsArg + 1 < args.Length && !args[boardsArg + 1].StartsWith("-") ? args[boardsArg + 1] : "Captures/boards-test";
                gameObject.AddComponent<AutoPilot>().RunBoards(dir);
                yield break;
            }
            if (midnightArg >= 0)
            {
                string dir = midnightArg + 1 < args.Length && !args[midnightArg + 1].StartsWith("-") ? args[midnightArg + 1] : "Captures/midnight-test";
                gameObject.AddComponent<AutoPilot>().RunMidnight(dir);
                yield break;
            }
            if (inputArg >= 0)
            {
                string dir = inputArg + 1 < args.Length && !args[inputArg + 1].StartsWith("-") ? args[inputArg + 1] : "/tmp/alibi-input";
                gameObject.AddComponent<AutoPilot>().Run(dir, true, true);
                yield break;
            }
            if (autoArg >= 0 || capArg >= 0)
            {
                string dir = capArg >= 0 && capArg + 1 < args.Length ? args[capArg + 1] : (autoArg + 1 < args.Length && !args[autoArg + 1].StartsWith("-") ? args[autoArg + 1] : "/tmp/alibi-autoplay");
                gameObject.AddComponent<AutoPilot>().Run(dir, capArg >= 0);
                yield break;
            }
            if (caseArg >= 0 && caseArg + 1 < args.Length)
            {
                var c = Cases.Get(args[caseArg + 1]);
                if (c != null) { StartCase(c, false); yield break; }
            }
            ShowTitle(true);
        }

        /// <summary>
        /// -alibiSaveCheck [dir]: load the save the way a player's launch does, log what came back,
        /// capture the title and the case files, save once and quit. It refuses to run against the
        /// real save folder, so point XDG_CONFIG_HOME at a throwaway folder first.
        /// </summary>
        IEnumerator SaveCheck(string dir)
        {
            // In a browser the save belongs to the page's origin (a test server's origin in
            // Tools/webtest.mjs), so there's nothing to redirect.
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            var home = Environment.GetEnvironmentVariable("HOME") ?? "~";
            var real = System.IO.Path.Combine(home, ".config", "unity3d");
            if (!web && (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")) || SaveData.Folder.StartsWith(real)))
            {
                Debug.LogError("[SaveCheck] FAIL: refusing to run against the real save folder " + SaveData.Folder + " (set XDG_CONFIG_HOME)");
                Quit();
                yield break;
            }
            var save = SaveData.Current;
            var solved = string.Join(",", save.cases.Where(c => c.solved).Select(c => c.id));
            Debug.Log($"[SaveCheck] folder={SaveData.Folder} loadedFrom={SaveData.LoadedFrom} solved=[{solved}] inProgress={save.inProgress?.caseId ?? "none"} shelved=[{string.Join(",", save.shelved.Select(b => $"{b.caseId}:{b.pinned.Count}pins"))}]");
            ShowTitle(true);
            yield return new WaitForSecondsRealtime(3f);
            if (!web) Debug.Log("[SaveCheck] " + DevCapture.Capture(System.IO.Path.Combine(dir, "title.png"), Screen.width, Screen.height));
            ShowSelect();
            yield return new WaitForSecondsRealtime(2f);
            if (!web) Debug.Log("[SaveCheck] " + DevCapture.Capture(System.IO.Path.Combine(dir, "case_files.png"), Screen.width, Screen.height));
            SaveData.Write();
            var files = System.IO.Directory.GetFiles(SaveData.Folder).Select(System.IO.Path.GetFileName).OrderBy(f => f);
            Debug.Log("[SaveCheck] files after one save: " + string.Join(", ", files));
            Debug.Log("[SaveCheck] done");
            Quit();
        }

        float lastAspect, aspectCheck;
        CaseDef introCase;
        public CaseDef IntroCase => introCase;

        /// <summary>The set is laid out for one aspect ratio; if the window changes shape, rebuild it.</summary>
        void CheckAspect()
        {
            if (Application.isBatchMode) return;
            aspectCheck -= Clock.Dt;
            if (aspectCheck > 0) return;
            aspectCheck = 0.5f;
            float a = Stage.LayoutAspect;
            if (lastAspect <= 0) { lastAspect = a; return; }
            if (Mathf.Abs(a - lastAspect) < 0.02f || Flow == Flow.Closing || Flow == Flow.Closed || Flow == Flow.Boot) return;
            lastAspect = a;
            var flow = Flow;
            var c = Session != null ? Session.Case : null;
            EndSession();
            ShowDecor(false);
            Destroy(Stage.gameObject);
            Stage = Stage.Build();
            switch (flow)
            {
                case Flow.Playing: if (c != null) StartCase(c, true); break;
                case Flow.Select: ShowSelect(); break;
                case Flow.Intro: if (introCase != null) ShowIntro(introCase); else ShowSelect(); break;
                default: ShowTitle(false); break;
            }
        }

        bool textSizeDirty;

        /// <summary>Chips, labels and memo slips are sized when the board is built, so a new text size rebuilds it.</summary>
        public void TextSizeChanged() => textSizeDirty = true;

        /// <summary>Once the menus are closed, rebuild the open case's board in place at the new text size.</summary>
        void CheckTextSize()
        {
            if (!textSizeDirty || Paused || Screens.AnyOverlayOpen) return;
            textSizeDirty = false;
            if (Flow != Flow.Playing || Session == null || Session.Solved) return;
            StartCase(Session.Case, true, true);
        }

        float dayCheck;

        /// <summary>The case files (and the docket drawer) roll over to the new day while they're on screen.</summary>
        void CheckDay()
        {
            if (Flow != Flow.Select) return;
            dayCheck -= Time.unscaledDeltaTime;
            if (dayCheck > 0) return;
            dayCheck = 1f;
            if (!Cases.RefreshToday()) return;
            Debug.Log($"[Docket] the day moved on to {Cases.Today:yyyy-MM-dd} with the case files open; redrawing them");
            Screens.RedrawSelect();
        }

        void Update()
        {
            CheckAspect();
            CheckTextSize();
            CheckDay();
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) BackOrPause();
            if (Flow == Flow.Playing && !Paused && Session != null)
            {
                if (kb.f1Key.wasPressedThisFrame || kb.hKey.wasPressedThisFrame) Session.Hint();
                if (kb.tabKey.wasPressedThisFrame) Screens.ToggleNotebook();
                // Page Up / Page Down page through the notebook, whatever is steering.
                var nb = Screens.OpenNotebook;
                if (nb != null && kb.pageDownKey.wasPressedThisFrame) nb.Scroll(0, 1);
                if (nb != null && kb.pageUpKey.wasPressedThisFrame) nb.Scroll(0, -1);
            }
            if (kb.f11Key.wasPressedThisFrame) Settings.Fullscreen = !Settings.Fullscreen;
            if (kb.f12Key.wasPressedThisFrame) Capture(null);
        }

        /// <summary>Esc / Start: close the top overlay, else pause or resume, else back to the title.</summary>
        public void BackOrPause(bool fromPad = false)
        {
            if (Screens.CloseTopOverlay()) return;
            if (Flow == Flow.Playing) SetPaused(!Paused);
            else if (Flow == Flow.Select || Flow == Flow.Intro) ShowTitle(false);
        }

        /// <summary>
        /// Pad B: "back" everywhere except the live board, where it's the right button (send a card
        /// back to the tray). Returns false when the caller should send the right click.
        /// </summary>
        public bool PadBack()
        {
            if (Screens.CloseTopOverlay()) return true;
            if (Flow == Flow.Playing)
            {
                if (Paused) { SetPaused(false); return true; }
                return false;
            }
            if (Flow == Flow.Select || Flow == Flow.Intro) ShowTitle(false);
            return true;
        }

        public void PadNotebook()
        {
            if (Flow == Flow.Playing && !Paused && Session != null) Screens.ToggleNotebook();
        }

        public void PadHint()
        {
            if (Flow == Flow.Playing && !Paused && Session != null && !Screens.NotebookOpen) Session.Hint();
        }

        public static void Capture(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                var dir = System.IO.Path.Combine(Application.persistentDataPath, "Screenshots");
                System.IO.Directory.CreateDirectory(dir);
                path = System.IO.Path.Combine(dir, $"alibi_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            }
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Alibi] screenshot " + path);
        }

        // ------------------------------------------------------------------ flow

        public void ShowTitle(bool first)
        {
            EndSession();
            Flow = Flow.Title;
            SetPaused(false);
            ShowDecor(true);
            Stage.Focus = 1f;
            if (first) { Stage.SetLampInstant(0.05f); Tween.Delay(0.6f, () => { Sfx.Play("lamp_click", 0.9f); Stage.DimLamp(0.85f, 0.25f); }); }
            else Stage.DimLamp(0.85f, 0.6f);
            Screens.ShowTitle();
            AudioDirector.I.PlayMusic("music_title", first ? 4f : 2f);
        }

        public void ShowSelect()
        {
            EndSession();
            Flow = Flow.Select;
            ShowDecor(true);
            Stage.Focus = 1f;
            Stage.DimLamp(0.9f, 0.5f);
            Screens.ShowSelect();
            AudioDirector.I.PlayMusic("music_title", 2f);
        }

        public void ShowIntro(CaseDef c)
        {
            introCase = c;
            ShowDecor(true);
            Flow = Flow.Intro;
            Screens.ShowIntro(c);
        }

        public void StartCase(CaseDef c, bool resume, bool quiet = false)
        {
            EndSession();
            Flow = Flow.Playing;
            SetPaused(false);
            ShowDecor(false);
            Stage.Focus = 0;
            Stage.DimLamp(1f, 0.8f);
            // Every case keeps its own board; starting this one afresh only drops this one's.
            var snap = resume ? SaveData.Current.BoardFor(c.Id) : null;
            if (!resume) SaveData.Current.Drop(c.Id);
            var rec = SaveData.Current.Record(c.Id);
            if (!resume) rec.plays++;
            SaveData.Write();
            Session = CaseSession.Begin(Stage, c, snap, quiet);
            Session.SolvedEvent += OnSolved;
            Screens.ShowHud(Session);
        }

        public void RestartCase()
        {
            if (Session == null) return;
            var c = Session.Case;
            SaveData.Current.Drop(c.Id);
            StartCase(c, false);
        }

        void EndSession()
        {
            if (Session != null)
            {
                if (!Session.Solved) Session.SaveProgress();
                Session.End();
                Session = null;
            }
            Screens?.HideHud();
            Stage.PushIn(0, 0.5f);
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0 : 1;
            if (p) Screens.ShowPause();
            else Screens.HidePause();
            Stage.Focus = p ? 0.8f : (Flow == Flow.Playing ? 0 : 1);
        }

        void OnSolved(CaseSession s)
        {
            Flow = Flow.Closing;
            var rec = SaveData.Current.Record(s.Case.Id);
            bool first = !rec.solved;
            rec.solved = true;
            rec.bestBadges = Mathf.Max(rec.bestBadges, s.Badges);
            rec.bestTime = rec.bestTime <= 0 ? s.Elapsed : Mathf.Min(rec.bestTime, s.Elapsed);
            rec.sealClean |= s.SealClean;
            rec.sealUnaided |= s.SealUnaided;
            rec.sealSwift |= s.SealSwift;
            Debug.Log($"[Seals] {s.Case.Id}: clean={s.SealClean} unaided={s.SealUnaided} swift={s.SealSwift} (time {s.Elapsed:0}s, par {s.Case.ParSeconds}s)");
            SaveData.Current.Drop(s.Case.Id);
            SaveData.Write();
            Screens.HideHud();
            StartCoroutine(Reconstruction.Play(this, s, () =>
            {
                Flow = Flow.Closed;
                Screens.ShowClosed(s, first);
            }));
        }

        public void NextCase(CaseDef after)
        {
            int i = Cases.IndexOf(after.Id);
            if (i + 1 < Cases.All.Count) ShowIntro(Cases.All[i + 1]);
            else ShowSelect();
        }

        public void Quit()
        {
            Session?.SaveProgress();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnApplicationQuit() => Session?.SaveProgress();
    }
}
