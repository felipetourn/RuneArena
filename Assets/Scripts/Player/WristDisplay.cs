using System.Collections;
using System.Collections.Generic;
using RuneArena.Effects;
using RuneArena.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneArena.Player
{
    // Goes on the left controller, which holds an open grimoire with one spread per rune: on the left the rune
    // in its spell's color with what it costs and its damage, on the right its name, what it does and its
    // combos. The pages are night blue, so the writing is pale and the headings gold.
    // The book is put away until the left grip is held: then it grows out of the hand, and it goes back when
    // the grip is let go, so it only takes the view while it's being read. The game doesn't stop for it.
    // X turns the page while it's out. In the editor it can also be pinned to the view, since the simulated
    // hand is rarely in sight.
    public class WristDisplay : MonoBehaviour
    {
        // How far each sorting order steps toward the reader.
        const float k_Step = 0.0001f;

        // The book made by Create Hand Props, in its own space: the spine along Z, each page this wide and
        // tall past the gap at the gutter, their top this high. Keep them the same as HandPropsBuilder's.
        const float k_PageWidth = 0.1f;
        const float k_PageHeight = 0.13f;
        const float k_PageTop = 0.016f;
        const float k_Gutter = 0.002f;
        // Half the width the writing keeps to, well inside the page's gold rule, which is 5% of the width in
        // from the edges.
        const float k_HalfInsideWidth = 0.035f;

        [SerializeField] bool m_PinToViewInEditor = true;
        [SerializeField] Vector3 m_ViewPosition = new Vector3(-0.13f, -0.09f, 0.4f);
        [Tooltip("The book it writes on. Create Hand Props makes it and fills this and the two below.")]
        [SerializeField] Transform m_Grimoire;
        [Tooltip("For the page being turned.")]
        [SerializeField] Material m_Parchment;
        [Tooltip("Plain Cinzel for the writing. TextMeshPro's default font if empty.")]
        [SerializeField] TMP_FontAsset m_Font;
        [Tooltip("Seconds a page takes to turn.")]
        [SerializeField] float m_TurnTime = 0.4f;
        [Tooltip("Seconds the book takes to come out of the hand, or to go back.")]
        [SerializeField] float m_ShowTime = 0.15f;

        [Header("Colors")]
        [SerializeField] Color m_TextColor = new Color(0.94f, 0.91f, 0.84f);
        [Tooltip("Headings, rules and the ring around the rune.")]
        [SerializeField] Color m_GoldColor = new Color(0.89f, 0.78f, 0.48f);
        [Tooltip("Combos that help.")]
        [SerializeField] Color m_HelpsColor = new Color(0.5f, 0.88f, 0.69f);
        [Tooltip("Combos that hurt, and how to unlock a rune.")]
        [SerializeField] Color m_HurtsColor = new Color(1f, 0.61f, 0.54f);
        [Tooltip("Thin rules, and the other runes in the row at the foot of the page.")]
        [SerializeField] Color m_DimColor = new Color(0.49f, 0.53f, 0.66f);
        [Tooltip("Under each rune, a little wider, so it reads apart from the ring.")]
        [SerializeField] Color m_ShadeColor = new Color(0.055f, 0.08f, 0.15f);

        static readonly Dictionary<string, Sprite> s_RuneSprites = new Dictionary<string, Sprite>();

        SpellBook m_Book;
        InputAction m_TurnAction;
        InputAction m_ShowAction;
        Vector3 m_GrimoireScale;
        // 0 put away, 1 all the way out.
        float m_Shown;

        Transform m_LeftPage;
        Transform m_RightPage;
        Transform m_LeftContent;
        Transform m_RightContent;
        Transform m_Leaf;
        // One per spell, in the book's order.
        int m_Spread;
        bool m_Turning;

        RuneSpell m_ShownSpell;
        TMP_Text m_CostText;
        TMP_Text m_StatText;

        int SpreadCount => m_Book != null && m_Book.Spells != null ? m_Book.Spells.Count : 0;

        void Awake()
        {
            m_TurnAction = new InputAction("Turn Page", InputActionType.Button, "<XRController>{LeftHand}/{PrimaryButton}");
            m_ShowAction = new InputAction("Show Book", InputActionType.Button, "<XRController>{LeftHand}/{GripButton}");
        }

        void OnEnable()
        {
            m_TurnAction.Enable();
            m_ShowAction.Enable();
        }

        void OnDisable()
        {
            m_TurnAction.Disable();
            m_ShowAction.Disable();
        }

        void Start()
        {
            m_Book = FindAnyObjectByType<SpellBook>(FindObjectsInactive.Include);
            if (m_Grimoire == null)
            {
                Debug.LogError("WristDisplay: there's no grimoire to write on; run RuneArena > Create Hand Props.");
                enabled = false;
                return;
            }

            m_Grimoire.gameObject.SetActive(true);
            var player = FindAnyObjectByType<PlayerHealth>();
            if (Application.isEditor && m_PinToViewInEditor && player != null)
            {
                m_Grimoire.SetParent(player.Head, false);
                m_Grimoire.localPosition = m_ViewPosition;
                // Pages toward the eyes, the top of the page up.
                m_Grimoire.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            }

            m_LeftPage = Page("Left Page", -1f);
            m_RightPage = Page("Right Page", 1f);
            m_Leaf = Leaf();
            if (SpreadCount > 0)
            {
                ShowPage(0, true);
                ShowPage(0, false);
            }
            m_GrimoireScale = m_Grimoire.localScale;
            SetShown(0f);
        }

        void OnDestroy()
        {
            m_TurnAction.Dispose();
            m_ShowAction.Dispose();
        }

        void Update()
        {
            // Unscaled, like the page turning, so it comes out while the game is paused too.
            float target = m_ShowAction.IsPressed() ? 1f : 0f;
            if (m_Shown != target)
                SetShown(Mathf.MoveTowards(m_Shown, target, Time.unscaledDeltaTime / Mathf.Max(m_ShowTime, 0.01f)));

            if (m_Shown > 0f && !m_Turning && SpreadCount > 1 && m_TurnAction.WasPressedThisFrame())
                StartCoroutine(Turn((m_Spread + 1) % SpreadCount));

            // Upgrades bought while reading change these.
            if (m_CostText != null && m_ShownSpell != null)
            {
                m_CostText.text = RuneLore.Number(m_Book.Cost(m_ShownSpell));
                m_StatText.text = RuneLore.Stat(m_ShownSpell, m_Book.Power(m_ShownSpell.Label));
            }
        }

        // Grows from nothing, easing in and out; put away altogether at 0, so it costs nothing while hidden.
        void SetShown(float shown)
        {
            m_Shown = shown;
            m_Grimoire.gameObject.SetActive(shown > 0f);
            m_Grimoire.localScale = m_GrimoireScale * Mathf.SmoothStep(0f, 1f, shown);
        }

        // The leaf lifts off the right page and lands on the left one. The right page gets the next spread
        // right away, hidden under it; the left one once it's down. Unscaled, so the book can be read while
        // the game is paused.
        IEnumerator Turn(int target)
        {
            m_Turning = true;
            ShowPage(target, false);

            m_Leaf.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / m_TurnTime)
            {
                m_Leaf.localRotation = Quaternion.Euler(0f, 0f, 180f * Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            m_Leaf.gameObject.SetActive(false);

            ShowPage(target, true);
            m_Spread = target;
            m_Turning = false;
        }

        void ShowPage(int spread, bool left)
        {
            Transform page = left ? m_LeftPage : m_RightPage;
            ref Transform content = ref left ? ref m_LeftContent : ref m_RightContent;
            if (content != null)
                Destroy(content.gameObject);
            content = new GameObject("Content").transform;
            content.SetParent(page, false);

            RuneSpell spell = m_Book.Spells[spread];
            if (left)
                RunePage(content, spell, spread);
            else
                InfoPage(content, spell, spread);
        }

        // The spread's number in a corner, then the rune large inside a ring, in its spell's color, the same it
        // has when drawn, with its cost and damage under it. Faded, and saying how to get it, until it's unlocked.
        void RunePage(Transform page, RuneSpell spell, int spread)
        {
            float width = k_HalfInsideWidth * 2f;
            TMP_Text number = Text(page, "Number", Roman(spread + 1), new Vector2(0f, 0.054f), new Vector2(width, 0.008f), 0.05f, TextAlignmentOptions.Left);
            number.color = m_GoldColor;

            bool unlocked = m_Book.IsUnlocked(spell.Label);
            Color color = unlocked ? spell.Color : Color.Lerp(spell.Color, m_ShadeColor, 0.6f);
            var center = new Vector2(0f, 0.019f);
            SpriteRenderer ring = Part(page, "Ring", RuneSprite("Ring"), 0.031f, m_GoldColor, 0);
            ring.transform.localPosition = Page2D(center);
            Rune(page, spell.Label, center, 0.018f, color);
            if (!unlocked)
            {
                TMP_Text locked = Text(page, "Locked", "Se desbloquea con una mejora esmeralda", new Vector2(0f, -0.0165f), new Vector2(width, 0.008f), 0.055f, TextAlignmentOptions.Center);
                locked.color = m_HurtsColor;
                AutoSize(locked, 0.6f);
            }

            m_ShownSpell = spell;
            m_CostText = Stat(page, "Cost", "Maná", -0.029f);
            Rule(page, new Vector2(0f, -0.037f), width, m_DimColor);
            m_StatText = Stat(page, "Stat", RuneLore.StatName(spell), -0.045f);
        }

        // A row of the rune page: what it is in gold on the left and its value on the right, which shrinks
        // to fit, since some spells give two numbers.
        TMP_Text Stat(Transform page, string name, string label, float y)
        {
            var row = new Vector2(k_HalfInsideWidth * 2f, 0.013f);
            TMP_Text title = Text(page, $"{name} Name", label, new Vector2(0f, y), row, 0.06f, TextAlignmentOptions.Left);
            title.color = m_GoldColor;
            title.characterSpacing = 10f;
            TMP_Text value = Text(page, name, "", new Vector2(0.011f, y), new Vector2(row.x - 0.022f, row.y), 0.095f, TextAlignmentOptions.Right);
            AutoSize(value, 0.5f);
            return value;
        }

        // The spell's name over a gold rule, what it does, and its combos, written green if they help and red
        // if they hurt, each after the other rune in its color, or a badge when it's about an enemy. At the
        // foot, a rune for each spread, with this one in its color.
        void InfoPage(Transform page, RuneSpell spell, int spread)
        {
            float width = k_HalfInsideWidth * 2f;
            TMP_Text title = Text(page, "Name", RuneLore.Name(spell.Label), new Vector2(0f, 0.049f), new Vector2(width, 0.014f), 0.115f, TextAlignmentOptions.Left);
            AutoSize(title, 0.6f);
            Rule(page, new Vector2(0f, 0.0395f), width, m_GoldColor);

            TMP_Text effect = Text(page, "Effect", RuneLore.Effect(spell), new Vector2(0f, 0.0255f), new Vector2(width, 0.024f), 0.066f, TextAlignmentOptions.TopLeft);
            effect.textWrappingMode = TextWrappingModes.Normal;
            effect.lineSpacing = -10f;
            AutoSize(effect, 0.6f);

            // The heading, and a rule from it to the edge.
            TMP_Text heading = Text(page, "Combos Name", "Combos", new Vector2(0f, 0.0085f), new Vector2(width, 0.008f), 0.05f, TextAlignmentOptions.Left);
            heading.color = m_GoldColor;
            heading.characterSpacing = 10f;
            Rule(page, new Vector2(0.0115f, 0.0085f), width - 0.023f, m_DimColor);

            List<RuneCombo> combos = RuneLore.Combos(spell, m_Book);
            for (int i = 0; i < combos.Count && i < 5; i++)
            {
                RuneCombo combo = combos[i];
                float y = -0.001f - i * 0.0105f;
                Color mark = combo.Good ? m_HelpsColor : m_HurtsColor;
                var at = new Vector2(-k_HalfInsideWidth + 0.005f, y);
                RuneSpell partner = combo.Partner != null ? m_Book.Find(combo.Partner) : null;
                if (partner != null)
                {
                    Rune(page, partner.Label, at, 0.0048f, partner.Color);
                }
                else
                {
                    SpriteRenderer badge = Part(page, "Enemy Mark", BarSprites.Badge, 0.0088f / BarSprites.BadgeHeight, mark, 1);
                    badge.transform.localPosition = Page2D(at);
                }
                TMP_Text text = Text(page, "Combo", combo.Text, new Vector2(0.007f, y), new Vector2(width - 0.014f, 0.01f), 0.062f, TextAlignmentOptions.Left);
                text.color = mark;
                AutoSize(text, 0.6f);
            }

            const float step = 0.0085f;
            float x = -step * (SpreadCount - 1) * 0.5f;
            for (int i = 0; i < SpreadCount; i++, x += step)
            {
                RuneSpell other = m_Book.Spells[i];
                bool here = i == spread;
                SpriteRenderer rune = Part(page, $"{other.Label} Mark", RuneSprite(other.Label), here ? 0.003f : 0.0024f, here ? other.Color : m_DimColor, 1);
                rune.transform.localPosition = Page2D(new Vector2(x, -0.052f));
                if (here)
                    Rule(page, new Vector2(x, -0.0563f), 0.006f, rune.color);
            }
        }

        // A hairline across the page, centered there.
        void Rule(Transform page, Vector2 center, float width, Color color)
        {
            SpriteRenderer rule = Part(page, "Rule", BarSprites.White, 1f, color, 0);
            rule.transform.localScale = new Vector3(width, 0.0006f, 1f);
            rule.transform.localPosition = Page2D(center);
        }

        // Enough for the book's spreads.
        static string Roman(int number)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return number >= 1 && number <= numerals.Length ? numerals[number - 1] : number.ToString();
        }

        // A rune centered there, size from its center to its widest edge, in its color over a slightly wider
        // one as dark as the paper, so it stands out where it crosses the ring. The sprites are made by
        // tools/textures, with the same shapes as the banners.
        void Rune(Transform page, string label, Vector2 center, float size, Color color)
        {
            Sprite outline = RuneSprite(label + "_Outline");
            Sprite stroke = RuneSprite(label);
            if (outline == null || stroke == null)
                return;
            SpriteRenderer under = Part(page, $"{label} Outline", outline, size, m_ShadeColor, 1);
            under.transform.localPosition = Page2D(center);
            SpriteRenderer over = Part(page, $"{label} Rune", stroke, size, color, 2);
            over.transform.localPosition = Page2D(center) + Vector3.back * 0.0001f;
        }

        static Sprite RuneSprite(string name)
        {
            if (!s_RuneSprites.TryGetValue(name, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>($"Runes/Rune_{name}");
                if (sprite == null)
                    Debug.LogError($"WristDisplay: Resources/Runes/Rune_{name} is missing; make it with tools/textures/generate.py --only Runes.");
                s_RuneSprites[name] = sprite;
            }
            return sprite;
        }

        // Lying on the paper, with the top of the writing toward the far end of the book.
        Transform Page(string name, float side)
        {
            var page = new GameObject(name).transform;
            page.SetParent(m_Grimoire, false);
            // Just over the paper, so nothing fights with it.
            page.localPosition = new Vector3(side * (k_Gutter + k_PageWidth * 0.5f), k_PageTop + 0.0005f, 0f);
            page.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return page;
        }

        // The page that turns: a blank sheet hinged on the spine, starting over the right page, paper on both faces.
        Transform Leaf()
        {
            var hinge = new GameObject("Turning Page").transform;
            hinge.SetParent(m_Grimoire, false);
            hinge.localPosition = new Vector3(0f, k_PageTop + 0.0012f, 0f);
            foreach (float tilt in new[] { 90f, -90f })
            {
                GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(face.GetComponent<Collider>());
                face.name = tilt > 0f ? "Front" : "Back";
                face.transform.SetParent(hinge, false);
                face.transform.localPosition = new Vector3(k_Gutter + k_PageWidth * 0.5f, 0f, 0f);
                face.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
                face.transform.localScale = new Vector3(k_PageWidth, k_PageHeight, 1f);
                var renderer = face.GetComponent<MeshRenderer>();
                if (m_Parchment != null)
                    renderer.sharedMaterial = m_Parchment;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            hinge.gameObject.SetActive(false);
            return hinge;
        }

        // A point on the page, a hair toward the reader.
        static Vector3 Page2D(Vector2 p) => new Vector3(p.x, p.y, -0.0003f);

        // Bold: the regular Cinzel is too thin to read at this size in the headset.
        // Written textScale times larger on an object that much smaller: auto-size steps by at least 0.05
        // and rounds to it, which at the page's sizes (0.05 to 0.115) would jump straight to its smallest.
        // Past even that, it is cut off rather than running off its box (Cinzel has no "…" to end it with).
        TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            const float textScale = 100f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Page2D(position);
            go.transform.localScale = Vector3.one / textScale;
            var text = go.AddComponent<TextMeshPro>();
            if (m_Font != null)
                text.font = m_Font;
            text.text = value;
            text.fontSize = fontSize * textScale;
            text.fontStyle = FontStyles.Bold;
            text.color = m_TextColor;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.rectTransform.sizeDelta = size * textScale;
            return text;
        }

        // Shrinks to fit instead of running out of its box, down to that fraction of its size.
        static void AutoSize(TMP_Text text, float smallest)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = text.fontSize * smallest;
        }

        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, float scale, Color color, int order) =>
            BarSprites.Part(parent, name, sprite, scale, color, order, k_Step);
    }
}
