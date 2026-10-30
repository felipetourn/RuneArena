using RuneArena.Effects;
using UnityEngine;

namespace RuneArena.Spells
{
    // The square flying from the rune to the spot on the floor. Raises the wall when it lands.
    public class EarthSeed : MonoBehaviour
    {
        EarthWallSpell m_Spell;
        Vector3 m_Ground;
        Material m_Material;
        float m_Duration;

        public void Launch(EarthWallSpell spell, Vector3 ground, Material material, float duration)
        {
            m_Duration = duration;
            m_Spell = spell;
            m_Ground = ground;
            m_Material = material;
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, m_Ground, m_Spell.TravelSpeed * Time.deltaTime);
            transform.Rotate(0f, 0f, 720f * Time.deltaTime, Space.Self);
            if (transform.position != m_Ground)
                return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            m_Spell.RaiseWall(m_Ground, Quaternion.LookRotation(forward.normalized), m_Material, m_Duration);
            Fx.ReleaseAll(gameObject);
            Destroy(gameObject);
        }
    }
}
