"""DeSaad, the healer, to Desaad.fbx and its albedo: rescaled, with the takes it uses renamed after the
healer's moves. Its download also carries a sword on its own bone, taken out of the .glb beforehand.

    blender -b -P desaad.py -- desaad_sin_espada.glb <output folder>

Model by CAPTAAINR (sketchfab.com/CAPTAAINR), CC-BY-4.0.
"""
import bpy, sys, os
sys.path.append(os.path.dirname(__file__))
import common

# Take -> clip. The skill raises both hands, which reads as the heal; its first attack is the zap.
CLIPS = {
    "IDLE001": "Idle",
    "MOVE001": "Walk",
    "SKILL001": "Heal",
    "ATTACK001": "Zap",
    "DAMAGE002": "Hit",
    "STUN001": "Stun",
    "DIE001": "Death",
}
HEIGHT = 2.0
# Its death collapses by this frame and then floats back up, which the game's sink can't hide: it holds here.
DEATH_END = 18


def hold_from(action, frame):
    """Every channel keeps its value at frame until the end of the action."""
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag = strip.channelbag(slot)
                if bag is None:
                    continue
                for curve in bag.fcurves:
                    value = curve.evaluate(frame)
                    for key in curve.keyframe_points:
                        if key.co.x >= frame:
                            key.co.y = value
                            key.handle_left.y = value
                            key.handle_right.y = value
                    curve.update()


args = sys.argv[sys.argv.index("--")+1:]
src, out = args
arm = common.load(src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
common.keep_actions(CLIPS)
hold_from(bpy.data.actions["Death"], DEATH_END)
height = common.top(arm, meshes, bpy.data.actions["Idle"])
common.flatten(arm, meshes, HEIGHT / height, "Desaad")

image = next(i for i in bpy.data.images if i.packed_file is not None)
with open(os.path.join(out, "Desaad_Albedo.png"), "wb") as f:
    f.write(image.packed_file.data)
meshes[0].name = "Body"
common.export(os.path.join(out, "Desaad.fbx"), [arm] + meshes)
