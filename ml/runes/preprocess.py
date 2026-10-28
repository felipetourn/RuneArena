# Mirror of Assets/Scripts/Strokes/StrokePreprocessor.cs. Any change here has to be made there too.
import numpy as np

SAMPLE_COUNT = 64
RASTER_SIZE = 28
RASTER_MARGIN = 2
RASTER_SUBSTEPS = 4


def rotate(q, v):
    """Rotates (N, 3) vectors by the Unity quaternion q = (x, y, z, w)."""
    u = np.asarray(q[:3], dtype=np.float64)
    w = q[3]
    t = 2.0 * np.cross(u, v)
    return v + w * t + np.cross(u, t)


def inverse(q):
    return (-q[0], -q[1], -q[2], q[3])


def project(points, head_rotation):
    """Rig-space points (N, >=3) into the head plane captured at stroke start (x = right, y = up)."""
    p = rotate(inverse(head_rotation), np.asarray(points, dtype=np.float64)[:, :3])
    return p[:, :2]


def path_length(points):
    return float(np.sum(np.linalg.norm(np.diff(points, axis=0), axis=1)))


def resample(points, n=SAMPLE_COUNT):
    points = np.asarray(points, dtype=np.float64)
    interval = path_length(points) / (n - 1)
    out = np.empty((n, 2))
    out[0] = prev = points[0]
    accumulated = 0.0
    count = 1
    i = 1

    while i < len(points) and count < n:
        cur = points[i]
        d = float(np.linalg.norm(cur - prev))
        if d > 0.0 and accumulated + d >= interval:
            prev = prev + (cur - prev) * ((interval - accumulated) / d)
            out[count] = prev
            count += 1
            accumulated = 0.0
        else:
            accumulated += d
            prev = cur
            i += 1

    out[count:] = points[-1]
    return out


def normalize(points):
    """Centroid at the origin and uniform scale so the larger bounding box side is 1."""
    size = float(np.max(points.max(axis=0) - points.min(axis=0)))
    scale = 1.0 / size if size > 1e-6 else 1.0
    return (points - points.mean(axis=0)) * scale


def rasterize(points, size=RASTER_SIZE, margin=RASTER_MARGIN, substeps=RASTER_SUBSTEPS):
    """Draws normalized points as a (size, size) float32 image in [0, 1], row 0 at the top."""
    center = (points.min(axis=0) + points.max(axis=0)) * 0.5
    scale = size - 1 - 2 * margin
    half = (size - 1) * 0.5
    px = (points[:, 0] - center[0]) * scale + half
    py = half - (points[:, 1] - center[1]) * scale

    t = np.arange(substeps) / substeps
    xs = (px[:-1, None] + (px[1:] - px[:-1])[:, None] * t).ravel()
    ys = (py[:-1, None] + (py[1:] - py[:-1])[:, None] * t).ravel()
    xs = np.append(xs, px[-1])
    ys = np.append(ys, py[-1])

    image = np.zeros((size, size), dtype=np.float64)
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    fx = xs - x0
    fy = ys - y0
    for dx, dy, w in ((0, 0, (1 - fx) * (1 - fy)), (1, 0, fx * (1 - fy)),
                      (0, 1, (1 - fx) * fy), (1, 1, fx * fy)):
        cx = x0 + dx
        cy = y0 + dy
        ok = (cx >= 0) & (cx < size) & (cy >= 0) & (cy < size)
        np.add.at(image, (cy[ok], cx[ok]), w[ok])

    return np.minimum(image, 1.0).astype(np.float32)


def process(stroke):
    """Raw stroke dict -> (64x2 normalized points, 28x28 image)."""
    points = normalize(resample(project(stroke["points"], stroke["headRotation"])))
    return points, rasterize(points)
