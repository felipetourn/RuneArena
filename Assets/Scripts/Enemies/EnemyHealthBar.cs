using RuneArena.Player;
using RuneArena.Strokes;
using TMPro;
using UnityEngine;

namespace RuneArena.Enemies
{
    // Added by Enemy. A bar over the enemy that shows up after its first hit. The lost part lingers
    // for a moment before draining, tinted by the kind of hit, and every hit pops a damage number.
    public class EnemyHealthBar : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static Material s_Material;

        const float k_BaseWidth = 0.8f;
        const float k_BaseHeight = 0.07f;
        const float k_Margin = 0.3f;
        const float k_TrailHold = 0.35f;
        const float k_TrailSpeed = 1.2f;

        static readonly Color k_Background = new Color(0.1f, 0.1f, 0.1f);
        static readonly Color k_Fill = new Color(0.85f, 0.15f, 0.15f);
        static readonly Color k_NormalTrail = new Color(1f, 1f, 1f);
        static readonly Color k_ComboTrail = new Color(1f, 0.85f, 0.1f);
        static readonly Color k_WeakenedTrail = new Color(0.45f, 0.65f, 0.9f);

        Enemy m_Enemy;
        Transform m_Head;
        Transform m_Root;
        Transform m_Fill;
        Transform m_Trail;
        Renderer m_TrailRenderer;
        MaterialPropertyBlock m_Block;
        float m_TrailValue = 1f;
        float m_Width = k_BaseWidth;
        float m_Height = k_BaseHeight;
        float m_TrailMovesAt;

        void Awake()
        {
            m_Enemy = GetComponent<Enemy>();
            m_Block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            m_Enemy.Damaged += OnDamaged;
            m_Enemy.Healed += OnHealed;
        }

        void OnDisable()
        {
            m_Enemy.Damaged -= OnDamaged;
            m_Enemy.Healed -= OnHealed;
        }

        void OnHealed(Enemy enemy, float amount)
        {
            if (m_Root == null)
                return;
            DamageNumber.SpawnHeal(m_Root.position, amount, m_Head);
        }

        void Start()
        {
            var player = FindAnyObjectByType<PlayerHealth>();
            m_Head = player != null ? player.Head : null;

            if (s_Material == null)
            {
                var recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
                s_Material = recorder != null ? recorder.LineMaterial : null;
            }

            float top = 0f;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y - transform.position.y);

            m_Width = k_BaseWidth * m_Enemy.BarScale;
            m_Height = k_BaseHeight * Mathf.Min(m_Enemy.BarScale, 1.6f);

            m_Root = new GameObject("Health Bar").transform;
            m_Root.SetParent(transform, false);
            m_Root.localPosition = Vector3.up * (top + k_Margin);

            Part("Background", m_Width, 0.01f, k_Background);
            m_Trail = Part("Trail", m_Width, 0.015f, k_NormalTrail);
            m_TrailRenderer = m_Trail.GetComponent<Renderer>();
            m_Fill = Part("Fill", m_Width, 0.02f, k_Fill);

            var level = new GameObject("Level");
            level.transform.SetParent(m_Root, false);
            level.transform.localPosition = new Vector3(-m_Width * 0.5f - 0.12f, 0f, 0f);
            var text = level.AddComponent<TextMeshPro>();
            text.text = m_Enemy.Level.ToString();
            text.fontSize = 1.2f;
            text.alignment = TextAlignmentOptions.Right;
            text.rectTransform.sizeDelta = new Vector2(0.3f, 0.1f);

            if (!string.IsNullOrEmpty(m_Enemy.DisplayName))
            {
                var title = new GameObject("Name");
                title.transform.SetParent(m_Root, false);
                title.transform.localPosition = new Vector3(0f, m_Height + 0.12f, 0f);
                var label = title.AddComponent<TextMeshPro>();
                label.text = m_Enemy.DisplayName;
                label.fontSize = 2.5f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(m_Width, 0.3f);
            }

            m_Root.gameObject.SetActive(m_Enemy.AlwaysShowBar);
        }

        void LateUpdate()
        {
            if (m_Root == null || !m_Root.gameObject.activeSelf)
                return;

            float health = m_Enemy.Health / m_Enemy.MaxHealth;
            // A heal raises the bar past the trail; the trail follows it up right away.
            if (health > m_TrailValue || Time.time >= m_TrailMovesAt)
                m_TrailValue = Mathf.MoveTowards(m_TrailValue, health, k_TrailSpeed * Time.deltaTime);

            Set(m_Fill, health);
            Set(m_Trail, Mathf.Max(m_TrailValue, health));

            if (m_Head != null)
                m_Root.rotation = Quaternion.LookRotation(m_Root.position - m_Head.position);
        }

        void OnDamaged(Enemy enemy, float amount, DamageKind kind)
        {
            if (m_Root == null)
                return;

            m_Root.gameObject.SetActive(true);
            m_TrailMovesAt = Time.time + k_TrailHold;
            SetColor(m_TrailRenderer, kind == DamageKind.Combo ? k_ComboTrail : kind == DamageKind.Weakened ? k_WeakenedTrail : k_NormalTrail);
            DamageNumber.Spawn(m_Root.position, amount, kind, m_Head);
        }

        void Set(Transform bar, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            Vector3 scale = bar.localScale;
            scale.x = Mathf.Max(m_Width * fraction, 0.0001f);
            bar.localScale = scale;
            bar.localPosition = new Vector3(-m_Width * 0.5f + scale.x * 0.5f, 0f, 0f);
        }

        // Thicker parts sit in front of thinner ones from either side.
        Transform Part(string name, float width, float depth, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(m_Root, false);
            go.transform.localScale = new Vector3(width, m_Height, depth);

            var renderer = go.GetComponent<Renderer>();
            if (s_Material != null)
                renderer.sharedMaterial = s_Material;
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
