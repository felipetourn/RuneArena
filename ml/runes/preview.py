import struct
import zlib

import numpy as np


def save_grid(rows, path, scale=3, gap=2):
    """rows: list of lists of 28x28 images in [0, 1]. Writes a grayscale PNG without extra dependencies."""
    size = rows[0][0].shape[0] * scale
    cols = max(len(r) for r in rows)
    height = len(rows) * (size + gap) + gap
    width = cols * (size + gap) + gap
    canvas = np.full((height, width), 40, dtype=np.uint8)

    for r, row in enumerate(rows):
        for c, image in enumerate(row):
            tile = np.kron(image, np.ones((scale, scale)))
            y = gap + r * (size + gap)
            x = gap + c * (size + gap)
            canvas[y:y + size, x:x + size] = (255 - tile * 255).astype(np.uint8)

    raw = b"".join(b"\x00" + canvas[y].tobytes() for y in range(height))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))
