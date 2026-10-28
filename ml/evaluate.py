import argparse
import os
from pathlib import Path

import numpy as np

from runes import LABELS
from runes.preprocess import process
from runes.protractor import Protractor
from runes.report import REJECT, confusion
from runes.strokes import load, load_folder

DEFAULT_DATA = os.path.expandvars(r"%USERPROFILE%\AppData\LocalLow\DefaultCompany\RuneArena\strokes")


def main():
    parser = argparse.ArgumentParser(description="Compare Protractor and the CNN on recorded strokes.")
    parser.add_argument("--data", default=DEFAULT_DATA, help="folder with <label>/*.json")
    parser.add_argument("--split", default=None, help="file listing the strokes to use (e.g. data/real_test_split.txt)")
    parser.add_argument("--model", default="../Assets/Models/rune_cnn.onnx")
    parser.add_argument("--protractor-threshold", type=float, default=0.85)
    parser.add_argument("--cnn-threshold", type=float, default=0.8)
    args = parser.parse_args()

    if args.split:
        strokes = [load(p) for p in Path(args.split).read_text(encoding="utf-8").split()]
    else:
        strokes = load_folder(args.data)
    if not strokes:
        print(f"no strokes in {args.data}")
        return

    processed = [process(s) for s in strokes]
    truth = [s["label"] for s in strokes]

    protractor = Protractor()
    p_pred = []
    for points, _ in processed:
        label, score = protractor.recognize(points)
        p_pred.append(label if score >= args.protractor_threshold else REJECT)
    print(confusion(list(zip(truth, p_pred)), "protractor"))

    if not Path(args.model).exists():
        print(f"\nno model at {args.model}")
        return

    import onnxruntime as ort
    session = ort.InferenceSession(args.model)
    c_pred = []
    for _, image in processed:
        logits = session.run(None, {"input": image[None, None]})[0][0]
        probs = np.exp(logits - logits.max())
        probs /= probs.sum()
        best = int(np.argmax(probs))
        c_pred.append(LABELS[best] if probs[best] >= args.cnn_threshold else REJECT)
    print()
    print(confusion(list(zip(truth, c_pred)), "cnn"))


if __name__ == "__main__":
    main()
