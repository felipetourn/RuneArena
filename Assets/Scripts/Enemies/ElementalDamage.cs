using UnityEngine;

namespace RuneArena.Enemies
{
    // Fire and ice mages take more from the opposite element and less from their own, like the combos
    // with water: the right spell is rewarded and the hit says so.
    public static class ElementalDamage
    {
        const string k_Fireball = "circle";
        const string k_Ice = "v";

        /// <summary>Damage multiplier of a spell on a mage of that element: strong, weak or 1.</summary>
        public static float Multiplier(RangedEnemy.Element mage, string spellLabel, float strong, float weak)
        {
            if (spellLabel == k_Fireball)
                return mage == RangedEnemy.Element.Ice ? strong : mage == RangedEnemy.Element.Fire ? weak : 1f;
            if (spellLabel == k_Ice)
                return mage == RangedEnemy.Element.Fire ? strong : mage == RangedEnemy.Element.Ice ? weak : 1f;
            return 1f;
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
