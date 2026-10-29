using TMPro;
using UnityEngine;

namespace RuneArena.Enemies
{
    // Number that pops over an enemy when it takes damage, rises and fades.
    // Combos are bigger and gold, weakened hits smaller and pale blue.
    public class DamageNumber : MonoBehaviour
    {
        const float k_Lifetime = 0.9f;
        const float k_Rise = 0.7f;

        TextMeshPro m_Text;
        Transform m_Head;
        Vector3 m_Start;
        Color m_Color;
        float m_Born;
        float m_Pop;

        public static void SpawnText(Vector3 position, string text, float size, Color color, Transform head)
        {
            Create(position, head, text, size, color).m_Pop = 0.4f;
        }

        public static void SpawnHeal(Vector3 position, float amount, Transform head)
        {
            DamageNumber number = Create(position, head, $"+{Mathf.Max(1, Mathf.RoundToInt(amount))}", 2.6f, new Color(0.3f, 1f, 0.4f));
            number.m_Pop = 0.2f;
        }

        public static void Spawn(Vector3 position, float amount, DamageKind kind, Transform head)
        {
            var go = new GameObject("Damage Number");
            var number = go.AddComponent<DamageNumber>();
            number.m_Head = head;
            number.m_Start = position + new Vector3(Random.Range(-0.25f, 0.25f), 0.1f, 0f);
            number.m_Born = Time.time;

            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
            text.fontStyle = FontStyles.Bold;
            int value = Mathf.Max(1, Mathf.RoundToInt(amount));

            switch (kind)
            {
                case DamageKind.Combo:
                    text.text = $"{value}!";
                    text.fontSize = 4.5f;
                    number.m_Color = new Color(1f, 0.85f, 0.1f);
                    number.m_Pop = 0.5f;
                    break;
                case DamageKind.Weakened:
                    text.text = value.ToString();
                    text.fontSize = 2.2f;
                    number.m_Color = new Color(0.55f, 0.75f, 1f);
                    break;
                default:
                    text.text = value.ToString();
                    text.fontSize = 3f;
                    number.m_Color = Color.white;
                    break;
            }

            number.m_Text = text;
            text.color = number.m_Color;
            go.transform.position = number.m_Start;
            Destroy(go, k_Lifetime);
        }

        static DamageNumber Create(Vector3 position, Transform head, string value, float size, Color color)
        {
            var go = new GameObject("Damage Number");
            var number = go.AddComponent<DamageNumber>();
            number.m_Head = head;
            number.m_Start = position + new Vector3(Random.Range(-0.25f, 0.25f), 0.1f, 0f);
            number.m_Born = Time.time;
            number.m_Color = color;

            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
            text.fontStyle = FontStyles.Bold;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            number.m_Text = text;

            go.transform.position = number.m_Start;
            Destroy(go, k_Lifetime);
            return number;
        }

        void LateUpdate()
        {
            float t = (Time.time - m_Born) / k_Lifetime;
            transform.position = m_Start + Vector3.up * (k_Rise * Mathf.Sqrt(t));

            // Combos start bigger and settle, so they read as a hit, not just a larger font.
            transform.localScale = Vector3.one * (1f + m_Pop * Mathf.Clamp01(1f - t * 4f));

            Color c = m_Color;
            c.a = t < 0.6f ? 1f : Mathf.Clamp01((1f - t) / 0.4f);
            m_Text.color = c;

            if (m_Head != null)
                transform.rotation = Quaternion.LookRotation(transform.position - m_Head.position);
        }
    }
}
