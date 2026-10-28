# Mirror of Assets/Scripts/Strokes/RuneShapes.cs.
import numpy as np

SQUARE = [(-1, 1), (1, 1), (1, -1), (-1, -1)]
TRIANGLE = [(0, 1), (0.866, -0.5), (-0.866, -0.5)]
LIGHTNING = [(-1, 1), (1, 1), (-1, -1), (1, -1)]


def circle(segments=32):
    a = np.pi * 0.5 - np.arange(segments) * 2 * np.pi / segments
    return list(zip(np.cos(a), np.sin(a)))


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
    }


# Shapes a player could draw that are NOT runes. They train the noise class.
def hard_negatives():
    diamond = [(0, 1), (1, 0), (0, -1), (-1, 0)]
    inverted_triangle = [(0, -1), (-0.866, 0.5), (0.866, 0.5)]
    mirrored_z = [(1, 1), (-1, 1), (1, -1), (-1, -1)]
    v = [(-1, 1), (0, -1), (1, 1)]
    t = np.linspace(0, 4 * np.pi, 48)
    spiral = list(zip(t / (4 * np.pi) * np.cos(t), t / (4 * np.pi) * np.sin(t)))
    return (closed_variants(diamond) + closed_variants(inverted_triangle)
            + open_variants(mirrored_z) + open_variants(v) + open_variants(spiral))
