"""Seated spectators for the stands, as billboard sprites: one column per person, sitting still in the bottom
row and cheering with their arms up in the top one. The colors are left to the shader, so each sprite is masks:
R is the shading, G says what each part is (1 clothes, 0.5 hair or hat, 0 skin), B what covers it (1 metal,
0.5 the light cloth of a toga, 0 nothing) and A is the cutout. The last two columns are the
legionaries (Spectators.m_Soldiers), in crested helmets. The channels are data, so the texture is imported as
linear."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

CLOTHES, HAIR, SKIN = 255, 128, 0
METAL, TOGA, BARE = 255, 128, 0

# Per column, fixed so the random proportions stay the same: what is on the head, and who wears a toga.
HEADS = ["none", "short", "palla", "hood", "laurel", "short", "galea", "galea_wide"]
TOGAS = [False, True, False, False, True, True, False, False]


def atlas(rng, people=8, width=256, height=512, scale=2):
    """Returns the RGBA atlas, people * width by 2 * height."""
    out = Image.new("RGBA", (people * width, 2 * height))
    for p in range(people):
        look = _look(p, rng)
        for row, cheering in ((1, False), (0, True)):
            cell = _person(look, cheering, width * scale, height * scale)
            cell = cell.resize((width, height), Image.LANCZOS)
            out.paste(cell, (p * width, row * height))
    return out


def _look(index, rng):
    return {
        "shoulders": rng.uniform(0.27, 0.36),
        "head": rng.uniform(0.085, 0.1),
        "hair": HEADS[index % 8],
        "toga": TOGAS[index % 8],
        "arms": ["both", "one", "both", "fists"][index % 4],
        "lean": rng.uniform(-0.03, 0.03),
    }


def _person(look, cheering, w, h):
    """One person in a cell w by h, sitting with the lap at the bottom."""
    alpha = Image.new("L", (w, h), 0)
    mask = Image.new("L", (w, h), CLOTHES)
    cover = Image.new("L", (w, h), BARE)
    # Lines drawn around the arms, so they read in front of a body of the same color.
    ink = Image.new("L", (w, h), 0)
    a, m, k, c = ImageDraw.Draw(alpha), ImageDraw.Draw(mask), ImageDraw.Draw(ink), ImageDraw.Draw(cover)
    cx = w * (0.5 + look["lean"])

    def limb(x0, y0, x1, y1, thickness, kind):
        t = thickness * w
        line = 0.012 * w
        for draw, value, width in ((k, 255, t + 2 * line), (a, 255, t), (m, kind, t), (c, BARE, t), (k, 0, t - line)):
            draw.line([(x0, y0), (x1, y1)], fill=value, width=int(width))
            for x, y in ((x0, y0), (x1, y1)):
                draw.ellipse([x - width / 2, y - width / 2, x + width / 2, y + width / 2], fill=value)

    s = look["shoulders"] * w
    shoulder_y, waist_y = 0.46 * h, 0.82 * h
    head_r = look["head"] * w * 2.2
    head_y = shoulder_y - head_r * 1.15

    # Lap and torso.
    lap = [cx - s * 0.95, waist_y - 0.03 * h, cx + s * 0.95, h + 0.05 * h]
    a.rounded_rectangle(lap, radius=0.04 * h, fill=255)
    m.rounded_rectangle(lap, radius=0.04 * h, fill=CLOTHES)
    torso = [(cx - s, shoulder_y), (cx + s, shoulder_y), (cx + s * 0.85, waist_y), (cx - s * 0.85, waist_y)]
    a.polygon(torso, fill=255)
    m.polygon(torso, fill=CLOTHES)
    a.rounded_rectangle([cx - s, shoulder_y - 0.03 * h, cx + s, shoulder_y + 0.05 * h], radius=0.04 * h, fill=255)
    if look["toga"]:
        _toga(c, cx, s, shoulder_y, waist_y, h)

    # Neck and head.
    neck = head_r * 0.45
    a.rectangle([cx - neck, head_y, cx + neck, shoulder_y + 2], fill=255)
    m.rectangle([cx - neck, head_y, cx + neck, shoulder_y + 2], fill=SKIN)
    head = [cx - head_r, head_y - head_r * 1.1, cx + head_r, head_y + head_r * 1.05]
    a.ellipse(head, fill=255)
    m.ellipse(head, fill=SKIN)
    _hair(a, m, c, look["hair"], cx, head_y, head_r, shoulder_y)

    # Arms: down to the lap, or up.
    arm = 0.075
    hand = arm * w * 0.75
    left, right = (cx - s * 0.92, shoulder_y + 0.02 * h), (cx + s * 0.92, shoulder_y + 0.02 * h)
    down_l, down_r = (cx - s * 0.75, waist_y + 0.04 * h), (cx + s * 0.75, waist_y + 0.04 * h)
    reach = min(s * 1.3, w * 0.5 - hand * 1.6 - abs(cx - w * 0.5))
    up_l = (cx - reach, head_y - head_r * 1.9)
    up_r = (cx + reach, head_y - head_r * 1.9)
    if cheering:
        style = look["arms"]
        ends = {"both": (up_l, up_r), "fists": ((cx - s * 0.75, head_y - head_r * 2.5), (cx + s * 0.75, head_y - head_r * 2.5)),
                "one": (down_l, (cx + reach * 0.9, head_y - head_r * 2.3))}[style]
    else:
        ends = (down_l, down_r)
    for (x0, y0), (x1, y1) in ((left, ends[0]), (right, ends[1])):
        limb(x0, y0, x1, y1, arm, CLOTHES)
        a.ellipse([x1 - hand, y1 - hand, x1 + hand, y1 + hand], fill=255)
        m.ellipse([x1 - hand, y1 - hand, x1 + hand, y1 + hand], fill=SKIN)

    return _shade(alpha, mask, cover, ink, w, h)


def _toga(c, cx, s, shoulder_y, waist_y, h):
    """A toga: a band of light cloth from the left shoulder across the chest to the right hip, and its end
    hanging down the left side."""
    band = 0.32 * s
    c.polygon([(cx - s, shoulder_y - 0.02 * h), (cx - s + band * 1.6, shoulder_y - 0.02 * h),
               (cx + s * 0.85, waist_y), (cx + s * 0.85 - band * 1.8, waist_y)], fill=TOGA)
    c.polygon([(cx - s, shoulder_y - 0.02 * h), (cx - s + band, shoulder_y), (cx - s * 0.7, waist_y),
               (cx - s * 0.85, waist_y)], fill=TOGA)


def _hair(a, m, c, kind, cx, y, r, shoulder_y):
    def fill(box, ellipse=True, kind_value=HAIR, cover=None):
        (a.ellipse if ellipse else a.rectangle)(box, fill=255)
        (m.ellipse if ellipse else m.rectangle)(box, fill=kind_value)
        if cover is not None:
            (c.ellipse if ellipse else c.rectangle)(box, fill=cover)

    def face(box):
        a.ellipse(box, fill=255)
        m.ellipse(box, fill=SKIN)
        c.ellipse(box, fill=BARE)

    def short():
        a.chord([cx - r * 1.05, y - r * 1.2, cx + r * 1.05, y + r * 0.9], 180, 360, fill=255)
        m.chord([cx - r * 1.05, y - r * 1.2, cx + r * 1.05, y + r * 0.9], 180, 360, fill=HAIR)

    if kind == "short":
        short()
    elif kind == "palla":
        # A veil over the head, falling to the shoulders, with the face in front of it. In the color of her
        # clothes: in the toga's light cloth a whole row of them read as ghosts.
        fill([cx - r * 1.2, y - r * 1.25, cx + r * 1.2, y + r * 0.9], kind_value=CLOTHES)
        fill([cx - r * 1.2, y - r * 0.1, cx + r * 1.2, shoulder_y + r * 0.2], ellipse=False, kind_value=CLOTHES)
        face([cx - r * 0.75, y - r * 0.6, cx + r * 0.75, y + r * 1.05])
    elif kind == "hood":
        fill([cx - r * 1.35, y - r * 1.45, cx + r * 1.35, y + r * 1.0], kind_value=CLOTHES)
        face([cx - r * 0.75, y - r * 0.55, cx + r * 0.75, y + r * 1.0])
    elif kind == "laurel":
        # Short hair under a wreath of leaves round the brow, its two ends meeting at the back.
        short()
        for i in range(9):
            t = np.pi * (1.05 + 0.9 * i / 8)
            lx, ly = cx + np.cos(t) * r * 1.08, y - r * 0.15 + np.sin(t) * r * 0.75
            leaf = r * 0.2
            fill([lx - leaf, ly - leaf * 0.65, lx + leaf, ly + leaf * 0.65], cover=METAL)
    elif kind in ("galea", "galea_wide"):
        # The legionary's helmet: a bowl, cheek guards down the face and a crest of the tunic's color, along
        # the helmet (seen edge on, tall) or across it (a centurion's, seen broad).
        # Whole, behind the bowl, so it sits on it.
        if kind == "galea":
            crest = [cx - r * 0.3, y - r * 2.15, cx + r * 0.3, y - r * 0.9]
        else:
            crest = [cx - r * 1.3, y - r * 1.75, cx + r * 1.3, y - r * 1.05]
        a.ellipse(crest, fill=255)
        m.ellipse(crest, fill=CLOTHES)
        a.chord([cx - r * 1.18, y - r * 1.32, cx + r * 1.18, y + r * 0.75], 180, 360, fill=255)
        m.chord([cx - r * 1.18, y - r * 1.32, cx + r * 1.18, y + r * 0.75], 180, 360, fill=HAIR)
        c.chord([cx - r * 1.18, y - r * 1.32, cx + r * 1.18, y + r * 0.75], 180, 360, fill=METAL)
        # The brim at the back of the neck, and the cheek guards.
        fill([cx - r * 1.25, y - r * 0.38, cx + r * 1.25, y - r * 0.2], ellipse=False, cover=METAL)
        for side in (-1, 1):
            x0, x1 = sorted((cx + side * r * 1.0, cx + side * r * 0.62))
            fill([x0, y - r * 0.3, x1, y + r * 0.7], ellipse=False, cover=METAL)


def _shade(alpha, mask, cover, ink, w, h):
    """Lit from above and a little from the left, darker toward the edges so the shapes read."""
    a = np.asarray(alpha, dtype=np.float32) / 255
    inside = np.asarray(alpha.filter(ImageFilter.GaussianBlur(w * 0.03)), dtype=np.float32) / 255
    yy, xx = np.mgrid[0:h, 0:w] / np.array([h, w], dtype=np.float32).reshape(2, 1, 1)
    light = 0.75 + 0.2 * (1 - yy) + 0.08 * (0.5 - xx)
    shade = np.clip(light * (0.55 + 0.45 * inside), 0, 1)
    # A dark line where one part meets another, so arms read in front of the body.
    g = np.asarray(mask, dtype=np.float32)
    b = np.asarray(cover, dtype=np.float32)
    # Edges of the parts and of what covers them, so a toga or a helmet reads against the rest.
    edges = np.asarray(Image.fromarray(np.maximum(g, b).astype(np.uint8)).filter(ImageFilter.FIND_EDGES), dtype=np.float32) > 40
    edges = np.asarray(Image.fromarray((edges * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)), dtype=np.float32) / 255
    shade *= 1 - 0.45 * np.maximum(edges, np.asarray(ink, dtype=np.float32) / 255)
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., 0] = (shade * 255).astype(np.uint8)
    rgba[..., 1] = g.astype(np.uint8)
    rgba[..., 2] = b.astype(np.uint8)
    rgba[..., 3] = (a * 255).astype(np.uint8)
    # Color under the cutout too, so mipmaps don't pull black into the edges.
    rgba[..., 0] = np.where(a > 0, rgba[..., 0], 160)
    return Image.fromarray(rgba, "RGBA")
