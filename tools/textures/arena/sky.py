"""Equirectangular sky panoramas for Unity's Skybox/Panoramic shader, with the sun and a layer of clouds.

The sun is placed with the same angles as the directional light in the scene (its X and Y rotation),
so the bright spot in the sky lines up with the shadows.
"""
import numpy as np

from .image import lerp, rgb
from .noise import blur, smoothstep, spectral

NOON = {
    "elevation": 62.0,
    "azimuth": 140.0,
    "zenith": rgb(54, 104, 186),
    "horizon": rgb(176, 202, 228),
    "horizon_away": rgb(150, 182, 220),
    "ground": rgb(150, 160, 170),
    "glow": rgb(255, 250, 235),
    "glow_strength": 0.35,
    "cloud_lit": rgb(255, 255, 252),
    "cloud_far": rgb(244, 246, 250),
    "cloud_shadow": rgb(168, 180, 200),
    "coverage": 0.32,
    "seed": 11,
}

SUNSET = {
    "elevation": 20.0,
    "azimuth": 250.0,
    "zenith": rgb(46, 62, 116),
    "horizon": rgb(255, 152, 76),
    "horizon_away": rgb(146, 116, 150),
    "ground": rgb(110, 90, 100),
    "glow": rgb(255, 176, 96),
    "glow_strength": 0.9,
    "cloud_lit": rgb(255, 196, 120),
    "cloud_far": rgb(214, 128, 132),
    "cloud_shadow": rgb(88, 70, 104),
    "coverage": 0.38,
    "seed": 23,
}


def directions(width, height):
    """World direction for every pixel, matching Skybox/Panoramic's lookup."""
    u = (np.arange(width) + 0.5) / width
    v = 1 - (np.arange(height) + 0.5) / height  # image rows go down, v goes up
    lon = (0.5 - u)[None, :] * 2 * np.pi
    lat = (1 - v)[:, None] * np.pi
    x = np.sin(lat) * np.cos(lon)
    y = np.cos(lat) * np.ones_like(lon)
    z = np.sin(lat) * np.sin(lon)
    return np.dstack([x, y, z])


def sun_direction(elevation, azimuth):
    """Toward the sun, for a directional light rotated by (elevation, azimuth, 0)."""
    e, a = np.radians(elevation), np.radians(azimuth)
    return np.array([-np.sin(a) * np.cos(e), np.sin(e), -np.cos(a) * np.cos(e)])


def _sample(tile, x, y):
    """Bilinear lookup that wraps, x and y in texels."""
    n = tile.shape[0]
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - x0, y - y0
    x0, y0 = x0 % n, y0 % n
    x1, y1 = (x0 + 1) % n, (y0 + 1) % n
    top = tile[y0, x0] * (1 - fx) + tile[y0, x1] * fx
    bottom = tile[y1, x0] * (1 - fx) + tile[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def panorama(preset, width=4096, height=2048):
    rng = np.random.default_rng(preset["seed"])
    d = directions(width, height)
    sun = sun_direction(preset["elevation"], preset["azimuth"])
    up = np.clip(d[..., 1], 0, 1)

    # Horizon color turns toward the sun: warm on that side at sunset, cooler on the other.
    flat = d[..., [0, 2]]
    flat_sun = sun[[0, 2]] / (np.linalg.norm(sun[[0, 2]]) + 1e-9)
    facing = ((flat @ flat_sun) / (np.linalg.norm(flat, axis=2) + 1e-6) + 1) * 0.5
    horizon = lerp(preset["horizon_away"], preset["horizon"], facing ** 1.5)
    sky = lerp(preset["zenith"], horizon, (1 - up) ** 3)

    cos_sun = np.clip(d @ sun, -1, 1)
    angle = np.arccos(cos_sun)
    glow = np.exp(-angle / 0.12) * 0.8 + np.exp(-angle / 0.5) * 0.4
    sky = sky + preset["glow"] * (glow * preset["glow_strength"])[..., None]

    # Clouds on a flat layer far above, seen in perspective. Far away a pixel covers more of the layer,
    # so it reads from a blurrier copy of the noise: without that the horizon turns to static.
    tile = spectral((1024, 1024), rng, beta=2.8, low=2, high=90)
    tile = tile + 0.25 * spectral((1024, 1024), rng, beta=1.8, low=20, high=160)
    levels = [tile] + [blur(tile, r) for r in (1, 2, 4, 8, 16)]
    threshold = np.quantile(tile, 1 - preset["coverage"])
    above = d[..., 1] > 0.02
    reach = np.where(above, 1.0 / np.maximum(d[..., 1], 0.02), 0)
    scale = 140
    px, pz = d[..., 0] * reach * scale, d[..., 2] * reach * scale
    footprint = reach * reach * scale * np.pi / height
    lod = np.clip(np.log2(np.maximum(footprint, 1)), 0, len(levels) - 1.001)

    def density_at(x, z):
        lo = np.floor(lod).astype(int)
        t = lod - lo
        value = np.zeros_like(x)
        for i, level in enumerate(levels):
            weight = np.where(lo == i, 1 - t, 0) + np.where(lo + 1 == i, t, 0)
            if np.any(weight > 0):
                value += weight * _sample(level, x, z)
        return value

    here = density_at(px, pz)
    density = smoothstep(here, threshold, threshold + 0.8)
    # Self-shadowing: denser toward the sun means less light gets through, and thick cores are darker.
    toward = density_at(px + sun[0] * 10, pz + sun[2] * 10)
    lit = 1 - smoothstep(toward - here, -0.3, 0.6)
    lit = lit * (1 - 0.45 * smoothstep(here, threshold + 0.4, threshold + 1.6))
    lit = np.clip(lit + 0.4 * np.exp(-angle / 0.4), 0, 1)
    lit_color = lerp(preset["cloud_far"], preset["cloud_lit"], np.exp(-angle / 1.2))
    cloud = lerp(preset["cloud_shadow"], lit_color, lit)
    fade = smoothstep(d[..., 1], 0.02, 0.2) * above
    sky = lerp(sky, cloud, density * fade * 0.95)

    # Below the horizon: haze over the ground, never really seen past the colosseum walls.
    below = smoothstep(-d[..., 1], 0.0, 0.08)
    sky = lerp(sky, lerp(horizon, preset["ground"], 0.6), below)

    # The sun disk itself, on top of everything.
    disk = smoothstep(cos_sun, np.cos(np.radians(0.9)), np.cos(np.radians(0.6)))
    sky = lerp(sky, np.array([1.0, 0.98, 0.92]), disk)
    return np.clip(sky, 0, 1)
