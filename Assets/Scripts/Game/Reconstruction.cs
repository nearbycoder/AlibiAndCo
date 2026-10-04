using System;
using System.Collections;
using System.Text.RegularExpressions;
using AlibiCo.Logic;
using TMPro;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>
    /// The payoff: the camera leans in on the culprit's line, a red cursor walks the timeline and
    /// Connie's reconstruction is typed out line by line.
    /// </summary>
    public static class Reconstruction
    {
        public static IEnumerator Play(GameRoot root, CaseSession s, Action done)
        {
            s.InputLocked = true;
            var stage = root.Stage;
            var c = s.Case;
            var lane = s.View.LaneById[c.Incident.Culprit];
            AudioDirector.I.PlayMusic("music_reveal", 1.5f);
            stage.SetPushTarget(stage.BoardToWorld(new Vector2((s.View.X0 + s.View.X1) / 2 - 0.5f, (lane.Top + lane.Bottom) / 2)));
            stage.PushIn(0.2f, 1.4f);
            stage.DimLamp(0.75f, 1.2f);

            // Cursor across the culprit's lane.
            var cursorRoot = new GameObject("reconCursor").transform;
            cursorRoot.SetParent(stage.BoardRoot, false);
            var line = Shapes.Quad(cursorRoot, "line", new Vector2(0.05f, lane.Height + 0.3f), Art.Unlit(new Color(0.85f, 0.15f, 0.12f, 0.95f), true), Vector3.zero);
            var glow = Shapes.Quad(cursorRoot, "glow", new Vector2(0.6f, lane.Height + 0.5f), Art.Unlit(new Color(0.9f, 0.2f, 0.1f, 0.25f), true, "shadow"), new Vector3(0, 0, 0.002f));
            cursorRoot.localPosition = new Vector3(s.View.X0, (lane.Top + lane.Bottom) / 2, -0.2f);

            // Caption band.
            var group = UiKit.Rect(UiKit.Root, "Reconstruction");
            group.Stretch();
            var cg = group.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            var band = UiKit.FadeRight(group, "band", new Color(0.025f, 0.022f, 0.03f, 0.93f), 0.62f);
            band.rectTransform.Place(new Vector2(0, 0), new Vector2(0.72f, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, 270));
            var head = UiKit.Text(band.transform, "RECONSTRUCTION", Art.SansBold, 22, Pal.Lamp, TextAlignmentOptions.Left);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(70, -26), new Vector2(-140, 30));
            head.characterSpacing = 8;
            var text = UiKit.Text(band.transform, "", Art.Typewriter, 34, Pal.Hex("F1E6CF"), TextAlignmentOptions.TopLeft);
            text.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(70, 30), new Vector2(-140, -90));
            UiKit.Fade(cg, true, 0.6f);
            var map = s.Map;
            map.Clear();
            map.SetZoom(true, 1.08f, 1.5f, new Vector2(0.985f, 0.03f));
            var stops = c.Itineraries.TryGetValue(c.Incident.Culprit, out var st) ? st : new System.Collections.Generic.List<Stop>();
            string pawnAt = stops.Count > 0 ? stops[0].Location : c.Incident.Location;
            map.PawnAt(pawnAt, true);
            map.Route(c.Incident.Location, c.Incident.Location, Pal.Red);
            yield return Wait(1.0f);

            var timeRx = new Regex(@"^(\d{1,2}):(\d{2})");
            foreach (var l in c.ReconstructionLines)
            {
                var m = timeRx.Match(l);
                if (m.Success)
                {
                    int t = int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
                    var target = new Vector3(s.View.TimeToX(t), cursorRoot.localPosition.y, cursorRoot.localPosition.z);
                    cursorRoot.MoveLocal(target, 0.9f, Ease.InOutCubic);
                    Sfx.Play("tick", 0.5f);
                    // Where was the culprit at this moment (or heading next)?
                    Stop at = null;
                    foreach (var stop in stops) if (stop.From <= t && stop.To >= t) { at = stop; break; }
                    if (at == null) foreach (var stop in stops) if (stop.From > t) { at = stop; break; }
                    if (at != null && at.Location != pawnAt)
                    {
                        map.Route(pawnAt, at.Location, new Color(0.75f, 0.16f, 0.12f, 0.9f), $"{Locations.Map.Minutes(pawnAt, at.Location)} min");
                        pawnAt = at.Location;
                        map.PawnAt(pawnAt);
                        Sfx.Play("footsteps", 0.45f);
                    }
                }
                text.text = l;
                text.maxVisibleCharacters = 0;
                float tt = 0;
                int last = 0;
                while (text.maxVisibleCharacters < l.Length)
                {
                    tt += Clock.Dt;
                    int n = Mathf.FloorToInt(tt * 55f);
                    if (n / 3 != last / 3) Sfx.Play("type", 0.2f, 1f, 0.15f);
                    last = n;
                    text.maxVisibleCharacters = n;
                    if (Clicked()) text.maxVisibleCharacters = l.Length;
                    yield return null;
                }
                float hold = 1.6f + l.Length * 0.025f;
                while (hold > 0 && !Clicked()) { hold -= Clock.Dt; yield return null; }
                yield return null;
            }
            UiKit.Fade(cg, false, 0.5f, () => UnityEngine.Object.Destroy(group.gameObject));
            map.SetZoom(false);
            stage.PushIn(0, 1.0f);
            stage.DimLamp(1f, 1f);
            yield return Wait(0.6f);
            UnityEngine.Object.Destroy(cursorRoot.gameObject);
            done?.Invoke();
        }

        static bool Clicked()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame) || (kb != null && kb.spaceKey.wasPressedThisFrame);
        }

        static IEnumerator Wait(float s)
        {
            while (s > 0) { s -= Clock.Dt; yield return null; }
        }
    }
}
