using System.Collections.Generic;
using UnityEngine;

namespace AlibiCo
{
    /// <summary>Plays a one-shot sound effect by name (variants name_1..n are picked at random).</summary>
    public static class Sfx
    {
        public static void Play(string name, float volume = 1f, float pitch = 1f, float pitchJitter = 0.04f) =>
            AudioDirector.I?.PlaySfx(name, volume, pitch, pitchJitter);
    }

    /// <summary>
    /// Music (two sources, crossfaded), ambience loops and a pool of SFX voices. Music ducks under
    /// memos and big moments. All clips are synthesised offline by Tools/synth_audio.py.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I { get; private set; }

        readonly Dictionary<string, List<AudioClip>> clips = new Dictionary<string, List<AudioClip>>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource musicA, musicB, rain, tick;
        bool aActive = true;
        string currentMusic;
        float duck = 1f, duckTarget = 1f, musicLevel = 1f;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        public static AudioDirector Build()
        {
            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            I = go.AddComponent<AudioDirector>();
            I.Init();
            return I;
        }

        void Init()
        {
            for (int i = 0; i < 14; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0;
                voices.Add(s);
            }
            musicA = MakeLoop();
            musicB = MakeLoop();
            rain = MakeLoop();
            tick = MakeLoop();
            ApplyVolumes();
        }

        AudioSource MakeLoop()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0;
            s.volume = 0;
            return s;
        }

        List<AudioClip> Clips(string name)
        {
            if (clips.TryGetValue(name, out var l)) return l;
            l = new List<AudioClip>();
            var one = Resources.Load<AudioClip>("Audio/" + name);
            if (one != null) l.Add(one);
            for (int i = 1; i <= 6; i++)
            {
                var v = Resources.Load<AudioClip>($"Audio/{name}_{i}");
                if (v == null) break;
                l.Add(v);
            }
            clips[name] = l;
            return l;
        }

        public void PlaySfx(string name, float volume, float pitch, float jitter)
        {
            var l = Clips(name);
            if (l.Count == 0) return;
            // Don't stack the same sound within a couple of frames.
            if (lastPlayed.TryGetValue(name, out var t) && Time.unscaledTime - t < 0.035f) return;
            lastPlayed[name] = Time.unscaledTime;
            AudioSource v = null;
            foreach (var s in voices) if (!s.isPlaying) { v = s; break; }
            if (v == null) v = voices[Random.Range(0, voices.Count)];
            v.clip = l[Random.Range(0, l.Count)];
            v.pitch = pitch * (1 + Random.Range(-jitter, jitter));
            v.volume = volume * Settings.Effects;
            v.Play();
        }

        public void PlayMusic(string name, float fade = 2.0f, float level = 1f)
        {
            musicLevel = level;
            if (currentMusic == name) return;
            currentMusic = name;
            var clip = name != null ? Resources.Load<AudioClip>("Audio/" + name) : null;
            var from = aActive ? musicA : musicB;
            var to = aActive ? musicB : musicA;
            aActive = !aActive;
            to.clip = clip;
            to.volume = 0;
            if (clip != null) to.Play();
            float fromStart = from.volume;
            Tween.Run((this, "music"), fade, k =>
            {
                from.volume = fromStart * (1 - k);
                if (clip != null) to.volume = k * MusicVolume;
            }, Ease.InOutSine, () => { from.Stop(); });
        }

        float MusicVolume => Settings.Music * 0.55f * duck * musicLevel;

        public void StartAmbience()
        {
            var r = Resources.Load<AudioClip>("Audio/amb_rain");
            if (r != null && !rain.isPlaying) { rain.clip = r; rain.Play(); }
            var t = Resources.Load<AudioClip>("Audio/amb_clock");
            if (t != null && !tick.isPlaying) { tick.clip = t; tick.Play(); }
            ApplyVolumes();
        }

        public void Duck(float amount, float hold = 0.6f)
        {
            duckTarget = amount;
            Tween.Delay(hold, () => duckTarget = 1f, (this, "duck"));
        }

        public void ApplyVolumes()
        {
            rain.volume = Settings.Effects * 0.24f;
            tick.volume = Settings.Effects * 0.07f;
        }

        void Update()
        {
            duck = Mathf.Lerp(duck, duckTarget, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5f));
            if (!Tween.Running((this, "music")))
            {
                var active = aActive ? musicA : musicB;
                if (active.clip != null) active.volume = MusicVolume;
            }
        }
    }
}
