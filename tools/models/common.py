"""Shared steps for turning the Sketchfab downloads (glTF) into FBX models for Unity."""
import bpy
from mathutils import Matrix


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    for o in list(bpy.data.objects):
        if o.name.startswith("Icosphere"):
            bpy.data.objects.remove(o)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    return arm


def select(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def flatten(arm, meshes, factor, name):
    """Takes the armature and its meshes out of the importer's empties and scales them by factor.
    The transforms stay on the objects: baking them in breaks the skinning of these files."""
    for o in [arm] + meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    arm.matrix_world = Matrix.Scale(factor, 4) @ arm.matrix_world
    for o in meshes:
        o.matrix_world = Matrix.Scale(factor, 4) @ o.matrix_world
    for o in list(bpy.data.objects):
        if o.type == 'EMPTY':
            bpy.data.objects.remove(o)
    arm.name = name
    arm.data.name = name


def keep_actions(renames):
    """renames maps the importer's action names (without the suffix) to the clip names in Unity."""
    for a in list(bpy.data.actions):
        short = a.name.replace("_GLTF_created_0", "")
        if short in renames:
            a.name = renames[short]
            a.use_fake_user = True
        else:
            bpy.data.actions.remove(a)


def save_image(image, path):
    image.filepath_raw = path
    image.file_format = 'PNG'
    image.save()


def export(path, objs):
    select(objs)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
