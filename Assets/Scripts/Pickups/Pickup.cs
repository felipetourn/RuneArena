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
    // so it can be left for later. Blinks before it disappears.
    public class Pickup : MonoBehaviour
    {
        const float k_BlinkTime = 3f;

        PickupKind m_Kind;
        float m_Amount;
        float m_Radius;
        float m_ExpireAt;
        Vector3 m_Base;
        PlayerHealth m_Health;
        PlayerMana m_Mana;
        Renderer m_Renderer;

        public void Setup(PickupKind kind, float amount, float radius, float lifetime, PlayerHealth health, PlayerMana mana)
        {
            m_Kind = kind;
            m_Amount = amount;
            m_Radius = radius;
            m_ExpireAt = Time.time + lifetime;
            m_Health = health;
            m_Mana = mana;
            m_Base = transform.position;
            m_Renderer = GetComponent<Renderer>();
        }

        void Update()
        {
            float left = m_ExpireAt - Time.time;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = m_Base + Vector3.up * (0.1f * Mathf.Sin(Time.time * 3f));
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
            if (m_Renderer != null)
                m_Renderer.enabled = left > k_BlinkTime || Mathf.Repeat(left, 0.3f) > 0.15f;

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

        bool TooFar()
        {
            Vector3 d = m_Health.Feet - m_Base;
            d.y = 0f;
            return d.sqrMagnitude > m_Radius * m_Radius;
        }
    }
}
