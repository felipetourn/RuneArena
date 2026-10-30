"""Arena sand and the stones of the colosseum. Each function returns (color, smoothness, height):
color is sRGB 0..1, smoothness 0..1, height in texels (it becomes the normal map)."""
import numpy as np

from .image import lerp, rgb
from .noise import blur, cells, remap, smoothstep, spectral


def sand(size, rng):
    """Raked arena sand with small stones. Large-scale variation is left to the floor layout, so this
    tile stays even and doesn't show its repeats."""
    shape = (size, size)
    broad = spectral(shape, rng, beta=2.0, low=2, high=24)
    grain = spectral(shape, rng, beta=0.6, low=60)
    ripples = np.sin(np.linspace(0, 2 * np.pi * 14, size, endpoint=False))[None, :] + 0.6 * spectral(shape, rng, beta=2.5, low=2, high=10)

    color = lerp(rgb(190, 163, 122), rgb(204, 180, 138), smoothstep(broad, -2.0, 2.0))
    color = color * (1 + 0.06 * grain[..., None])
    # Dark mineral specks and pale shell bits.
    color = lerp(color, rgb(92, 78, 64), (spectral(shape, rng, beta=0.0, low=200) > 2.6) * 0.8)
    color = lerp(color, rgb(236, 226, 206), (spectral(shape, rng, beta=0.0, low=200) > 2.8) * 0.7)

    height = 0.4 * broad + 0.35 * grain + 0.4 * ripples

    # Pebbles: the cells that come out small enough, rounded, a bit darker or lighter than the sand.
    d1, _, index = cells(shape, 500, rng)
    radius = rng.uniform(1.0, 1.0, 500)[index] * rng.uniform(1.5, 6.0, 500)[index] * size / 1024
    pebble = smoothstep(radius - d1, 0.0, 1.5)
    tint = rng.uniform(0.85, 1.1, 500)[index]
    hue = lerp(rgb(196, 184, 166), rgb(150, 128, 104), rng.random(500)[index])
    color = lerp(color, hue * tint[..., None], pebble)
    dome = np.sqrt(np.clip(1 - (d1 / np.maximum(radius, 1e-3)) ** 2, 0, 1))
    height = height + pebble * dome * radius * 0.8

    smoothness = 0.08 + 0.12 * pebble
    return color, smoothness, height


def _row_blocks(width, rng, lo, hi):
    """Block lengths for one course that add up to the tile width exactly."""
    lengths = []
    while sum(lengths) < width - hi:
        lengths.append(int(rng.uniform(lo, hi)))
    lengths.append(width - sum(lengths))
    if lengths[-1] < lo * 0.6 and len(lengths) > 1:
        lengths[-2] += lengths.pop()
    return lengths


def travertine(size, rows, rng):
    """Ashlar wall: courses of travertine blocks with thin joints, pitted and weathered.
    A tile is rows courses tall and as wide as it is tall."""
    shape = (size, size)
    course = size // rows
    block = np.zeros(shape, int)
    edge = np.zeros(shape)
    tops = np.zeros(shape)
    count = 0

    x = np.arange(size)
    for r in range(rows):
        y0, y1 = r * course, (r + 1) * course if r < rows - 1 else size
        offset = int(rng.uniform(0, size))
        start = 0
        for length in _row_blocks(size, rng, course * 1.3, course * 2.8):
            u = (x - offset - start) % size
            inside = u < length
            dx = np.minimum(u, length - 1 - u).astype(float)
            for y in range(y0, y1):
                dy = min(y - y0, y1 - 1 - y)
                block[y, inside] = count
                edge[y, inside] = np.minimum(dx[inside], dy)
                tops[y, inside] = (y - y0) / (y1 - y0)
            start += length
            count += 1

    tint = rng.uniform(0.86, 1.06, count)[block]
    warm = rng.uniform(0.0, 1.0, count)[block]
    tint = rng.uniform(0.94, 1.04, count)[block]
    base = lerp(rgb(222, 206, 174), rgb(212, 186, 146), warm) * tint[..., None]

    # Travertine is layered: faint horizontal strata, bent a little.
    warp = spectral(shape, rng, beta=2.5, low=1, high=8)
    strata = np.sin(np.linspace(0, 2 * np.pi * rows * 7, size, endpoint=False)[:, None] + warp * 1.5)
    broad = spectral(shape, rng, beta=2.2, low=2, high=40)
    color = base * (1 + 0.015 * strata[..., None] + 0.05 * broad[..., None])

    # Pores: small holes stretched along the strata.
    pores = spectral(shape, rng, beta=1.0, low=20, stretch=(5.0, 1.0))
    pits = smoothstep(pores, 2.4, 3.0)
    color = lerp(color, rgb(150, 128, 98), pits * 0.7)

    # Rain grime runs down from the joints and darkens the lower part of each block.
    streaks = spectral(shape, rng, beta=1.5, low=3, stretch=(1.0, 8.0))
    grime = smoothstep(streaks, 0.3, 2.0) * 0.16 + (1 - tops) ** 4 * 0.06
    color = color * (1 - grime[..., None])

    # Bevelled edges, chipped here and there; joints sit back and are darker.
    joint = size / 512 * 1.4
    bevel = size / 512 * 6
    chips = smoothstep(spectral(shape, rng, beta=1.2, low=16), 0.9, 1.8) * smoothstep(-edge, -bevel * 3, 0)
    face = smoothstep(edge, joint, joint + bevel)
    height = face * 3.0 - chips * 2.0 + 0.25 * broad - pits * 1.2 + 0.03 * strata
    mortar = 1 - smoothstep(edge, joint * 0.5, joint + 1)
    color = lerp(color, rgb(176, 162, 136), mortar)
    color = color * (1 - 0.25 * chips[..., None])
    # Inside the joints there is little light.
    color = color * (0.85 + 0.15 * face[..., None])

    smoothness = 0.12 + 0.06 * face - 0.08 * pits
    return color, smoothness, height


def marble(size, rng, slabs=2, cream=False):
    """White marble with grey veins, cut into slabs (joints every 1/slabs of the tile, both ways)."""
    shape = (size, size)
    warp = spectral(shape, rng, beta=2.5, low=1, high=5)
    warp2 = spectral(shape, rng, beta=2.0, low=4, high=24)
    thickness = 0.6 + 0.8 * smoothstep(spectral(shape, rng, beta=2.0, low=1, high=6), -1.0, 1.5)
    yy, xx = np.mgrid[0:size, 0:size] / size

    def veins(kx, ky, width, turbulence):
        s = np.sin(np.pi * (kx * xx + ky * yy) + turbulence * warp + 0.08 * turbulence * warp2)
        return np.exp(-(s / (width * thickness)) ** 2)

    main = veins(2, 2, 0.06, 2.0)
    fine = veins(-4, 2, 0.03, 3.0) * 0.5
    clouds = spectral(shape, rng, beta=2.2, low=2, high=30)

    white = rgb(232, 228, 218) if not cream else rgb(226, 214, 192)
    grey = rgb(150, 148, 146)
    color = lerp(white, rgb(214, 210, 202), smoothstep(clouds, -0.5, 2.0) * 0.7)
    color = lerp(color, grey, np.clip(main * 0.7 + fine * 0.45, 0, 1))
    color = lerp(color, rgb(196, 170, 120), veins(2, -2, 0.025, 2.5) * 0.3)
    color = color * (1 + 0.02 * spectral(shape, rng, beta=0.5, low=80)[..., None])

    # Slab joints.
    step = size // slabs
    u = np.minimum(np.arange(size) % step, step - np.arange(size) % step)
    gap = size / 1024 * 2.0
    joint = np.minimum(u[None, :], u[:, None]).astype(float)
    face = smoothstep(joint, gap * 0.5, gap + 2)
    color = color * (0.7 + 0.3 * face[..., None])

    wear = spectral(shape, rng, beta=1.8, low=3, high=60)
    height = face * 2.0 + 0.08 * wear - main * 0.08
    smoothness = 0.42 + 0.12 * face - 0.1 * smoothstep(wear, 1.0, 2.5)
    return color, smoothness, height


def granite(size, rng, slabs=1):
    """Speckled grey granite for the plinth and steps: grains of quartz, feldspar and mica."""
    shape = (size, size)
    color = np.ones((size, size, 3)) * rgb(124, 120, 118)
    count = int(size * size / 14)
    d1, d2, index = cells(shape, count, rng)
    kind = rng.random(count)[index]
    shade = rng.uniform(0.85, 1.1, count)[index]
    edge = 0.6 + 0.4 * smoothstep(d2 - d1, 0.0, 1.0)
    color = color * shade[..., None]
    color = lerp(color, rgb(58, 56, 58), (kind < 0.12) * edge)
    color = lerp(color, rgb(150, 130, 124), ((kind > 0.6) & (kind < 0.8)) * edge)
    color = lerp(color, rgb(196, 194, 190), (kind > 0.94) * edge)
    color = color * (1 + 0.06 * spectral(shape, rng, beta=2.0, low=2, high=20)[..., None])

    step = size // slabs
    u = np.minimum(np.arange(size) % step, step - np.arange(size) % step).astype(float)
    face = smoothstep(u, size / 512 * 1.5, size / 512 * 4)[None, :] * np.ones((size, 1))
    color = color * (0.7 + 0.3 * face[..., None])

    height = face * 2.0 + 0.15 * spectral(shape, rng, beta=1.0, low=20)
    smoothness = 0.35 + 0.1 * (kind > 0.92) * edge
    return color, smoothness, height


def earth(size, rng):
    """Packed earth for the walls the earth rune raises: the arena's sand pressed into layers,
    with stones and dry cracks. Darker and browner than the floor, but clearly the same ground."""
    shape = (size, size)
    yy = np.arange(size)[:, None] / size

    # Layers of slightly different soil, wavy, a few per tile.
    warp = spectral(shape, rng, beta=2.6, low=1, high=6)
    layers = np.sin(2 * np.pi * (6 * yy + 0.12 * warp))
    band = smoothstep(layers, -0.3, 0.6)
    color = lerp(rgb(150, 118, 84), rgb(176, 142, 100), band)
    color = color * (1 + 0.08 * spectral(shape, rng, beta=1.8, low=3, high=60)[..., None])
    color = color * (1 + 0.05 * spectral(shape, rng, beta=0.5, low=80)[..., None])

    # Stones stuck in it.
    d1, _, index = cells(shape, 260, rng)
    radius = rng.uniform(2.0, 9.0, 260)[index] * size / 1024
    stone = smoothstep(radius - d1, 0.0, 1.5)
    tint = lerp(rgb(150, 132, 108), rgb(104, 88, 70), rng.random(260)[index])
    color = lerp(color, tint, stone)

    # Dry cracks along cell edges, only where a noise lets them through.
    c1, c2, _ = cells(shape, 40, rng)
    crack = (1 - smoothstep(c2 - c1, 0.0, 2.5 * size / 512)) * smoothstep(spectral(shape, rng, beta=2.0, low=2, high=12), -0.2, 0.6)
    color = color * (1 - 0.55 * crack[..., None])

    height = 1.2 * band + 0.3 * spectral(shape, rng, beta=1.2, low=10) + stone * 2.0 - crack * 3.0
    smoothness = 0.06 + 0.1 * stone
    return color, smoothness, height
