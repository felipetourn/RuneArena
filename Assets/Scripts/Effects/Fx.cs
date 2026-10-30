using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Effects
{
    public enum FxGrowth
    {
        // Shrinks away: sparks, sparkles.
        Shrink,
        // Swells a little as it fades: smoke, dust.
        Swell,
        // Starts small and grows to full size: shockwave rings, flashes.
        Expand,
    }

    // What a burst of particles looks like. Speed, size and lifetime are maximums; each particle
    // gets somewhere between about half and all of it.
    public struct FxBurst
    {
        public Material Material;
        public Color Color;
        public int Count;
        public float Size;
        public float Speed;
        public float Lifetime;
        public float Gravity;
        // Radius of the sphere they start in.
        public float Radius;
        // Slows them down over their life; 0 for none.
        public float Drag;
        // Drawn stretched along their velocity, for streaks.
        public bool Stretch;
        // Lying flat on the floor instead of facing the camera.
        public bool Flat;
        public FxGrowth Growth;
    }

    // Particle effects built in code from the materials in the FxLibrary. Kept simple and small, because
    // transparent particles are what the Quest's GPU struggles with most.
    public static class Fx
    {
        static FxLibrary s_Library;
        static bool s_Loaded;
        static ParticleSystem.EmitParams s_Emit;

        public static FxLibrary Library
        {
            get
            {
                if (!s_Loaded)
                {
                    s_Library = Resources.Load<FxLibrary>("FxLibrary");
                    s_Loaded = true;
                    if (s_Library == null)
                        Debug.LogWarning("Fx: no FxLibrary in Resources. Run RuneArena > Create Effects.");
                }
                return s_Library;
            }
        }

        // One-off burst that cleans itself up.
        public static ParticleSystem Burst(Vector3 position, in FxBurst burst)
        {
            if (burst.Material == null || burst.Count <= 0)
                return null;
            ParticleSystem ps = Create("Fx Burst", burst, false);
            ps.transform.position = position;
            ps.Play();
            ps.Emit(burst.Count);
            Object.Destroy(ps.gameObject, burst.Lifetime + 0.1f);
            return ps;
        }

        // Burst spread along a line: count particles per point.
        public static void Along(IReadOnlyList<Vector3> points, in FxBurst burst)
        {
            if (burst.Material == null || points.Count == 0)
                return;
            ParticleSystem ps = Create("Fx Along", burst, false);
            ps.Play();
            int perPoint = Mathf.Max(burst.Count, 1);
            s_Emit = new ParticleSystem.EmitParams();
            foreach (Vector3 p in points)
            {
                for (int k = 0; k < perPoint; k++)
                {
                    s_Emit.position = p + Random.insideUnitSphere * burst.Radius;
                    ps.Emit(s_Emit, 1);
                }
            }
            Object.Destroy(ps.gameObject, burst.Lifetime + 0.1f);
        }

        // Keeps emitting as it moves with parent, perSecond over time and perMeter over distance.
        // Stop it with Release.
        public static ParticleSystem Attach(Transform parent, in FxBurst burst, float perSecond, float perMeter)
        {
            if (burst.Material == null)
                return null;
            ParticleSystem ps = Create("Fx Trail", burst, true);
            ps.transform.SetParent(parent, false);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = perSecond;
            emission.rateOverDistance = perMeter;
            ps.Play();
            return ps;
        }

        // Stops a trail and lets its last particles finish where they are.
        public static void Release(ParticleSystem ps)
        {
            if (ps == null)
                return;
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(ps.gameObject, ps.main.startLifetime.constantMax + 0.1f);
        }

        static ParticleSystem Create(string name, in FxBurst b, bool looping)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = looping;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(b.Lifetime * 0.6f, b.Lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(b.Speed * 0.4f, b.Speed);
            main.startSize = new ParticleSystem.MinMaxCurve(b.Size * 0.6f, b.Size);
            main.startColor = b.Color;
            main.gravityModifier = b.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(b.Count * 4, 64);
            if (!b.Stretch && !b.Flat)
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(b.Radius, 0.001f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            switch (b.Growth)
            {
                case FxGrowth.Shrink:
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
                    break;
                case FxGrowth.Swell:
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
                    break;
                case FxGrowth.Expand:
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
                    break;
            }

            if (b.Drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 100f;
                limit.drag = b.Drag;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = b.Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (b.Stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1.5f;
            }
            else if (b.Flat)
            {
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            }
            return ps;
        }
    }
}
