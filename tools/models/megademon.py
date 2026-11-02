"""Megademon, the stage 3 boss, to Megademon.fbx and its textures. It comes as one 30 second take with
all its moves one after the other; this cuts it into clips.

    blender -b -P megademon.py -- Skeleton.fbx <its textures folder> <output folder>

Its three materials stay three, in the order of TEXTURES.
"""
import bpy, sys, os, shutil
sys.path.append(os.path.dirname(__file__))
import common

# Clip -> (start, end) in seconds of the take, and whether it plays back and forth to loop. The cuts
# sit where it barely moves, so the clips blend into each other.
CLIPS = {
    "Idle": (13.0, 13.6, True),
    "Leap": (0.3, 3.6, False),
    "Combo": (3.6, 5.9, False),
    "Combo2": (7.0, 9.4, False),
    "Cleave": (13.6, 15.3, False),
    "Whirl": (15.4, 17.0, False),
    "Collapse": (17.8, 20.6, False),
    "Reform": (24.4, 27.1, False),
}
TEXTURES = {"m327_1": "Megademon_Body", "m327_2": "Megademon_Armor", "Sword2": "Megademon_Blade"}
# Standing ready, blades up.
HEIGHT = 2.7

args = sys.argv[sys.argv.index("--")+1:]
src, textures, out = args
arm = common.load_fbx(src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
take = arm.animation_data.action
for name, (start, end, pingpong) in CLIPS.items():
    common.cut(take, start, end, name, pingpong)
bpy.data.actions.remove(take)
height = common.top(arm, meshes, bpy.data.actions["Idle"])
common.flatten(arm, meshes, HEIGHT / height, "Megademon")

# One mesh per armor piece in the download: joined, they are one draw per material.
common.select(meshes)
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "Body"
for slot in body.material_slots:
    source = slot.material.name
    slot.material.name = TEXTURES[source]
    shutil.copy(os.path.join(textures, f"{source}_Diffuse.png"), os.path.join(out, f"{TEXTURES[source]}_Albedo.png"))
    shutil.copy(os.path.join(textures, f"{source}_Normal.png"), os.path.join(out, f"{TEXTURES[source]}_Normal.png"))
print("SLOTS", [s.material.name for s in body.material_slots])
common.export(os.path.join(out, "Megademon.fbx"), [arm, body])
