"""Desk props for Alibi & Co.: lamp, mug, rotary phone, pencil, magnifier, push pin, desk spike,
brass pawn, board frame molding and the desk top. Run through ArtSource/build_assets.py."""
import math

import bmesh
import bpy
from mathutils import Vector, Matrix

from lib import (box, cylinder, sphere, torus, lathe, extrude_profile, material, smooth, bevel, join,
                 parent_all, mesh_obj, link)

BRASS = "brass"
BLACK = "plastic_141414"


def box_uv(ob, scale=1.0):
    """Box-project UVs (good enough for wood and leather tiles)."""
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name="uv")
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co
            if ax == 2:
                uv[li].uv = (v.x * scale, v.y * scale)
            elif ax == 1:
                uv[li].uv = (v.x * scale, v.z * scale)
            else:
                uv[li].uv = (v.y * scale, v.z * scale)
    return ob


# --------------------------------------------------------------------------- lamp

def build_lamp():
    brass = material(BRASS, "C9A24A", metallic=0.9, rough=0.3)
    green = material("glass_1F5E3B", "1F5E3B", rough=0.15)
    glow = material("glow_FFD58A", "FFE2A8", emission="FFD58A")
    parts = []
    base = cylinder("base", 0.09, 0.022, (0, 0, 0), brass, seg=48)
    base.scale = (1.25, 1, 1)
    bevel(base, 0.006, 3)
    parts.append(base)
    parts.append(cylinder("step", 0.05, 0.015, (0, 0, 0.022), brass, seg=32))
    parts.append(cylinder("stem", 0.009, 0.3, (0, 0, 0.035), brass, seg=16))
    # Arm across the top that holds the shade.
    arm = cylinder("arm", 0.007, 0.16, (0, 0, 0), brass, seg=12)
    arm.rotation_euler = (0, math.radians(90), 0)
    arm.location = (-0.08, 0, 0.33)
    parts.append(arm)
    # Shade: half cylinder, open at the bottom, slightly flared.
    bm = bmesh.new()
    segs = 24
    L = 0.27
    R = 0.075
    rows = []
    for x in (-L / 2, L / 2):
        row = []
        for i in range(segs + 1):
            a = math.pi * i / segs
            row.append(bm.verts.new((x, R * math.cos(a) * 1.12, R * math.sin(a))))
        rows.append(row)
    for i in range(segs):
        bm.faces.new((rows[0][i], rows[0][i + 1], rows[1][i + 1], rows[1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    shade = mesh_obj("shade", bm, green)
    sol = shade.modifiers.new("thick", "SOLIDIFY")
    sol.thickness = 0.004
    shade.location = (0, 0, 0.29)
    smooth(shade, 60)
    parts.append(shade)
    for x in (-L / 2, L / 2):
        # Brass half-disc end plates.
        bmc = bmesh.new()
        ring = [bmc.verts.new((x, R * 1.14 * math.cos(math.pi * i / segs), R * 1.02 * math.sin(math.pi * i / segs))) for i in range(segs + 1)]
        bmc.faces.new(ring)
        bmesh.ops.recalc_face_normals(bmc, faces=bmc.faces)
        plate = mesh_obj("plate", bmc, brass)
        sol2 = plate.modifiers.new("thick", "SOLIDIFY")
        sol2.thickness = 0.004
        plate.location = (0, 0, 0.29)
        parts.append(plate)
    bulb = sphere("bulb", 0.025, (0, 0, 0.3), glow, scale=(2.2, 1, 1))
    parts.append(bulb)
    chain = cylinder("chain", 0.0018, 0.1, (0.05, -0.05, 0.2), brass, seg=6)
    parts.append(chain)
    parts.append(sphere("pull", 0.007, (0.05, -0.05, 0.2), brass))
    root = parent_all("lamp", parts)
    shade.name = "shade"
    return root


# --------------------------------------------------------------------------- mug

def build_mug():
    cream = material("ceramic_EDE3CC", "EDE3CC", rough=0.25)
    red = material("ceramic_8E2B2B", "8E2B2B", rough=0.3)
    coffee = material("ceramic_2A1810", "2A1810", rough=0.1)
    outer = [(0.0, 0.0), (0.036, 0.0), (0.040, 0.003), (0.042, 0.02), (0.044, 0.09), (0.045, 0.098), (0.041, 0.1), (0.039, 0.095), (0.037, 0.02), (0.0, 0.012)]
    body = lathe("body", outer, cream, 48)
    stripe = lathe("stripe", [(0.0441, 0.07), (0.0446, 0.072), (0.0446, 0.08), (0.0441, 0.082)], red, 48)
    liquid = cylinder("coffee", 0.0385, 0.002, (0, 0, 0.078), coffee, seg=40)
    handle = torus("handle", 0.024, 0.0065, (0.052, 0, 0.055), cream, rot=(math.radians(90), 0, 0))
    handle.scale = (0.8, 1, 1.2)
    return parent_all("mug", [body, stripe, liquid, handle])


# --------------------------------------------------------------------------- rotary phone

def build_phone():
    black = material(BLACK, "141414", rough=0.18)
    cream = material("paper_E9E1CC", "E9E1CC", rough=0.6)
    chrome = material("metal_C8CCD0", "C8CCD0", metallic=1.0, rough=0.2)
    parts = []
    # Body: tapered block.
    bm = bmesh.new()
    w0, d0, w1, d1, h = 0.2, 0.22, 0.15, 0.13, 0.085
    vs = [bm.verts.new(v) for v in [(-w0 / 2, -d0 / 2, 0), (w0 / 2, -d0 / 2, 0), (w0 / 2, d0 / 2, 0), (-w0 / 2, d0 / 2, 0),
                                     (-w1 / 2, -0.035, h), (w1 / 2, -0.035, h), (w1 / 2, 0.085, h), (-w1 / 2, 0.085, h)]]
    for f in [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]:
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    body = mesh_obj("body", bm, black)
    bevel(body, 0.012, 4, 20)
    parts.append(body)
    # Dial on the sloped front: chrome disc, dark finger holes, cream centre label.
    dial = cylinder("dial", 0.055, 0.006, (0, 0, 0), chrome, seg=48)
    dark = material("plastic_0A0A0A", "0A0A0A", rough=0.4)
    holes = []
    for i in range(10):
        a = math.radians(60 + i * 26)
        holes.append(cylinder(f"hole{i}", 0.0085, 0.0012, (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.006), dark, seg=16))
    face = cylinder("face", 0.024, 0.0014, (0, 0, 0.006), cream, seg=32)
    stop = box("stop", (0.006, 0.02, 0.004), (0.045, -0.03, 0.008), chrome)
    dial_root = parent_all("dialGroup", [dial, face, stop] + holes)
    dial_root.location = (0, -0.0755, 0.0452)
    dial_root.rotation_euler = (math.radians(48.6), 0, 0)
    parts.append(dial_root)
    # Cradle prongs and the handset across the top.
    for x in (-0.06, 0.06):
        p = box("prong", (0.02, 0.03, 0.03), (x, 0.03, h + 0.012), black, 0.006)
        parts.append(p)
    bar = cylinder("handle", 0.016, 0.24, (0, 0, 0), black, seg=20)
    bar.rotation_euler = (0, math.radians(90), 0)
    bar.location = (-0.12, 0.03, h + 0.04)
    parts.append(bar)
    for x in (-0.11, 0.11):
        cup = cylinder("cup", 0.03, 0.04, (x, 0.03, h + 0.012), black, seg=24, r2=0.024)
        parts.append(cup)
    # Coiled cord: a tube along a lazy curve from the handset end to the body.
    curve = bpy.data.curves.new("cord", "CURVE")
    curve.dimensions = "3D"
    spl = curve.splines.new("BEZIER")
    pts = [(-0.135, 0.03, h + 0.03), (-0.17, -0.04, 0.03), (-0.12, -0.13, 0.01), (-0.06, -0.12, 0.02)]
    spl.bezier_points.add(len(pts) - 1)
    for bp_, p in zip(spl.bezier_points, pts):
        bp_.co = p
        bp_.handle_left_type = bp_.handle_right_type = "AUTO"
    curve.bevel_depth = 0.004
    curve.bevel_resolution = 3
    cord_ob = bpy.data.objects.new("cord", curve)
    link(cord_ob)
    cord_ob.data.materials.append(black)
    bpy.context.view_layer.objects.active = cord_ob
    cord_ob.select_set(True)
    bpy.ops.object.convert(target="MESH")
    parts.append(bpy.context.active_object)
    return parent_all("phone", parts)


# --------------------------------------------------------------------------- pencil

def build_pencil():
    yellow = material("col_E7B53C", "E7B53C", rough=0.35)
    wood = material("col_D9A87A", "D9A87A", rough=0.7)
    lead = material("col_2A2A2A", "2A2A2A", rough=0.4)
    ferrule = material("metal_B8B0A0", "B8B0A0", metallic=1, rough=0.35)
    eraser = material("col_D98C8C", "D98C8C", rough=0.8)
    L = 0.16
    body = cylinder("body", 0.0042, L, (0, 0, 0), yellow, seg=6)
    cone = cylinder("cone", 0.0042, 0.018, (0, 0, L), wood, seg=12, r2=0.0012)
    tip = cylinder("tip", 0.0012, 0.004, (0, 0, L + 0.018), lead, seg=8, r2=0.0001)
    fer = cylinder("ferrule", 0.0046, 0.012, (0, 0, -0.012), ferrule, seg=16)
    er = cylinder("eraser", 0.0043, 0.01, (0, 0, -0.022), eraser, seg=16)
    root = parent_all("pencil", [body, cone, tip, fer, er])
    root.rotation_euler = (0, math.radians(90), 0)
    root.location = (0, 0, 0.0045)
    outer = parent_all("pencil_root", [root])
    outer.name = "pencil"
    root.name = "pencil_body"
    return outer


# --------------------------------------------------------------------------- magnifier

def build_magnifier():
    brass = material(BRASS, "C9A24A", metallic=0.9, rough=0.3)
    glass = material("glass_DDE8EA", "DDE8EA", rough=0.02)
    wood = material("wood_3B2416", "3B2416", rough=0.4)
    ring = torus("ring", 0.05, 0.005, (0, 0, 0.008), brass, seg=64, rseg=12)
    lens = cylinder("lens", 0.049, 0.004, (0, 0, 0.006), glass, seg=48)
    collar = cylinder("collar", 0.0075, 0.025, (0, 0, 0), brass, seg=16)
    collar.rotation_euler = (0, math.radians(90), 0)
    collar.location = (0.052, 0, 0.008)
    handle = cylinder("handle", 0.009, 0.1, (0, 0, 0), wood, seg=16, r2=0.007)
    handle.rotation_euler = (0, math.radians(90), 0)
    handle.location = (0.077, 0, 0.008)
    knob = sphere("knob", 0.008, (0.178, 0, 0.008), wood)
    return parent_all("magnifier", [ring, lens, collar, handle, knob])


# --------------------------------------------------------------------------- push pin, spike, pawn

def build_pin():
    head = material("plastic_C0392B", "C0392B", rough=0.25)
    steel = material("metal_C8CCD0", "C8CCD0", metallic=1, rough=0.25)
    prof = [(0.0, 0.0), (0.0055, 0.0), (0.0062, 0.0015), (0.0035, 0.003), (0.003, 0.006), (0.0058, 0.0075), (0.006, 0.0095), (0.0045, 0.0115), (0.0, 0.012)]
    h = lathe("head", prof, head, 32)
    needle = cylinder("needle", 0.0006, 0.008, (0, 0, -0.008), steel, seg=8)
    return parent_all("pin", [h, needle])


def build_spike():
    iron = material("metal_2E2E30", "2E2E30", metallic=0.8, rough=0.5)
    brass = material(BRASS, "C9A24A", metallic=0.9, rough=0.3)
    base = lathe("base", [(0, 0), (0.045, 0), (0.046, 0.004), (0.04, 0.014), (0.012, 0.02), (0, 0.021)], iron, 40)
    ring = torus("ring", 0.041, 0.0025, (0, 0, 0.006), brass, seg=48)
    spike = cylinder("spike", 0.0025, 0.16, (0, 0, 0.02), material("metal_9AA0A6", "9AA0A6", metallic=1, rough=0.25), seg=10, r2=0.0003)
    return parent_all("spike", [base, ring, spike])


def build_pawn():
    brass = material(BRASS, "C9A24A", metallic=0.9, rough=0.3)
    # A brass "detective": pawn body in a trench coat silhouette with a fedora.
    prof = [(0.0, 0.0), (0.016, 0.0), (0.017, 0.003), (0.012, 0.006), (0.011, 0.02), (0.008, 0.03), (0.0065, 0.034), (0.0072, 0.036),
            (0.0075, 0.04), (0.007, 0.044), (0.004, 0.047), (0.0, 0.048)]
    body = lathe("body", prof, brass, 32)
    brim = cylinder("brim", 0.011, 0.0015, (0, 0, 0.0445), brass, seg=32)
    crown = cylinder("crown", 0.0065, 0.008, (0, 0, 0.0455), brass, seg=24, r2=0.0055)
    return parent_all("pawn", [body, brim, crown])


# --------------------------------------------------------------------------- frame & desk

def build_frame_bar():
    wood = material("wood_4A2E1A", "4A2E1A", rough=0.45)
    # Molding profile (y across the bar, z up), 5 cm wide, 3.5 cm tall.
    prof = [(-0.025, 0.0), (0.025, 0.0), (0.025, 0.018), (0.02, 0.026), (0.012, 0.034), (0.004, 0.035), (-0.006, 0.032),
            (-0.014, 0.026), (-0.018, 0.02), (-0.022, 0.02), (-0.025, 0.016)]
    bar = extrude_profile("frame_bar", prof, 1.0, wood)
    smooth(bar, 35)
    box_uv(bar, 2.0)
    root = parent_all("frame_bar_root", [bar])
    root.name = "frame_bar"
    bar.name = "bar"
    return root


def build_frame_corner():
    wood = material("wood_3E2615", "3E2615", rough=0.45)
    brass = material(BRASS, "C9A24A", metallic=0.9, rough=0.3)
    blk = box("block", (0.05, 0.05, 0.036), (0, 0, 0.018), wood, 0.004)
    box_uv(blk, 2.0)
    ros = lathe("rosette", [(0, 0.036), (0.012, 0.036), (0.014, 0.039), (0.008, 0.044), (0, 0.045)], brass, 24)
    return parent_all("frame_corner", [blk, ros])


def build_desk():
    planks = []
    wood_cols = ["5E3B22", "6A4428", "573620", "644026", "5B3922"]
    W, D, T = 4.4, 3.2, 0.05
    n = 8
    pw = D / n
    for i in range(n):
        y = -D / 2 + pw * (i + 0.5)
        p = box(f"plank{i}", (W, pw - 0.003, T), (0, y, -T / 2), material(f"wood_{wood_cols[i % len(wood_cols)]}", wood_cols[i % len(wood_cols)], rough=0.4), 0.0025)
        box_uv(p, 1.6)
        planks.append(p)
    # Leather blotter under the case board (dark green, brass corners).
    leather = material("leather_2F4636", "2F4636", rough=0.55)
    blot = box("blotter", (2.6, 1.15, 0.006), (0, 0.33, 0.003), leather, 0.002)
    box_uv(blot, 3.0)
    brass = material(BRASS, "C9A24A", metallic=0.9, rough=0.3)
    corners = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = box("corner", (0.06, 0.06, 0.008), (sx * 1.3 - sx * 0.03, 0.33 + sy * 0.575 - sy * 0.03, 0.004), brass, 0.002)
            corners.append(c)
    return parent_all("desk", planks + [blot] + corners)


# --------------------------------------------------------------------------- case folder, fountain pen

def build_folder():
    """A closed manila case folder, a little overstuffed: sheets peek out, a red string winds round a button."""
    manila = material("paper_D9BC82", "D9BC82", rough=0.7)
    manila_dark = material("paper_C9A96C", "C9A96C", rough=0.7)
    sheet = material("paper_F1EBDD", "F1EBDD", rough=0.8)
    sheet2 = material("paper_E9E2D0", "E9E2D0", rough=0.8)
    red = material("col_8E2B2B", "8E2B2B", rough=0.6)
    W, D = 0.33, 0.24
    parts = []
    back = box("back", (W, D, 0.0012), (0, 0, 0.0006), manila_dark)
    parts.append(back)
    # Sheets fanned slightly inside, peeking out of the open edge.
    for i, (dx, dy, rot, m) in enumerate([(0.012, -0.004, 2.2, sheet), (0.02, 0.006, -1.4, sheet2), (0.006, 0.01, 3.4, sheet)]):
        sh = box("sheet", (W - 0.02, D - 0.03, 0.0006), (dx, dy, 0.0016 + i * 0.0007), m)
        sh.rotation_euler = (0, 0, math.radians(rot))
        parts.append(sh)
    # Front cover with a tab along the top edge.
    bm = bmesh.new()
    outline = [(-W / 2, -D / 2), (W / 2, -D / 2), (W / 2, D / 2 - 0.004), (W * 0.18, D / 2 - 0.004), (W * 0.15, D / 2 + 0.018),
               (-W * 0.15, D / 2 + 0.018), (-W * 0.18, D / 2 - 0.004), (-W / 2, D / 2 - 0.004)]
    vs = [bm.verts.new((x, y, 0.0045)) for x, y in outline]
    bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, vec=(0, 0, 0.0012), verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    cover = mesh_obj("cover", bm, manila)
    box_uv(cover, 4.0)
    parts.append(cover)
    # A label on the cover and a ruled box for the case number.
    parts.append(box("label", (0.12, 0.035, 0.0004), (-0.07, 0.07, 0.0062), material("paper_F4EEDF", "F4EEDF", rough=0.8)))
    # String tie: button on the cover, string wound round it and trailing off.
    btn = cylinder("button", 0.009, 0.002, (0.12, -0.02, 0.0058), red, seg=20)
    parts.append(btn)
    pts = [(0.12, -0.02), (0.129, -0.01), (0.115, -0.006), (0.108, -0.026), (0.13, -0.03), (0.155, -0.06), (0.17, -0.1), (0.16, -0.13)]
    bm = bmesh.new()
    prev = None
    for x, y in pts:
        v = bm.verts.new((x, y, 0.0072))
        if prev:
            bm.edges.new((prev, v))
        prev = v
    st = mesh_obj("string", bm, red)
    mod = st.modifiers.new("skin", "SKIN")
    for v in st.data.skin_vertices[0].data:
        v.radius = (0.0009, 0.0009)
    sub = st.modifiers.new("sub", "SUBSURF")
    sub.levels = 2
    parts.append(st)
    return parent_all("folder", parts)


def build_pen():
    """A black lacquer fountain pen with gold trim, cap posted on the end."""
    black = material(BLACK, "141414", rough=0.12)
    gold = material(BRASS, "C9A24A", metallic=0.9, rough=0.3)
    nib = material("metal_D8C27A", "D8C27A", metallic=1, rough=0.25)
    body = lathe("barrel", [(0.0, 0.0), (0.0052, 0.002), (0.0058, 0.02), (0.0056, 0.075), (0.0046, 0.085), (0.0, 0.087)], black, 32)
    grip = lathe("section", [(0.0, 0.085), (0.0045, 0.085), (0.0038, 0.1), (0.0, 0.1)], black, 24)
    n = lathe("nib", [(0.0, 0.098), (0.0034, 0.098), (0.0006, 0.118), (0.0, 0.119)], nib, 24)
    n.scale = (1.0, 0.45, 1.0)
    cap = lathe("cap", [(0.0, -0.05), (0.0049, -0.049), (0.0063, -0.03), (0.0063, 0.008), (0.0, 0.008)], black, 32)
    band = torus("band", 0.0064, 0.0009, (0, 0, -0.004), gold, seg=32, rseg=8)
    band2 = torus("band2", 0.0058, 0.0007, (0, 0, 0.074), gold, seg=32, rseg=8)
    clip = box("clip", (0.0016, 0.0025, 0.04), (0, 0.0066, -0.028), gold, 0.0006)
    root = parent_all("pen_body", [body, grip, n, cap, band, band2, clip])
    root.rotation_euler = (0, math.radians(90), 0)
    root.location = (0, 0, 0.0065)
    outer = parent_all("pen", [root])
    return outer


BUILDERS = {
    "lamp": build_lamp,
    "mug": build_mug,
    "phone": build_phone,
    "pencil": build_pencil,
    "magnifier": build_magnifier,
    "pin": build_pin,
    "spike": build_spike,
    "pawn": build_pawn,
    "frame_bar": build_frame_bar,
    "frame_corner": build_frame_corner,
    "desk": build_desk,
    "folder": build_folder,
    "pen": build_pen,
}

PREVIEW_SIZE = {"lamp": 0.35, "mug": 0.12, "phone": 0.25, "pencil": 0.18, "magnifier": 0.2, "pin": 0.02, "spike": 0.15,
                "pawn": 0.05, "frame_bar": 0.5, "frame_corner": 0.06, "desk": 3.0,
                "folder": 0.35, "pen": 0.15}
