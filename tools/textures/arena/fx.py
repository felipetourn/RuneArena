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
