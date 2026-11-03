using RuneArena.Effects;
using RuneArena.Player;
using RuneArena.Spells;
using UnityEngine;

namespace RuneArena.Enemies
{
    // One fireball of the dragon boss's fire rain. It goes up from the mouth and out of sight while a ring
    // grows on the floor where it will land, then comes down there and burns whoever is inside the ring.
    public class MeteorStrike : MonoBehaviour
    {
        const float k_Rise = 0.6f;
        const float k_Fall = 0.45f;
        const float k_Height = 14f;
        const float k_BallRadius = 0.35f;

        Vector3 m_From;
        Vector3 m_Target;
        float m_Radius;
        float m_Damage;
        PlayerHealth m_Player;
        float m_StartAt;
        float m_LandAt;
        Transform m_Ball;
        bool m_Falling;

        // delay is the seconds from now until it lands.
        public static void Launch(Vector3 from, Vector3 target, float delay, float radius, float damage, PlayerHealth player, Color color)
        {
            var strike = new GameObject("Meteor Strike").AddComponent<MeteorStrike>();
            strike.m_From = from;
            strike.m_Target = target;
            strike.m_Radius = radius;
            strike.m_Damage = damage;
            strike.m_Player = player;
            strike.m_StartAt = Time.time;
            strike.m_LandAt = Time.time + Mathf.Max(delay, k_Rise + k_Fall);

            // The zone fills out until the moment it lands.
            DangerZone.Show(target, radius, color, strike.m_LandAt - Time.time);
            strike.m_Ball = strike.Ball(from);
        }

        // Two of them: one going up and one coming down, so the trail doesn't streak across the sky between.
        Transform Ball(Vector3 position)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.name = "Fireball";
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * (k_BallRadius * 2f);
            var renderer = ball.GetComponent<Renderer>();
            if (Fx.Library != null && Fx.Library.FireCore != null)
                renderer.sharedMaterial = Fx.Library.FireCore;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SpellFx.FireballTrail(ball.transform, k_BallRadius);
            return ball.transform;
        }

        void Drop(Transform ball)
        {
            Fx.ReleaseAll(ball.gameObject);
            Destroy(ball.gameObject);
        }

        void Update()
        {
            float now = Time.time;
            float fallAt = m_LandAt - k_Fall;
            if (now < m_StartAt + k_Rise)
            {
                float t = (now - m_StartAt) / k_Rise;
                m_Ball.position = m_From + Vector3.up * (k_Height * t * t);
            }
            else if (now < fallAt)
            {
                // Gone up out of sight.
                if (m_Ball != null && !m_Falling)
                {
                    Drop(m_Ball);
                    m_Ball = null;
                }
            }
            else if (now < m_LandAt)
            {
                if (!m_Falling)
                {
                    m_Falling = true;
                    if (m_Ball != null)
                        Drop(m_Ball);
                    m_Ball = Ball(m_Target + Vector3.up * k_Height);
                }
                float t = (now - fallAt) / k_Fall;
                m_Ball.position = m_Target + Vector3.up * (k_Height * (1f - t * t));
            }
            else
            {
                Land();
            }
        }

        void Land()
        {
            SpellFx.FireballExplosion(m_Target, m_Radius);
            if (m_Player != null && !m_Player.IsDead)
            {
                Vector3 feet = m_Player.Feet;
                Vector3 flat = feet - m_Target;
                flat.y = 0f;
                if (flat.magnitude <= m_Radius && m_Player.Head.position.y - m_Target.y < 3f)
                    m_Player.Damage(m_Damage);
            }
            if (m_Ball != null)
                Drop(m_Ball);
            Destroy(gameObject);
        }
    }
}
