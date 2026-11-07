"""Tillagemon, the stage 1 boss, to Tillagemon.fbx and its textures: rescaled, and its takes renamed
after the ground boss's moves.

    blender -b -P tillagemon.py -- TillagemonBoss01_Skeleton.fbx <its textures folder> <output folder>

Its two materials stay two, Tillagemon_A and Tillagemon_B, in that order.
"""
import bpy, sys, os, shutil
sys.path.append(os.path.dirname(__file__))
import common

# Take -> clip. The slam lifts the pitchfork overhead and brings it down 0.8 s in; it is also the
# entrance. The summon is a string of stomps, the volley a thrust.
CLIPS = {
    "Battle_Stand": "Idle",
    "Battle_Walk": "Walk",
    "Battle_Run": "Run",
    "Abnormal_Stun": "Stun",
    "Damage_Weak_01": "Hit",
    "Damage_Die": "Death",
    "Attack_Base_06": "Slam",
    "Attack_Base_03": "Volley",
    "Attack_Base_08_Ready": "ChargeReady",
    "Attack_Base_04": "Summon",
    # Its three plain blows: a swing, a thrust and a sweep of the scythe.
    "Attack_Base_01": "Strike1",
    "Attack_Base_02": "Strike2",
    "Attack_Base_05": "Strike3",
    # The end of its charged stab: it rears back with the pitchfork raised and drives it forward. Only that
    # first part is kept, as its throw (cut below).
    "Attack_Base_10_End": "ChargedStab",
}
# Seconds of ChargedStab kept as the throw: the wind-up and the drive forward, which lets go at 0.65.
THROW = (0.0, 0.75)
HEIGHT = 3.6

args = sys.argv[sys.argv.index("--")+1:]
src, textures, out = args
arm = common.load_fbx(src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
common.rename_clips(CLIPS)
common.cut(bpy.data.actions["ChargedStab"], *THROW, "Throw")
bpy.data.actions.remove(bpy.data.actions["ChargedStab"])
height = common.top(arm, meshes, bpy.data.actions["Idle"])
common.flatten(arm, meshes, HEIGHT / height, "Tillagemon")

for slot, (name, source) in zip(meshes[0].material_slots, (("A", "01"), ("B", "02"))):
    slot.material.name = f"Tillagemon_{name}"
    shutil.copy(os.path.join(textures, f"TillagemonBoss01_{source}_D.png"), os.path.join(out, f"Tillagemon_{name}_Albedo.png"))
    shutil.copy(os.path.join(textures, f"TillagemonBoss01_{source}_N.png"), os.path.join(out, f"Tillagemon_{name}_Normal.png"))
meshes[0].name = "Body"
common.export(os.path.join(out, "Tillagemon.fbx"), [arm] + meshes)
