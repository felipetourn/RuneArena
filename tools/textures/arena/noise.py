"""Tileable noise. Everything here wraps around the edges, so the textures repeat without seams."""
import numpy as np
from scipy.spatial import cKDTree


def spectral(shape, rng, beta=2.0, low=1.0, high=None, stretch=(1.0, 1.0)):
    """Random field with a 1/f^beta power spectrum, zero mean and unit deviation.

    low and high are in cycles per tile. stretch > 1 on an axis makes the features longer along it.
    """
    ny, nx = shape
    kx = np.fft.fftfreq(nx) * nx / stretch[0]
    ky = np.fft.fftfreq(ny) * ny / stretch[1]
    k = np.sqrt(kx[None, :] ** 2 + ky[:, None] ** 2)
    keep = k >= low
    if high is not None:
        keep &= k <= high
    amp = np.where(keep, np.maximum(k, 1e-6) ** (-beta / 2), 0.0)
    spectrum = amp * np.exp(1j * rng.uniform(0, 2 * np.pi, shape))
    field = np.fft.ifft2(spectrum).real
    return (field - field.mean()) / (field.std() + 1e-9)


def cells(shape, count, rng):
    """Worley noise: distance to the nearest and second nearest of count random points, and the nearest's index."""
    ny, nx = shape
    points = rng.uniform(0, 1, (count, 2)) * (nx, ny)
    tree = cKDTree(points, boxsize=(nx, ny))
    yy, xx = np.mgrid[0:ny, 0:nx]
    grid = np.column_stack([xx.ravel() + 0.5, yy.ravel() + 0.5]) % (nx, ny)
    d, i = tree.query(grid, k=2)
    return d[:, 0].reshape(shape), d[:, 1].reshape(shape), i[:, 0].reshape(shape)


def blur(a, radius):
    """Wrapping box blur, three passes, which is close to a gaussian."""
    if radius < 1:
        return a
    out = a.astype(np.float64)
    for _ in range(3):
        for axis in (0, 1):
            acc = np.zeros_like(out)
            for s in range(-radius, radius + 1):
                acc += np.roll(out, s, axis)
            out = acc / (2 * radius + 1)
    return out


def remap(a, lo, hi):
    """Linear map so that lo goes to 0 and hi to 1, clamped."""
    return np.clip((a - lo) / (hi - lo), 0.0, 1.0)


def smoothstep(a, lo, hi):
    t = remap(a, lo, hi)
    return t * t * (3 - 2 * t)
