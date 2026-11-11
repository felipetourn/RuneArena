using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuneArena.Player
{
    // Goes on the left controller, with the WristDisplay: the health and mana bars, lying along the bracer
    // so they stand upright when the hand is raised. They fill from the elbow toward the hand, with a notch
    // every so many points and the number at the elbow's end. Create Hand Props adds it along with the bracer.
    public class WristBars : MonoBehaviour
    {
        // The sprites in Resources/Bars at 100 px per unit. Keep them the same as EnemyHealthBar's.
        const float k_FrameHeight = 0.64f;
        const float k_FillInset = 0.12f;
        const float k_FillHeight = 0.48f;
        const float k_FillTips = 0.52f;

        // The bracer made by Create Hand Props: a band around the forearm, its axis this far under the
        // controller. Keep them the same as HandPropsBuilder's.
        const float k_BracerRadius = 0.042f;
        const float k_BracerHeight = -0.02f;
        // Just off the leather. The bars and their numbers stay between the bracer's rims, which stand 3 mm
        // proud of it, so they can lie this close.
        const float k_Lift = 0.001f;

        [Tooltip("The bracer the bars lie on, so they take its scale. Create Hand Props fills it. On the controller itself if empty.")]
        [SerializeField] Transform m_Bracer;
        [Tooltip("Middle of the bars along the forearm, from the controller, at the bracer's own size. Its leather runs from -0.082 to -0.198, between the rims; the number goes past the elbow's end of the bar.")]
        [SerializeField] float m_Along = -0.137f;
        [SerializeField] float m_Length = 0.09f;
        [SerializeField] float m_Thickness = 0.013f;
        [Tooltip("Degrees each bar sits around the forearm from its top: health to the left, mana to the right.")]
        [SerializeField] float m_Spread = 17f;
        [Tooltip("Points between notches.")]
        [SerializeField] float m_NotchEvery = 20f;
        [Tooltip("Plain Cinzel for the numbers. TextMeshPro's default font if empty.")]
        [SerializeField] TMP_FontAsset m_Font;

        [Header("Colors")]
        [SerializeField] Color m_FrameColor = new Color(0.89f, 0.78f, 0.48f);
        [Tooltip("Green, like the rune on the health pickups.")]
        [SerializeField] Color m_HealthColor = new Color(0.3f, 0.85f, 0.45f);
        [Tooltip("Health and its frame with a notch or less left.")]
        [SerializeField] Color m_CriticalColor = new Color(1f, 0.35f, 0.27f);
        [SerializeField] Color m_ManaColor = new Color(0.3f, 0.72f, 0.91f);
        [Tooltip("Mana bar color while exhausted after running out.")]
        [SerializeField] Color m_ExhaustedColor = new Color(0.55f, 0.45f, 0.75f);
        [SerializeField] Color m_NotchColor = new Color(0.07f, 0.05f, 0.04f);
        [SerializeField] Color m_TextColor = new Color(1f, 0.91f, 0.76f);

        static Sprite s_FrameSprite;
        static Sprite s_FillSprite;
        static Sprite s_White;

        // A frame with its fill inside. Scale is what the sprites are scaled by, Max what the notches were
        // last laid out for.
        class Bar
        {
            public SpriteRenderer Frame;
            public SpriteRenderer Fill;
            public Transform Notches;
            public TMP_Text Text;
            public float Scale;
            public float Max;
        }

        PlayerHealth m_Health;
        PlayerMana m_Mana;
        Bar m_HealthBar;
        Bar m_ManaBar;

        void Start()
        {
            m_Health = FindAnyObjectByType<PlayerHealth>();
            m_Mana = FindAnyObjectByType<PlayerMana>();
            if (s_FrameSprite == null)
            {
                s_FrameSprite = Resources.Load<Sprite>("Bars/bar_frame");
                s_FillSprite = Resources.Load<Sprite>("Bars/bar_fill");
            }
            if (s_FrameSprite == null || s_FillSprite == null)
            {
                Debug.LogError("WristBars: the bar sprites are missing from Assets/Resources/Bars.");
                enabled = false;
                return;
            }

            m_HealthBar = Build("Health", -m_Spread, m_HealthColor);
            m_ManaBar = Build("Mana", m_Spread, m_ManaColor);
        }

        void Update()
        {
            if (m_Health != null)
            {
                bool critical = m_Health.Health <= m_NotchEvery;
                m_HealthBar.Frame.color = critical ? m_CriticalColor : m_FrameColor;
                Show(m_HealthBar, m_Health.Health, m_Health.MaxHealth, critical ? m_CriticalColor : m_HealthColor);
            }
            // The mana gets its maximum in its own Start.
            if (m_Mana != null && m_Mana.MaxMana > 0f)
                Show(m_ManaBar, m_Mana.Mana, m_Mana.MaxMana, m_Mana.IsExhausted ? m_ExhaustedColor : m_ManaColor);
        }

        // That many degrees around the forearm from its top, facing away from it, its length along the arm.
        Bar Build(string name, float degrees, Color color)
        {
            float angle = degrees * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            var root = new GameObject($"{name} Bar").transform;
            root.SetParent(m_Bracer != null ? m_Bracer : transform, false);
            root.localPosition = new Vector3(0f, k_BracerHeight, m_Along) + outward * (k_BracerRadius + k_Lift);
            // The sprites' right is toward the hand, so the fill grows that way.
            root.localRotation = Quaternion.LookRotation(-outward, Vector3.Cross(-outward, Vector3.forward));
            // Sorted as one piece, so the fill never draws over the frame of the other bar.
            root.gameObject.AddComponent<SortingGroup>();

            var bar = new Bar { Scale = m_Thickness / k_FrameHeight };
            bar.Frame = Part(root, "Frame", s_FrameSprite, bar.Scale, m_FrameColor, 0);
            bar.Frame.drawMode = SpriteDrawMode.Sliced;
            bar.Frame.size = new Vector2(m_Length / bar.Scale, k_FrameHeight);
            bar.Fill = Part(root, "Fill", s_FillSprite, bar.Scale, color, 1);
            bar.Fill.drawMode = SpriteDrawMode.Sliced;
            bar.Notches = new GameObject("Notches").transform;
            bar.Notches.SetParent(root, false);

            // Past the elbow's end, turned to read with the hand up.
            var label = new GameObject("Number");
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(-m_Length * 0.5f - 0.009f, 0f, 0f);
            label.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            bar.Text = label.AddComponent<TextMeshPro>();
            if (m_Font != null)
                bar.Text.font = m_Font;
            bar.Text.fontSize = 0.085f;
            bar.Text.fontStyle = FontStyles.Bold;
            bar.Text.color = m_TextColor;
            bar.Text.alignment = TextAlignmentOptions.Center;
            bar.Text.textWrappingMode = TextWrappingModes.NoWrap;
            bar.Text.rectTransform.sizeDelta = new Vector2(0.024f, 0.012f);
            return bar;
        }

        // Grows from the elbow's end of the trough. Sliced sprites can't get narrower than their tips, so
        // below that the fill is squeezed instead.
        void Show(Bar bar, float value, float max, Color color)
        {
            if (!Mathf.Approximately(max, bar.Max))
                Notch(bar, max);

            float fraction = Mathf.Clamp01(value / max);
            bar.Fill.enabled = fraction > 0f;
            bar.Fill.color = color;
            float full = m_Length / bar.Scale - k_FillInset * 2f;
            float width = full * fraction;
            float drawn = Mathf.Max(width, k_FillTips);
            bar.Fill.size = new Vector2(drawn, k_FillHeight);

            Transform t = bar.Fill.transform;
            t.localScale = new Vector3(bar.Scale * width / drawn, bar.Scale, 1f);
            t.localPosition = new Vector3((-full + width) * 0.5f * bar.Scale, t.localPosition.y, t.localPosition.z);
            bar.Text.text = Mathf.CeilToInt(value).ToString();
        }

        // A dark line across the trough every m_NotchEvery points. Laid out again when the maximum changes,
        // with the difficulty or an upgrade.
        void Notch(Bar bar, float max)
        {
            bar.Max = max;
            foreach (Transform old in bar.Notches)
                Destroy(old.gameObject);
            if (m_NotchEvery <= 0f)
                return;

            float trough = m_Length - k_FillInset * 2f * bar.Scale;
            for (float at = m_NotchEvery; at < max - 0.01f; at += m_NotchEvery)
            {
                SpriteRenderer notch = Part(bar.Notches, "Notch", WhiteSprite(), 1f, m_NotchColor, 2);
                notch.transform.localScale = new Vector3(0.0012f, k_FillHeight * bar.Scale, 1f);
                notch.transform.localPosition += Vector3.right * (trough * (at / max - 0.5f));
            }
        }

        // A plain white square a unit across.
        static Sprite WhiteSprite()
        {
            if (s_White == null)
                s_White = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
            return s_White;
        }

        // Sorting order decides what draws on top; the small step toward the viewer only backs it up.
        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, float scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.back * (0.0003f * order);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
