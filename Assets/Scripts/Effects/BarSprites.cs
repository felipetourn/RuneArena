using UnityEngine;

namespace RuneArena.Effects
{
    // The sprites in Resources/Bars, shared by the enemies' health bars, the player's wrist bars and the
    // grimoire, and the pieces they are put together from. At 100 px per unit: the frame is 64 px tall and its
    // trough starts 12 px in from the tips, the fill is 48 px tall and can't get narrower than its two tips.
    public static class BarSprites
    {
        public const float FrameHeight = 0.64f;
        public const float FillInset = 0.12f;
        public const float FillHeight = 0.48f;
        public const float FillTips = 0.52f;
        public const float PlateHeight = 0.4f;
        public const float BadgeWidth = 1.28f;
        public const float BadgeHeight = 1.12f;

        static Sprite s_Frame;
        static Sprite s_Fill;
        static Sprite s_Plate;
        static Sprite s_Badge;
        static Sprite s_White;

        public static Sprite Frame => Load(ref s_Frame, "bar_frame");
        public static Sprite Fill => Load(ref s_Fill, "bar_fill");
        public static Sprite Plate => Load(ref s_Plate, "bar_plate");
        public static Sprite Badge => Load(ref s_Badge, "bar_badge");

        /// <summary>A plain white square a unit across, for rules and notches.</summary>
        public static Sprite White
        {
            get
            {
                if (s_White == null)
                    s_White = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
                return s_White;
            }
        }

        static Sprite Load(ref Sprite sprite, string name)
        {
            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>($"Bars/{name}");
                if (sprite == null)
                    Debug.LogError($"BarSprites: Resources/Bars/{name} is missing.");
            }
            return sprite;
        }

        /// <summary>A sprite of the bar, scaled and tinted. Sorting order decides what draws on top; a step
        /// toward the viewer for each order, as small as the bar is, only backs it up.</summary>
        public static SpriteRenderer Part(Transform parent, string name, Sprite sprite, float scale, Color color, int order,
                                          float step, SpriteDrawMode drawMode = SpriteDrawMode.Simple)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.back * (step * order);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = drawMode;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>Fills that fraction of the trough of a bar this wide, whose sprites are scaled by scale,
        /// growing from its left end. Sliced sprites can't get narrower than their tips, so below that the
        /// fill is squeezed instead.</summary>
        public static void SetFill(SpriteRenderer fill, float width, float scale, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            fill.enabled = fraction > 0f;
            float full = width / scale - FillInset * 2f;
            float filled = full * fraction;
            float drawn = Mathf.Max(filled, FillTips);
            fill.size = new Vector2(drawn, FillHeight);

            Transform t = fill.transform;
            t.localScale = new Vector3(scale * filled / drawn, scale, 1f);
            t.localPosition = new Vector3((-full + filled) * 0.5f * scale, t.localPosition.y, t.localPosition.z);
        }
    }
}
