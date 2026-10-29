using UnityEngine;

namespace RuneArena.Spells
{
    // Short-lived effect: scales from one size to another and disappears.
    public class SpellVisual : MonoBehaviour
    {
        Vector3 m_From;
        Vector3 m_To;
        float m_Duration;
        float m_Start;

        public static void Spawn(GameObject go, Vector3 from, Vector3 to, float duration)
        {
            var visual = go.AddComponent<SpellVisual>();
            visual.m_From = from;
            visual.m_To = to;
            visual.m_Duration = duration;
            visual.m_Start = Time.time;
            go.transform.localScale = from;
            Destroy(go, duration);
        }

        void Update()
        {
            transform.localScale = Vector3.Lerp(m_From, m_To, (Time.time - m_Start) / m_Duration);
        }
    }
}
