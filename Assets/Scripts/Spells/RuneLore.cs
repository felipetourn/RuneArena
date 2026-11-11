using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RuneArena.Spells
{
    /// <summary>A combo as the grimoire lists it: with which rune (null when it's about an enemy), whether
    /// it helps or hurts, and what it does.</summary>
    public readonly struct RuneCombo
    {
        public readonly string Partner;
        public readonly bool Good;
        public readonly string Text;

        public RuneCombo(string partner, bool good, string text)
        {
            Partner = partner;
            Good = good;
            Text = text;
        }
    }

    // What the grimoire says about each rune: its name, what it does and its combos. The numbers are read
    // from the spells, so the book follows the balance; the combos themselves live in each spell's code
    // (the wet multipliers, ElementalDamage, Armor.Breaker), so keep these in step when one changes.
    public static class RuneLore
    {
        /// <summary>The spell's name, as the upgrades and the grimoire call it.</summary>
        public static string Name(string label)
        {
            switch (label)
            {
                case "circle": return "Bola de fuego";
                case "lightning": return "Rayo";
                case "triangle": return "Ola";
                case "square": return "Pared";
                case "spiral": return "Hiedra";
                case "v": return "Hielo";
                default: return label;
            }
        }

        /// <summary>One or two lines on what the spell does.</summary>
        public static string Effect(RuneSpell spell)
        {
            switch (spell)
            {
                case FireballSpell _: return "Golpea al primero que toca y explota, empujando a los de alrededor.";
                case LightningSpell _: return "Cae donde apuntás. Sobre un enemigo lo aturde; en el piso daña alrededor.";
                case WaveSpell _: return "Sale de tus pies: empuja, frena y moja todo lo que toca.";
                case EarthWallSpell _: return "Levanta una pared que frena a los enemigos y sus disparos. Los voladores pasan por arriba.";
                case IvySpell ivy: return $"Cubre el piso {Number(ivy.Duration)} s: daña y frena a quien la pisa.";
                case IceSpell ice: return $"Congela {Number(ice.FreezeTime)} s al primero que toca.";
                default: return "";
            }
        }

        /// <summary>Name of the second stat: what the wall's upgrades make longer, the damage for the rest.</summary>
        public static string StatName(RuneSpell spell) => spell is EarthWallSpell ? "Dura" : "Daño";

        /// <summary>The second stat with the upgrades' power applied.</summary>
        public static string Stat(RuneSpell spell, float power)
        {
            switch (spell)
            {
                case FireballSpell fire: return $"{Number(fire.Damage * power)} + {Number(fire.ExplosionDamage * power)} en área";
                case LightningSpell bolt: return $"{Number(bolt.DirectDamage * power)}, en el piso {Number(bolt.FloorDamage * power)}";
                case WaveSpell wave: return Number(wave.Damage * power);
                case EarthWallSpell wall: return $"{Number(wall.Duration * power)} s";
                case IvySpell ivy: return $"{Number(ivy.DamagePerSecond * power)} por segundo";
                case IceSpell ice: return Number(ice.Damage * power);
                default: return "";
            }
        }

        /// <summary>The good combos first, then the bad ones. The book gives the other spells' numbers.</summary>
        public static List<RuneCombo> Combos(RuneSpell spell, SpellBook book)
        {
            var combos = new List<RuneCombo>();
            var fire = book.Find("circle") as FireballSpell;
            var bolt = book.Find("lightning") as LightningSpell;
            var ice = book.Find("v") as IceSpell;
            switch (spell)
            {
                case FireballSpell f:
                    combos.Add(new RuneCombo(null, true, "Rompe la armadura de hielo"));
                    combos.Add(new RuneCombo(null, true, "Fuerte contra magos de hielo"));
                    combos.Add(new RuneCombo("triangle", false, $"-{Percent(1f - f.WetMultiplier)} a mojados"));
                    combos.Add(new RuneCombo("v", false, $"-{Percent(1f - f.WetMultiplier)} a congelados"));
                    combos.Add(new RuneCombo("spiral", false, "Quema la hiedra"));
                    break;
                case LightningSpell l:
                    combos.Add(new RuneCombo("triangle", true, $"+{Percent(l.WetMultiplier - 1f)} a mojados"));
                    combos.Add(new RuneCombo(null, true, "Rompe la armadura de metal"));
                    combos.Add(new RuneCombo("square", false, "Se apaga contra la pared"));
                    break;
                case WaveSpell _:
                    if (bolt != null)
                        combos.Add(new RuneCombo("lightning", true, $"El rayo hace +{Percent(bolt.WetMultiplier - 1f)}"));
                    if (ice != null)
                        combos.Add(new RuneCombo("v", true, $"El hielo congela x{Number(ice.WetMultiplier)}"));
                    combos.Add(new RuneCombo(null, true, "Rompe la armadura de magma"));
                    combos.Add(new RuneCombo(null, true, "Fuerte contra magos de fuego"));
                    if (fire != null)
                        combos.Add(new RuneCombo("circle", false, $"El fuego hace -{Percent(1f - fire.WetMultiplier)}"));
                    break;
                case IceSpell i:
                    combos.Add(new RuneCombo("triangle", true, $"Congela x{Number(i.WetMultiplier)} a mojados"));
                    combos.Add(new RuneCombo(null, true, "Fuerte contra magos de fuego"));
                    if (fire != null)
                        combos.Add(new RuneCombo("circle", false, $"El fuego lo derrite: -{Percent(1f - fire.WetMultiplier)}"));
                    break;
                case IvySpell v:
                    combos.Add(new RuneCombo("square", true, $"Junto a la pared, raíces de {Number(v.RootDamage * book.Power(v.Label))}"));
                    combos.Add(new RuneCombo("circle", false, "El fuego la quema"));
                    break;
                case EarthWallSpell _:
                    combos.Add(new RuneCombo("spiral", true, "La hiedra al lado echa raíces"));
                    combos.Add(new RuneCombo("lightning", false, "El rayo se apaga contra ella"));
                    combos.Add(new RuneCombo(null, false, "También frena tus hechizos"));
                    break;
            }
            return combos;
        }

        /// <summary>With a decimal comma, and only when it has decimals.</summary>
        public static string Number(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        static string Percent(float fraction) => $"{Mathf.RoundToInt(fraction * 100f)}%";
    }
}
