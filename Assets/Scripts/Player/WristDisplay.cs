using RuneArena.Strokes;
using TMPro;
using UnityEngine;

namespace RuneArena.Player
{
    // Goes on the left controller: health and mana bars over the wrist, to glance at when needed.
    // In the editor it can also be pinned to the view, since the simulated hand is rarely in sight.
    public class WristDisplay : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        const float k_Width = 0.09f;
        const float k_BarHeight = 0.009f;
        const float k_Gap = 0.006f;

        [SerializeField] Vector3 m_LocalPosition = new Vector3(0f, 0.035f, -0.1f);
        [SerializeField] Vector3 m_LocalRotation = new Vector3(45f, 0f, 0f);
        [SerializeField] bool m_PinToViewInEditor = true;
        [SerializeField] Vector3 m_ViewPosition = new Vector3(-0.13f, -0.09f, 0.4f);

        [Header("Colors")]
        [SerializeField] Color m_Background = new Color(0.1f, 0.1f, 0.1f);
        [SerializeField] Color m_HealthColor = new Color(0.85f, 0.2f, 0.2f);
        [SerializeField] Color m_ManaColor = new Color(0.25f, 0.5f, 1f);
        [Tooltip("Mana bar color while exhausted after running out.")]
        [SerializeField] Color m_ExhaustedColor = new Color(0.45f, 0.35f, 0.6f);

        PlayerHealth m_Health;
        PlayerMana m_Mana;
        Transform m_HealthFill;
        Transform m_ManaFill;
        Renderer m_ManaRenderer;
        TMP_Text m_HealthText;
        TMP_Text m_ManaText;
        MaterialPropertyBlock m_Block;
        bool m_Exhausted;

        void Start()
        {
            m_Health = FindAnyObjectByType<PlayerHealth>();
            m_Mana = FindAnyObjectByType<PlayerMana>();
            m_Block = new MaterialPropertyBlock();

            var recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
            Material material = recorder != null ? recorder.LineMaterial : null;

            var root = new GameObject("Wrist Display").transform;
            if (Application.isEditor && m_PinToViewInEditor && m_Health != null)
            {
                root.SetParent(m_Health.Head, false);
                root.localPosition = m_ViewPosition;
            }
            else
            {
                root.SetParent(transform, false);
                root.localPosition = m_LocalPosition;
                root.localRotation = Quaternion.Euler(m_LocalRotation);
            }

            m_HealthFill = Bar(root, "Health", k_Gap * 0.5f + k_BarHeight * 0.5f, m_HealthColor, material, out _, out m_HealthText);
            m_ManaFill = Bar(root, "Mana", -(k_Gap * 0.5f + k_BarHeight * 0.5f), m_ManaColor, material, out m_ManaRenderer, out m_ManaText);
        }

        void Update()
        {
            if (m_Health != null)
                Fill(m_HealthFill, m_Health.Health / m_Health.MaxHealth, m_HealthText, m_Health.Health);

            if (m_Mana != null && m_Mana.MaxMana > 0f)
            {
                Fill(m_ManaFill, m_Mana.Mana / m_Mana.MaxMana, m_ManaText, m_Mana.Mana);
                if (m_Mana.IsExhausted != m_Exhausted)
                {
                    m_Exhausted = m_Mana.IsExhausted;
                    SetColor(m_ManaRenderer, m_Exhausted ? m_ExhaustedColor : m_ManaColor);
                }
            }
        }

        static void Fill(Transform fill, float fraction, TMP_Text text, float value)
        {
            fraction = Mathf.Clamp01(fraction);
            Vector3 scale = fill.localScale;
            scale.x = Mathf.Max(k_Width * fraction, 0.0001f);
            fill.localScale = scale;
            fill.localPosition = new Vector3(-k_Width * 0.5f + scale.x * 0.5f, fill.localPosition.y, 0f);
            text.text = Mathf.CeilToInt(value).ToString();
        }

        Transform Bar(Transform root, string name, float y, Color color, Material material, out Renderer fillRenderer, out TMP_Text text)
        {
            Transform back = Quad(root, $"{name} Background", new Vector3(0f, y, 0f), new Vector3(k_Width, k_BarHeight, 0.002f), m_Background, material);
            back.SetAsFirstSibling();

            // Thicker than the background so it shows from either side.
            Transform fill = Quad(root, $"{name} Fill", new Vector3(0f, y, 0f), new Vector3(k_Width, k_BarHeight * 0.8f, 0.004f), color, material);
            fillRenderer = fill.GetComponent<Renderer>();

            var label = new GameObject($"{name} Text");
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(k_Width * 0.5f + 0.015f, y, 0f);
            text = label.AddComponent<TextMeshPro>();
            text.fontSize = 0.07f;
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.sizeDelta = new Vector2(0.025f, k_BarHeight * 1.5f);
            return fill;
        }

        Transform Quad(Transform parent, string name, Vector3 position, Vector3 scale, Color color, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetColor(renderer, color);
            return go.transform;
        }

        void SetColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, color);
            renderer.SetPropertyBlock(m_Block);
        }
    }
}
