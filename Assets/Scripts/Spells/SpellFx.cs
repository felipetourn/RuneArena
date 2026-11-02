using RuneArena.Effects;
using UnityEngine;

namespace RuneArena.Spells
{
    // The particle effects, lights and sounds of each spell, in one place so they can be tuned together.
    // Everything here is only looks: none of it touches gameplay, and all of it does nothing
    // if the FxLibrary is missing.
    static class SpellFx
    {
        static FxLibrary Lib => Fx.Library;
        static SfxLibrary Sounds => Sfx.Library;

        // Material for a spell's body, or the given fallback (the plain tinted one) without the library.
        public static Material Surface(Material fromLibrary, Material fallback)
        {
            return fromLibrary != null ? fromLibrary : fallback;
        }

        // ---- Fire ----

        public static void FireballTrail(Transform ball, float radius)
        {
            Sound(Sounds?.FireLaunch, ball.position);
            FxLight.Attach(ball, new Color(1f, 0.55f, 0.2f), 5f, 1.6f);
            if (Lib == null)
                return;
            Fx.Attach(ball, new FxBurst
            {
                Material = Lib.FireSheet,
                Color = new Color(1f, 0.8f, 0.6f, 0.95f),
                Count = 60,
                Size = radius * 2.6f,
                Speed = 0.4f,
                Lifetime = 0.4f,
                Radius = radius * 0.5f,
                Animated = true,
                Growth = FxGrowth.Swell,
            }, 60f, 0f);
            Fx.Attach(ball, new FxBurst
            {
                Material = Lib.Spark,
                Color = new Color(1f, 0.7f, 0.3f),
                Count = 40,
                Size = 0.04f,
                Speed = 0.8f,
                Lifetime = 0.6f,
                Gravity = 0.3f,
                Radius = radius * 0.6f,
                Stretch = true,
                Growth = FxGrowth.Shrink,
            }, 0f, 6f);
        }

        public static void FireballExplosion(Vector3 point, float radius)
        {
            Sound(Sounds?.FireExplode, point);
            FxLight.Flash(point, new Color(1f, 0.6f, 0.25f), radius * 6f, 3.5f, 0.4f);
            if (Lib == null)
                return;
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.Glow,
                Color = new Color(1f, 0.75f, 0.4f),
                Count = 1,
                Size = radius * 3f,
                Lifetime = 0.2f,
                Growth = FxGrowth.Expand,
            });
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.FireSheet,
                Color = new Color(1f, 0.75f, 0.5f, 1f),
                Count = 14,
                Size = radius * 2f,
                Speed = radius * 3.5f,
                Lifetime = 0.6f,
                Radius = radius * 0.3f,
                Drag = 5f,
                Animated = true,
                Growth = FxGrowth.Swell,
            });
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.SmokeSheet,
                Color = new Color(0.3f, 0.27f, 0.25f, 0.7f),
                Count = 7,
                Size = radius * 2.2f,
                Speed = radius * 1.2f,
                Lifetime = 1.8f,
                Gravity = -0.08f,
                Radius = radius * 0.4f,
                Drag = 2f,
                Animated = true,
                Growth = FxGrowth.Swell,
            });
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.Spark,
                Color = new Color(1f, 0.65f, 0.25f),
                Count = 22,
                Size = 0.05f,
                Speed = 6f,
                Lifetime = 0.7f,
                Gravity = 1f,
                Radius = 0.1f,
                Drag = 1.5f,
                Stretch = true,
                Growth = FxGrowth.Shrink,
            });
            GroundRing(point, radius * 2.2f, new Color(1f, 0.5f, 0.15f, 0.8f), 0.35f);
        }

        // Something burning away on the floor (ivy under fire).
        public static void BurnPatch(Vector3 center, float radius)
        {
            if (Lib == null)
                return;
            Fx.Burst(center, new FxBurst
            {
                Material = Lib.FireSheet,
                Color = new Color(1f, 0.75f, 0.5f, 1f),
                Count = 16,
                Size = 0.8f,
                Speed = 1.2f,
                Lifetime = 0.7f,
                Gravity = -0.4f,
                Radius = radius * 0.8f,
                Animated = true,
                Growth = FxGrowth.Swell,
            });
            Fx.Burst(center, new FxBurst
            {
                Material = Lib.SmokeSheet,
                Animated = true,
                Color = new Color(0.2f, 0.18f, 0.17f, 0.6f),
                Count = 10,
                Size = 0.7f,
                Speed = 0.6f,
                Lifetime = 1.6f,
                Gravity = -0.12f,
                Radius = radius * 0.7f,
                Growth = FxGrowth.Swell,
            });
        }

        // ---- Lightning ----

        public static void LightningImpact(Vector3 point, Color color, float loudness = 1f)
        {
            Sound(Sounds?.LightningStrike, point, loudness);
            FxLight.Flash(point + Vector3.up, Color.Lerp(color, Color.white, 0.5f), 12f, 5f * loudness, 0.2f);
            if (Lib == null)
                return;
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.Glow,
                Color = Color.Lerp(color, Color.white, 0.6f),
                Count = 1,
                Size = 2.5f,
                Lifetime = 0.18f,
                Growth = FxGrowth.Expand,
            });
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.Spark,
                Color = Color.Lerp(color, Color.white, 0.4f),
                Count = 28,
                Size = 0.04f,
                Speed = 7f,
                Lifetime = 0.45f,
                Gravity = 1.2f,
                Radius = 0.05f,
                Drag = 2f,
                Stretch = true,
                Growth = FxGrowth.Shrink,
            });
        }

        // Line material and colors for a bolt: a white-hot core that tints toward the spell's color.
        public static void StyleBolt(LineRenderer line, Material fallback, Color color, float width)
        {
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (Lib == null || Lib.Line == null)
            {
                RuneSpell.Tint(line, fallback, color);
                line.widthMultiplier = width;
                return;
            }
            line.sharedMaterial = Lib.Line;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = width * 3f;
            Color hot = Color.Lerp(color, Color.white, 0.7f);
            line.startColor = hot;
            line.endColor = hot;
        }

        // ---- Water ----

        // The water itself: a curling wave mesh, as a child of the wave's box so it stretches with it.
        // The box is half a meter deep; the water reaches further back behind its front.
        public static void WaveBody(Transform wave)
        {
            Sound(Sounds?.WaveSplash, wave.position);
            if (Lib == null || Lib.Water == null)
                return;
            var body = new GameObject("Wave Water");
            body.transform.SetParent(wave, false);
            body.transform.localScale = new Vector3(1f, 1.15f, 4f);
            body.transform.localPosition = new Vector3(0f, 0.07f, -1.5f);
            body.AddComponent<MeshFilter>().sharedMesh = SpellMeshes.Wave;
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Lib.Water;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            body.AddComponent<WaveWater>();
        }

        // Foam blown off the lip of the wave, and the splash where it comes down in front.
        // The emitters are children of the box too, so they spread along its width.
        public static void WaveSpray(Transform wave, Color color)
        {
            if (Lib == null)
                return;
            Color foam = Color.Lerp(color, Color.white, 0.8f);
            Spray(wave, new FxBurst
            {
                Material = Lib.Puff,
                Color = foam * new Color(1f, 1f, 1f, 0.8f),
                Count = 60,
                Size = 0.5f,
                Speed = 1.5f,
                Lifetime = 0.6f,
                Gravity = 1.4f,
                Growth = FxGrowth.Swell,
            }, 45f, new Vector3(0f, 0.45f, 0.4f), -60f);
            Spray(wave, new FxBurst
            {
                Material = Lib.Puff,
                Color = foam * new Color(1f, 1f, 1f, 0.7f),
                Count = 40,
                Size = 0.7f,
                Speed = 1f,
                Lifetime = 0.5f,
                Gravity = 1f,
                Growth = FxGrowth.Swell,
            }, 30f, new Vector3(0f, -0.45f, 0.5f), -80f);
        }

        // An emitter along a thin box across the wave at local position, throwing up and forward
        // (pitch -90 is straight up).
        static void Spray(Transform wave, in FxBurst burst, float perSecond, Vector3 position, float pitch)
        {
            ParticleSystem spray = Fx.Attach(wave, burst, perSecond, 0f);
            if (spray == null)
                return;
            // The wave stretches as it travels; the emitter's box follows, the drops keep their size.
            ParticleSystem.MainModule main = spray.main;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            ParticleSystem.ShapeModule shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = position;
            shape.scale = new Vector3(0.9f, 0.05f, 0.1f);
            shape.rotation = new Vector3(pitch, 0f, 0f);
        }

        // ---- Earth ----

        public static void Dust(Vector3 ground, float width, bool sound = true)
        {
            if (sound)
                Sound(Sounds?.EarthRise, ground);
            if (Lib == null)
                return;
            Fx.Burst(ground + Vector3.up * 0.2f, new FxBurst
            {
                Material = Lib.SmokeSheet,
                Animated = true,
                Color = new Color(0.55f, 0.45f, 0.33f, 0.6f),
                Count = 14,
                Size = 0.9f,
                Speed = 1.8f,
                Lifetime = 1.1f,
                Gravity = -0.05f,
                Radius = width * 0.45f,
                Drag = 3f,
                Growth = FxGrowth.Swell,
            });
        }

        // Chunks and dust when a wall breaks.
        public static void Debris(Vector3 center, float width, float height)
        {
            Sound(Sounds?.EarthBreak, center);
            if (Lib == null)
                return;
            Dust(center - Vector3.up * (height * 0.5f), width, false);
            Fx.Burst(center, new FxBurst
            {
                Material = Lib.Puff,
                Color = new Color(0.4f, 0.32f, 0.24f, 1f),
                Count = 24,
                Size = 0.18f,
                Speed = 4f,
                Lifetime = 0.9f,
                Gravity = 2f,
                Radius = width * 0.4f,
                Growth = FxGrowth.Shrink,
            });
        }

        public static void SeedTrail(Transform seed)
        {
            if (Lib == null)
                return;
            Fx.Attach(seed, new FxBurst
            {
                Material = Lib.Puff,
                Color = new Color(0.5f, 0.4f, 0.3f, 0.6f),
                Count = 40,
                Size = 0.25f,
                Speed = 0.2f,
                Lifetime = 0.5f,
                Radius = 0.1f,
                Growth = FxGrowth.Swell,
            }, 0f, 20f);
        }

        // ---- Ivy ----

        public static void Leaves(Vector3 center, float radius, Color color)
        {
            Sound(Sounds?.IvyGrow, center);
            if (Lib == null)
                return;
            Fx.Burst(center + Vector3.up * 0.1f, new FxBurst
            {
                Material = Lib.Puff,
                Color = Color.Lerp(color, new Color(0.25f, 0.5f, 0.15f), 0.5f),
                Count = 20,
                Size = 0.2f,
                Speed = 1.5f,
                Lifetime = 0.8f,
                Gravity = 0.6f,
                Radius = radius * 0.6f,
                Growth = FxGrowth.Shrink,
            });
        }

        // ---- Ice ----

        public static void FrostTrail(Transform shard)
        {
            Sound(Sounds?.IceLaunch, shard.position);
            FxLight.Attach(shard, new Color(0.6f, 0.85f, 1f), 3f, 0.8f);
            if (Lib == null)
                return;
            Fx.Attach(shard, new FxBurst
            {
                Material = Lib.Glow,
                Color = new Color(0.75f, 0.92f, 1f, 0.8f),
                Count = 50,
                Size = 0.08f,
                Speed = 0.2f,
                Lifetime = 0.5f,
                Gravity = 0.15f,
                Radius = 0.05f,
                Growth = FxGrowth.Shrink,
            }, 0f, 25f);
        }

        public static void Shatter(Vector3 point, Color color)
        {
            Sound(Sounds?.IceShatter, point);
            if (Lib == null)
                return;
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.Spark,
                Color = Color.Lerp(color, Color.white, 0.5f),
                Count = 20,
                Size = 0.05f,
                Speed = 4f,
                Lifetime = 0.5f,
                Gravity = 1.5f,
                Radius = 0.08f,
                Drag = 1f,
                Stretch = true,
                Growth = FxGrowth.Shrink,
            });
            Fx.Burst(point, new FxBurst
            {
                Material = Lib.Puff,
                Color = new Color(0.85f, 0.95f, 1f, 0.6f),
                Count = 8,
                Size = 0.5f,
                Speed = 0.8f,
                Lifetime = 0.8f,
                Radius = 0.15f,
                Drag = 2f,
                Growth = FxGrowth.Swell,
            });
        }

        // ---- Shared ----

        static void Sound(AudioClip clip, Vector3 position, float volume = 1f)
        {
            Sfx.Play(clip, position, volume);
        }

        // Flat ring growing on the floor under the point.
        public static void GroundRing(Vector3 point, float size, Color color, float lifetime)
        {
            if (Lib == null)
                return;
            if (!Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, LayerMask.GetMask("Floor"), QueryTriggerInteraction.Ignore))
                return;
            Fx.Burst(hit.point + Vector3.up * 0.03f, new FxBurst
            {
                Material = Lib.Ring,
                Color = color,
                Count = 1,
                Size = size,
                Lifetime = lifetime,
                Flat = true,
                Growth = FxGrowth.Expand,
            });
        }

        // Where a summoned enemy climbs out of the floor: a glowing ring, dust and sparks rising for
        // as long as it takes.
        // size scales it all: 1 for a skeleton, more for a boss.
        public static void Summoning(Vector3 ground, Color color, float duration, float size = 1f)
        {
            GroundRing(ground, 2.2f * size, color, duration);
            Dust(ground, 1.2f * size, false);
            FxLight.Flash(ground + Vector3.up * 0.5f * size, color, 4f * size, 2f, duration);
            if (Lib == null)
                return;
            Fx.Burst(ground + Vector3.up * 0.3f, new FxBurst
            {
                Material = Lib.Glow,
                Color = color,
                Count = Mathf.RoundToInt(18 * size),
                Size = 0.18f * Mathf.Sqrt(size),
                Speed = 2.5f * Mathf.Sqrt(size),
                Lifetime = duration,
                Gravity = -0.3f,
                Radius = 0.5f * size,
                Drag = 1f,
                Growth = FxGrowth.Shrink,
            });
        }
    }
}
