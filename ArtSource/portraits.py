"""Parametric busts for the ten suspects, rendered as studio photographs.

Each character is a small dict of choices (skin, hair style/colour, hat, glasses, facial hair,
clothes). Renders go to ArtSource/renders/portrait_<id>.png; Tools/photo_finish.py then gives
them a 1980s print look and writes Assets/Resources/Portraits/<id>.png.
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

import lib
from lib import material, sphere, cylinder, box, torus, smooth, mesh_obj, link, lathe

CHARACTERS = {
    "agnes": dict(skin="E8C4A8", hair="bun", hair_col="B9B4AC", glasses="round", clothes="cardigan", cloth_col="8C7BA6",
                  shirt_col="F1ECE0", brow=-6, mouth=-0.3, age=0.8, earrings="pearl", face_w=0.95),
    "bram": dict(skin="5E3B26", hair="crop", hair_col="141110", clothes="whites", cloth_col="EFEDE6", shirt_col="EFEDE6",
                 brow=4, mouth=-0.1, age=0.1, face_w=1.0, hat="baker"),
    "clem": dict(skin="E2B596", hair="bald_sides", hair_col="9C958A", mustache="walrus", clothes="tweed", cloth_col="7A5B3A",
                 shirt_col="E9E1CF", tie="bow", tie_col="7A1F24", brow=-2, mouth=0.15, age=0.6, face_w=1.05),
    "marlow": dict(skin="E6C2A2", hair="slick", hair_col="2A211C", mustache="pencil", clothes="suit", cloth_col="2B3446",
                   shirt_col="E8E6DF", tie="tie", tie_col="6E2A2A", brow=-8, mouth=0.05, age=0.4, face_w=0.92),
    "ines": dict(skin="E0B48E", hair="bob", hair_col="2B1B14", clothes="breton", cloth_col="23314F", shirt_col="F2EFE6",
                 scarf="7E1F1F", brow=-4, mouth=0.1, age=0.25, face_w=0.93),
    "rolf": dict(skin="E4B79E", hair="short", hair_col="CFCAC2", beard="full", beard_col="DAD5CC", hat="flatcap", hat_col="5A5148",
                 clothes="jumper", cloth_col="27334A", brow=2, mouth=-0.05, age=0.85, face_w=1.06),
    "nell": dict(skin="F0D2BC", hair="long", hair_col="8E3B1E", clothes="duffel", cloth_col="3F5E44", shirt_col="E9E2D3",
                 brow=6, mouth=-0.15, age=0.0, face_w=0.9),
    "elias": dict(skin="D9A78A", hair="short", hair_col="8F8A84", beard="full", beard_col="9A948C", hat="peaked", hat_col="1E2738",
                  clothes="pea", cloth_col="1E2738", brow=-3, mouth=-0.1, age=0.7, face_w=1.04),
    "cole": dict(skin="D6A283", hair="short", hair_col="3B2B20", stubble=True, hat="doorman", hat_col="6A1E25",
                 clothes="doorman", cloth_col="6A1E25", brow=-10, mouth=-0.25, age=0.45, face_w=1.08),
    "rook": dict(skin="E7AE97", hair="swept", hair_col="E6E2DA", mustache="walrus", mustache_col="ECE8E0", clothes="tux",
                 cloth_col="141416", shirt_col="F3F1EC", tie="bow", tie_col="141416", brow=-5, mouth=0.25, age=0.9, face_w=1.07),
}


def skin_mat(c):
    m = material("skin_" + c["skin"], c["skin"], rough=0.55)
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    try:
        bsdf.inputs["Subsurface Weight"].default_value = 0.15
        bsdf.inputs["Subsurface Radius"].default_value = (0.02, 0.008, 0.005)
    except KeyError:
        pass
    return m


def head(c, skin):
    """Egg-shaped head with a tapered jaw. Front faces -Y."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=32, radius=0.1)
    w = c.get("face_w", 1.0)
    for v in bm.verts:
        z = v.co.z
        x, y = v.co.x, v.co.y
        k = 1.0
        if z < 0:
            k = 1 - 0.32 * (-z / 0.1) ** 1.4
        v.co.x = x * 0.86 * w * k
        v.co.y = y * 0.95 * (1 - 0.1 * max(0, -z / 0.1))
        v.co.z = z * 1.22
        # Cheekbones forward a touch, chin forward.
        if y < 0 and -0.03 < z < 0.02:
            v.co.y -= 0.006
        if z < -0.08 and y < 0:
            v.co.y -= 0.01
    ob = mesh_obj("head", bm, skin)
    return smooth(ob, 80)


def face(c, skin, parts):
    dark = material("col_1A1412", "1A1412", rough=0.3)
    white = material("col_EFEBE4", "EFEBE4", rough=0.2)
    lip = material("skin_lip_" + c["skin"], _shade(c["skin"], 0.78), rough=0.45)
    brow_col = material("hair_" + c.get("brow_col", c["hair_col"]), c.get("brow_col", c["hair_col"]), rough=0.8)
    for sx in (-1, 1):
        parts.append(sphere("eyewhite", 0.0128, (sx * 0.032, -0.083, 0.012), white, scale=(1.15, 0.6, 0.85)))
        parts.append(sphere("iris", 0.0074, (sx * 0.032, -0.091, 0.012), dark))
        # Eyelid line above the eye.
        lid = box("lid", (0.026, 0.004, 0.003), (sx * 0.032, -0.088, 0.02), material("skin_lid_" + c["skin"], _shade(c["skin"], 0.85)))
        parts.append(lid)
        b = box("brow", (0.03, 0.006, 0.006), (sx * 0.033, -0.088, 0.034), brow_col, 0.002)
        b.rotation_euler = (0, math.radians(sx * c.get("brow", 0)), 0)
        parts.append(b)
        ear = sphere("ear", 0.018, (sx * 0.09 * c.get("face_w", 1.0), 0.0, 0.0), skin, scale=(0.45, 0.8, 1.25))
        parts.append(ear)
    nose = sphere("nose", 0.0155, (0, -0.101, -0.008), skin, scale=(0.8, 1.25, 1.5))
    parts.append(nose)
    tip = sphere("noseTip", 0.011, (0, -0.108, -0.022), skin)
    parts.append(tip)
    # Mouth: a gentle arc; positive "mouth" curves up (smile), negative down.
    curve = c.get("mouth", 0)
    bmm = bmesh.new()
    pts = []
    for i in range(9):
        t = i / 8 - 0.5
        pts.append((t * 0.042, -0.094 + abs(t) * 0.01, -0.052 + curve * 0.02 * (t * t * 4 - 1) * -1))
    prev = None
    for p in pts:
        v = bmm.verts.new(p)
        if prev:
            bmm.edges.new((prev, v))
        prev = v
    mouth = mesh_obj("mouth", bmm, lip)
    mod = mouth.modifiers.new("skin", "SKIN")
    for v in mouth.data.skin_vertices[0].data:
        v.radius = (0.0032, 0.0032)
    parts.append(mouth)
    if c.get("age", 0) > 0.55:
        # Smile lines / wrinkles suggested by darker strokes.
        line = material("skin_line_" + c["skin"], _shade(c["skin"], 0.82))
        for sx in (-1, 1):
            l = box("fold", (0.003, 0.003, 0.03), (sx * 0.032, -0.09, -0.04), line)
            l.rotation_euler = (0, math.radians(sx * 20), 0)
            parts.append(l)


def _shade(hexcol, k):
    r, g, b = lib.hex_rgb(hexcol)
    return "%02X%02X%02X" % (int(r * k * 255), int(g * k * 255), int(b * k * 255))


def shell(name, r, loc, scale, mat, face_top=0.045, side_keep=None, seg=96):
    """A sphere of hair with the face region cut away (front, below the hairline)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=64, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        ax = abs(c.x)
        if side_keep is not None:
            # Long styles: open only the face oval.
            if c.y < -0.02 and c.z < face_top and ax < side_keep:
                kill.append(f)
            continue
        hairline = face_top + 0.02 - 0.25 * ax          # higher in the middle, lower at the temples
        if c.y < -0.035 and c.z < hairline:
            kill.append(f)
        elif -0.035 <= c.y < 0.035 and c.z < 0.012:
            kill.append(f)                               # above the ears
        elif c.y >= 0.035 and c.z < -0.075:
            kill.append(f)                               # nape
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    ob = mesh_obj(name, bm, mat)
    sol = ob.modifiers.new("thick", "SOLIDIFY")
    sol.thickness = 0.006
    return smooth(ob, 80)


def hair(c, parts):
    style = c.get("hair")
    col = material("hair_" + c["hair_col"], c["hair_col"], rough=0.75)
    w = c.get("face_w", 1.0)
    if style in ("short", "crop", "slick", "swept", "bun"):
        cut = 0.05 if style != "crop" else 0.06
        parts.append(shell("hairCap", 0.106, (0, 0.004, 0.012), (0.9 * w, 1.0, 1.18), col, face_top=cut))
    if style == "slick":
        parts.append(shell("slick", 0.108, (0, 0.012, 0.02), (0.9 * w, 1.02, 1.15), col, face_top=0.06))
    if style == "swept":
        parts.append(shell("swept", 0.11, (0, 0.012, 0.03), (0.92 * w, 1.04, 1.12), col, face_top=0.065))
        parts.append(sphere("quiff", 0.045, (0, -0.055, 0.11), col, scale=(1.5, 0.8, 0.55)))
    if style == "bun":
        parts.append(sphere("bun", 0.045, (0, 0.075, 0.12), col))
    if style == "bob":
        parts.append(shell("bob", 0.116, (0, 0.012, 0.0), (0.98 * w, 1.04, 1.14), col, face_top=0.055, side_keep=0.068))
        parts.append(sphere("fringe", 0.06, (0, -0.075, 0.075), col, scale=(1.45, 0.45, 0.42)))
    if style == "long":
        parts.append(shell("long", 0.118, (0, 0.012, 0.0), (0.98 * w, 1.04, 1.16), col, face_top=0.052, side_keep=0.07))
        back = cylinder("longBack", 0.1, 0.22, (0, 0.035, -0.29), col, seg=32, r2=0.09)
        back.scale = (1.1, 0.75, 1)
        parts.append(back)
        for sx in (-1, 1):
            lock = cylinder("lock", 0.03, 0.2, (sx * 0.085, -0.01, -0.25), col, seg=16, r2=0.022)
            parts.append(lock)
        fringe = sphere("fringe", 0.06, (0.025, -0.075, 0.075), col, scale=(1.5, 0.45, 0.42))
        fringe.rotation_euler = (0, math.radians(-14), 0)
        parts.append(fringe)
    if style == "bald_sides":
        for sx in (-1, 1):
            parts.append(sphere("side", 0.05, (sx * 0.07 * w, 0.03, 0.0), col, scale=(0.5, 1.2, 1.0)))
        parts.append(sphere("back", 0.08, (0, 0.06, -0.005), col, scale=(1.1, 0.6, 0.8)))


def facial_hair(c, parts):
    m = c.get("mustache")
    if m:
        col = material("hair_" + c.get("mustache_col", c["hair_col"]), c.get("mustache_col", c["hair_col"]), rough=0.8)
        if m == "walrus":
            parts.append(sphere("mustache", 0.03, (0, -0.096, -0.036), col, scale=(1.5, 0.55, 0.55)))
        else:
            parts.append(box("mustache", (0.04, 0.006, 0.004), (0, -0.098, -0.038), col, 0.0015))
    b = c.get("beard")
    if b:
        col = material("hair_" + c.get("beard_col", c["hair_col"]), c.get("beard_col", c["hair_col"]), rough=0.85)
        parts.append(sphere("beard", 0.075, (0, -0.045, -0.075), col, scale=(1.05, 0.9, 0.85)))
        parts.append(sphere("mustache", 0.03, (0, -0.096, -0.036), col, scale=(1.4, 0.55, 0.5)))
    if c.get("stubble"):
        col = material("stubble_" + c["skin"], _shade(c["skin"], 0.72), rough=0.9)
        parts.append(sphere("stubble", 0.083, (0, -0.03, -0.06), col, scale=(1.0, 0.85, 0.8)))


def glasses(c, parts):
    if c.get("glasses") != "round":
        return
    frame = material("metal_8A7A55", "8A7A55", metallic=1, rough=0.3)
    for sx in (-1, 1):
        parts.append(torus("lens", 0.017, 0.0016, (sx * 0.032, -0.101, 0.012), frame, rot=(math.radians(90), 0, 0)))
        arm = box("arm", (0.002, 0.08, 0.002), (sx * 0.05, -0.06, 0.016), frame)
        parts.append(arm)
    parts.append(box("bridge", (0.016, 0.002, 0.002), (0, -0.104, 0.016), frame))


def hat(c, parts):
    h = c.get("hat")
    if not h:
        return
    col = material("fabric_" + c.get("hat_col", "222222"), c.get("hat_col", "222222"), rough=0.8)
    w = c.get("face_w", 1.0)
    if h == "flatcap":
        parts.append(sphere("cap", 0.108, (0, 0.0, 0.06), col, scale=(0.95 * w, 1.05, 0.55)))
        brim = cylinder("brim", 0.06, 0.008, (0, -0.075, 0.06), col, seg=24)
        brim.scale = (1.3, 0.8, 1)
        parts.append(brim)
    elif h in ("peaked", "doorman"):
        crown = cylinder("crown", 0.1, 0.07, (0, 0.0, 0.07), col, seg=40, r2=0.11)
        crown.scale = (0.95 * w, 1.0, 1)
        parts.append(crown)
        band = cylinder("band", 0.101, 0.02, (0, 0.0, 0.07), material("fabric_141414", "141414"), seg=40)
        band.scale = (0.96 * w, 1.01, 1)
        parts.append(band)
        peak = cylinder("peak", 0.07, 0.006, (0, -0.07, 0.07), material("plastic_111111", "111111", rough=0.2), seg=24)
        peak.scale = (1.2, 0.75, 1)
        peak.rotation_euler = (math.radians(-12), 0, 0)
        parts.append(peak)
        if h == "doorman" or h == "peaked":
            parts.append(sphere("badge", 0.012, (0, -0.1, 0.11), material("brass", "C9A24A", metallic=0.9, rough=0.3), scale=(1.2, 0.4, 1)))
    elif h == "baker":
        cap = cylinder("toque", 0.095, 0.05, (0, 0.0, 0.07), material("fabric_F4F2EC", "F4F2EC", rough=0.9), seg=32, r2=0.1)
        parts.append(cap)


def torso(c, parts):
    clothes = c.get("clothes", "suit")
    col = material("fabric_" + c["cloth_col"], c["cloth_col"], rough=0.85)
    shirt = material("fabric_" + c.get("shirt_col", "EEEEEE"), c.get("shirt_col", "EEEEEE"), rough=0.7)
    skin = skin_mat(c)
    parts.append(cylinder("neck", 0.056, 0.12, (0, 0.016, -0.21), skin, seg=24))
    body = sphere("torso", 0.24, (0, 0.035, -0.33), col, scale=(1.0, 0.5, 0.7))
    parts.append(body)
    if clothes in ("suit", "tux", "tweed", "doorman"):
        # V of shirt between lapels.
        bm = bmesh.new()
        vs = [bm.verts.new(p) for p in [(-0.05, -0.105, -0.17), (0.05, -0.105, -0.17), (0, -0.12, -0.31)]]
        bm.faces.new(vs)
        v = mesh_obj("shirtV", bm, shirt)
        sol = v.modifiers.new("t", "SOLIDIFY")
        sol.thickness = 0.004
        parts.append(v)
        for sx in (-1, 1):
            lap = box("lapel", (0.03, 0.01, 0.14), (sx * 0.045, -0.118, -0.25), col, 0.004)
            lap.rotation_euler = (math.radians(-6), math.radians(sx * 18), 0)
            parts.append(lap)
        if clothes == "doorman":
            for i in range(3):
                parts.append(sphere("button", 0.008, (0.0, -0.125, -0.24 - i * 0.045), material("brass", "C9A24A", metallic=0.9, rough=0.3)))
    if clothes in ("whites", "cardigan", "breton", "jumper"):
        neckline = cylinder("neckline", 0.075, 0.02, (0, 0.02, -0.175), shirt if clothes in ("cardigan", "whites") else col, seg=32, r2=0.062)
        neckline.scale = (1.0, 0.75, 1)
        parts.append(neckline)
    if clothes in ("duffel", "pea"):
        for sx in (-1, 1):
            c2 = box("collar", (0.022, 0.07, 0.075), (sx * 0.07, 0.0, -0.17), col, 0.008)
            c2.rotation_euler = (0, math.radians(sx * 18), 0)
            parts.append(c2)
    if clothes == "breton":
        stripe = material("fabric_F2EFE6", "F2EFE6", rough=0.8)
        for i in range(4):
            dz = 0.06 + i * 0.035
            rr = 0.24 * math.sqrt(max(0.0, 1 - (dz / (0.24 * 0.7)) ** 2)) + 0.002
            r = torus("stripe", rr, 0.006, (0, 0.035, -0.33 + dz), stripe, rot=(0, 0, 0))
            r.scale = (1.0, 0.5, 1)
            parts.append(r)
    if clothes == "duffel" or clothes == "pea":
        for sx in (-1, 1):
            parts.append(sphere("toggle", 0.01, (sx * 0.03, -0.12, -0.25), material("wood_C9B08A", "C9B08A"), scale=(1, 0.6, 1.8)))
    tie = c.get("tie")
    if tie == "tie":
        parts.append(box("tie", (0.022, 0.008, 0.11), (0, -0.124, -0.24), material("fabric_" + c["tie_col"], c["tie_col"]), 0.003))
        parts.append(box("knot", (0.022, 0.01, 0.018), (0, -0.122, -0.175), material("fabric_" + c["tie_col"], c["tie_col"]), 0.004))
    if tie == "bow":
        m = material("fabric_" + c["tie_col"], c["tie_col"], rough=0.6)
        for sx in (-1, 1):
            parts.append(sphere("bow", 0.02, (sx * 0.02, -0.118, -0.17), m, scale=(1.2, 0.5, 0.8)))
        parts.append(sphere("knot", 0.008, (0, -0.122, -0.17), m))
    if c.get("scarf"):
        m = material("fabric_" + c["scarf"], c["scarf"], rough=0.9)
        sc = torus("scarf", 0.056, 0.02, (0, 0.0, -0.16), m)
        sc.scale = (1.1, 1.0, 0.8)
        parts.append(sc)
        parts.append(box("scarfTail", (0.03, 0.012, 0.09), (0.04, -0.11, -0.22), m, 0.005))
    if c.get("earrings") == "pearl":
        pearl = material("ceramic_F2EDE2", "F2EDE2", rough=0.15)
        for sx in (-1, 1):
            parts.append(sphere("pearl", 0.007, (sx * 0.088, -0.01, -0.035), pearl))


def build_character(cid):
    c = CHARACTERS[cid]
    parts = []
    skin = skin_mat(c)
    parts.append(head(c, skin))
    face(c, skin, parts)
    hair(c, parts)
    facial_hair(c, parts)
    glasses(c, parts)
    hat(c, parts)
    torso(c, parts)
    return lib.parent_all("bust_" + cid, parts)


def studio(cid):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x = sc.render.resolution_y = 512
    sc.eevee.taa_render_samples = 64
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    world = bpy.data.worlds.new("w")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.02, 0.02, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.3
    # Backdrop: a mottled studio paper.
    backdrop = box("backdrop", (1.6, 0.02, 1.4), (0, 0.45, -0.1), material("backdrop_" + cid, BACKDROPS.get(cid, "6E6A62"), rough=1.0))
    cam_data = bpy.data.cameras.new("cam")
    cam_data.lens = 85
    cam = bpy.data.objects.new("cam", cam_data)
    link(cam)
    yaw = math.radians(12 if hash(cid) % 2 else -12)
    dist = 0.92
    cam.location = (math.sin(yaw) * dist, -math.cos(yaw) * dist, 0.03)
    target = Vector((0, 0, -0.075))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam

    def area(name, energy, size, loc, color):
        L = bpy.data.lights.new(name, "AREA")
        L.energy = energy
        L.size = size
        L.color = color
        o = bpy.data.objects.new(name, L)
        o.location = loc
        o.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        link(o)

    area("key", 70, 0.6, (-0.7, -0.8, 0.5), (1.0, 0.88, 0.72))
    area("fill", 18, 1.0, (0.9, -0.7, 0.1), (0.75, 0.85, 1.0))
    area("rim", 45, 0.4, (0.4, 0.6, 0.6), (1.0, 0.9, 0.8))
    area("bg", 25, 1.0, (0, 0.1, 0.4), (1.0, 0.92, 0.8))


BACKDROPS = {
    "agnes": "7C7569", "bram": "6A6C66", "clem": "7A6E5E", "marlow": "5B6068", "ines": "6E6A60", "rolf": "6B6658",
    "nell": "726C61", "elias": "5F6466", "cole": "6A625A", "rook": "5C5650",
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
    lib.reset()
    x = 0
    for cid in CHARACTERS:
        b = build_character(cid)
        b.location.x = x
        x += 0.4
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, "ArtSource", "portraits.blend"))
