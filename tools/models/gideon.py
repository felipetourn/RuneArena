"""Gideon (Paragon, by Epic Games) to Gideon.fbx for the stage 2 boss: its lowest-but-one level of detail,
with its seven materials baked into one texture set, and the clips the boss uses, which come in
their own files.

    blender -b -P gideon.py -- Gideon_Undertow.FBX <textures folder> <animations folder> <output folder> 2048

Epic released the Paragon characters for Unreal Engine projects.
"""
import bpy, sys, os
import numpy as np
sys.path.append(os.path.dirname(__file__))
import common

# Clip -> animation file. The stage 2 boss: a rift mage that keeps its distance, shoots bolts, opens
# rifts under the player, steps through space, and once hurt levitates into a storm.
CLIPS = {
    "Idle": "Idle",
    "Run": "Jog_Fwd",
    "Bolt1": "Primary_Attack_A_Medium",
    "Bolt2": "Primary_Attack_B_Medium",
    "Bolt3": "Primary_Attack_C_Medium",
    "Cast": "Cast",
    "Rift": "Cosmic_Rift",
    "Blink": "Torn_Space",
    "Levitate": "Emote_Master_Levitate_T3",
    "Portal": "Emote_InfinitePortal_T3",
    "Hit": "HitReact_Front",
    "Death": "Death_Back",
}
# Material -> (color texture, alpha texture or None). The glowing ring has no texture: it bakes as its
# color into the emission.
MATERIALS = {
    "M_Head": ("Head_BaseColor", None),
    "M2_body": ("body_color", "body_opmask"),
    "M3_leg": ("leg_color", "leg_opas_mask"),
    "M5_whell": ("whell_color", None),
    "M_Undertow_Barnacle_Tongue_Dem": ("tongue_color", "tongue_mask"),
    "M6_hawk": ("hawk_color", "Hawk_mask"),
    "M_Gideon_Ring_Glow": (None, None),
}
GLOW = (0.35, 0.85, 1.0, 1.0)
LOW, HIGH = "Gideon_Undertow_LOD3", "Gideon_Undertow_LOD0"

args = sys.argv[sys.argv.index("--")+1:]
src, textures, animations, out, size = args[0], args[1], args[2], args[3], int(args[4])
arm = common.load_fbx(src)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name not in (LOW, HIGH):
        bpy.data.objects.remove(o)

# The clips: each file brings its own copy of the skeleton, dropped once its take is kept.
for clip, file in CLIPS.items():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(animations, file + ".FBX"), ignore_leaf_bones=True)
    for o in set(bpy.data.objects) - before:
        if o.animation_data and o.animation_data.action:
            action = o.animation_data.action
            action.name = clip
            action.use_fake_user = True
            # The files also key their own skeleton object's transform, which would undo the main one's.
            for fc in [fc for fc in action.fcurves if not fc.data_path.startswith("pose.")]:
                action.fcurves.remove(fc)
        bpy.data.objects.remove(o)
for a in list(bpy.data.actions):
    if a.name not in CLIPS:
        bpy.data.actions.remove(a)
# The entrance is the end of its portal emote: it drops out of a portal overhead, lands and stands up.
common.cut(bpy.data.actions["Portal"], 5.6, 8.3, "Entrance")
bpy.data.actions.remove(bpy.data.actions["Portal"])
arm.animation_data_create()
arm.animation_data.action = bpy.data.actions["Idle"]
arm.data.pose_position = 'REST'
bpy.context.view_layer.update()

low, high = bpy.data.objects[LOW], bpy.data.objects[HIGH]


def image(name):
    return bpy.data.images.load(os.path.join(textures, name + ".png")) if name else None


# The download's materials point at textures it doesn't have: rebuilt as color, alpha and glow.
for m in {s.material for o in (low, high) for s in o.material_slots if s.material}:
    color, alpha = MATERIALS.get(m.name, MATERIALS["M3_leg"] if "Default" in m.name else (None, None))
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    em = nt.nodes.new('ShaderNodeEmission')
    out_node = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(em.outputs[0], out_node.inputs[0])
    m["color"] = color or ""
    m["alpha"] = alpha or ""
    m["glow"] = m.name == "M_Gideon_Ring_Glow"

# Baked one material at a time, each part only from its own original, like the skeleton.
uv = low.data.uv_layers.new(name="Atlas", do_init=True)
low.data.uv_layers.active = uv
common.select([low])
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.context.scene.tool_settings.use_uv_select_sync = True
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
bpy.ops.mesh.separate(type='MATERIAL')
bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.data.objects if o.type == 'MESH' and o != high]
for p in parts:
    p.data.uv_layers.active = p.data.uv_layers["Atlas"]
    p.data.uv_layers["Atlas"].active_render = True

sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 4; sc.cycles.use_denoising = False
sc.render.bake.use_selected_to_active = True
sc.render.bake.cage_extrusion = 0.01
sc.render.bake.max_ray_distance = 0.03
sc.render.bake.margin = 6
sc.render.bake.use_clear = False


def show(kind):
    """Every material emits one thing: its color texture, its alpha mask, or its glow."""
    for m in {s.material for s in high.material_slots if s.material}:
        nt = m.node_tree
        em = next(n for n in nt.nodes if n.type == 'EMISSION')
        for n in [n for n in nt.nodes if n.type == 'TEX_IMAGE' and n.name != "BakeTarget"]:
            nt.nodes.remove(n)
        name = m["color"] if kind == "color" else m["alpha"] if kind == "alpha" else ""
        if name:
            t = nt.nodes.new('ShaderNodeTexImage')
            t.image = image(name)
            if kind == "alpha":
                t.image.colorspace_settings.name = 'Non-Color'
            nt.links.new(t.outputs[0], em.inputs[0])
        elif kind == "glow":
            em.inputs[0].default_value = GLOW if m["glow"] else (0, 0, 0, 1)
        elif kind == "alpha":
            em.inputs[0].default_value = (1, 1, 1, 1)
        else:
            em.inputs[0].default_value = GLOW if m["glow"] else (0.5, 0.5, 0.5, 1)


def target(name, color):
    img = bpy.data.images.new(name, size, size, alpha=False)
    if not color:
        img.colorspace_settings.name = 'Non-Color'
    return img


def bake(img, kind, **kw):
    for part in parts:
        mat = part.material_slots[0].material
        nt = mat.node_tree
        n = nt.nodes.get("BakeTarget") or nt.nodes.new('ShaderNodeTexImage')
        n.name = "BakeTarget"; n.image = img; nt.nodes.active = n
        bpy.ops.object.select_all(action='DESELECT')
        high.select_set(True)
        part.select_set(True); bpy.context.view_layer.objects.active = part
        bpy.ops.object.bake(type=kind, **kw)


albedo = target("Albedo", True); show("color"); bake(albedo, 'EMIT')
alpha = target("Alpha", False); show("alpha"); bake(alpha, 'EMIT')
glow = target("Glow", True); show("glow"); bake(glow, 'EMIT')
normal = target("Normal", False); normal.generated_color = (0.5, 0.5, 1, 1)
for m in {s.material for s in high.material_slots if s.material}:
    nt = m.node_tree
    em = next(n for n in nt.nodes if n.type == 'EMISSION')
    bsdf = nt.nodes.get("Bsdf") or nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.name = "Bsdf"
    nt.links.new(bsdf.outputs[0], next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL').inputs[0])
bake(normal, 'NORMAL', normal_space='TANGENT')


def pixels(img):
    return np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)


rgba = pixels(albedo); rgba[..., 3] = pixels(alpha)[..., 0]
final = bpy.data.images.new("Gideon_Albedo", size, size, alpha=True)
final.pixels[:] = rgba.ravel()
half = size // 2
for img, name in ((final, "Albedo"), (normal, "Normal"), (glow, "Emission")):
    img.scale(half, half)
    common.save_image(img, os.path.join(out, f"Gideon_{name}.png"))

bpy.data.objects.remove(high)
common.select(parts)
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "Body"
mat = bpy.data.materials.new("Gideon")
for p in body.data.polygons:
    p.material_index = 0
body.data.materials.clear(); body.data.materials.append(mat)
for name in [l.name for l in body.data.uv_layers if l.name != "Atlas"]:
    body.data.uv_layers.remove(body.data.uv_layers[name])
body.data.uv_layers["Atlas"].name = "UVMap"
body.data.name = "Gideon"
print("TRIS", sum(len(p.vertices) - 2 for p in body.data.polygons))

arm.data.pose_position = 'POSE'
common.flatten(arm, [body], 1.0, "Gideon")
common.export(os.path.join(out, "Gideon.fbx"), [arm, body])
