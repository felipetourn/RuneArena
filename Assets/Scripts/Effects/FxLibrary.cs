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
    }
}
