import argparse
from pathlib import Path

import numpy as np

from runes import LABELS
from runes.preprocess import process
from runes.strokes import save
from runes.synth import generate
from runes.preview import save_grid


def main():
    parser = argparse.ArgumentParser(description="Generate synthetic rune strokes.")
    parser.add_argument("--out", default="data/synthetic")
    parser.add_argument("--per-class", type=int, default=2000)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--preview", default="data/synthetic_preview.png")
    args = parser.parse_args()

    rng = np.random.default_rng(args.seed)
    out = Path(args.out)
    preview = []

    for label in LABELS:
        strokes = generate(label, args.per_class, rng)
        for i, stroke in enumerate(strokes):
            save(stroke, out / label / f"syn_{i:05d}.json")
        preview.append([process(s)[1] for s in strokes[:12]])
        print(f"{label}: {len(strokes)}")

    if args.preview:
        save_grid(preview, args.preview)
        print(f"preview: {args.preview}")


if __name__ == "__main__":
    main()
