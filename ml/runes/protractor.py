# Mirror of Assets/Scripts/Strokes/ProtractorRecognizer.cs, used as the baseline.
import numpy as np

from .preprocess import normalize, resample
from .shapes import rune_variants


def _unit(points):
    v = points.ravel()
    return v / np.linalg.norm(v)


class Protractor:
    def __init__(self, max_rotation_degrees=30.0):
        self.max_rotation = np.radians(max_rotation_degrees)
        self.labels = []
        templates = []
        for label, variants in rune_variants().items():
            for path in variants:
                self.labels.append(label)
                templates.append(_unit(normalize(resample(path))))
        self.templates = np.array(templates)

    def recognize(self, normalized):
        """Returns (label, score) for 64x2 normalized points. Score is a cosine similarity."""
        c = _unit(normalized)
        tx, ty = self.templates[:, 0::2], self.templates[:, 1::2]
        cx, cy = c[0::2], c[1::2]
        a = tx @ cx + ty @ cy
        b = tx @ cy - ty @ cx
        angle = np.clip(np.arctan2(b, a), -self.max_rotation, self.max_rotation)
        scores = a * np.cos(angle) + b * np.sin(angle)
        best = int(np.argmax(scores))
        return self.labels[best], float(scores[best])
