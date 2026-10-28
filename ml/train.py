import argparse
from pathlib import Path

import numpy as np
import torch
from torch import nn

from runes import LABELS
from runes.preprocess import RASTER_SIZE, process
from runes.report import REJECT, confusion
from runes.strokes import load_folder


class RuneNet(nn.Module):
    def __init__(self, classes=len(LABELS)):
        super().__init__()
        self.features = nn.Sequential(
            nn.Conv2d(1, 16, 3, padding=1), nn.ReLU(), nn.MaxPool2d(2),
            nn.Conv2d(16, 32, 3, padding=1), nn.ReLU(), nn.MaxPool2d(2),
        )
        self.classifier = nn.Sequential(
            nn.Flatten(),
            nn.Linear(32 * (RASTER_SIZE // 4) ** 2, 64), nn.ReLU(), nn.Dropout(0.3),
            nn.Linear(64, classes),
        )

    def forward(self, x):
        return self.classifier(self.features(x))


def to_tensors(strokes):
    x = np.stack([process(s)[1] for s in strokes])[:, None]
    y = np.array([LABELS.index(s["label"]) for s in strokes])
    return torch.from_numpy(x), torch.from_numpy(y)


def split(strokes, fraction, rng):
    test, train = [], []
    for label in LABELS:
        group = [s for s in strokes if s["label"] == label]
        order = rng.permutation(len(group))
        n_test = int(round(len(group) * fraction))
        test += [group[i] for i in order[:n_test]]
        train += [group[i] for i in order[n_test:]]
    return train, test


def predict(model, x, threshold):
    with torch.no_grad():
        probs = torch.softmax(model(x), dim=1)
    conf, idx = probs.max(dim=1)
    return [LABELS[i] if c >= threshold else REJECT for c, i in zip(conf.tolist(), idx.tolist())]


def main():
    parser = argparse.ArgumentParser(description="Train the rune CNN and export it to ONNX.")
    parser.add_argument("--synthetic", default="data/synthetic")
    parser.add_argument("--real", default=None, help="folder with real strokes (<label>/*.json)")
    parser.add_argument("--real-test", type=float, default=0.4, help="fraction of real strokes held out for testing")
    parser.add_argument("--real-weight", type=int, default=10, help="times each real training stroke is repeated")
    parser.add_argument("--epochs", type=int, default=12)
    parser.add_argument("--threshold", type=float, default=0.8)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--checkpoint", default="checkpoints/rune_cnn.pt")
    parser.add_argument("--onnx", default="../Assets/Models/rune_cnn.onnx")
    args = parser.parse_args()

    torch.manual_seed(args.seed)
    rng = np.random.default_rng(args.seed)

    synthetic = load_folder(args.synthetic)
    train, val = split(synthetic, 0.1, rng)
    real_test = []
    if args.real:
        real_train, real_test = split(load_folder(args.real), args.real_test, rng)
        train += real_train * args.real_weight
        Path("data").mkdir(exist_ok=True)
        Path("data/real_test_split.txt").write_text("\n".join(s["_path"] for s in real_test), encoding="utf-8")
        print(f"real: {len(real_train)} train, {len(real_test)} test")

    print(f"synthetic: {len(train)} train, {len(val)} val")
    x_train, y_train = to_tensors(train)
    x_val, y_val = to_tensors(val)

    model = RuneNet()
    optimizer = torch.optim.Adam(model.parameters(), lr=1e-3)
    loss_fn = nn.CrossEntropyLoss()

    for epoch in range(args.epochs):
        model.train()
        order = torch.randperm(len(x_train))
        total = 0.0
        for i in range(0, len(order), 128):
            batch = order[i:i + 128]
            optimizer.zero_grad()
            loss = loss_fn(model(x_train[batch]), y_train[batch])
            loss.backward()
            optimizer.step()
            total += loss.item() * len(batch)

        model.eval()
        with torch.no_grad():
            acc = (model(x_val).argmax(dim=1) == y_val).float().mean().item()
        print(f"epoch {epoch + 1:2d}  loss {total / len(order):.4f}  val acc {acc * 100:.1f}%")

    model.eval()
    print()
    print(confusion(list(zip([LABELS[i] for i in y_val.tolist()], predict(model, x_val, args.threshold))), "synthetic val"))
    if real_test:
        x_test, y_test = to_tensors(real_test)
        print()
        print(confusion(list(zip([LABELS[i] for i in y_test.tolist()], predict(model, x_test, args.threshold))), "real test"))

    Path(args.checkpoint).parent.mkdir(parents=True, exist_ok=True)
    torch.save(model.state_dict(), args.checkpoint)
    export(model, args.onnx, x_val[:16])


def export(model, path, sample):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    dummy = torch.zeros(1, 1, RASTER_SIZE, RASTER_SIZE)
    kwargs = dict(input_names=["input"], output_names=["logits"], opset_version=15)
    try:
        torch.onnx.export(model, dummy, path, dynamo=False, **kwargs)
    except TypeError:
        torch.onnx.export(model, dummy, path, **kwargs)
    print(f"\nonnx: {path}")

    try:
        import onnxruntime as ort
    except ImportError:
        return
    session = ort.InferenceSession(path)
    with torch.no_grad():
        expected = model(sample).numpy()
    got = np.concatenate([session.run(None, {"input": sample[i:i + 1].numpy()})[0] for i in range(len(sample))])
    print(f"onnx check: max abs diff {np.abs(expected - got).max():.2e}")


if __name__ == "__main__":
    main()
