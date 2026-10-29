using UnityEngine;

namespace RuneArena.Spells
{
    // Jagged line from the sky to the target, redrawn every frame so it flickers.
    public class LightningBolt : MonoBehaviour
    {
        const int k_Points = 10;
        const float k_Jitter = 0.4f;

        LineRenderer m_Line;
        Vector3 m_From;
        Vector3 m_To;

        public void Strike(Vector3 from, Vector3 to, float duration)
        {
            m_Line = GetComponent<LineRenderer>();
            m_Line.positionCount = k_Points;
            m_From = from;
            m_To = to;
            Redraw();
            Destroy(gameObject, duration);
        }

        void Update()
        {
            Redraw();
        }

        void Redraw()
        {
            for (int i = 0; i < k_Points; i++)
            {
                float t = i / (k_Points - 1f);
                Vector3 p = Vector3.Lerp(m_From, m_To, t);
                if (i > 0 && i < k_Points - 1)
                    p += new Vector3(Random.Range(-k_Jitter, k_Jitter), 0f, Random.Range(-k_Jitter, k_Jitter));
                m_Line.SetPosition(i, p);
            }
        }
    }
}
