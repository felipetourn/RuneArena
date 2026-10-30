"""Writing the maps the way Unity's URP Lit reads them."""
from pathlib import Path

import numpy as np
from PIL import Image


def normal_from_height(height, strength):
    """Tangent-space normal map (OpenGL convention, green up) from a height field.

    height is in texels' worth of units: strength scales the slopes. Rows go down the image while
    v goes up in Unity, hence the sign on the vertical slope.
    """
    du = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * strength
    dv = (np.roll(height, 1, 0) - np.roll(height, -1, 0)) * 0.5 * strength
    n = np.dstack([-du, -dv, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n


def save_normal(path, normal):
    rgb = np.clip((normal * 0.5 + 0.5) * 255 + 0.5, 0, 255).astype(np.uint8)
    _save(path, rgb, "RGB")


def save_albedo(path, color, smoothness=None, alpha=None):
    """color is sRGB in 0..1. URP Lit can read smoothness from the albedo alpha; alpha is for cutouts.
    Only one of the two fits in the alpha channel."""
    rgb = np.clip(color * 255 + 0.5, 0, 255).astype(np.uint8)
    extra = smoothness if smoothness is not None else alpha
    if extra is None:
        _save(path, rgb, "RGB")
        return
    a = np.clip(extra * 255 + 0.5, 0, 255).astype(np.uint8)
    _save(path, np.dstack([rgb, a]), "RGBA")


def _save(path, array, mode):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(array, mode).save(path, optimize=True)


def lerp(a, b, t):
    t = np.asarray(t)
    if t.ndim == 2:
        t = t[..., None]
    return np.asarray(a) * (1 - t) + np.asarray(b) * t


def rgb(r, g, b):
    return np.array([r, g, b], dtype=np.float64) / 255.0
