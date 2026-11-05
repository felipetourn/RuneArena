"""Seated spectators for the stands, as billboard sprites: one column per person, sitting still in the bottom
row and cheering with their arms up in the top one. The colors are left to the shader, so each sprite is masks:
R is the shading, G says what each part is (1 clothes, 0.5 hair or hat, 0 skin) and A is the cutout. The
channels are data, so the texture is imported as linear."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

CLOTHES, HAIR, SKIN = 255, 128, 0


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
        "hair": ["none", "short", "long", "hood", "helmet", "short", "long", "cap"][index % 8],
        "arms": ["both", "one", "both", "fists"][index % 4],
        "lean": rng.uniform(-0.03, 0.03),
    }


def _person(look, cheering, w, h):
    """One person in a cell w by h, sitting with the lap at the bottom."""
    alpha = Image.new("L", (w, h), 0)
    mask = Image.new("L", (w, h), CLOTHES)
    # Lines drawn around the arms, so they read in front of a body of the same color.
    ink = Image.new("L", (w, h), 0)
    a, m, k = ImageDraw.Draw(alpha), ImageDraw.Draw(mask), ImageDraw.Draw(ink)
    cx = w * (0.5 + look["lean"])

    def limb(x0, y0, x1, y1, thickness, kind):
        t = thickness * w
        line = 0.012 * w
        for draw, value, width in ((k, 255, t + 2 * line), (a, 255, t), (m, kind, t), (k, 0, t - line)):
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

    # Neck and head.
    neck = head_r * 0.45
    a.rectangle([cx - neck, head_y, cx + neck, shoulder_y + 2], fill=255)
    m.rectangle([cx - neck, head_y, cx + neck, shoulder_y + 2], fill=SKIN)
    head = [cx - head_r, head_y - head_r * 1.1, cx + head_r, head_y + head_r * 1.05]
    a.ellipse(head, fill=255)
    m.ellipse(head, fill=SKIN)
    _hair(a, m, look["hair"], cx, head_y, head_r, shoulder_y)

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

    return _shade(alpha, mask, ink, w, h)


def _hair(a, m, kind, cx, y, r, shoulder_y):
    def fill(box, ellipse=True, kind_value=HAIR):
        (a.ellipse if ellipse else a.rectangle)(box, fill=255)
        (m.ellipse if ellipse else m.rectangle)(box, fill=kind_value)

    if kind == "short":
        a.chord([cx - r * 1.05, y - r * 1.2, cx + r * 1.05, y + r * 0.9], 180, 360, fill=255)
        m.chord([cx - r * 1.05, y - r * 1.2, cx + r * 1.05, y + r * 0.9], 180, 360, fill=HAIR)
    elif kind == "long":
        fill([cx - r * 1.12, y - r * 1.2, cx + r * 1.12, y + r * 0.6])
        fill([cx - r * 1.12, y - r * 0.2, cx - r * 0.55, shoulder_y + r * 0.6], ellipse=False)
        fill([cx + r * 0.55, y - r * 0.2, cx + r * 1.12, shoulder_y + r * 0.6], ellipse=False)
        # The face in front of it.
        a.ellipse([cx - r * 0.75, y - r * 0.6, cx + r * 0.75, y + r * 1.05], fill=255)
        m.ellipse([cx - r * 0.75, y - r * 0.6, cx + r * 0.75, y + r * 1.05], fill=SKIN)
    elif kind == "hood":
        fill([cx - r * 1.35, y - r * 1.45, cx + r * 1.35, y + r * 1.0], kind_value=CLOTHES)
        a.ellipse([cx - r * 0.75, y - r * 0.55, cx + r * 0.75, y + r * 1.0], fill=255)
        m.ellipse([cx - r * 0.75, y - r * 0.55, cx + r * 0.75, y + r * 1.0], fill=SKIN)
    elif kind == "helmet":
        a.chord([cx - r * 1.15, y - r * 1.3, cx + r * 1.15, y + r * 0.7], 180, 360, fill=255)
        m.chord([cx - r * 1.15, y - r * 1.3, cx + r * 1.15, y + r * 0.7], 180, 360, fill=HAIR)
        fill([cx - r * 0.12, y - r * 1.75, cx + r * 0.12, y - r * 0.9], ellipse=False)
    elif kind == "cap":
        fill([cx - r * 1.3, y - r * 0.45, cx + r * 1.3, y - r * 0.15], ellipse=False)
        a.chord([cx - r, y - r * 1.25, cx + r, y + r * 0.2], 180, 360, fill=255)
        m.chord([cx - r, y - r * 1.25, cx + r, y + r * 0.2], 180, 360, fill=HAIR)


def _shade(alpha, mask, ink, w, h):
    """Lit from above and a little from the left, darker toward the edges so the shapes read."""
    a = np.asarray(alpha, dtype=np.float32) / 255
    inside = np.asarray(alpha.filter(ImageFilter.GaussianBlur(w * 0.03)), dtype=np.float32) / 255
    yy, xx = np.mgrid[0:h, 0:w] / np.array([h, w], dtype=np.float32).reshape(2, 1, 1)
    light = 0.75 + 0.2 * (1 - yy) + 0.08 * (0.5 - xx)
    shade = np.clip(light * (0.55 + 0.45 * inside), 0, 1)
    # A dark line where one part meets another, so arms read in front of the body.
    g = np.asarray(mask, dtype=np.float32)
    edges = np.asarray(Image.fromarray(g.astype(np.uint8)).filter(ImageFilter.FIND_EDGES), dtype=np.float32) > 40
    edges = np.asarray(Image.fromarray((edges * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)), dtype=np.float32) / 255
    shade *= 1 - 0.45 * np.maximum(edges, np.asarray(ink, dtype=np.float32) / 255)
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., 0] = (shade * 255).astype(np.uint8)
    rgba[..., 1] = g.astype(np.uint8)
    rgba[..., 3] = (a * 255).astype(np.uint8)
    # Color under the cutout too, so mipmaps don't pull black into the edges.
    rgba[..., 0] = np.where(a > 0, rgba[..., 0], 160)
    return Image.fromarray(rgba, "RGBA")
