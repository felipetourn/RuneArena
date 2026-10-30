using System;
using System.Collections.Generic;
using RuneArena.Player;
using RuneArena.Spells;
using RuneArena.Waves;
using UnityEngine;

namespace RuneArena.Game
{
    public enum UpgradeTier
    {
        Bronze,
        Silver,
        Gold,
        Diamond,
    }

    public class Upgrade
    {
        public readonly string Key;
        public readonly UpgradeTier Tier;
        public readonly string Title;
        public readonly string Description;
        readonly Action m_Apply;

        public Upgrade(string key, UpgradeTier tier, string title, string description, Action apply)
        {
            Key = key;
            Tier = tier;
            Title = title;
            Description = description;
            m_Apply = apply;
        }

        public void Apply() => m_Apply();
    }

    // Goes on the Game object. After each cleared wave the player picks one of a few upgrades, or skips.
    // Each card rolls its tier: bronze, silver and gold are bigger versions of the same stat upgrades,
    // diamond is a one-off special. Skipping moves odds from bronze to the better tiers (like the rare
    // card offset in Slay the Spire); picking gold or better resets them.
    public class Upgrades : MonoBehaviour
    {
        static readonly float[] k_BaseOdds = { 50f, 35f, 12.5f, 2.5f };
        static readonly float[] k_Power = { 0.10f, 0.20f, 0.35f };
        static readonly float[] k_Cost = { 0.08f, 0.15f, 0.25f };
        static readonly float[] k_Health = { 10f, 20f, 35f };
        static readonly float[] k_Mana = { 8f, 15f, 25f };
        static readonly float[] k_Regen = { 0.10f, 0.20f, 0.35f };
        static readonly float[] k_Speed = { 0.04f, 0.08f, 0.12f };

        [SerializeField] int m_Choices = 3;
        [Tooltip("Odds points moved from bronze to the better tiers each time the player skips.")]
        [SerializeField] float m_SkipShift = 10f;
        [SerializeField] float m_MinBronze = 10f;
        [Tooltip("Mana given right away for skipping, so skipping is a choice and not just a wait.")]
        [SerializeField] float m_SkipMana = 25f;

        readonly float[] m_Odds = (float[])k_BaseOdds.Clone();
        readonly List<Upgrade> m_Candidates = new List<Upgrade>();
        readonly List<Upgrade> m_Offer = new List<Upgrade>();
        readonly HashSet<string> m_TakenSpecials = new HashSet<string>();

        SpellBook m_Book;
        PlayerHealth m_PlayerHealth;
        PlayerMana m_PlayerMana;
        PlayerMovement m_Movement;
        ThreatIndicators m_Threats;

        public float SkipMana => m_SkipMana;

        void Start()
        {
            m_Book = FindAnyObjectByType<SpellBook>(FindObjectsInactive.Include);
            m_PlayerHealth = FindAnyObjectByType<PlayerHealth>();
            m_PlayerMana = FindAnyObjectByType<PlayerMana>();
            m_Movement = FindAnyObjectByType<PlayerMovement>();
        }

        ThreatIndicators Threats => m_Threats != null ? m_Threats : m_Threats = FindAnyObjectByType<ThreatIndicators>();

        public float Odds(UpgradeTier tier) => m_Odds[(int)tier];

        // minimum raises the floor for every card, as for the reward after a stage's boss.
        public IReadOnlyList<Upgrade> Roll(UpgradeTier minimum = UpgradeTier.Bronze)
        {
            m_Offer.Clear();
            for (int i = 0; i < m_Choices; i++)
            {
                Upgrade upgrade = null;
                // A tier with nothing left (all diamonds taken, say) falls back to the one below.
                for (int tier = (int)RollTier(minimum); tier >= 0 && upgrade == null; tier--)
                    upgrade = Pick((UpgradeTier)tier);
                if (upgrade != null)
                    m_Offer.Add(upgrade);
            }
            return m_Offer;
        }

        public void Choose(Upgrade upgrade)
        {
            upgrade.Apply();
            if (upgrade.Tier == UpgradeTier.Diamond)
                m_TakenSpecials.Add(upgrade.Key);
            if (upgrade.Tier >= UpgradeTier.Gold)
                Array.Copy(k_BaseOdds, m_Odds, m_Odds.Length);
        }

        public void Skip()
        {
            if (m_PlayerMana != null)
                m_PlayerMana.Add(m_SkipMana);

            float shift = Mathf.Min(m_SkipShift, m_Odds[0] - m_MinBronze);
            if (shift <= 0f)
                return;
            float better = k_BaseOdds[1] + k_BaseOdds[2] + k_BaseOdds[3];
            m_Odds[0] -= shift;
            for (int i = 1; i < m_Odds.Length; i++)
                m_Odds[i] += shift * k_BaseOdds[i] / better;
        }

        // Tiers below minimum are left out, so the rest keep their odds relative to each other.
        UpgradeTier RollTier(UpgradeTier minimum)
        {
            float total = 0f;
            for (int i = (int)minimum; i < m_Odds.Length; i++)
                total += m_Odds[i];
            float roll = UnityEngine.Random.value * total;
            for (int i = (int)minimum; i < m_Odds.Length; i++)
            {
                roll -= m_Odds[i];
                if (roll < 0f)
                    return (UpgradeTier)i;
            }
            return minimum;
        }

        // A random upgrade of that tier that isn't already on offer.
        Upgrade Pick(UpgradeTier tier)
        {
            m_Candidates.Clear();
            if (tier == UpgradeTier.Diamond)
                AddSpecials();
            else
                AddStats(tier);

            for (int i = m_Candidates.Count - 1; i >= 0; i--)
            {
                foreach (Upgrade offered in m_Offer)
                {
                    if (offered.Key == m_Candidates[i].Key)
                    {
                        m_Candidates.RemoveAt(i);
                        break;
                    }
                }
            }
            return m_Candidates.Count > 0 ? m_Candidates[UnityEngine.Random.Range(0, m_Candidates.Count)] : null;
        }

        void AddStats(UpgradeTier tier)
        {
            int t = (int)tier;

            if (m_Book != null)
            {
                foreach (RuneSpell spell in m_Book.Spells)
                {
                    string label = spell.Label;
                    string name = SpellName(label);
                    if (!m_Book.IsUnlocked(label))
                    {
                        if (tier == UpgradeTier.Gold)
                            Add($"unlock-{label}", tier, $"Nuevo: {name}", $"Desbloquea la runa de {name.ToLower()}.", () => m_Book.Unlock(label));
                        continue;
                    }

                    float power = k_Power[t];
                    string effect = label == "square" ? $"dura {Pct(power)} más" : $"hace {Pct(power)} más daño";
                    Add($"power-{label}", tier, $"{name} +{Pct(power)}", $"{name} {effect}.", () => m_Book.AddPower(label, power));

                    float cost = k_Cost[t];
                    Add($"cost-{label}", tier, $"{name} barata", $"{name} cuesta {Pct(cost)} menos maná.", () => m_Book.ReduceCost(label, cost));
                }
            }

            if (m_PlayerHealth != null)
            {
                float health = k_Health[t];
                Add("health", tier, "Vitalidad", $"+{health:0} de vida máxima, y te cura lo mismo.", () => m_PlayerHealth.AddMaxHealth(health));
            }
            if (m_PlayerMana != null)
            {
                float mana = k_Mana[t];
                float regen = k_Regen[t];
                Add("mana", tier, "Reserva", $"+{mana:0} de maná máxima.", () => m_PlayerMana.AddMax(mana));
                Add("regen", tier, "Flujo", $"La maná se regenera {Pct(regen)} más rápido.", () => m_PlayerMana.AddRegen(regen));
            }
            // An unlock like a new rune, so gold. Only normal has to earn it: easy starts with it, hard never gets it.
            if (tier == UpgradeTier.Gold && GameSession.Difficulty == Difficulty.Normal && Threats != null && !Threats.Active)
                Add("sense", tier, "Sexto sentido", "Unas marcas rojas en el borde de la vista te avisan de los enemigos que no estás mirando.", () => Threats.Active = true);
            if (m_Movement != null)
            {
                float speed = k_Speed[t];
                Add("speed", tier, "Agilidad", $"Te movés {Pct(speed)} más rápido.", () => m_Movement.AddSpeed(speed));
            }
        }

        void AddSpecials()
        {
            if (m_Book != null)
            {
                if (m_Book.IsUnlocked("lightning"))
                    Special("chain", "Cadena", "El rayo directo salta a otro enemigo cercano, con la mitad de daño.", () => m_Book.AddPerk(SpellPerks.ChainLightning));
                if (m_Book.IsUnlocked("circle"))
                    Special("inferno", "Infierno", "La explosión de la bola de fuego es el doble de grande.", () => m_Book.AddPerk(SpellPerks.BigExplosion));
                if (m_Book.IsUnlocked("triangle"))
                    Special("tide", "Marea alta", "La ola es 50% más ancha y llega 50% más lejos.", () => m_Book.AddPerk(SpellPerks.HighTide));
                if (m_Book.IsUnlocked("square"))
                    Special("bastion", "Bastión", "La pared de tierra dura el doble.", () => m_Book.AddPower("square", 1f));
            }
            if (m_PlayerHealth != null)
                Special("second-chance", "Segunda oportunidad", "La próxima vez que caigas, te levantás con la mitad de la vida.", () => m_PlayerHealth.AddExtraLife());
        }

        void Special(string key, string title, string description, Action apply)
        {
            if (!m_TakenSpecials.Contains(key))
                Add(key, UpgradeTier.Diamond, title, description, apply);
        }

        void Add(string key, UpgradeTier tier, string title, string description, Action apply)
        {
            m_Candidates.Add(new Upgrade(key, tier, title, description, apply));
        }

        static string Pct(float fraction) => $"{Mathf.RoundToInt(fraction * 100f)}%";

        static string SpellName(string label)
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
    }
}
