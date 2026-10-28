"""Checks that the C# pipeline (RuneArena > Export Parity Data) matches the Python one."""
from pathlib import Path

import numpy as np

from runes.preprocess import RASTER_SIZE, process
from runes.protractor import Protractor
from runes.strokes import load


def main():
    path = Path("data/parity_cs.txt")
    if not path.exists():
        print("run RuneArena > Export Parity Data in Unity first")
        return

    protractor = Protractor()
    lines = path.read_text(encoding="utf-8").splitlines()
    worst = 0.0
    mismatches = 0
    for line in lines:
        parts = line.split("\t")
        name, cs_label, cs_score = parts[0], parts[1], float(parts[2])
        cs_image = np.array(parts[3:], dtype=np.float64).reshape(RASTER_SIZE, RASTER_SIZE)

        points, image = process(load(Path("data/mouse") / name))
        label, score = protractor.recognize(points)
        diff = float(np.abs(image - cs_image).max())
        worst = max(worst, diff)
        if label != cs_label or abs(score - cs_score) > 1e-3 or diff > 1e-3:
            mismatches += 1
            print(f"{name}: python {label} {score:.4f} / c# {cs_label} {cs_score:.4f} / max pixel diff {diff:.2e}")

    print(f"{len(lines)} strokes, max pixel diff {worst:.2e}, mismatches {mismatches}")


if __name__ == "__main__":
    main()
