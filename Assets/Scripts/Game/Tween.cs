using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlibiCo
{
    public enum Ease { Linear, InSine, OutSine, InOutSine, InCubic, OutCubic, InOutCubic, OutQuint, OutBack, OutBackSoft, OutElastic, InBack }

    /// <summary>
    /// The time base for UI, tweens and memos: real (unscaled) time, so they keep moving while the
    /// game is paused. While VideoRecorder runs (Time.captureDeltaTime set) every frame is exactly one
    /// capture step instead, so a recording plays at true speed however fast the machine renders it.
    /// </summary>
    public static class Clock
    {
        static bool Capturing => Time.captureDeltaTime > 0;
        public static float Dt => Capturing ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        public static float Now => Capturing ? Time.frameCount * Time.captureDeltaTime : Time.unscaledTime;
    }

    public static class Easing
    {
        public static float Apply(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.InSine: return 1 - Mathf.Cos(t * Mathf.PI / 2);
                case Ease.OutSine: return Mathf.Sin(t * Mathf.PI / 2);
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1 - Mathf.Pow(1 - t, 3);
                case Ease.InOutCubic: return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                case Ease.OutQuint: return 1 - Mathf.Pow(1 - t, 5);
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
                case Ease.OutBackSoft: { const float c1 = 0.9f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
                case Ease.InBack: { const float c1 = 1.70158f, c3 = c1 + 1; return c3 * t * t * t - c1 * t * t; }
                case Ease.OutElastic:
                {
                    if (t <= 0 || t >= 1) return t;
                    const float c4 = 2 * Mathf.PI / 3;
                    return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * c4) + 1;
                }
                default: return t;
            }
        }
    }

    /// <summary>
    /// Tiny tween runner. Tweens keyed by an owner object replace each other, so re-triggering an
    /// animation on the same thing never fights the previous one.
    /// </summary>
    public sealed class Tween : MonoBehaviour
    {
        sealed class Job
        {
            public object Key;
            public float Delay, Duration, T;
            public Ease Ease;
            public Action<float> Step;
            public Action Done;
            public bool Unscaled;
            public bool Dead;
        }

        static Tween instance;
        readonly List<Job> jobs = new List<Job>();
        readonly List<Job> adding = new List<Job>();

        static Tween I
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[Tween]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Tween>();
                }
                return instance;
            }
        }

        public static void Run(object key, float duration, Action<float> step, Ease ease = Ease.OutCubic,
            Action done = null, float delay = 0, bool unscaled = true)
        {
            if (key != null) Kill(key);
            var j = new Job { Key = key, Duration = Mathf.Max(0.0001f, duration), Step = step, Ease = ease, Done = done, Delay = delay, Unscaled = unscaled };
            I.adding.Add(j);
        }

        public static void Delay(float seconds, Action action, object key = null) =>
            Run(key, 0.0001f, null, Ease.Linear, action, seconds);

        public static void Kill(object key)
        {
            if (instance == null || key == null) return;
            foreach (var j in instance.jobs) if (Equals(j.Key, key)) j.Dead = true;
            foreach (var j in instance.adding) if (Equals(j.Key, key)) j.Dead = true;
        }

        public static bool Running(object key)
        {
            if (instance == null || key == null) return false;
            foreach (var j in instance.jobs) if (!j.Dead && Equals(j.Key, key)) return true;
            foreach (var j in instance.adding) if (!j.Dead && Equals(j.Key, key)) return true;
            return false;
        }

        void Update()
        {
            if (adding.Count > 0) { jobs.AddRange(adding); adding.Clear(); }
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (j.Dead) continue;
                float dt = j.Unscaled ? Clock.Dt : Time.deltaTime;
                if (j.Delay > 0) { j.Delay -= dt; if (j.Delay > 0) continue; }
                j.T += dt / j.Duration;
                float k = Easing.Apply(j.Ease, j.T);
                try { j.Step?.Invoke(j.T >= 1 ? Easing.Apply(j.Ease, 1) : k); }
                catch (Exception e) { Debug.LogException(e); j.Dead = true; continue; }
                if (j.T >= 1)
                {
                    j.Dead = true;
                    try { j.Done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
            jobs.RemoveAll(x => x.Dead);
        }
    }

    public static class TweenExt
    {
        public static void MoveLocal(this Transform t, Vector3 to, float dur, Ease ease = Ease.OutCubic, Action done = null, float delay = 0)
        {
            var from = t.localPosition;
            Tween.Run((t, "pos"), dur, k => { if (t) t.localPosition = Vector3.LerpUnclamped(from, to, k); }, ease, done, delay);
        }

        public static void MoveWorld(this Transform t, Vector3 to, float dur, Ease ease = Ease.OutCubic, Action done = null, float delay = 0)
        {
            var from = t.position;
            Tween.Run((t, "pos"), dur, k => { if (t) t.position = Vector3.LerpUnclamped(from, to, k); }, ease, done, delay);
        }

        public static void ScaleTo(this Transform t, Vector3 to, float dur, Ease ease = Ease.OutCubic, Action done = null, float delay = 0)
        {
            var from = t.localScale;
            Tween.Run((t, "scale"), dur, k => { if (t) t.localScale = Vector3.LerpUnclamped(from, to, k); }, ease, done, delay);
        }

        public static void RotateLocal(this Transform t, Quaternion to, float dur, Ease ease = Ease.OutCubic, Action done = null, float delay = 0)
        {
            var from = t.localRotation;
            Tween.Run((t, "rot"), dur, k => { if (t) t.localRotation = Quaternion.SlerpUnclamped(from, to, k); }, ease, done, delay);
        }

        /// <summary>A quick scale punch (overshoot and settle).</summary>
        /// <summary>
        /// A quick scale pop. Pass <paramref name="rest"/> when another tween may be resizing the
        /// object at the same moment: the pop then settles on that scale, not on whatever in-between
        /// size it caught when it started.
        /// </summary>
        public static void Punch(this Transform t, float amount = 0.15f, float dur = 0.35f, Vector3? rest = null)
        {
            var baseScale = rest ?? t.localScale;
            Tween.Run((t, "punch"), dur, k =>
            {
                if (!t) return;
                float s = 1 + amount * Mathf.Sin(k * Mathf.PI) * (1 - k);
                t.localScale = baseScale * s;
            }, Ease.Linear, () => { if (t) t.localScale = baseScale; });
        }

        /// <summary>Shake in the local XY plane, decaying.</summary>
        public static void Shake(this Transform t, float amount = 0.02f, float dur = 0.4f, float freq = 38f)
        {
            var basePos = t.localPosition;
            float seed = UnityEngine.Random.value * 100;
            Tween.Run((t, "shake"), dur, k =>
            {
                if (!t) return;
                float a = amount * (1 - k);
                t.localPosition = basePos + new Vector3(
                    Mathf.Sin((seed + k * dur) * freq) * a,
                    Mathf.Cos((seed * 1.7f + k * dur) * freq * 1.13f) * a * 0.6f, 0);
            }, Ease.Linear, () => { if (t) t.localPosition = basePos; });
        }
    }
}
