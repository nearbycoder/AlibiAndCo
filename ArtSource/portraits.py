"""Stylised busts for the suspects, rendered as studio photographs.

Heads, hair, hats and torsos are metaballs (so skull, cheeks, nose, ears and neck blend into one
organic surface), converted to meshes. Facial features are tapered curves projected onto that
surface: brows, lids, lashes, lips, smile lines. Each character is a dict of choices; renders go
to ArtSource/renders/portrait_<id>.png and Tools/photo_finish.py gives them a 1980s print look.

Units are metres, the head is about 0.24 tall, +Z up and the face looks down -Y.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Quaternion, Vector
from mathutils.bvhtree import BVHTree

import lib
from lib import cylinder, link, material, mesh_obj, smooth

CHARACTERS = {
    "agnes": dict(skin="EBC7AE", hair="bun", hair_col="C9C3B8", glasses="round", clothes="cardigan", cloth_col="8C7BA6",
                  shirt_col="F1ECE0", brow=8, mouth=-0.35, age=0.85, earrings="pearl", face_w=0.94, jaw=0.9, eye_col="5C7088",
                  lid=0.45, nose=0.9, tilt=4),
    "bram": dict(skin="6B4128", hair="crop", hair_col="18110D", clothes="whites", cloth_col="EFEDE6", shirt_col="EFEDE6",
                 brow=-6, mouth=0.1, age=0.1, face_w=1.0, jaw=1.08, hat="baker", eye_col="3A2618", lid=0.3, nose=1.1,
                 lips=1.25, tilt=-3),
    "clem": dict(skin="E5B898", hair="bald_sides", hair_col="A39C90", mustache="walrus", clothes="tweed", cloth_col="7A5B3A",
                 shirt_col="E9E1CF", tie="bow", tie_col="7A1F24", brow=-3, mouth=0.25, age=0.65, face_w=1.06, jaw=1.0,
                 eye_col="4E5B3A", lid=0.5, nose=1.2, cheeks=1.25, tilt=-2),
    "marlow": dict(skin="E8C4A4", hair="slick", hair_col="2A211C", mustache="pencil", clothes="suit", cloth_col="2B3446",
                   shirt_col="E8E6DF", tie="tie", tie_col="7E2E2E", brow=-12, mouth=0.15, age=0.4, face_w=0.9, jaw=1.05,
                   eye_col="3B2C22", lid=0.55, nose=1.05, smirk=0.5, tilt=3),
    "ines": dict(skin="E2B792", hair="bob", hair_col="2B1B14", clothes="breton", cloth_col="23314F", shirt_col="F2EFE6",
                 scarf="8A2222", brow=-4, mouth=0.1, age=0.2, face_w=0.92, jaw=0.88, eye_col="2E4A2E", lid=0.4, nose=0.85,
                 lips=1.2, lipstick="A2343A", tilt=-4),
    "rolf": dict(skin="E6BBA2", hair="short", hair_col="D3CEC6", beard="full", beard_col="DCD7CE", hat="flatcap", hat_col="5A5148",
                 clothes="jumper", cloth_col="27334A", brow=4, mouth=0.0, age=0.85, face_w=1.06, jaw=1.05, eye_col="4A6A88",
                 lid=0.55, nose=1.2, cheeks=1.3, tilt=2),
    "nell": dict(skin="F2D6C2", hair="long", hair_col="9A4220", clothes="duffel", cloth_col="3F5E44", shirt_col="E9E2D3",
                 brow=10, mouth=-0.2, age=0.0, face_w=0.88, jaw=0.86, eye_col="4D6A3A", lid=0.25, nose=0.8, freckles=True,
                 tilt=5),
    "elias": dict(skin="DBAA8E", hair="short", hair_col="8F8A84", beard="full", beard_col="9C968E", hat="peaked", hat_col="1E2738",
                  clothes="pea", cloth_col="1E2738", brow=-2, mouth=-0.15, age=0.7, face_w=1.04, jaw=1.08, eye_col="56708A",
                  lid=0.6, nose=1.15, cheeks=1.15, tilt=-2),
    "cole": dict(skin="D8A586", hair="short", hair_col="3B2B20", stubble=True, hat="doorman", hat_col="6A1E25",
                 clothes="doorman", cloth_col="6A1E25", brow=-14, mouth=-0.3, age=0.45, face_w=1.1, jaw=1.18, eye_col="3A2A1E",
                 lid=0.6, nose=1.15, tilt=0),
    "rook": dict(skin="E9B39C", hair="swept", hair_col="E8E4DC", mustache="walrus", mustache_col="EEEAE2", clothes="tux",
                 cloth_col="141416", shirt_col="F3F1EC", tie="bow", tie_col="141416", brow=-6, mouth=0.35, age=0.9, face_w=1.06,
                 jaw=1.0, eye_col="5A6E86", lid=0.55, nose=1.25, cheeks=1.35, tilt=3),
}

BAND_Z = 0.064          # where a hat's band sits: just above the brows at the front


# --------------------------------------------------------------------------- helpers

def _shade(hexcol, k):
    r, g, b = lib.hex_rgb(hexcol)
    return "%02X%02X%02X" % tuple(max(0, min(255, int(v * k * 255))) for v in (r, g, b))


def _mix(a, b, t):
    ra, ga, ba = lib.hex_rgb(a)
    rb, gb, bb = lib.hex_rgb(b)
    return "%02X%02X%02X" % tuple(int((x + (y - x) * t) * 255) for x, y in ((ra, rb), (ga, gb), (ba, bb)))


def metaball(name, elements, mat, res=0.0022, threshold=0.6, smooth_iters=12, smooth_factor=0.7):
    """Fuse ellipsoids (E dicts: centre, radius, per-axis scale, rotation, negative) into one surface.

    Positive ellipsoids are unioned by a voxel remesh, negatives are cut away with booleans, and a
    smoothing pass turns the creases where they meet into soft fillets. (Named for what it imitates;
    real metaballs were too hard to size by hand.)"""
    bm = bmesh.new()
    negs = []
    for e in elements:
        tgt = bmesh.new() if e.get("neg") else bm
        before = set(tgt.verts)
        bmesh.ops.create_uvsphere(tgt, u_segments=40, v_segments=24, radius=1.0)
        new_verts = [v for v in tgt.verts if v not in before]
        s = Vector(e.get("size", (1, 1, 1))) * e["r"]
        rot = Euler(tuple(math.radians(a) for a in e.get("rot", (0, 0, 0)))).to_matrix()
        for v in new_verts:
            v.co = rot @ Vector((v.co.x * s.x, v.co.y * s.y, v.co.z * s.z)) + Vector(e["co"])
        if e.get("neg"):
            negs.append(mesh_obj(name + "_neg", tgt))
    ob = mesh_obj(name, bm, mat)
    dg = bpy.context.evaluated_depsgraph_get

    def bake(o):
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg()))
        old = o.data
        o.modifiers.clear()
        o.data = me
        bpy.data.meshes.remove(old)

    rm = ob.modifiers.new("fuse", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = res
    bake(ob)
    for n in negs:
        bo = ob.modifiers.new("cut", "BOOLEAN")
        bo.operation = "DIFFERENCE"
        bo.object = n
        bo.solver = "EXACT"
    if negs:
        bake(ob)
        for n in negs:
            bpy.data.objects.remove(n, do_unlink=True)
        rm = ob.modifiers.new("fuse2", "REMESH")
        rm.mode = "VOXEL"
        rm.voxel_size = res
        bake(ob)
    if smooth_iters:
        sm = ob.modifiers.new("soften", "SMOOTH")
        sm.factor = smooth_factor
        sm.iterations = smooth_iters
        bake(ob)
    if mat and not ob.data.materials:
        ob.data.materials.append(mat)
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def E(co, r, size=None, rot=None, neg=False, stiff=2.0, kind="ELLIPSOID"):
    d = dict(type=kind, co=co, r=r, neg=neg, stiff=stiff)
    if size is not None:
        d["size"] = size
    if rot is not None:
        d["rot"] = rot
    return d


class Surface:
    """Ray-casts onto a mesh to place features on it."""

    def __init__(self, ob):
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.transform(ob.matrix_world)
        self.tree = BVHTree.FromBMesh(bm)
        bm.free()

    def front(self, x, z, off=0.0, depth_from=-0.5):
        """Point on the surface seen from the front at (x, z), pushed `off` along the normal."""
        hit, normal, _, _ = self.tree.ray_cast(Vector((x, depth_from, z)), Vector((0, 1, 0)))
        if hit is None:
            return None, None
        return hit + normal * off, normal

    def toward(self, origin, direction, off=0.0):
        hit, normal, _, _ = self.tree.ray_cast(Vector(origin), Vector(direction).normalized())
        if hit is None:
            return None, None
        return hit + normal * off, normal


def tube(name, pts, radii, mat, flat=1.0, res=3):
    """A smooth tapered tube through points (NURBS curve with per-point radius)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 1.0
    cu.bevel_resolution = res
    cu.use_fill_caps = True
    cu.resolution_u = 8
    sp = cu.splines.new("NURBS")
    sp.points.add(len(pts) - 1)
    for i, (p, r) in enumerate(zip(pts, radii)):
        sp.points[i].co = (p[0], p[1], p[2], 1)
        sp.points[i].radius = r
    sp.use_endpoint_u = True
    sp.order_u = min(4, len(pts))
    ob = bpy.data.objects.new(name, cu)
    link(ob)
    ob.data.materials.append(mat)
    if flat != 1.0:
        ob.scale = (1, flat, 1)
    return ob


def surface_curve(surf, name, xz, radii, mat, off, flat=1.0):
    pts = []
    for x, z in xz:
        p, _ = surf.front(x, z, off)
        if p is None:
            return None
        pts.append(p)
    return tube(name, pts, radii, mat, flat)


def projected_patch(surf, name, outline, mat, off, thick=0.003, cuts=9):
    """A flat polygon (x, z) outline draped over the surface from the front, then thickened."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, 0, z)) for x, z in outline]
    f = bm.faces.new(vs)
    bmesh.ops.triangulate(bm, faces=[f])
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    for v in bm.verts:
        p, _ = surf.front(v.co.x, v.co.z, off)
        if p is not None:
            v.co = p
    ob = mesh_obj(name, bm, mat)
    sol = ob.modifiers.new("t", "SOLIDIFY")
    sol.thickness = thick
    sol.offset = 1
    return smooth(ob, 60)


# --------------------------------------------------------------------------- materials

def skin_mat(c):
    name = "skin_" + c["skin"]
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = material(name, c["skin"], rough=0.5)
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    try:
        bsdf.inputs["Subsurface Weight"].default_value = 0.12
        bsdf.inputs["Subsurface Radius"].default_value = (0.012, 0.005, 0.003)
        bsdf.inputs["Subsurface Scale"].default_value = 0.6
        bsdf.inputs["Specular IOR Level"].default_value = 0.35
    except KeyError:
        pass
    # Blush on cheeks, nose and ears: distance from those points in object space -> warmer tint.
    tc = nt.nodes.new("ShaderNodeTexCoord")
    absn = nt.nodes.new("ShaderNodeVectorMath")
    absn.operation = "ABSOLUTE"
    nt.links.new(tc.outputs["Object"], absn.inputs[0])
    base = Vector(lib.hex_rgb(c["skin"]))
    warm = _mix(c["skin"], "C8504A", 0.28)
    rgb = nt.nodes.new("ShaderNodeRGB")
    rgb.outputs[0].default_value = (*[lib.srgb_to_linear(v) for v in lib.hex_rgb(c["skin"])], 1)
    acc = rgb.outputs[0]
    spots = [((0.048, -0.075, -0.03), 0.035, 0.55 * c.get("cheeks", 1.0)),
             ((0.0, -0.115, -0.02), 0.02, 0.5 * c.get("cheeks", 1.0)),
             ((0.092, 0.0, -0.005), 0.03, 0.4)]
    for center, radius, amount in spots:
        d = nt.nodes.new("ShaderNodeVectorMath")
        d.operation = "DISTANCE"
        nt.links.new(absn.outputs[0], d.inputs[0])
        d.inputs[1].default_value = center
        mr = nt.nodes.new("ShaderNodeMapRange")
        mr.inputs["From Min"].default_value = 0
        mr.inputs["From Max"].default_value = radius
        mr.inputs["To Min"].default_value = min(1.0, amount)
        mr.inputs["To Max"].default_value = 0
        nt.links.new(d.outputs["Value"], mr.inputs["Value"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        nt.links.new(mr.outputs["Result"], mix.inputs["Factor"])
        nt.links.new(acc, mix.inputs["A"])
        mix.inputs["B"].default_value = (*[lib.srgb_to_linear(v) for v in lib.hex_rgb(warm)], 1)
        acc = mix.outputs["Result"]
    # Fine mottling.
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 180
    noise.inputs["Detail"].default_value = 3
    mixn = nt.nodes.new("ShaderNodeMix")
    mixn.data_type = "RGBA"
    mixn.inputs["Factor"].default_value = 0.06
    nt.links.new(acc, mixn.inputs["A"])
    nt.links.new(noise.outputs["Color"], mixn.inputs["B"])
    nt.links.new(mixn.outputs["Result"], bsdf.inputs["Base Color"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.08
    nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return m


def hair_mat(col, name=None, strand_scale=110, rough=0.5):
    name = name or "hair_" + col
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = material(name, col, rough=rough)
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    # Strands: a wave texture running front-to-back, broken up by noise, as both colour and bump.
    tc = nt.nodes.new("ShaderNodeTexCoord")
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "BANDS"
    wave.bands_direction = "X"
    wave.inputs["Scale"].default_value = strand_scale
    wave.inputs["Distortion"].default_value = 6
    wave.inputs["Detail"].default_value = 3
    nt.links.new(tc.outputs["Object"], wave.inputs["Vector"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.6
    nt.links.new(wave.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    dark = nt.nodes.new("ShaderNodeMix")
    dark.data_type = "RGBA"
    dark.inputs["Factor"].default_value = 0.25
    base = (*[lib.srgb_to_linear(v) for v in lib.hex_rgb(col)], 1)
    dark.inputs["A"].default_value = base
    dark.inputs["B"].default_value = (*[lib.srgb_to_linear(v) for v in lib.hex_rgb(_shade(col, 0.7))], 1)
    nt.links.new(wave.outputs["Fac"], dark.inputs["Factor"])
    nt.links.new(dark.outputs["Result"], bsdf.inputs["Base Color"])
    try:
        bsdf.inputs["Sheen Weight"].default_value = 0.1
    except KeyError:
        pass
    return m


def fabric_mat(col, kind="plain"):
    name = f"fabric_{col}_{kind}"
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = material(name, col, rough=0.85)
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 400 if kind != "tweed" else 140
    noise.inputs["Detail"].default_value = 6
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.25 if kind != "knit" else 0.5
    nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    try:
        bsdf.inputs["Sheen Weight"].default_value = 0.12
    except KeyError:
        pass
    base = (*[lib.srgb_to_linear(v) for v in lib.hex_rgb(col)], 1)
    if kind == "tweed":
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.inputs["Factor"].default_value = 0.35
        mix.inputs["A"].default_value = base
        nt.links.new(noise.outputs["Color"], mix.inputs["B"])
        nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    elif kind == "knit":
        wave = nt.nodes.new("ShaderNodeTexWave")
        wave.bands_direction = "Z"
        wave.inputs["Scale"].default_value = 90
        nt.links.new(wave.outputs["Fac"], bump.inputs["Height"])
    return m


def breton_mat(navy, white):
    m = material("fabric_breton", navy, rough=0.85)
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = 2 * math.pi / 0.034
    nt.links.new(sep.outputs["Z"], mul.inputs[0])
    sin = nt.nodes.new("ShaderNodeMath")
    sin.operation = "SINE"
    nt.links.new(mul.outputs[0], sin.inputs[0])
    gt = nt.nodes.new("ShaderNodeMath")
    gt.operation = "GREATER_THAN"
    gt.inputs[1].default_value = 0.25
    nt.links.new(sin.outputs[0], gt.inputs[0])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["A"].default_value = (*[lib.srgb_to_linear(v) for v in lib.hex_rgb(white)], 1)
    mix.inputs["B"].default_value = (*[lib.srgb_to_linear(v) for v in lib.hex_rgb(navy)], 1)
    nt.links.new(gt.outputs[0], mix.inputs["Factor"])
    nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    return m


# --------------------------------------------------------------------------- head

def head(c, skin):
    w = c.get("face_w", 1.0)
    jaw = c.get("jaw", 1.0)
    nose = c.get("nose", 1.0)
    age = c.get("age", 0.3)
    els = [
        E((0, 0.012, 0.025), 0.1, (0.84 * w, 0.98, 1.0)),                         # cranium
        E((0, -0.018, -0.045), 0.085, (0.74 * w * jaw, 0.86, 0.78)),               # face mass / jaw
        E((0, -0.052, -0.098), 0.04, (0.95 * jaw, 0.8, 0.7)),                      # chin
        E((0, -0.06, 0.035), 0.06, (1.1 * w, 0.6, 0.45)),                          # brow ridge / forehead
        E((0, 0.016, -0.135), 0.066, (0.98, 0.92, 1.2)),                           # neck
    ]
    for sx in (-1, 1):
        els += [
            E((sx * 0.042 * w, -0.05, -0.024), 0.024, (1.0, 0.85, 0.85)),          # cheekbones
            E((sx * 0.087 * w, 0.008, -0.012), 0.021, (0.38, 0.68, 1.05)),         # ears
            E((sx * 0.034, -0.098, 0.014), 0.0135, (1.2, 0.8, 0.8), neg=True, stiff=1.5),  # eye sockets
            E((sx * 0.012 * nose, -0.105, -0.034), 0.0105 * nose),                # nostril wings
        ]
    els += [
        E((0, -0.096, 0.0), 0.012 * nose, (0.8, 0.8, 2.0), rot=(-18, 0, 0)),      # nose bridge
        E((0, -0.111 - 0.004 * (nose - 1), -0.026), 0.0135 * nose),               # nose tip
        E((0, -0.104, -0.075), 0.012, (1.6, 0.6, 0.8), neg=True, stiff=1.2),      # under-lip groove
    ]
    if age > 0.6:
        els.append(E((0, -0.075, -0.07), 0.03, (1.6, 0.6, 0.6)))                  # jowls
    ob = metaball("head", els, skin)
    return ob


def eyes(c, surf, parts, skin, look):
    white = material("eye_white", "F2EEE8", rough=0.08)
    iris_m = material("iris_" + c.get("eye_col", "3A2A1E"), c.get("eye_col", "3A2A1E"), rough=0.1)
    pupil_m = material("pupil", "0A0807", rough=0.05)
    lash = material("lash_" + c["hair_col"], _shade(c["hair_col"], 0.35) if c["hair_col"] > "888888" else "16110E", rough=0.6)
    brow_m = hair_mat(c.get("brow_col", _shade(c["hair_col"], 0.92)), "brow_" + c["hair_col"], 900)
    lid = c.get("lid", 0.45)
    for sx in (-1, 1):
        center = Vector((sx * 0.034, -0.084, 0.013))
        r = 0.0135
        ball = lib.sphere("eyeball", r, center, white, seg=32, rings=16)
        parts.append(ball)
        # Iris + pupil discs on the front, turned toward the camera.
        d = (look - center).normalized()
        q = Vector((0, -1, 0)).rotation_difference(d)
        for nm, rad, m, push in (("iris", 0.0068, iris_m, 0.0004), ("pupil", 0.0032, pupil_m, 0.0008)):
            disc = cylinder(nm, rad, 0.0006, (0, 0, -0.0003), m, seg=24)
            disc.rotation_mode = "QUATERNION"
            disc.rotation_quaternion = q @ Quaternion((1, 0, 0), math.radians(90))
            disc.location = center + d * (r - 0.0012 + push)
            parts.append(disc)
        # Upper lid: a skin shell over the top of the eyeball, its edge angled by mood.
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=24, radius=r * 1.12)
        cut = r * (0.72 - lid * 0.62)
        tilt = math.radians(sx * c.get("brow", 0) * 0.4)
        kill = [v for v in bm.verts if v.co.z + v.co.x * math.tan(tilt) < cut]
        bmesh.ops.delete(bm, geom=kill, context="VERTS")
        bmesh.ops.translate(bm, vec=center, verts=bm.verts)
        lid_ob = mesh_obj("lid", bm, skin)
        sol = lid_ob.modifiers.new("t", "SOLIDIFY")
        sol.thickness = 0.0015
        parts.append(smooth(lid_ob, 80))
        # Lash line along the lid edge.
        pts, radii = [], []
        for i in range(7):
            a = math.radians(-70 + i * 140 / 6)
            x = math.sin(a) * r * 1.0
            z = cut - x * math.tan(tilt)
            y = -math.sqrt(max(0.0, (r * 1.14) ** 2 - x * x - z * z))
            pts.append(center + Vector((x, y, z)))
            radii.append(0.0009 if i in (0, 6) else 0.0014)
        parts.append(tube("lash", pts, radii, lash))
        # Lower lid: a soft skin line.
        lo = []
        for i in range(5):
            a = math.radians(-55 + i * 110 / 4)
            x = math.sin(a) * r * 0.95
            z = -r * 0.62 - 0.0005 * math.cos(a)
            y = -math.sqrt(max(0.0, (r * 1.08) ** 2 - x * x - z * z))
            lo.append(center + Vector((x, y, z)))
        parts.append(tube("lowerLid", lo, [0.0006, 0.0013, 0.0016, 0.0013, 0.0006], skin))
        # Brow: arched tube projected onto the brow ridge, angled for mood (negative = scowl).
        ang = math.radians(c.get("brow", 0))
        xz = []
        for i in range(6):
            t = i / 5
            x = sx * (0.016 + t * 0.034)
            z = 0.038 + math.sin(t * math.pi) * 0.006 + (t - 0.5) * 0.034 * math.tan(-ang) * -1
            xz.append((x, z))
        b = surface_curve(surf, "brow", xz, [0.0022, 0.0034, 0.0036, 0.0032, 0.0026, 0.0014], brow_m, 0.0018)
        if b:
            b.scale = (1, 1, 1)
            parts.append(b)


def mouth(c, surf, parts):
    lip_col = c.get("lipstick") or _mix(c["skin"], "A0504A", 0.35)
    lip = material("lip_" + lip_col, lip_col, rough=0.35)
    line = material("mouthline_" + c["skin"], _shade(c["skin"], 0.45), rough=0.6)
    k = c.get("lips", 1.0)
    curve = c.get("mouth", 0.0)
    smirk = c.get("smirk", 0.0)
    width = 0.024

    def z_at(t):   # t in [-1, 1]
        corner = curve * 0.007 * (t * t) + smirk * 0.004 * max(0, t) * t
        return -0.058 + corner

    upper = [(t * width, z_at(t) + 0.0028 * (1 - t * t) * k) for t in (-1, -0.6, -0.25, 0, 0.25, 0.6, 1)]
    lower = [(t * width * 0.85, z_at(t) - 0.004 * (1 - t * t) * k - 0.0006) for t in (-1, -0.5, 0, 0.5, 1)]
    mid = [(t * width, z_at(t)) for t in (-1.05, -0.5, 0, 0.5, 1.05)]
    o = surface_curve(surf, "upperLip", upper, [0.0008, 0.0018 * k, 0.0021 * k, 0.0019 * k, 0.0021 * k, 0.0018 * k, 0.0008], lip, 0.0005)
    if o:
        parts.append(o)
    o = surface_curve(surf, "lowerLip", lower, [0.001, 0.0028 * k, 0.0034 * k, 0.0028 * k, 0.001], lip, 0.0008)
    if o:
        parts.append(o)
    o = surface_curve(surf, "mouthLine", mid, [0.0004, 0.0009, 0.001, 0.0009, 0.0004], line, 0.0011)
    if o:
        parts.append(o)


def lines(c, surf, parts, skin):
    """Age: smile lines and a forehead crease or two."""
    age = c.get("age", 0)
    if age < 0.4:
        return
    crease = material("crease_" + c["skin"], _shade(c["skin"], 0.8), rough=0.6)
    for sx in (-1, 1):
        xz = [(sx * 0.022, -0.03), (sx * 0.031, -0.047), (sx * 0.034, -0.064)]
        o = surface_curve(surf, "smileLine", xz, [0.0006, 0.0012, 0.0006], crease, 0.0002)
        if o:
            parts.append(o)
    if age > 0.6:
        for i in range(2):
            z = 0.062 + i * 0.012
            xz = [(-0.03, z - 0.002), (0, z + 0.001), (0.03, z - 0.002)]
            o = surface_curve(surf, "forehead", xz, [0.0004, 0.0008, 0.0004], crease, 0.0)
            if o:
                parts.append(o)


def freckles(c, surf, parts):
    if not c.get("freckles"):
        return
    m = material("freckle", _shade(c["skin"], 0.72), rough=0.6)
    import random
    rnd = random.Random(7)
    for _ in range(46):
        sx = rnd.choice((-1, 1))
        x = sx * rnd.uniform(0.012, 0.055)
        z = rnd.uniform(-0.035, -0.008)
        p, n = surf.front(x, z, 0.0)
        if p is not None:
            parts.append(lib.sphere("freckle", rnd.uniform(0.0007, 0.0012), p, m, seg=6, rings=4, scale=(1, 0.3, 1)))


# --------------------------------------------------------------------------- hair

def hair(c, parts):
    style = c.get("hair")
    col = hair_mat(c["hair_col"])
    w = c.get("face_w", 1.0)

    def cap(s=1.08, dz=0.0, dy=0.0):
        return E((0, 0.012 + dy, 0.028 + dz), 0.1, (0.86 * w * s, 1.0 * s, 1.0 * s))

    # Negative volumes carve a smooth hairline: the face, the nape and round the ears.
    def face_cut(top=0.0):
        return E((0, -0.12, -0.04 + top), 0.1, (0.92 * w, 0.95, 1.05))

    nape_cut = E((0, 0.07, -0.125), 0.09, (1.25, 0.8, 0.6))
    ear_cuts = [E((sx * 0.105 * w, -0.005, -0.035), 0.045, (0.75, 1.0, 1.05)) for sx in (-1, 1)]
    for e in [nape_cut] + ear_cuts:
        e["neg"] = True
    els = None
    if style == "short":
        els = [cap(1.075), E((0, -0.02, 0.1), 0.06, (1.3, 1.4, 0.5))]
    elif style == "crop":
        els = [cap(1.045)]
    elif style == "slick":
        els = [cap(1.06, 0.002, 0.004), E((-0.01, -0.02, 0.1), 0.045, (1.5, 1.6, 0.42))]   # smooth, combed back
    elif style == "swept":
        els = [cap(1.06, 0.002), E((0.006, -0.045, 0.1), 0.045, (1.7, 1.2, 0.5), rot=(0, -6, 0))]
    elif style == "bun":
        # A soft set: a cap with curls round the hairline and crown, and a bun behind.
        els = [cap(1.05, 0.004)]
        # Curls scattered over the crown (a Fibonacci spiral over the upper half of the cap).
        n = 26
        for i in range(n):
            t = (i + 0.5) / n
            zc = 1 - t * 0.85
            rr = math.sqrt(max(0.0, 1 - zc * zc))
            a = i * math.pi * (3 - math.sqrt(5))
            x, y = math.cos(a) * rr, math.sin(a) * rr
            pos = (x * 0.092 * w, 0.012 + y * 0.104, 0.03 + zc * 0.1)
            if pos[2] < 0.045 and pos[1] < -0.02:
                continue
            els.append(E(pos, 0.026 + 0.006 * ((i * 7) % 3) / 2))
        els += [E((0, 0.088, 0.085), 0.04)]
        f = face_cut(0.008)
        f["neg"] = True
        parts.append(metaball("hair", els + [f, nape_cut] + ear_cuts, col, res=0.0022, smooth_iters=5, smooth_factor=0.6))
        return
    elif style == "bob":
        # The bob's sides sit outside and behind the eyes; the top edge of the face opening is the fringe.
        els = [cap(1.1, -0.002, 0.008),
               E((-0.086 * w, 0.004, -0.05), 0.045, (0.72, 1.0, 1.4)), E((0.086 * w, 0.004, -0.05), 0.045, (0.72, 1.0, 1.4)),
               E((0, 0.05, -0.05), 0.08, (1.2, 0.8, 0.9))]                                      # back of the bob
        fc = E((0, -0.125, -0.058), 0.085, (0.84 * w, 1.05, 1.06), neg=True)
        els += [fc, E((0, 0.03, -0.14), 0.09, (1.5, 1.2, 0.5), neg=True)]
        parts.append(metaball("hair", els, col, res=0.0024, smooth_iters=16))
        return
    elif style == "long":
        els = [cap(1.11, -0.002, 0.008),
               E((0, 0.05, -0.11), 0.088, (1.15 * w, 0.78, 1.7)),                               # fall down the back
               E((-0.08, -0.005, -0.1), 0.042, (0.75, 0.9, 2.0)), E((0.08, -0.005, -0.1), 0.042, (0.75, 0.9, 2.0)),
               E((0.012, -0.03, 0.098), 0.046, (1.4, 1.2, 0.4))]                                # volume swept to one side
        els += [E((0, -0.125, -0.056), 0.085, (0.86 * w, 1.05, 1.02), neg=True)]
        parts.append(metaball("hair", els, col, res=0.0024, smooth_iters=16))
        return
    elif style == "bald_sides":
        els = [E((sx * 0.074 * w, 0.03, -0.005), 0.05, (0.6, 1.25, 0.95)) for sx in (-1, 1)] + \
              [E((0, 0.07, -0.01), 0.072, (1.25, 0.62, 0.8)),
               E((0, -0.04, 0.035), 0.1, (0.9, 0.72, 0.62), neg=True),
               E((0, 0.01, 0.1), 0.095, (1.0, 1.05, 0.6), neg=True)] + ear_cuts
        parts.append(metaball("hair", els, col, res=0.0024, smooth_iters=14))
        return
    if els:
        f = face_cut()
        f["neg"] = True
        parts.append(metaball("hair", els + [f, nape_cut] + ear_cuts, col, res=0.0024, smooth_iters=14))


def facial_hair(c, surf, parts):
    m = c.get("mustache")
    beard = c.get("beard")
    if beard:
        col = hair_mat(c.get("beard_col", c["hair_col"]), "beard_" + c.get("beard_col", c["hair_col"]), 500, 0.7)
        w = c.get("face_w", 1.0) * c.get("jaw", 1.0)
        els = [E((0, -0.06, -0.085), 0.06, (1.15 * w, 0.75, 0.85)),
               E((0, -0.078, -0.105), 0.04, (1.2, 0.8, 0.9)),
               E((-0.06 * w, -0.03, -0.05), 0.035, (0.7, 1.2, 1.4)), E((0.06 * w, -0.03, -0.05), 0.035, (0.7, 1.2, 1.4)),
               E((0, -0.112, -0.06), 0.02, (1.4, 0.9, 0.6), neg=True)]                       # mouth opening
        parts.append(metaball("beard", els, col, res=0.0024))
        m = m or "full"
    if m:
        mc = c.get("mustache_col", c.get("beard_col", c["hair_col"]))
        col = hair_mat(mc, "stache_" + mc, 600, 0.7)
        if m in ("walrus", "full"):
            for sx in (-1, 1):
                xz = [(sx * 0.003, -0.044), (sx * 0.016, -0.046), (sx * 0.028, -0.054), (sx * 0.033, -0.064)]
                o = surface_curve(surf, "stache", xz, [0.0055, 0.0065, 0.005, 0.0022], col, 0.0035)
                if o:
                    parts.append(o)
        else:  # pencil
            for sx in (-1, 1):
                xz = [(sx * 0.002, -0.047), (sx * 0.014, -0.047), (sx * 0.024, -0.051)]
                o = surface_curve(surf, "stache", xz, [0.0012, 0.0015, 0.0008], col, 0.001)
                if o:
                    parts.append(o)
    if c.get("stubble") and False:   # projected stubble read as dirt; left out
        sm = material("stubble_" + c["skin"], _shade(c["skin"], 0.7), rough=0.9)
        nt = sm.node_tree
        bsdf = nt.nodes["Principled BSDF"]
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 900
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.45
        ramp.color_ramp.elements[1].position = 0.6
        nt.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        nt.links.new(ramp.outputs["Color"], bsdf.inputs["Alpha"])
        sm.blend_method = "HASHED" if hasattr(sm, "blend_method") else sm.blend_method
        outline = [(-0.07, -0.02), (-0.04, -0.045), (-0.03, -0.075), (0, -0.09), (0.03, -0.075), (0.04, -0.045),
                   (0.07, -0.02), (0.06, -0.1), (0, -0.13), (-0.06, -0.1)]
        parts.append(projected_patch(surf, "stubble", outline, sm, 0.0006, 0.0004, 5))


def glasses(c, surf, parts):
    if c.get("glasses") != "round":
        return
    frame = material("metal_8A7A55", "8A7A55", metallic=1, rough=0.25)
    glass = material("glass_lens", "F4F4F0", rough=0.02, transmission=1.0, alpha=0.15)
    for sx in (-1, 1):
        cx, cz = sx * 0.034, 0.012
        p, _ = surf.front(cx, cz, 0.0)
        y = (p.y if p else -0.1) - 0.012
        ring = lib.torus("lens", 0.0175, 0.0013, (cx, y, cz), frame, rot=(math.radians(90), 0, 0))
        parts.append(ring)
        arm = lib.box("arm", (0.0018, 0.1, 0.0018), (sx * 0.072 * c.get("face_w", 1.0), y + 0.05, cz + 0.004), frame)
        parts.append(arm)
    p, _ = surf.front(0, 0.016, 0.0)
    bridge = tube("bridge", [(-0.017, p.y - 0.01, 0.014), (0, p.y - 0.006, 0.018), (0.017, p.y - 0.01, 0.014)],
                  [0.0011, 0.0011, 0.0011], frame)
    parts.append(bridge)


def earrings(c, parts):
    if c.get("earrings") != "pearl":
        return
    pearl = material("pearl", "F4EFE4", rough=0.12)
    for sx in (-1, 1):
        parts.append(lib.sphere("pearl", 0.0065, (sx * 0.09 * c.get("face_w", 1.0), -0.006, -0.042), pearl, seg=16, rings=8))


# --------------------------------------------------------------------------- hats

def hat(c, parts):
    h = c.get("hat")
    if not h:
        return
    w = c.get("face_w", 1.0)
    col = fabric_mat(c.get("hat_col", "222222"), "tweed" if h == "flatcap" else "plain")
    z0 = BAND_Z
    made = []
    if h == "flatcap":
        made.append(metaball("cap", [E((0, -0.004, z0 + 0.032), 0.108, (0.98 * w, 1.14, 0.42)),
                                     E((0, -0.07, z0 + 0.026), 0.07, (1.15 * w, 0.85, 0.36)),
                                     E((0, 0.0, z0 - 0.05), 0.13, (1.2, 1.4, 0.4), neg=True)], col, smooth_iters=16))
        made.append(_peak("brim", 0.07 * w, 0.042, col, z0 + 0.008, -0.118, -10))
    elif h in ("peaked", "doorman"):
        tall = 0.082 if h == "peaked" else 0.092
        w = 0.96 + (w - 1.0) * 0.5     # hats are sized to the skull, which varies less than the face
        prof = [(0.0, z0 + tall), (0.112 * w, z0 + tall), (0.12 * w, z0 + tall - 0.007), (0.106 * w, z0 + 0.03),
                (0.102 * w, z0), (0.0, z0)]
        crown = lib.lathe("crown", prof, col, seg=72)
        crown.scale = (1.0, 1.1, 1.0)
        crown.location.y = 0.008
        made.append(crown)
        if h == "peaked":
            band_col = material("fabric_141414", "141414", rough=0.6)
        else:
            band_col = material("brass", "C9A24A", metallic=0.9, rough=0.3)
        band = lib.lathe("band", [(0.1035 * w, z0 - 0.001), (0.1045 * w, z0 + 0.024), (0.0, z0 + 0.024), (0.0, z0 - 0.001)], band_col, seg=72)
        band.scale = (1.0, 1.1, 1.0)
        band.location.y = 0.008
        made.append(band)
        made.append(_peak("peak", 0.078 * w, 0.058, material("patent", "0E0E10", rough=0.12), z0 + 0.002, -0.128, -16))
        badge = lib.sphere("badge", 0.012, (0, -0.118, z0 + 0.04), material("brass", "C9A24A", metallic=0.9, rough=0.3),
                           seg=24, rings=12, scale=(1.3, 0.35, 1.0))
        made.append(badge)
    elif h == "baker":
        cloth = fabric_mat("F4F2EC")
        band = lib.lathe("toqueBand", [(0.0, z0), (0.104 * w, z0), (0.11 * w, z0 + 0.05), (0.0, z0 + 0.05)], cloth, seg=72)
        band.scale = (1.0, 1.08, 1.0)
        made.append(band)
        els = [E((0, 0.0, z0 + 0.085), 0.075, (1.35 * w, 1.35, 0.75))]
        for i in range(10):
            a = i / 10 * math.tau
            els.append(E((math.cos(a) * 0.078 * w, math.sin(a) * 0.08, z0 + 0.088), 0.042))
        made.append(metaball("toque", els, cloth, smooth_iters=10))
    for o in made:
        o.rotation_euler = (math.radians(4), 0, 0)   # worn tipped forward a touch
    parts.extend(made)


def _peak(name, rx, ry, mat, z, y0, droop_deg):
    """A curved cap peak: half an ellipse, bent down at the front."""
    bm = bmesh.new()
    n = 24
    outer = []
    for i in range(n + 1):
        a = math.pi * i / n
        outer.append(bm.verts.new((math.cos(a) * rx, -math.sin(a) * ry, 0)))
    f = bm.faces.new(outer)
    bmesh.ops.triangulate(bm, faces=[f])
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3, use_grid_fill=True)
    droop = math.tan(math.radians(-droop_deg))
    for v in bm.verts:
        v.co.z = -abs(v.co.y) * droop * 0.6 - (v.co.x / rx) ** 2 * 0.006
        v.co.y += y0 + 0.02
        v.co.z += z
    ob = mesh_obj(name, bm, mat)
    sol = ob.modifiers.new("t", "SOLIDIFY")
    sol.thickness = 0.004
    return smooth(ob, 60)


# --------------------------------------------------------------------------- torso

def torso(c, parts):
    clothes = c.get("clothes", "suit")
    kind = {"tweed": "tweed", "jumper": "knit", "cardigan": "knit"}.get(clothes, "plain")
    col = breton_mat(c["cloth_col"], "F2EFE6") if clothes == "breton" else fabric_mat(c["cloth_col"], kind)
    shirt = fabric_mat(c.get("shirt_col", "EEEEEE"))
    broad = 1.08 if clothes in ("pea", "doorman", "jumper", "tux") else 1.0
    first = len(parts)
    els = [E((0, 0.035, -0.33), 0.2, (1.04 * broad, 0.56, 0.6)),                     # chest
           E((0, 0.025, -0.205), 0.075, (1.3, 0.9, 0.55))]                            # base of the neck / trapezius
    for sx in (-1, 1):
        els.append(E((sx * 0.15 * broad, 0.045, -0.35), 0.1, (0.82, 0.8, 1.25), rot=(0, sx * -24, 0)))     # sloping shoulder and arm
    body = metaball("torso", els, col, res=0.005, smooth_iters=26)
    parts.append(body)
    surf = Surface(body)

    def patch(name, outline, mat, off, thick=0.003):
        parts.append(projected_patch(surf, name, outline, mat, off, thick))

    if clothes in ("suit", "tux", "tweed", "doorman"):
        patch("shirtV", [(-0.045, -0.16), (0.045, -0.16), (0.0, -0.33)], shirt, 0.0025)
        lap = col if clothes != "tux" else material("satin_1A1A1E", "1A1A1E", rough=0.42)
        for sx in (-1, 1):
            patch("lapel", [(sx * 0.045, -0.165), (sx * 0.095, -0.2), (sx * 0.075, -0.24), (sx * 0.02, -0.34), (sx * 0.005, -0.33)],
                  lap, 0.0055, 0.004)
            patch("collarPt", [(sx * 0.008, -0.175), (sx * 0.048, -0.162), (sx * 0.03, -0.205)], shirt, 0.0065, 0.002)
        if clothes == "doorman":
            brass = material("brass", "C9A24A", metallic=0.9, rough=0.3)
            for i in range(3):
                p, _ = surf.front(0.0, -0.36 - i * 0.04, 0.006)
                if p:
                    parts.append(lib.sphere("button", 0.008, p, brass, seg=16, rings=8, scale=(1, 0.5, 1)))
            for sx in (-1, 1):   # epaulettes
                p, _ = surf.toward((sx * 0.17, 0.03, 0.0), (0, 0, -1), 0.0)
                if p:
                    parts.append(lib.box("epaulette", (0.07, 0.05, 0.008), p, brass, 0.003))
    if clothes in ("whites", "cardigan", "breton", "jumper"):
        neck_col = shirt if clothes in ("cardigan", "whites") else col
        ring = lib.torus("neckline", 0.058, 0.011, (0, 0.012, -0.19), neck_col, rot=(math.radians(-8), 0, 0))
        ring.scale = (1.15, 0.95, 1.0)
        parts.append(ring)
        if clothes == "cardigan":
            for sx in (-1, 1):
                patch("cardFront", [(sx * 0.004, -0.2), (sx * 0.06, -0.19), (sx * 0.03, -0.42), (sx * 0.004, -0.42)], col, 0.004, 0.004)
            buttons = material("button_E8E0CC", "E8E0CC", rough=0.3)
            for i in range(2):
                p, _ = surf.front(0.0, -0.3 - i * 0.05, 0.009)
                if p:
                    parts.append(lib.sphere("button", 0.006, p, buttons, seg=12, rings=6, scale=(1, 0.5, 1)))
        if clothes == "whites":
            buttons = material("button_D8D2C4", "D8D2C4", rough=0.3)
            for sx in (-1, 1):
                for i in range(2):
                    p, _ = surf.front(sx * 0.04, -0.28 - i * 0.06, 0.004)
                    if p:
                        parts.append(lib.sphere("button", 0.007, p, buttons, seg=12, rings=6, scale=(1, 0.5, 1)))
    if clothes in ("duffel", "pea"):
        for sx in (-1, 1):
            patch("collar", [(sx * 0.035, -0.17), (sx * 0.095, -0.175), (sx * 0.085, -0.225), (sx * 0.045, -0.25)], col, 0.005, 0.005)
        tog = material("wood_C9B08A", "C9B08A", rough=0.5) if clothes == "duffel" else material("button_141414", "141414", rough=0.3)
        for sx in (-1, 1):
            for i in range(2):
                p, _ = surf.front(sx * 0.045, -0.3 - i * 0.06, 0.006)
                if p:
                    sc = (1, 0.5, 2.0) if clothes == "duffel" else (1, 0.5, 1)
                    parts.append(lib.sphere("toggle", 0.008, p, tog, seg=12, rings=6, scale=sc))
    tie = c.get("tie")
    if tie == "tie":
        tm = fabric_mat(c["tie_col"])
        patch("tie", [(-0.008, -0.19), (0.008, -0.19), (0.014, -0.3), (0.0, -0.33), (-0.014, -0.3)], tm, 0.004, 0.003)
        p, _ = surf.front(0, -0.2, 0.012)
        if p:
            parts.append(lib.sphere("knot", 0.011, p, tm, seg=16, rings=8, scale=(1.1, 0.6, 1)))
    if tie == "bow":
        m = fabric_mat(c["tie_col"])
        p, _ = surf.front(0, -0.205, 0.02)
        if p:
            for sx in (-1, 1):
                parts.append(lib.sphere("bow", 0.017, p + Vector((sx * 0.017, 0, 0)), m, seg=16, rings=8, scale=(1.2, 0.5, 0.75)))
            parts.append(lib.sphere("knot", 0.007, p + Vector((0, -0.003, 0)), m, seg=12, rings=6))
    if c.get("scarf"):
        m = fabric_mat(c["scarf"], "knit")
        sc = lib.torus("scarf", 0.06, 0.02, (0, 0.006, -0.18), m, rot=(math.radians(-10), 0, 0))
        sc.scale = (1.15, 1.0, 0.85)
        parts.append(sc)
        patch("scarfTail", [(0.03, -0.2), (0.06, -0.21), (0.065, -0.33), (0.035, -0.33)], m, 0.008, 0.006)
    for o in parts[first:]:
        o.location.z += 0.042   # sit the shoulders higher so the neck reads short and solid
    return surf


# --------------------------------------------------------------------------- assembly

def build_character(cid):
    c = CHARACTERS[cid]
    parts = []
    skin = skin_mat(c)
    h = head(c, skin)
    parts.append(h)
    surf = Surface(h)
    look = Vector((0.15 if hash(cid) % 2 else -0.15, -0.9, 0.02))
    eyes(c, surf, parts, skin, look)
    mouth(c, surf, parts)
    lines(c, surf, parts, skin)
    freckles(c, surf, parts)
    hair(c, parts)
    facial_hair(c, surf, parts)
    glasses(c, surf, parts)
    earrings(c, parts)
    hat(c, parts)
    torso(c, parts)
    root = lib.parent_all("bust_" + cid, parts)
    root.rotation_euler = (0, math.radians(c.get("tilt", 0)) * 0.5, 0)
    return root


def studio(cid):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x = sc.render.resolution_y = 768
    sc.eevee.taa_render_samples = 96
    try:
        sc.eevee.use_raytracing = True
        sc.eevee.use_shadows = True
    except AttributeError:
        pass
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Punchy"
    except TypeError:
        pass
    world = bpy.data.worlds.new("w")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.045, 0.04, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.25
    # Backdrop: a curved studio sweep with a soft mottled texture.
    bd = material("backdrop_" + cid, BACKDROPS.get(cid, "6E6A62"), rough=1.0)
    nt = bd.node_tree
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3
    noise.inputs["Detail"].default_value = 4
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["Factor"].default_value = 0.12
    mix.inputs["A"].default_value = nt.nodes["Principled BSDF"].inputs["Base Color"].default_value
    nt.links.new(noise.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    sweep = lib.box("backdrop", (3.0, 0.02, 2.4), (0, 0.42, -0.3), bd)

    cam_data = bpy.data.cameras.new("cam")
    cam_data.lens = 90
    cam_data.dof.use_dof = False
    cam_data.dof.aperture_fstop = 9
    cam = bpy.data.objects.new("cam", cam_data)
    link(cam)
    yaw = math.radians(14 if hash(cid) % 2 else -14)
    dist = 1.28
    cam.location = (math.sin(yaw) * dist, -math.cos(yaw) * dist, 0.04)
    target = Vector((0, 0, -0.075))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.dof.focus_distance = (Vector((0, -0.09, 0.01)) - cam.location).length
    sc.camera = cam

    def area(name, energy, size, loc, color, aim=(0, 0, 0)):
        L = bpy.data.lights.new(name, "AREA")
        L.energy = energy
        L.size = size
        L.color = color
        o = bpy.data.objects.new(name, L)
        o.location = loc
        o.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        link(o)

    side = 1 if hash(cid) % 2 else -1
    area("key", 60, 0.6, (-0.75 * side, -0.85, 0.55), (1.0, 0.9, 0.78), (0, 0, -0.02))
    area("fill", 9, 1.4, (0.95 * side, -0.75, -0.05), (0.78, 0.86, 1.0))
    area("rim", 70, 0.3, (0.45 * side, 0.6, 0.5), (1.0, 0.9, 0.78), (0, 0, 0.0))
    area("rim2", 30, 0.3, (-0.5 * side, 0.55, 0.25), (0.82, 0.88, 1.0), (0, 0, 0.0))
    area("bg", 22, 1.0, (0.25 * side, 0.1, 0.3), (1.0, 0.92, 0.8), (0, 0.42, -0.1))


BACKDROPS = {
    "agnes": "8A8274", "bram": "6E726C", "clem": "84786A", "marlow": "616872", "ines": "7A7468", "rolf": "746E60",
    "nell": "7E776A", "elias": "656C70", "cole": "746A60", "rook": "645E58",
}


def build_all(only, preview):
    for cid in CHARACTERS:
        if only and cid not in only:
            continue
        lib.reset()
        build_character(cid)
        studio(cid)
        out = os.path.join(lib.RENDERS, f"portrait_{cid}.png")
        lib.render_preview(out)
        print(f"[portraits] rendered {cid}")
    if only:
        return
    lib.reset()
    x = 0
    for cid in CHARACTERS:
        b = build_character(cid)
        b.location.x = x
        x += 0.5
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, "ArtSource", "portraits.blend"))
