"""Skeleton dragon (CC-BY 4.0, TRSgamings on Sketchfab) to Dragon.fbx and its textures.

    blender -b -P dragon.py -- skeleton_dragon_ff.glb <output folder>

It keeps 8k triangles and its material; only the clips the flying enemy uses are kept.
"""
import bpy, sys, os
sys.path.append(os.path.dirname(__file__))
import common
args = sys.argv[sys.argv.index("--")+1:]
src, out = args
arm = common.load(src)
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
mat = mesh.material_slots[0].material

# Flying clip: the aerial attack, a hover with wing beats. Brought down to the height of the ground
# clips, so the pivot is under the body in every clip.
acts = {a.name.replace("_GLTF_created_0", ""): a for a in bpy.data.actions}
def first(action, index):
    fc = action.fcurves.find('pose.bones["Bip001_00_11"].location', index=index)
    return fc, fc.keyframe_points[0].co[1]
fly, fly_z = first(acts["attack_5"], 2)
_, ground_z = first(acts["pettyidle"], 2)
for k in fly.keyframe_points:
    for p in (k.co, k.handle_left, k.handle_right):
        p[1] -= fly_z - ground_z

common.keep_actions({"attack_5": "Fly", "cast_1": "WindUp", "hurtsmall": "Hit", "dead": "Death",
                     "stunidle": "Stunned"})
# About 2.2 m wide.
common.flatten(arm, [mesh], 0.8, "Dragon")
while len(mesh.data.uv_layers) > 1:
    mesh.data.uv_layers.remove(mesh.data.uv_layers[-1])
mesh.name = "Body"
mesh.data.name = "Dragon"

# The glossiness map is left out: Unity's Lit gets a flat smoothness instead.
names = {"BSDF_DIFFUSE": "Albedo", "NORMAL_MAP": "Normal", "EMISSION": "Emission"}
for n in mat.node_tree.nodes:
    if n.type == 'TEX_IMAGE':
        for l in mat.node_tree.links:
            if l.from_node == n and l.to_node.type in names:
                common.save_image(n.image, os.path.join(out, f"Dragon_{names[l.to_node.type]}.png"))
                break
common.export(os.path.join(out, "Dragon.fbx"), [arm, mesh])
