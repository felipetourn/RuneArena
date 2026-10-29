# Mirror of Assets/Scripts/Strokes/RuneShapes.cs.
import numpy as np

SQUARE = [(-1, 1), (1, 1), (1, -1), (-1, -1)]
TRIANGLE = [(0, 1), (0.866, -0.5), (-0.866, -0.5)]
LIGHTNING = [(-1, 1), (1, 1), (-1, -1), (1, -1)]
V = [(-1, 1), (0, -1), (1, 1)]


def circle(segments=32):
    a = np.pi * 0.5 - np.arange(segments) * 2 * np.pi / segments
    return list(zip(np.cos(a), np.sin(a)))


def spiral(start, direction, turns=1.5, n=48):
    """Drawn from the center outwards."""
    t = np.linspace(0.0, turns * 2 * np.pi, n)
    r = 0.15 + t / t[-1]
    a = start + direction * t
    return list(zip(r * np.cos(a), r * np.sin(a)))


def spiral_variants():
    variants = []
    for start in np.radians(np.arange(0, 360, 45)):
        for direction in (1, -1):
            variants += open_variants(spiral(start, direction))
    return variants


def closed_variants(vertices, start_stride=1):
    n = len(vertices)
    variants = []
    for start in range(0, n, start_stride):
        for direction in (-1, 1):
            variants.append(np.array([vertices[(start + direction * k) % n] for k in range(n + 1)], dtype=np.float64))
    return variants


def open_variants(vertices):
    forward = np.array(vertices, dtype=np.float64)
    return [forward, forward[::-1].copy()]


def rune_variants():
    return {
        "square": closed_variants(SQUARE),
        "triangle": closed_variants(TRIANGLE),
        "circle": closed_variants(circle(), 4),
        "lightning": open_variants(LIGHTNING),
        "spiral": spiral_variants(),
        "v": open_variants(V),
    }


CLOSED = {"square", "circle", "triangle"}


# Shapes a player could draw that are NOT runes. They train the noise class.
def hard_negatives():
    diamond = [(0, 1), (1, 0), (0, -1), (-1, 0)]
    inverted_triangle = [(0, -1), (-0.866, 0.5), (0.866, 0.5)]
    mirrored_z = [(1, 1), (-1, 1), (1, -1), (-1, -1)]
    return closed_variants(diamond) + closed_variants(inverted_triangle) + open_variants(mirrored_z)
