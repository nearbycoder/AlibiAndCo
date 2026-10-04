using UnityEngine;

namespace AlibiCo
{
    /// <summary>One-shot particle bursts: cork dust under a pin, ink from a stamp, shards from a lock.</summary>
    public static class Fx
    {
        static Material dustMat, inkMat, sparkMat;

        static ParticleSystem Burst(string name, Vector3 pos, Material mat, int count, float speed, float size, float life, Color color, float gravity = 0, float spread = 1f)
        {
            if (Settings.ReducedMotion) count = Mathf.Max(1, count / 3);
            var go = new GameObject("fx_" + name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.15f * spread;
            sh.rotation = new Vector3(90, 0, 0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0.4f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        public static void Dust(Vector3 pos)
        {
            if (dustMat == null) dustMat = Art.Unlit(new Color(0.85f, 0.7f, 0.5f, 0.7f), true, "dot");
            Burst("dust", pos + Vector3.up * 0.2f, dustMat, 14, 1.4f, 0.12f, 0.6f, new Color(0.9f, 0.75f, 0.55f, 0.8f), -0.1f, 2f);
        }

        public static void Ink(Vector3 pos)
        {
            if (inkMat == null) inkMat = Art.Unlit(new Color(0.65f, 0.1f, 0.12f, 0.9f), true, "dot");
            Burst("ink", pos + Vector3.up * 0.4f, inkMat, 18, 2.2f, 0.09f, 0.45f, new Color(0.7f, 0.12f, 0.14f, 1f), 0.6f, 1.5f);
        }

        public static void Sparks(Vector3 pos, Color c)
        {
            if (sparkMat == null) sparkMat = Art.Unlit(Color.white, true, "dot");
            Burst("sparks", pos + Vector3.up * 0.4f, sparkMat, 26, 3.2f, 0.1f, 0.7f, c, 0.3f, 1f);
        }
    }
}
