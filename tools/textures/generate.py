"""Generates the arena's textures into the Unity project.

Every stone texture tiles. The size each tile covers in the world is set where the materials are
made (ColosseumBuilderEditor), so if you change it here change it there too:
  sand 3 m, travertine 3 m (5 courses of 0.6 m), marble 2 m (slabs of 1 m), granite 1.5 m.
The earth (for the earth rune's walls) and the ivy are tiled by the effects' materials instead.

The sun angles of the two skies are the ones in ArenaLighting's presets.
The particle sprites go to a Fx folder next to the arena textures.
"""
import argparse
import time
from pathlib import Path

import numpy as np

from arena import fx, props, sky, stone
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
    ]
    for name, job in jobs:
        if not wanted(name):
            continue
        start = time.time()
        job()
        print(f"{name}: {time.time() - start:.1f}s")


def gate(out, rng):
    color, alpha, height, _ = props.portcullis(512, rng)
    save_albedo(out / "Gate_Albedo.png", color, alpha=alpha)
    save_normal(out / "Gate_Normal.png", normal_from_height(height, 0.6))


def banner(out, rng):
    color, alpha, height = props.banner(512, 1536, rng)
    save_albedo(out / "Banner_Albedo.png", color, alpha=alpha)
    save_normal(out / "Banner_Normal.png", normal_from_height(height, 0.4))


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
