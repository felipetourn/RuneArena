using UnityEngine;

namespace RuneArena.Strokes
{
    // Placeholder flight for a launched rune until each spell has its own effect.
    public class RuneProjectile : MonoBehaviour
    {
        Vector3 m_Velocity;

        public void Launch(Vector3 velocity, float lifetime)
        {
            m_Velocity = velocity;
            Destroy(gameObject, lifetime);
        }

        void Update()
        {
            transform.position += m_Velocity * Time.deltaTime;
        }
    }
}
