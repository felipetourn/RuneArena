using UnityEngine;

namespace RuneArena.Effects
{
    // The materials particle effects are made with. Lives in Resources so Fx can find it without
    // references in every scene; RuneArena > Create Effects builds it.
    [CreateAssetMenu(menuName = "RuneArena/Fx Library")]
    public class FxLibrary : ScriptableObject
    {
        [Tooltip("Additive soft dot: glows, sparkles, magic.")]
        public Material Glow;
        [Tooltip("Additive streak, for stretched particles: sparks, embers.")]
        public Material Spark;
        [Tooltip("Alpha-blended cloud: smoke, dust, ash.")]
        public Material Puff;
        [Tooltip("Additive cloud: flames.")]
        public Material Flame;
        [Tooltip("Additive circle, laid flat for shockwaves.")]
        public Material Ring;
        [Tooltip("Additive glow across a line renderer.")]
        public Material Line;
        [Tooltip("Additive 4x4 flipbook of a flame burst.")]
        public Material FireSheet;
        [Tooltip("Alpha-blended 4x4 flipbook of a smoke puff.")]
        public Material SmokeSheet;

        [Header("Spell surfaces")]
        [Tooltip("Glowing core of the fireball.")]
        public Material FireCore;
        [Tooltip("Packed earth for the earth seed.")]
        public Material Earth;
        [Tooltip("Rock slabs of the earth wall.")]
        public Material Rock;
        [Tooltip("Mat of leaves for the ivy patch.")]
        public Material Ivy;
        [Tooltip("Shiny blue ice for the shards.")]
        public Material Ice;
        [Tooltip("See-through painted water for the wave.")]
        public Material Water;
        public Material Vine;
        public Material Thorn;

        // False for a library made by an older version of Create Effects, missing the newer materials.
        public bool IsComplete => FireSheet != null && SmokeSheet != null && FireCore != null && Earth != null && Ivy != null && Ice != null
                                  && Water != null && Vine != null && Thorn != null && Rock != null;
    }
}
