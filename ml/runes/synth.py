"""Synthetic strokes in the same raw format the StrokeRecorder writes.

A clean 2D template becomes something a hand would draw in the air: rounded corners,
uneven proportions, slow wobble, tremor, uneven speed, a closing gap or overshoot,
a drawing plane that is neither flat nor facing the head, and an arbitrary head pose.
"""
import numpy as np

from .preprocess import rotate
from .shapes import CLOSED, hard_negatives, rune_variants

FRAME_TIME = 1.0 / 72.0
MIN_POINT_DISTANCE = 0.006
MAX_POINTS = 512
MIN_POINTS = 8


def _axis_angle(axis, degrees):
    half = np.radians(degrees) * 0.5
    s = np.sin(half)
    return np.array([axis[0] * s, axis[1] * s, axis[2] * s, np.cos(half)])


def _mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return np.array([
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    ])


def _euler(pitch, yaw, roll):
    # Same order as Unity's Quaternion.Euler: Z, then X, then Y.
    return _mul(_mul(_axis_angle((0, 1, 0), yaw), _axis_angle((1, 0, 0), pitch)), _axis_angle((0, 0, 1), roll))


def _dense(path, n):
    seg = np.linalg.norm(np.diff(path, axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    u = np.linspace(0.0, s[-1], n)
    return np.column_stack([np.interp(u, s, path[:, 0]), np.interp(u, s, path[:, 1])])


def _round_corners(path, rng):
    if rng.random() < 0.25:
        return path
    cut = rng.uniform(0.05, 0.18)
    for _ in range(rng.integers(1, 3)):
        a, b = path[:-1], path[1:]
        q = a * (1 - cut) + b * cut
        r = a * cut + b * (1 - cut)
        inner = np.empty((2 * len(q), 2))
        inner[0::2], inner[1::2] = q, r
        path = np.vstack([path[:1], inner, path[-1:]])
    return path


def _jitter_vertices(path, rng):
    # Few-vertex shapes: corners land in slightly different places (shorter bars, off-center apex).
    if len(path) > 8:
        return path
    out = path + rng.normal(0.0, 0.12, path.shape)
    if np.allclose(path[0], path[-1]):
        out[-1] = out[0]
    return out


def _distort(path, rng):
    angle = np.radians(rng.uniform(-15, 15))
    c, s = np.cos(angle), np.sin(angle)
    aspect = np.exp(rng.uniform(-0.7, 0.7))
    m = np.array([[c, -s], [s, c]]) @ np.array([[np.sqrt(aspect), rng.uniform(-0.1, 0.1)],
                                                [0.0, 1.0 / np.sqrt(aspect)]])
    path = path @ m.T
    path -= (path.min(axis=0) + path.max(axis=0)) * 0.5
    return path * (rng.uniform(0.12, 0.40) / np.max(path.max(axis=0) - path.min(axis=0)))


def _wobble(path, rng, closed):
    n = len(path)
    t = np.linspace(0.0, 1.0, n)
    size = np.max(path.max(axis=0) - path.min(axis=0))
    out = path.copy()
    for axis in range(2):
        for _ in range(3):
            freq = rng.uniform(0.5, 4.0)
            amp = rng.uniform(0.0, 0.025) * size
            out[:, axis] += amp * np.sin(2 * np.pi * (freq * t + rng.random()))

    if closed:
        # The end rarely lands on the start: leave a gap or keep going past it.
        gap = rng.normal(0.0, 0.05 * size, 2)
        ramp = np.clip((t - 0.8) / 0.2, 0.0, 1.0)
        out += ramp[:, None] * gap
        if rng.random() < 0.5:
            out = np.vstack([out, out[1:int(n * rng.uniform(0.03, 0.15))] + gap])
            n = len(out)

    trim_start = int(n * rng.uniform(0.0, 0.03))
    trim_end = n - int(n * rng.uniform(0.0, 0.05))
    return out[trim_start:trim_end]


def _timed(path, rng):
    """Samples the path once per frame with an uneven drawing speed."""
    seg = np.linalg.norm(np.diff(path, axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    duration = rng.uniform(0.5, 2.0)
    frames = max(int(duration / FRAME_TIME), MIN_POINTS * 2)

    speed = 1.0 + 0.5 * np.sin(2 * np.pi * (rng.uniform(1, 4) * np.linspace(0, 1, frames) + rng.random()))
    u = np.concatenate([[0.0], np.cumsum(speed[:-1])])
    u *= s[-1] / u[-1]
    xy = np.column_stack([np.interp(u, s, path[:, 0]), np.interp(u, s, path[:, 1])])
    return xy, np.arange(frames) * FRAME_TIME


def _to_rig(xy, rng):
    tilt = _euler(rng.uniform(-15, 15), rng.uniform(-15, 15), 0.0)
    p = rotate(tilt, np.column_stack([xy, np.zeros(len(xy))]))
    p[:, 2] += rng.uniform(0.3, 0.55) + rng.uniform(-1.5, 1.5) * (xy[:, 0] ** 2 + xy[:, 1] ** 2)
    p += rng.normal(0.0, rng.uniform(0.0005, 0.002), p.shape)

    head = _euler(rng.uniform(-30, 30), rng.uniform(-180, 180), rng.uniform(-5, 5))
    head_position = np.array([0.0, 1.6, 0.0]) + rng.normal(0.0, 0.05, 3)
    return rotate(head, p) + head_position, head


def _filter(points, times):
    kept = [0]
    for i in range(1, len(points)):
        if np.linalg.norm(points[i] - points[kept[-1]]) >= MIN_POINT_DISTANCE:
            kept.append(i)
            if len(kept) == MAX_POINTS:
                break
    return points[kept], times[kept]


def make_stroke(path, label, rng, closed):
    path = _round_corners(_jitter_vertices(path, rng), rng)
    path = _distort(path, rng)
    path = _wobble(_dense(path, 256), rng, closed)
    xy, times = _timed(path, rng)
    points, head = _to_rig(xy, rng)
    points, times = _filter(points, times)
    if len(points) < MIN_POINTS:
        return None

    return {
        "label": label,
        "source": "synthetic",
        "device": "synthetic",
        "headRotation": [round(float(v), 5) for v in head],
        "points": [[round(float(x), 5), round(float(y), 5), round(float(z), 5), round(float(t), 5)]
                   for (x, y, z), t in zip(points, times)],
    }


def _scribble(rng):
    points = rng.uniform(-1, 1, (rng.integers(3, 9), 2))
    for _ in range(3):
        points = np.vstack([points[:1], (points[:-1] * 3 + points[1:]) / 4, (points[:-1] + points[1:] * 3) / 4, points[-1:]])
    return points


def _partial(rng):
    variants = [v for vs in rune_variants().values() for v in vs]
    path = _dense(variants[rng.integers(len(variants))], 128)
    length = int(len(path) * rng.uniform(0.25, 0.65))
    start = rng.integers(0, len(path) - length)
    return path[start:start + length]


def noise_path(rng):
    kind = rng.random()
    if kind < 0.3:
        return _scribble(rng), False
    if kind < 0.5:
        return rng.uniform(-1, 1, (rng.integers(2, 6), 2)), False
    if kind < 0.7:
        return _partial(rng), False
    negatives = hard_negatives()
    path = negatives[rng.integers(len(negatives))]
    return path, bool(np.allclose(path[0], path[-1]))


def generate(label, count, rng):
    variants = rune_variants().get(label)
    strokes = []
    while len(strokes) < count:
        if label == "noise":
            path, closed = noise_path(rng)
        else:
            path = variants[rng.integers(len(variants))]
            closed = label in CLOSED
        stroke = make_stroke(path, label, rng, closed)
        if stroke is not None:
            strokes.append(stroke)
    return strokes
