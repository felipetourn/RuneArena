using System.Collections.Generic;
using RuneArena.Enemies;
using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Spells
{
    public readonly struct SpellCast
    {
        // Center of the drawn rune, where the spell starts.
        public readonly Vector3 Origin;
        // Where the player aimed: the right controller in VR, the mouse on desktop.
        public readonly Ray Aim;
        public readonly PlayerHealth Player;
        // Unlit material shared by every spell visual, tinted per spell.
        public readonly Material Material;

        public SpellCast(Vector3 origin, Ray aim, PlayerHealth player, Material material)
        {
            Origin = origin;
            Aim = aim;
            Player = player;
            Material = material;
        }
    }

    // One per rune, on the same object as the RuneCaster. Holds the spell's tuning and spawns its effect.
    public abstract class RuneSpell : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        // Label the recognizer gives the rune that casts this spell.
        public abstract string Label { get; }
        public abstract Color Color { get; }
        public abstract float ManaCost { get; }

        public abstract void Cast(in SpellCast cast);

        protected static int EnemyMask => LayerMask.GetMask("Enemy");
        protected static int FloorMask => LayerMask.GetMask("Floor");
        protected static int WallMask => LayerMask.GetMask("Wall");

        protected static bool IsInLayer(Collider collider, int mask)
        {
            return (mask & (1 << collider.gameObject.layer)) != 0;
        }

        // Primitive without a collider, tinted with the spell color.
        public static GameObject CreateVisual(PrimitiveType type, string name, Material material, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            Tint(go.GetComponent<Renderer>(), material, color);
            return go;
        }

        public static void Tint(Renderer renderer, Material material, Color color)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(k_BaseColor, color);
            renderer.SetPropertyBlock(block);
        }

        // Every enemy with a collider inside the sphere, once each.
        public static void EnemiesInSphere(Vector3 center, float radius, List<Enemy> output)
        {
            output.Clear();
            foreach (Collider c in Physics.OverlapSphere(center, radius, EnemyMask, QueryTriggerInteraction.Ignore))
            {
                Enemy enemy = c.GetComponentInParent<Enemy>();
                if (enemy != null && !enemy.IsDead && !output.Contains(enemy))
                    output.Add(enemy);
            }
        }

        protected static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
