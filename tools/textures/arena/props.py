"""Textures that aren't stone: the iron gates and the banners. Same return shape as stone.py,
except that these use alpha as a cutout instead of smoothness."""
import numpy as np

from .image import lerp, rgb
from .noise import remap, smoothstep, spectral


def portcullis(size, rng, columns=9, rows=6):
    """Iron grate for the gates: a frame, flat bars both ways and rivets where they cross.
    Returns (color, alpha, height, smoothness)."""
    shape = (size, size)
    yy, xx = (np.mgrid[0:size, 0:size] + 0.5) / size

    def bars(t, count, width):
        # Distance to the nearest bar center, in units of the bar half-width.
        cell = t * count
        d = np.abs(cell - np.round(cell)) / count
        return d / (width * 0.5)

    frame = np.minimum(np.minimum(xx, 1 - xx), np.minimum(yy, 1 - yy))
    frame_width = 0.05
    vertical = bars(xx, columns, 0.035)
    horizontal = bars(yy, rows, 0.045)

    solid = (vertical < 1) | (horizontal < 1) | (frame < frame_width)
    # Rounded bars, with the horizontal ones in front of the vertical ones.
    h_v = np.sqrt(np.clip(1 - vertical ** 2, 0, 1))
    h_h = np.sqrt(np.clip(1 - horizontal ** 2, 0, 1)) + 0.4 * (horizontal < 1)
    h_f = smoothstep(frame, 0, frame_width * 0.3) * (frame < frame_width) * 1.2
    height = np.maximum(np.maximum(h_v, h_h), h_f) * 4.0

    crossing = (vertical < 1.6) & (horizontal < 1.6)
    cx = (xx * columns - np.round(xx * columns)) / columns
    cy = (yy * rows - np.round(yy * rows)) / rows
    rivet = np.sqrt(cx ** 2 + cy ** 2) / 0.012
    rivet_mask = (rivet < 1) & crossing
    height = height + rivet_mask * np.sqrt(np.clip(1 - rivet ** 2, 0, 1)) * 3.0

    rust = smoothstep(spectral(shape, rng, beta=1.6, low=3), 0.2, 1.8)
    pitting = spectral(shape, rng, beta=0.5, low=80)
    color = lerp(rgb(58, 56, 56), rgb(112, 68, 42), rust * 0.75)
    color = color * (1 + 0.08 * pitting[..., None])
    color = lerp(color, rgb(120, 118, 114), rivet_mask * 0.6)
    # Worn edges catch the light.
    color = lerp(color, rgb(104, 102, 100), smoothstep(height, 3.2, 4.0) * 0.4 * (1 - rust))

    alpha = solid.astype(float)
    smoothness = 0.35 - 0.25 * rust + 0.15 * rivet_mask
    return color, alpha, height, smoothness


def banner(width, height, rng, cloth=rgb(128, 18, 24), emblem=rgb(212, 168, 72), glyph=None):
    """Hanging cloth with a border and a rune in a ring, cut into a swallowtail at the bottom. glyph is a
    list of polylines in -1..1, like the runes the player draws; the lightning one by default.
    Returns (color, alpha, height)."""
    shape = (height, width)
    yy, xx = (np.mgrid[0:height, 0:width] + 0.5)
    u = xx / width
    v = 1 - yy / height  # 0 at the bottom, 1 at the top, like the mesh UVs
    aspect = height / width

    # Folds: a few soft vertical waves that grow toward the bottom, where the cloth hangs free.
    folds = np.zeros(shape)
    for _ in range(4):
        k = rng.uniform(2.0, 5.0)
        folds += np.sin(2 * np.pi * (k * u + rng.random())) / k
    folds *= 0.6 + 0.4 * (1 - v)
    weave = (np.sin(xx * 1.6) * np.sin(yy * 1.6)) * 0.5

    cloth = cloth * (1 + 0.08 * spectral(shape, rng, beta=2.0, low=2, high=20)[..., None])

    # Swallowtail: a V notch in the bottom edge.
    cut = 0.22 - np.abs(u - 0.5) * 0.42
    alpha = (v > cut).astype(float)

    border_x = np.minimum(u, 1 - u) * width
    edge_bottom = (v - cut) * height / np.sqrt(1 + (0.42 * aspect) ** 2)
    border = np.minimum(np.minimum(border_x, (1 - v) * height), edge_bottom)
    band = (border > width * 0.04) & (border < width * 0.09)

    # Emblem: a ring with the rune inside, in the upper half.
    cx, cy = 0.5, 0.64
    px, py = (u - cx), (v - cy) * aspect
    r = np.sqrt(px ** 2 + py ** 2)
    ring = np.abs(r - 0.3) < 0.025
    if glyph is None:
        glyph = [[(-1, 1), (0.67, -0.33), (-0.67, -0.33), (1, -1)]]
    rune = np.zeros(shape, bool)
    for line in glyph:
        points = np.array(line) * 0.17
        for (x0, y0), (x1, y1) in zip(points[:-1], points[1:]):
            dx, dy = x1 - x0, y1 - y0
            t = np.clip(((px - x0) * dx + (py - y0) * dy) / max(dx * dx + dy * dy, 1e-9), 0, 1)
            rune |= np.hypot(px - (x0 + t * dx), py - (y0 + t * dy)) < 0.03
    trim = band | ring | rune

    shade = 1 + 0.25 * folds[..., None]
    color = lerp(cloth, emblem, trim) * shade
    color = color * (1 + 0.04 * weave[..., None])
    relief = folds * 6 + weave * 0.3 + trim * 0.6
    return np.clip(color, 0, 1), alpha, relief


def ivy(size, rng, count=260):
    """A mat of ivy leaves seen from above, tiling, with the gaps cut out.
    Returns (color, alpha, height)."""
    color = np.zeros((size, size, 3))
    alpha = np.zeros((size, size))
    height = np.zeros((size, size))
    greens = [rgb(52, 92, 36), rgb(70, 118, 44), rgb(40, 76, 32), rgb(88, 128, 52)]

    for _ in range(count):
        cx, cy = rng.uniform(0, size, 2)
        s = rng.uniform(0.035, 0.07) * size
        turn = rng.uniform(0, 2 * np.pi)
        r = int(s * 1.3) + 2
        xs = (np.arange(-r, r + 1) + int(cx)) % size
        ys = (np.arange(-r, r + 1) + int(cy)) % size
        gx, gy = np.meshgrid(np.arange(-r, r + 1) + (int(cx) - cx), np.arange(-r, r + 1) + (int(cy) - cy))
        a = np.arctan2(gy, gx) - turn
        dist = np.hypot(gx, gy)
        # Five rounded lobes, the classic ivy outline.
        outline = s * (0.62 + 0.38 * np.abs(np.cos(2.5 * a)))
        inside = dist < outline
        if not inside.any():
            continue
        dome = np.sqrt(np.clip(1 - (dist / np.maximum(outline, 1e-3)) ** 2, 0, 1))
        vein = np.exp(-(np.abs(np.sin(5 * a / 2 * 2)) * dist / (0.06 * s)) ** 2) * (dist < outline * 0.9)
        base = greens[rng.integers(len(greens))] * rng.uniform(0.85, 1.15)
        leaf = base * (0.75 + 0.35 * dome)[..., None]
        leaf = lerp(leaf, base * 1.45, vein * 0.6)
        lift = rng.uniform(0.0, 1.0)

        block = np.ix_(ys, xs)
        top = inside & (dome + lift > height[block])
        sub_color = color[block]
        sub_color[top] = leaf[top]
        color[block] = sub_color
        sub_h = height[block]
        sub_h[top] = (dome + lift)[top]
        height[block] = sub_h
        sub_a = alpha[block]
        sub_a[inside] = 1.0
        alpha[block] = sub_a

    return np.clip(color, 0, 1), alpha, height * 3.0
