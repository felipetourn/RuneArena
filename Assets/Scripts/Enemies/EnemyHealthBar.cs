using System.Collections.Generic;
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
        Transform m_ArmorRoot;
        Transform m_ArmorFill;
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
            m_Enemy.ArmorHit += OnArmorHit;
            m_Enemy.ArmorBroken += OnArmorBroken;
        }

        void OnDisable()
        {
            m_Enemy.Damaged -= OnDamaged;
            m_Enemy.Healed -= OnHealed;
            m_Enemy.ArmorHit -= OnArmorHit;
            m_Enemy.ArmorBroken -= OnArmorBroken;
        }

        void OnArmorHit(Enemy enemy, float amount)
        {
            if (m_Root == null)
                return;
            DamageNumber.SpawnText(m_Root.position, Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), 2f, new Color(0.7f, 0.7f, 0.75f), m_Head);
        }

        void OnArmorBroken(Enemy enemy)
        {
            if (m_Root == null)
                return;
            if (m_ArmorRoot != null)
                m_ArmorRoot.gameObject.SetActive(false);
            DamageNumber.SpawnText(m_Root.position + Vector3.up * 0.3f, "¡Armadura rota!", 3f, Armor.ColorOf(m_Enemy.Armor), m_Head);
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

            if (m_Enemy.IsArmored)
                BuildArmor();
            // Armored enemies show their armor from the start, so the player knows what to use.
            m_Root.gameObject.SetActive(m_Enemy.AlwaysShowBar || m_Enemy.IsArmored);
        }

        void LateUpdate()
        {
            // Armor given after spawning (the test menu) still gets its bar.
            if (m_Root != null && m_ArmorFill == null && m_Enemy.IsArmored)
            {
                BuildArmor();
                m_Root.gameObject.SetActive(true);
            }
            if (m_Root == null || !m_Root.gameObject.activeSelf)
                return;

            float health = m_Enemy.Health / m_Enemy.MaxHealth;
            // A heal raises the bar past the trail; the trail follows it up right away.
            if (health > m_TrailValue || Time.time >= m_TrailMovesAt)
                m_TrailValue = Mathf.MoveTowards(m_TrailValue, health, k_TrailSpeed * Time.deltaTime);

            Set(m_Fill, health);
            Set(m_Trail, Mathf.Max(m_TrailValue, health));
            if (m_ArmorFill != null && m_Enemy.MaxArmor > 0f)
                Set(m_ArmorFill, m_Enemy.ArmorPoints / m_Enemy.MaxArmor);

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
            bar.localPosition = new Vector3(-m_Width * 0.5f + scale.x * 0.5f, bar.localPosition.y, 0f);
        }

        // A bar of the armor's color over the health bar, with the rune that breaks it drawn at its end.
        void BuildArmor()
        {
            Color color = Armor.ColorOf(m_Enemy.Armor);
            m_ArmorRoot = new GameObject("Armor").transform;
            m_ArmorRoot.SetParent(m_Root, false);
            m_ArmorRoot.localPosition = Vector3.up * (m_Height * 1.4f);

            Part("Armor Background", m_Width, 0.01f, k_Background, m_ArmorRoot);
            m_ArmorFill = Part("Armor Fill", m_Width, 0.02f, color, m_ArmorRoot);

            string label = Armor.Breaker(m_Enemy.Armor);
            List<Vector2> glyph = null;
            foreach (var variant in RuneShapes.CreateVariants())
            {
                if (variant.Key == label)
                {
                    glyph = variant.Value;
                    break;
                }
            }
            if (glyph == null)
                return;

            Vector2 min = glyph[0];
            Vector2 max = glyph[0];
            foreach (Vector2 p in glyph)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 center = (min + max) * 0.5f;
            float size = 0.16f / Mathf.Max(max.x - min.x, max.y - min.y);

            var go = new GameObject("Breaker Rune");
            go.transform.SetParent(m_ArmorRoot, false);
            go.transform.localPosition = new Vector3(m_Width * 0.5f + 0.14f, 0f, 0f);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = 0.015f;
            line.positionCount = glyph.Count;
            for (int i = 0; i < glyph.Count; i++)
            {
                Vector2 p = (glyph[i] - center) * size;
                line.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
            if (s_Material != null)
                line.sharedMaterial = s_Material;
            SetColor(line, color);
        }

        // Thicker parts sit in front of thinner ones from either side.
        Transform Part(string name, float width, float depth, Color color, Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent != null ? parent : m_Root, false);
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
