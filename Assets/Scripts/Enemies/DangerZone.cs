using RuneArena.Spells;
using RuneArena.Strokes;
using UnityEngine;

namespace RuneArena.Enemies
{
    // A spot about to be hit, marked on the floor until it is: its edge the full size from the start, and a
    // disc inside that grows to fill it right as the hit lands. Clearer than the particle ring, which starts
    // small and fades out as it grows.
    public class DangerZone : MonoBehaviour
    {
        const int k_Segments = 48;
        const float k_EdgeWidth = 0.05f;

        static Material s_Material;

        Transform m_Fill;
        float m_Diameter;
        float m_Start;
        float m_Duration;

        public static void Show(Vector3 point, float radius, Color color, float duration)
        {
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, LayerMask.GetMask("Floor"), QueryTriggerInteraction.Ignore))
                point = hit.point;

            var zone = new GameObject("Danger Zone").AddComponent<DangerZone>();
            zone.transform.position = point + Vector3.up * 0.02f;
            zone.m_Diameter = radius * 2f;
            zone.m_Start = Time.time;
            zone.m_Duration = Mathf.Max(duration, 0.01f);

            // Flat on the floor: the line faces its own z, which the rotation points up.
            var edge = new GameObject("Edge");
            edge.transform.SetParent(zone.transform, false);
            edge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var line = edge.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = k_EdgeWidth;
            line.positionCount = k_Segments;
            for (int i = 0; i < k_Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / k_Segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
            RuneSpell.Tint(line, LineMaterial(), color);

            GameObject fill = RuneSpell.CreateVisual(PrimitiveType.Cylinder, "Fill", LineMaterial(), color * 0.6f);
            fill.transform.SetParent(zone.transform, false);
            zone.m_Fill = fill.transform;
            zone.Grow();

            Destroy(zone.gameObject, duration);
        }

        void Update()
        {
            Grow();
        }

        void Grow()
        {
            float size = m_Diameter * Mathf.Clamp01((Time.time - m_Start) / m_Duration);
            m_Fill.localScale = new Vector3(size, 0.005f, size);
        }

        static Material LineMaterial()
        {
            if (s_Material == null)
            {
                var recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
                s_Material = recorder != null ? recorder.LineMaterial : null;
            }
            return s_Material;
        }
    }
}
