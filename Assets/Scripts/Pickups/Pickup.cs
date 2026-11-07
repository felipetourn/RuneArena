using RuneArena.Effects;
using RuneArena.Player;
using UnityEngine;

namespace RuneArena.Pickups
{
    public enum PickupKind
    {
        Health,
        Mana,
    }

    // Floats where an enemy died. Picked up by walking close, but only if the player is missing some of it,
    // so it can be left for later. Near the end it shakes and its rune glows brighter, then it shatters.
    public class Pickup : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        // Meters it shakes and how much brighter its rune gets, at the very end.
        const float k_MaxShake = 0.04f;
        const float k_MaxGlow = 4f;

        PickupKind m_Kind;
        float m_Amount;
        float m_Radius;
        float m_Lifetime;
        float m_Warning;
        float m_ExpireAt;
        Vector3 m_Base;
        PlayerHealth m_Health;
        PlayerMana m_Mana;
        Renderer m_Renderer;
        MaterialPropertyBlock m_Rune;
        Color m_RuneColor;
        Color m_RuneGlow;
        Color m_StoneColor;

        /// <summary>warning is the seconds before it expires during which it shakes and glows.</summary>
        public void Setup(PickupKind kind, float amount, float radius, float lifetime, float warning, PlayerHealth health, PlayerMana mana)
        {
            m_Kind = kind;
            m_Amount = amount;
            m_Radius = radius;
            m_Lifetime = lifetime;
            m_Warning = warning;
            m_ExpireAt = Time.time + lifetime;
            m_Health = health;
            m_Mana = mana;
            m_Base = transform.position;
            m_Renderer = GetComponent<Renderer>();
            if (m_Renderer == null)
                return;

            // The dropper colored the stone (submesh 0) and its rune (submesh 1); the warning builds on those.
            var stone = new MaterialPropertyBlock();
            m_Renderer.GetPropertyBlock(stone, 0);
            m_StoneColor = stone.GetColor(k_BaseColor);
            m_Rune = new MaterialPropertyBlock();
            m_Renderer.GetPropertyBlock(m_Rune, 1);
            m_RuneColor = m_Rune.GetColor(k_BaseColor);
            m_RuneGlow = m_Rune.GetColor(k_EmissionColor);
        }

        /// <summary>0 until the warning starts, rising to 1 when it expires. A stone that lives less than the warning
        /// starts it at once instead of halfway through.</summary>
        public static float WarningProgress(float left, float warning, float lifetime)
        {
            float span = Mathf.Min(warning, lifetime);
            if (span <= 0f)
                return left <= 0f ? 1f : 0f;
            return Mathf.Clamp01(1f - left / span);
        }

        void Update()
        {
            float left = m_ExpireAt - Time.time;
            if (left <= 0f)
            {
                Shatter();
                return;
            }

            float warning = WarningProgress(left, m_Warning, m_Lifetime);
            Vector3 bob = Vector3.up * (0.1f * Mathf.Sin(Time.time * 3f));
            transform.position = m_Base + bob + Random.insideUnitSphere * (k_MaxShake * warning * warning);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
            if (m_Renderer != null && warning > 0f)
            {
                m_Rune.SetColor(k_EmissionColor, m_RuneGlow * (1f + (k_MaxGlow - 1f) * warning));
                m_Renderer.SetPropertyBlock(m_Rune, 1);
            }

            if (m_Health == null || TooFar())
                return;

            if (m_Kind == PickupKind.Health && m_Health.Health < m_Health.MaxHealth)
            {
                m_Health.Heal(m_Amount);
                PickedUp(Sfx.Library != null ? Sfx.Library.HealthPickup : null);
            }
            else if (m_Kind == PickupKind.Mana && m_Mana != null && m_Mana.Mana < m_Mana.MaxMana)
            {
                m_Mana.Add(m_Amount);
                PickedUp(Sfx.Library != null ? Sfx.Library.ManaPickup : null);
            }
        }

        void PickedUp(AudioClip sound)
        {
            Sfx.Play(sound, transform.position, 1f, 0.03f);
            Destroy(gameObject);
        }

        // Stone chips that fly out and fall, and a flash of its rune's color.
        void Shatter()
        {
            Vector3 center = transform.position;
            Sfx.Play(Sfx.Library != null ? Sfx.Library.EarthBreak : null, center, 0.8f, 0.05f);
            Fx.ReleaseAll(gameObject);
            FxLibrary fx = Fx.Library;
            if (fx != null)
            {
                float size = transform.localScale.x;
                Fx.Burst(center, new FxBurst
                {
                    Material = fx.Puff,
                    Color = m_StoneColor,
                    Count = 14,
                    Size = size * 0.3f,
                    Speed = 2.5f,
                    Lifetime = 0.8f,
                    Gravity = 2.5f,
                    Radius = size * 0.3f,
                    Growth = FxGrowth.Shrink,
                });
                Fx.Burst(center, new FxBurst
                {
                    Material = fx.Spark,
                    Color = m_RuneColor,
                    Count = 10,
                    Size = size * 0.15f,
                    Speed = 2f,
                    Lifetime = 0.5f,
                    Gravity = 1f,
                    Radius = size * 0.2f,
                    Stretch = true,
                    Growth = FxGrowth.Shrink,
                });
                Fx.Burst(center, new FxBurst
                {
                    Material = fx.Glow,
                    Color = m_RuneColor,
                    Count = 1,
                    Size = size * 3f,
                    Lifetime = 0.2f,
                    Growth = FxGrowth.Expand,
                });
            }
            Destroy(gameObject);
        }

        bool TooFar()
        {
            Vector3 d = m_Health.Feet - m_Base;
            d.y = 0f;
            return d.sqrMagnitude > m_Radius * m_Radius;
        }
    }
}
