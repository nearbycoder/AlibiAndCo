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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Paused = false; Time.timeScale = 1; }   // see Art.ResetStatics

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
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            foreach (var cam in FindObjectsByType<Camera>()) Destroy(cam.gameObject);
            foreach (var l in FindObjectsByType<Light>()) Destroy(l.gameObject);

            var args = Environment.GetCommandLineArgs();
            SaveData.UnlockAll = args.Contains("-alibiUnlockAll");

            AudioDirector.Build();
            UiKit.Init();
            Stage = Stage.Build();
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
            int recordArg = Array.IndexOf(args, "-alibiRecord");
            bool automated = inputArg >= 0 || autoArg >= 0 || capArg >= 0 || recordArg >= 0;
            int textArg = Array.IndexOf(args, "-alibiTextSize");
            if (textArg >= 0 && textArg + 1 < args.Length && int.TryParse(args[textArg + 1], out var ts)) Settings.TextSizeOverride = ts;
            if (automated) SaveData.UseVolatile();
            // A saved resolution choice; automated runs keep the size they were launched with.
            else if (PlayerPrefs.HasKey("resolution")) Settings.ApplyResolution();
            int resArg = Array.IndexOf(args, "-alibiResolution");
            if (resArg >= 0 && resArg + 1 < args.Length) Settings.ApplyResolution(args[resArg + 1]);
            UiKit.ApplyScale();
            yield return null;
            if (recordArg >= 0)
            {
                string dir = recordArg + 1 < args.Length && !args[recordArg + 1].StartsWith("-") ? args[recordArg + 1] : "/tmp/alibi-record";
                int casesArg = Array.IndexOf(args, "-alibiRecordCases");
                int maxCases = casesArg >= 0 && casesArg + 1 < args.Length && int.TryParse(args[casesArg + 1], out var n) ? n : 99;
                gameObject.AddComponent<Showcase>().Run(dir, maxCases, args.Contains("-alibiTrailer"));
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

        float lastAspect, aspectCheck;
        CaseDef introCase;

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

        void Update()
        {
            CheckAspect();
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (Screens.CloseTopOverlay()) return;
                if (Flow == Flow.Playing) SetPaused(!Paused);
                else if (Flow == Flow.Select || Flow == Flow.Intro) ShowTitle(false);
            }
            if (Flow == Flow.Playing && !Paused && Session != null)
            {
                if (kb.f1Key.wasPressedThisFrame || kb.hKey.wasPressedThisFrame) Session.Hint();
                if (kb.tabKey.wasPressedThisFrame) Screens.ToggleNotebook();
            }
            if (kb.f11Key.wasPressedThisFrame) Settings.Fullscreen = !Settings.Fullscreen;
            if (kb.f12Key.wasPressedThisFrame) Capture(null);
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

        public void StartCase(CaseDef c, bool resume)
        {
            EndSession();
            Flow = Flow.Playing;
            SetPaused(false);
            ShowDecor(false);
            Stage.Focus = 0;
            Stage.DimLamp(1f, 0.8f);
            var snap = resume ? SaveData.Current.inProgress : null;
            if (!resume && SaveData.Current.inProgress != null && SaveData.Current.inProgress.caseId == c.Id) SaveData.Current.inProgress = null;
            var rec = SaveData.Current.Record(c.Id);
            if (!resume) rec.plays++;
            SaveData.Write();
            Session = CaseSession.Begin(Stage, c, snap);
            Session.SolvedEvent += OnSolved;
            Screens.ShowHud(Session);
        }

        public void RestartCase()
        {
            if (Session == null) return;
            var c = Session.Case;
            SaveData.Current.inProgress = null;
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
            SaveData.Current.inProgress = null;
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
