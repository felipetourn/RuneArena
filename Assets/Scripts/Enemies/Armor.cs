using UnityEngine;

namespace RuneArena.Enemies
{
    public enum ArmorType
    {
        None,
        // Broken by fire: fire melts ice.
        Ice,
        // Broken by the wave: water puts fire out.
        Magma,
        // Broken by lightning: metal conducts it.
        Metal,
    }

    public static class Armor
    {
        // Label of the rune whose spell breaks the armor in one hit.
        public static string Breaker(ArmorType type)
        {
            switch (type)
            {
                case ArmorType.Ice: return "circle";
                case ArmorType.Magma: return "triangle";
                case ArmorType.Metal: return "lightning";
                default: return null;
            }
        }

        public static Color ColorOf(ArmorType type)
        {
            switch (type)
            {
                case ArmorType.Ice: return new Color(0.6f, 0.9f, 1f);
                case ArmorType.Magma: return new Color(1f, 0.45f, 0.1f);
                case ArmorType.Metal: return new Color(0.6f, 0.62f, 0.68f);
                default: return Color.white;
            }
        }
    }
}
