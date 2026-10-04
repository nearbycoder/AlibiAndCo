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
            yield return null;
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

        void Update()
        {
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
