using System.IO;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// Renders the main camera plus the UI canvas into a PNG at any resolution. Used for automated
    /// screenshots from a headless Editor (no Game view) and by the autopilot.
    /// </summary>
    public static class DevCapture
    {
        /// <summary>Editor helper: play the open case to the accusation with the solver's moves.</summary>
        public static string SolveAndAccuse()
        {
            var root = GameRoot.I;
            if (root.Session == null) return "no session";
            root.StartCoroutine(Solve(root.Session));
            return "solving";
        }

        static System.Collections.IEnumerator Solve(CaseSession s)
        {
            for (int guard = 0; guard < 30; guard++)
            {
                foreach (var id in System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(s.Board.TrayCards, x => !x.IsUnknown), x => x.Id)))
                    s.AutoPin(id);
                yield return new WaitForSecondsRealtime(0.6f);
                var path = AlibiCo.Logic.Solver.ShortestSolution(AlibiCo.Logic.Solver.Shadow(s.Board));
                if (path == null || path.Count == 0) break;
                var m = path[0];
                if (m.Kind == "link") { s.AutoPin(m.A); s.AutoPin(m.B); s.AutoLink(m.A, m.B); }
                else s.Confront(s.ViewOf(m.A));
                yield return new WaitForSecondsRealtime(2.5f);
            }
            s.AutoAccuse(s.Case.Incident.Culprit);
        }

        public static string Capture(string path, int width = 1920, int height = 1080)
        {
            var stage = Stage.I;
            if (stage == null) return "no stage";
            var cam = stage.Cam;
            var canvas = UiKit.Canvas;
            var oldMode = canvas.renderMode;
            var oldCam = canvas.worldCamera;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var oldTarget = cam.targetTexture;
            float oldAspect = cam.aspect;
            try
            {
                stage.SuppressDof = true;
                stage.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
                cam.targetTexture = rt;
                cam.aspect = width / (float)height;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                return "saved " + path;
            }
            finally
            {
                stage.SuppressDof = false;
                cam.targetTexture = oldTarget;
                cam.aspect = oldAspect;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCam;
                rt.Release();
                Object.Destroy(rt);
            }
        }
    }
}
