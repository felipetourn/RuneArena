"""Generates the arena's textures into the Unity project.

Every stone texture tiles. The size each tile covers in the world is set where the materials are
made (ColosseumBuilderEditor), so if you change it here change it there too:
  sand 3 m, travertine 3 m (5 courses of 0.6 m), marble 2 m (slabs of 1 m), granite 1.5 m.
The earth (for the earth rune's walls) and the ivy are tiled by the effects' materials instead.

The sun angles of the two skies are the ones in ArenaLighting's presets.
The particle sprites go to a Fx folder next to the arena textures, and the spectators to a Crowd one.
"""
import argparse
import time
from pathlib import Path

import numpy as np

from arena import fx, props, sky, spectators, stone
from arena.image import normal_from_height, save_albedo, save_normal


def main():
    parser = argparse.ArgumentParser(description="Generate the arena textures.")
    parser.add_argument("--out", default="../../Assets/Textures/Arena")
    parser.add_argument("--seed", type=int, default=7)
    parser.add_argument("--sky-width", type=int, default=4096)
    parser.add_argument("--only", nargs="*", help="names to generate, all by default")
    args = parser.parse_args()

    out = Path(args.out)
    rng = np.random.default_rng(args.seed)

    def wanted(name):
        return not args.only or name in args.only

    def stone_set(name, maps, strength):
        color, smoothness, height = maps
        save_albedo(out / f"{name}_Albedo.png", color, smoothness=smoothness)
        save_normal(out / f"{name}_Normal.png", normal_from_height(height, strength))

    jobs = [
        ("Sand", lambda: stone_set("Sand", stone.sand(1024, rng), 0.5)),
        ("Travertine", lambda: stone_set("Travertine", stone.travertine(1024, 5, rng), 0.45)),
        ("Marble", lambda: stone_set("Marble", stone.marble(1024, rng, slabs=2), 0.4)),
        ("Granite", lambda: stone_set("Granite", stone.granite(512, rng, slabs=1), 0.4)),
        ("Gate", lambda: gate(out, rng)),
        ("Banner", lambda: banner(out, rng)),
        ("Sky", lambda: skies(out, args.sky_width)),
        ("Earth", lambda: stone_set("Earth", stone.earth(1024, rng), 0.5)),
        ("Ivy", lambda: ivy(out.parent / "Fx", rng)),
        ("Fx", lambda: effects(out.parent / "Fx", rng)),
        ("Parchment", lambda: parchment(out, rng)),
        ("Runes", lambda: runes(out.parent.parent / "Resources" / "Runes")),
        ("Ring", lambda: ring(out.parent.parent / "Resources" / "Runes")),
        ("Cards", lambda: cards(out.parent / "UI")),
        # Its own generator, so it comes out the same whatever else is made.
        ("Spectators", lambda: crowd(out.parent / "Crowd", np.random.default_rng(args.seed))),
    ]
    for name, job in jobs:
        if not wanted(name):
            continue
        start = time.time()
        job()
        print(f"{name}: {time.time() - start:.1f}s")


def crowd(out, rng):
    out.mkdir(parents=True, exist_ok=True)
    spectators.atlas(rng).save(out / "Spectators.png")


def gate(out, rng):
    color, alpha, height, _ = props.portcullis(512, rng)
    save_albedo(out / "Gate_Albedo.png", color, alpha=alpha)
    save_normal(out / "Gate_Normal.png", normal_from_height(height, 0.6))


def circle(n, radius=1.0):
    return [(radius * np.cos(a), radius * np.sin(a)) for a in np.linspace(0, 2 * np.pi, n + 1)]


def spiral(n=48):
    # 1.5 turns from the center out, like spiral() in ml/runes/shapes.py.
    last = 3 * np.pi
    return [((0.15 + t / last) * np.cos(t), (0.15 + t / last) * np.sin(t)) for t in np.linspace(0, last, n)]


# One banner per rune, in the color of its spell (see the spells' Color in the Arena scene), with the
# rune as the player draws it. Light cloths get a dark emblem instead of gold.
GOLD = (212, 168, 72)
BANNERS = {
    "circle": ((150, 42, 16), GOLD, [circle(32)]),
    "lightning": ((196, 160, 32), (62, 40, 14), [[(-1, 1), (1, 1), (-1, -1), (1, -1)]]),
    "triangle": ((24, 70, 150), GOLD, [[(0, 0.95), (1.1, -0.95), (-1.1, -0.95), (0, 0.95)]]),
    "square": ((96, 62, 30), GOLD, [[(-1, 1), (1, 1), (1, -1), (-1, -1), (-1, 1)]]),
    "spiral": ((34, 96, 40), GOLD, [spiral()]),
    "v": ((118, 182, 212), (26, 46, 82), [[(-1, 1), (0, -1), (1, 1)]]),
}


def banner(out, rng):
    from arena.image import rgb
    for label, (cloth, emblem, glyph) in BANNERS.items():
        color, alpha, height = props.banner(512, 1536, rng, rgb(*cloth), rgb(*emblem), glyph)
        save_albedo(out / f"Banner_{label}_Albedo.png", color, alpha=alpha)
        save_normal(out / f"Banner_{label}_Normal.png", normal_from_height(height, 0.4))


# One page of the grimoire the player holds; HandPropsBuilder's pages are 10 x 13 cm, the same shape.
def parchment(out, rng):
    color, smoothness = props.parchment(512, 666, rng)
    save_albedo(out / "Parchment_Albedo.png", color, smoothness=smoothness)


# The runes on the grimoire's pages, the same shapes as on the banners: a white stroke the game tints with
# the spell's color, and a wider one it puts under it so it reads apart from the ring.
def runes(out):
    for label, (_, _, glyph) in BANNERS.items():
        for suffix, radius in (("", 10), ("_Outline", 17)):
            color, alpha = fx.rune(256, glyph, radius)
            save_albedo(out / f"Rune_{label}{suffix}.png", color, alpha=alpha)


# What the grimoire draws around a spread's rune: a thin circle with short ticks around it, like a dial.
# White, for the game to tint.
def ring(out, ticks=48):
    lines = [circle(96)]
    for a in np.linspace(0, 2 * np.pi, ticks, endpoint=False):
        lines.append([(1.07 * np.cos(a), 1.07 * np.sin(a)), (1.12 * np.cos(a), 1.12 * np.sin(a))])
    color, alpha = fx.rune(256, lines, 1.2)
    save_albedo(out / "Rune_Ring.png", color, alpha=alpha)


# The upgrade cards: the obsidian button and its states in light gray, keeping their shading, so the
# tier's color tinted over them comes out clear. The dark originals would turn any tint almost black.
CARD_STATES = ("", "_hover", "_pressed", "_disabled")


def cards(out):
    from PIL import Image

    def load(state):
        source = np.asarray(Image.open(out / f"obsidiana_boton{state}.png").convert("RGBA")).astype(np.float32) / 255
        return source[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32), source[..., 3]

    # Stretched between the plain button's darkest and brightest, so the rim keeps standing out, and the same
    # for every state, so the pressed one still goes darker and the hover one lighter.
    light, alpha = load("")
    seen = light[alpha > 0.5]
    low, high = np.percentile(seen, 2), np.percentile(seen, 98)
    for state in CARD_STATES:
        light, alpha = load(state)
        gray = 0.6 + 0.4 * np.clip((light - low) / max(high - low, 1e-3), 0, 1)
        card = np.dstack([gray, gray, gray, alpha])
        Image.fromarray((card * 255 + 0.5).astype(np.uint8), "RGBA").save(out / f"obsidiana_carta{state}.png")


def ivy(out, rng):
    color, alpha, height = props.ivy(512, rng)
    save_albedo(out / "Ivy_Albedo.png", color, alpha=alpha)
    save_normal(out / "Ivy_Normal.png", normal_from_height(height, 0.5))


def effects(out, rng):
    for name, (color, alpha) in (("Fx_Glow", fx.glow(128)), ("Fx_Spark", fx.spark(128)),
                                 ("Fx_Puff", fx.puff(128, rng)), ("Fx_Ring", fx.ring(256)),
                                 ("Fx_Line", fx.line(64, 16)), ("Fx_Water", fx.water(256, 512, rng)),
                                 ("Fx_FireSheet", fx.fire_sheet(128, rng)), ("Fx_SmokeSheet", fx.smoke_sheet(128, rng))):
        save_albedo(out / f"{name}.png", color, alpha=alpha)


def skies(out, width):
    for name, preset in (("Noon", sky.NOON), ("Sunset", sky.SUNSET)):
        save_albedo(out / f"Sky_{name}.png", sky.panorama(preset, width, width // 2))


if __name__ == "__main__":
    main()
