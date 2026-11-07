using UnityEngine;

namespace RuneArena.Enemies
{
    // Fire and ice mages take more from the opposite element and less from their own, like the combos
    // with water: the right spell is rewarded and the hit says so. Water counts against fire: it puts it
    // out, and does little to ice.
    public static class ElementalDamage
    {
        /// <summary>Damage multiplier of a spell on a mage of that element: strong, weak or 1.</summary>
        public static float Multiplier(RangedEnemy.Element mage, string spellLabel, float strong, float weak)
        {
            switch (spellLabel)
            {
                case "circle": return mage == RangedEnemy.Element.Ice ? strong : mage == RangedEnemy.Element.Fire ? weak : 1f;
                case "triangle":
                case "v": return mage == RangedEnemy.Element.Fire ? strong : mage == RangedEnemy.Element.Ice ? weak : 1f;
                default: return 1f;
            }
        }

        /// <summary>How a hit reads, from everything that multiplied its damage.</summary>
        public static DamageKind KindFor(float netMultiplier)
        {
            if (Mathf.Approximately(netMultiplier, 1f))
                return DamageKind.Normal;
            return netMultiplier > 1f ? DamageKind.Combo : DamageKind.Weakened;
        }
    }
}
