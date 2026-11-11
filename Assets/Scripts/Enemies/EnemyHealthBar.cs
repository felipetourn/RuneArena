using System.Collections.Generic;
using RuneArena.Player;
using RuneArena.Strokes;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuneArena.Enemies
{
    // Added by Enemy. A bar over the enemy that shows up after its first hit. The lost part lingers
    // for a moment before draining, tinted by the kind of hit, and every hit pops a damage number.
    // A boss also has a thin bar under it with its fury: full, a special move is coming.
    // Armor sits over the bar as plates of its color, with the rune that breaks it in a badge at the end.
    public class EnemyHealthBar : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static Material s_Material;
        static Sprite s_FrameSprite;
        static Sprite s_FillSprite;
        static Sprite s_PlateSprite;
        static Sprite s_BadgeSprite;

        const float k_BaseWidth = 0.8f;
        const float k_BaseHeight = 0.07f;
        const float k_Margin = 0.3f;
        const float k_TrailHold = 0.35f;
        const float k_TrailSpeed = 1.2f;
        const int k_ArmorPlates = 4;

        // The sprites in Resources/Bars at 100 px per unit: the frame is 64 px tall and its trough
        // starts 12 px in from the tips, the fill is 48 px tall and can't get narrower than its two tips.
        const float k_FrameHeight = 0.64f;
        const float k_FillInset = 0.12f;
        const float k_FillHeight = 0.48f;
        const float k_FillTips = 0.52f;
        const float k_PlateHeight = 0.4f;
        const float k_BadgeWidth = 1.28f;
        const float k_BadgeHeight = 1.12f;

        static readonly Color k_Frame = new Color(0.8f, 0.86f, 1f);
        static readonly Color k_FrameFury = new Color(1f, 0.62f, 0.3f);
        static readonly Color k_Fill = new Color(0.92f, 0.24f, 0.32f);
        static readonly Color k_NormalTrail = new Color(0.96f, 0.82f, 0.66f);
        static readonly Color k_ComboTrail = new Color(1f, 0.85f, 0.1f);
        static readonly Color k_WeakenedTrail = new Color(0.45f, 0.65f, 0.9f);
        static readonly Color k_Fury = new Color(1f, 0.55f, 0.1f);
        static readonly Color k_FuryFull = new Color(1f, 0.9f, 0.4f);
        static readonly Color k_PlateGone = new Color(0.16f, 0.19f, 0.25f);

        // A frame with its fills inside. Width is in world units, Scale is what the sprites are scaled by.
        class Bar
        {
            public Transform Root;
            public SpriteRenderer Frame;
            public float Width;
            public float Scale;
        }

        Enemy m_Enemy;
        Transform m_Head;
        Transform m_Root;
        Bar m_Health;
        SpriteRenderer m_Fill;
        SpriteRenderer m_Trail;
        Transform m_ArmorRoot;
        SpriteRenderer[] m_Plates;
        Color m_PlateColor;
        Bar m_Fury;
        SpriteRenderer m_FuryFill;
        float m_TrailValue = 1f;
        float m_Width = k_BaseWidth;
        float m_Height = k_BaseHeight;
        float m_TrailMovesAt;

        void Awake()
        {
            m_Enemy = GetComponent<Enemy>();
        }

        void OnEnable()
        {
            m_Enemy.Damaged += OnDamaged;
            m_Enemy.Healed += OnHealed;
            m_Enemy.ArmorHit += OnArmorHit;
            m_Enemy.ArmorBroken += OnArmorBroken;
            m_Enemy.Died += OnDied;
        }

        void OnDisable()
        {
            m_Enemy.Damaged -= OnDamaged;
            m_Enemy.Healed -= OnHealed;
            m_Enemy.ArmorHit -= OnArmorHit;
            m_Enemy.ArmorBroken -= OnArmorBroken;
            m_Enemy.Died -= OnDied;
        }

        // Gone as soon as it dies, not when the body does.
        void OnDied(Enemy enemy)
        {
            if (m_Root != null)
                m_Root.gameObject.SetActive(false);
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
            if (s_FrameSprite == null)
            {
                s_FrameSprite = Resources.Load<Sprite>("Bars/bar_frame");
                s_FillSprite = Resources.Load<Sprite>("Bars/bar_fill");
                s_PlateSprite = Resources.Load<Sprite>("Bars/bar_plate");
                s_BadgeSprite = Resources.Load<Sprite>("Bars/bar_badge");
                if (s_FrameSprite == null)
                    Debug.LogError("EnemyHealthBar: the bar sprites are missing from Assets/Resources/Bars.");
            }

            float top = 0f;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y - transform.position.y);

            m_Width = k_BaseWidth * m_Enemy.BarScale;
            m_Height = k_BaseHeight * Mathf.Min(m_Enemy.BarScale, 1.6f);

            m_Root = new GameObject("Health Bar").transform;
            m_Root.SetParent(transform, false);
            m_Root.localPosition = Vector3.up * (top + k_Margin);
            // Sorted as one piece by distance, so a far bar's fill never draws over a near bar's frame.
            m_Root.gameObject.AddComponent<SortingGroup>();

            m_Health = BuildBar("Health", m_Root, Vector3.zero, m_Width, m_Height);
            m_Trail = AddFill(m_Health, "Trail", k_NormalTrail, 1);
            m_Fill = AddFill(m_Health, "Fill", k_Fill, 2);

            // The level goes in a badge left of the bar, with some room between them.
            float badgeScale = m_Height * 2.2f / k_BadgeHeight;
            float badgeX = -m_Width * 0.5f - k_BadgeWidth * badgeScale * 0.5f - m_Height * 0.4f;
            SpriteRenderer levelBadge = Part(m_Root, "Level Badge", s_BadgeSprite, badgeScale, k_Frame, 3);
            levelBadge.drawMode = SpriteDrawMode.Simple;
            levelBadge.transform.localPosition += Vector3.right * badgeX;

            var level = new GameObject("Level");
            level.transform.SetParent(m_Root, false);
            level.transform.localPosition = new Vector3(badgeX, 0f, -0.003f);
            var text = level.AddComponent<TextMeshPro>();
            text.text = m_Enemy.Level.ToString();
            text.fontSize = 1.2f * m_Height / k_BaseHeight;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.sortingOrder = 4;
            text.rectTransform.sizeDelta = new Vector2(k_BadgeWidth, k_BadgeHeight) * badgeScale;

            if (!string.IsNullOrEmpty(m_Enemy.DisplayName))
            {
                var title = new GameObject("Name");
                title.transform.SetParent(m_Root, false);
                // Above the armor plates, when it has them.
                title.transform.localPosition = new Vector3(0f, m_Height * 1.6f + 0.12f, 0f);
                var label = title.AddComponent<TextMeshPro>();
                label.text = m_Enemy.DisplayName;
                label.fontSize = 2.5f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(m_Width, 0.3f);
            }

            if (m_Enemy.IsBoss)
                BuildFury();
            if (m_Enemy.IsArmored)
                BuildArmor();
            // Armored enemies show their armor from the start, so the player knows what to use.
            m_Root.gameObject.SetActive(m_Enemy.AlwaysShowBar || m_Enemy.IsArmored);
        }

        void LateUpdate()
        {
            // Armor given after spawning (the test menu) still gets its plates.
            if (m_Root != null && m_Plates == null && m_Enemy.IsArmored)
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

            SetFill(m_Health, m_Fill, health);
            SetFill(m_Health, m_Trail, Mathf.Max(m_TrailValue, health));
            if (m_FuryFill != null)
            {
                SetFill(m_Fury, m_FuryFill, m_Enemy.Fury);
                // Once full the frame turns orange and the fill blinks, until the move comes out.
                bool full = m_Enemy.Fury >= 1f;
                m_Fury.Frame.color = full ? k_FrameFury : k_Frame;
                m_FuryFill.color = full && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? k_FuryFull : k_Fury;
            }
            if (m_Plates != null && m_Enemy.MaxArmor > 0f)
            {
                // Each plate holds an equal share of the armor and fades out as its share is lost.
                float plates = m_Enemy.ArmorPoints / m_Enemy.MaxArmor * m_Plates.Length;
                for (int i = 0; i < m_Plates.Length; i++)
                    m_Plates[i].color = Color.Lerp(k_PlateGone, m_PlateColor, Mathf.Clamp01(plates - i));
            }

            if (m_Head != null)
                m_Root.rotation = Quaternion.LookRotation(m_Root.position - m_Head.position);
        }

        void OnDamaged(Enemy enemy, float amount, DamageKind kind)
        {
            if (m_Root == null)
                return;

            m_Root.gameObject.SetActive(true);
            m_TrailMovesAt = Time.time + k_TrailHold;
            m_Trail.color = kind == DamageKind.Combo ? k_ComboTrail : kind == DamageKind.Weakened ? k_WeakenedTrail : k_NormalTrail;
            DamageNumber.Spawn(m_Root.position, amount, kind, m_Head);
        }

        void BuildFury()
        {
            float height = m_Height * 0.5f;
            m_Fury = BuildBar("Fury", m_Root, Vector3.down * (m_Height * 0.5f + height * 0.5f + m_Height * 0.12f), m_Width, height);
            m_FuryFill = AddFill(m_Fury, "Fury Fill", k_Fury, 1);
        }

        // A row of plates of the armor's color over the health bar, with the rune that breaks it
        // drawn in a badge at the end.
        void BuildArmor()
        {
            m_PlateColor = Armor.ColorOf(m_Enemy.Armor);
            m_ArmorRoot = new GameObject("Armor").transform;
            m_ArmorRoot.SetParent(m_Root, false);

            float gap = m_Height * 0.12f;
            float height = m_Height * 0.55f;
            float width = (m_Width - gap * (k_ArmorPlates - 1)) / k_ArmorPlates;
            float scale = height / k_PlateHeight;
            float y = m_Height * 0.5f + gap + height * 0.5f;
            m_Plates = new SpriteRenderer[k_ArmorPlates];
            for (int i = 0; i < k_ArmorPlates; i++)
            {
                m_Plates[i] = Part(m_ArmorRoot, $"Plate {i + 1}", s_PlateSprite, scale, m_PlateColor, 0);
                m_Plates[i].size = new Vector2(width / scale, k_PlateHeight);
                m_Plates[i].transform.localPosition = new Vector3(-m_Width * 0.5f + width * 0.5f + i * (width + gap), y, 0f);
            }

            float badgeScale = m_Height * 2.2f / k_BadgeHeight;
            var badgeAt = new Vector3(m_Width * 0.5f + k_BadgeWidth * badgeScale * 0.5f + m_Height * 0.3f, m_Height * 0.4f, 0f);
            SpriteRenderer badge = Part(m_ArmorRoot, "Badge", s_BadgeSprite, badgeScale, m_PlateColor, 3);
            badge.drawMode = SpriteDrawMode.Simple;
            badge.transform.localPosition = badgeAt + badge.transform.localPosition;

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
            float size = m_Height * 1.1f / Mathf.Max(max.x - min.x, max.y - min.y);

            var go = new GameObject("Breaker Rune");
            go.transform.SetParent(m_ArmorRoot, false);
            go.transform.localPosition = badgeAt + Vector3.back * 0.003f;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = m_Height * 0.2f;
            line.positionCount = glyph.Count;
            line.sortingOrder = 4;
            for (int i = 0; i < glyph.Count; i++)
            {
                Vector2 p = (glyph[i] - center) * size;
                line.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
            if (s_Material != null)
                line.sharedMaterial = s_Material;
            var block = new MaterialPropertyBlock();
            block.SetColor(k_BaseColor, m_PlateColor);
            line.SetPropertyBlock(block);
        }

        static Bar BuildBar(string name, Transform parent, Vector3 position, float width, float height)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            var bar = new Bar { Root = root, Width = width, Scale = height / k_FrameHeight };
            bar.Frame = Part(root, "Frame", s_FrameSprite, bar.Scale, k_Frame, 0);
            bar.Frame.size = new Vector2(width / bar.Scale, k_FrameHeight);
            return bar;
        }

        static SpriteRenderer AddFill(Bar bar, string name, Color color, int order)
        {
            SpriteRenderer fill = Part(bar.Root, name, s_FillSprite, bar.Scale, color, order);
            fill.size = new Vector2(k_FillTips, k_FillHeight);
            return fill;
        }

        // Grows from the left of the trough. Sliced sprites can't get narrower than their tips,
        // so below that the fill is squeezed instead.
        static void SetFill(Bar bar, SpriteRenderer fill, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            fill.enabled = fraction > 0f;
            float full = bar.Width / bar.Scale - k_FillInset * 2f;
            float width = full * fraction;
            float drawn = Mathf.Max(width, k_FillTips);
            fill.size = new Vector2(drawn, k_FillHeight);

            Transform t = fill.transform;
            t.localScale = new Vector3(bar.Scale * width / drawn, bar.Scale, 1f);
            t.localPosition = new Vector3((-full + width) * 0.5f * bar.Scale, t.localPosition.y, t.localPosition.z);
        }

        // Sorting order decides what draws on top; the small step toward the viewer only backs it up.
        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, float scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.back * (0.0005f * order);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
