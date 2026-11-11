"""Sprites for particles and glowing lines: white, with the shape in the alpha channel, so the
particle color tints them. Returns (color, alpha)."""
import numpy as np

from .noise import smoothstep, spectral


def _grid(size):
    yy, xx = (np.mgrid[0:size, 0:size] + 0.5) / size * 2 - 1
    return xx, yy


def _white(alpha):
    return np.ones(alpha.shape + (3,)), np.clip(alpha, 0, 1)


def glow(size):
    """Soft round dot: a bright core and a wide falloff."""
    xx, yy = _grid(size)
    r = np.sqrt(xx ** 2 + yy ** 2)
    alpha = np.exp(-(r / 0.22) ** 2) * 0.8 + np.exp(-(r / 0.55) ** 2) * 0.35
    alpha *= 1 - smoothstep(r, 0.85, 1.0)
    return _white(alpha / alpha.max())


def spark(size):
    """Thin streak along the texture's horizontal axis, for stretched particles."""
    xx, yy = _grid(size)
    alpha = np.exp(-(yy / 0.12) ** 2) * np.exp(-(xx / 0.7) ** 4)
    alpha += np.exp(-(xx ** 2 + yy ** 2) / 0.02) * 0.6
    return _white(alpha / alpha.max())


def puff(size, rng):
    """Cloudy blob for smoke, dust and flames: soft edge broken up by noise."""
    xx, yy = _grid(size)
    r = np.sqrt(xx ** 2 + yy ** 2)
    noise = spectral((size, size), rng, beta=2.6, low=2, high=10)
    edge = r + 0.15 * noise
    alpha = (1 - smoothstep(edge, 0.2, 0.9)) ** 1.5
    alpha *= 0.85 + 0.15 * smoothstep(noise, -1.5, 1.5)
    return _white(alpha)


def ring(size):
    """Thin circle, for shockwaves on the floor."""
    xx, yy = _grid(size)
    r = np.sqrt(xx ** 2 + yy ** 2)
    alpha = np.exp(-((r - 0.82) / 0.07) ** 2) + 0.35 * np.exp(-((r - 0.78) / 0.18) ** 2)
    alpha *= 1 - smoothstep(r, 0.96, 1.0)
    return _white(alpha / alpha.max())


def line(width, length):
    """Across a line renderer: a bright core in the middle of v fading to the edges. Even along u,
    so it works with any line length."""
    v = (np.arange(width) + 0.5) / width * 2 - 1
    profile = np.exp(-(v / 0.18) ** 2) * 0.9 + np.exp(-(v / 0.5) ** 2) * 0.35
    profile = profile / profile.max()
    alpha = np.repeat(profile[:, None], length, axis=1)
    return _white(alpha)


def water(width, height, rng):
    """Painted water for the wave mesh. u runs along the wave (and tiles), v up its back, over the
    crest and down the curl: deep blue at the bottom of the back, lighter toward the top, foam on the
    lip and where it crashes down at the front."""
    v = (np.arange(height) + 0.5)[:, None] / height
    v = 1 - v  # image rows go down, v goes up
    v = np.repeat(v, width, axis=1)
    deep, mid, light = np.array([0.05, 0.25, 0.5]), np.array([0.12, 0.48, 0.75]), np.array([0.55, 0.85, 0.95])

    streaks = spectral((height, width), rng, beta=1.6, low=2, high=60, stretch=(0.3, 3.0))
    fine = spectral((height, width), rng, beta=1.6, low=8, high=40)

    t = smoothstep(v, 0.0, 0.62)
    color = deep * (1 - t)[..., None] + mid * t[..., None]
    color = color + (light - mid) * (smoothstep(v, 0.45, 0.7) * 0.6)[..., None]
    color = color * (1 + 0.12 * streaks[..., None])

    # Foam: thick on the lip, broken up by noise, and again where the curl hits the water.
    lip = np.exp(-((v - 0.76) / 0.07) ** 2)
    crash = smoothstep(v, 0.9, 1.0)
    foam = np.clip(lip * smoothstep(streaks + 0.6 * fine, -1.2, 0.4) + crash * smoothstep(fine, -0.8, 0.6), 0, 1)
    color = color * (1 - foam[..., None]) + np.array([0.95, 0.98, 1.0]) * foam[..., None]

    alpha = 0.6 + 0.2 * smoothstep(v, 0.2, 0.6) + 0.2 * foam
    alpha = alpha * (0.92 + 0.08 * fine)
    return np.clip(color, 0, 1), np.clip(alpha, 0, 1)


def _sheet(frames, size, columns):
    rows = int(np.ceil(len(frames) / columns))
    color = np.zeros((rows * size, columns * size, 3))
    alpha = np.zeros((rows * size, columns * size))
    for i, (c, a) in enumerate(frames):
        # Unity's texture sheet animation reads frames left to right, top row first.
        r, k = divmod(i, columns)
        color[r * size:(r + 1) * size, k * size:(k + 1) * size] = c
        alpha[r * size:(r + 1) * size, k * size:(k + 1) * size] = a
    return color, alpha


def fire_sheet(size, rng, frames=16, columns=4):
    """Flipbook of a flame burst: starts as a bright tight core, swells, breaks into licks and fades."""
    noise = spectral((size * 2, size), rng, beta=2.6, low=2, high=10)
    fine = spectral((size * 2, size), rng, beta=2.0, low=6, high=20)
    xx, yy = _grid(size)
    out = []
    for f in range(frames):
        t = f / (frames - 1)
        shift = int(t * size * 0.8)
        n = np.roll(noise, -shift, axis=0)[:size] + 0.35 * np.roll(fine, -shift * 2, axis=0)[:size]
        # Teardrop that grows and rises: wide at the bottom, pointed at the top.
        radius = 0.35 + 0.45 * np.sqrt(t)
        cy = 0.1 - 0.25 * t
        shape = np.sqrt(xx ** 2 * (1.3 + 0.8 * np.clip(-(yy - cy), 0, None)) + (yy - cy) ** 2) / radius
        heat = (1 - smoothstep(shape + 0.35 * n, 0.3, 1.0)) * (1 - 0.85 * t ** 1.5)
        color = np.stack([np.ones_like(heat), smoothstep(heat, 0.15, 0.85) * 0.9 + 0.1, smoothstep(heat, 0.55, 1.0) * 0.8], -1)
        out.append((color, smoothstep(heat, 0.05, 0.45)))
    return _sheet(out, size, columns)


def smoke_sheet(size, rng, frames=16, columns=4):
    """Flipbook of a smoke puff billowing out and thinning. Grey, lit a little from above."""
    a = spectral((size, size), rng, beta=2.6, low=2, high=10)
    b = spectral((size, size), rng, beta=2.6, low=2, high=10)
    xx, yy = _grid(size)
    r = np.sqrt(xx ** 2 + yy ** 2)
    out = []
    for f in range(frames):
        t = f / (frames - 1)
        n = np.cos(t * np.pi * 0.5) * a + np.sin(t * np.pi * 0.5) * b
        radius = 0.45 + 0.4 * t
        density = (1 - smoothstep(r / radius + 0.25 * n, 0.4, 1.0)) * (1 - t) ** 1.2
        light = 0.65 + 0.35 * smoothstep(-yy - 0.3 * n, -0.8, 0.8)
        color = np.repeat(light[..., None], 3, axis=2)
        out.append((color, np.clip(density * 1.3, 0, 1)))
    return _sheet(out, size, columns)


def rune(size, lines, radius):
    """A rune for the grimoire: the polylines (y up, any scale) centered and scaled so their longer side
    spans the middle 78% of the sprite, drawn radius texels each side of the line, round at the ends and
    corners."""
    xx, yy = _grid(size)
    px, py = xx * size / 2, -yy * size / 2
    points = np.concatenate([np.asarray(line, float) for line in lines])
    lo, hi = points.min(0), points.max(0)
    center = (lo + hi) / 2
    scale = 0.78 * size / (hi - lo).max()
    d = np.full(xx.shape, np.inf)
    for line in lines:
        pts = (np.asarray(line, float) - center) * scale
        for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
            dx, dy = x1 - x0, y1 - y0
            t = np.clip(((px - x0) * dx + (py - y0) * dy) / max(dx * dx + dy * dy, 1e-9), 0, 1)
            d = np.minimum(d, np.hypot(px - (x0 + t * dx), py - (y0 + t * dy)))
    return _white(np.clip(radius + 0.5 - d, 0, 1))
