import json
from pathlib import Path

from . import LABELS


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(stroke, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(stroke, f, separators=(",", ":"))


def load_folder(root):
    """Strokes under root/<label>/*.json, for every known label that has a folder."""
    strokes = []
    root = Path(root)
    for label in LABELS:
        for path in sorted((root / label).glob("*.json")):
            stroke = load(path)
            stroke["_path"] = str(path)
            strokes.append(stroke)
    return strokes
