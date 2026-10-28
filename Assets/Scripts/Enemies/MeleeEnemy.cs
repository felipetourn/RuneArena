using UnityEngine;

namespace RuneArena.Enemies
{
    // Walks up to the player and hits after a visible wind-up, so the player can back off in time.
    public class MeleeEnemy : GroundEnemy
    {
        [SerializeField] float m_AttackRange = 1.6f;
        [SerializeField] float m_Damage = 15f;
        [SerializeField] float m_WindUp = 1f;
        [SerializeField] float m_Cooldown = 2f;

        float m_HitAt = -1f;
        float m_ReadyAt;

        protected override void Think(float deltaTime)
        {
            Vector3 feet = Player.Feet;
            float distance = FlatDistance(transform.position, feet);

            if (m_HitAt >= 0f)
            {
                Stop();
                FaceTowards(feet, 360f, deltaTime);
                if (Time.time >= m_HitAt)
                {
                    // Backing off during the wind-up dodges the hit.
                    if (distance <= m_AttackRange * 1.2f)
                        Player.Damage(m_Damage);
                    m_HitAt = -1f;
                    m_ReadyAt = Time.time + m_Cooldown;
                    Telegraphing = false;
                }
                return;
            }

            if (distance <= m_AttackRange)
            {
                Stop();
                FaceTowards(feet, 360f, deltaTime);
                if (Time.time >= m_ReadyAt)
                {
                    m_HitAt = Time.time + m_WindUp;
                    Telegraphing = true;
                }
                return;
            }

            MoveTo(feet);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            m_HitAt = -1f;
            m_ReadyAt = Time.time + m_Cooldown * 0.5f;
        }
    }
}
