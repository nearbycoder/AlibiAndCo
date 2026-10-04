"""Wendell Fry's press photographs from the Harvest Ball (case 3), rendered as small scenes.

    flicker  the ballroom mid-waltz as the chandeliers dip
    terrace  the Grand's terrace in the rain; far behind, a figure in a yellow oilskin on the cliff path
    toast    Capt. Rook toasts the Mayor beneath the big chandelier
    raffle   Capt. Rook draws the raffle, hair oddly damp

People are fused ellipsoids (portraits.metaball), posed limb by limb; Rook and the Mayor wear the
same busts as the suspect portraits. Every frame is lit by a press flash beside the lens, so the
subjects pop and the room falls away into the dark, the way a 1986 flash photo looks.
"""
import math
import os
import random

import bpy
from mathutils import Vector

import lib
import portraits
from lib import material, box, cylinder, sphere, torus, link
from portraits import E, metaball

rnd = random.Random(11)


def emissive(name, hexcol, strength):
    m = material(name, hexcol, rough=0.4)
    b = m.node_tree.nodes.get("Principled BSDF")
    r, g, bb = (lib.srgb_to_linear(v) for v in lib.hex_rgb(hexcol))
    b.inputs["Emission Color"].default_value = (r, g, bb, 1)
    b.inputs["Emission Strength"].default_value = strength
    return m


def limb(a, b, r, squash=1.0):
    """An ellipsoid spanning a -> b with radius r (E dict)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler()
    return E(tuple((a + b) / 2), r, (1.0, squash, d.length / 2 / r + 0.35), rot=tuple(math.degrees(x) for x in rot))


def person(loc, coat="1B1B1E", skin="E2B596", height=1.75, yaw=0.0, dress=False, pose="down", head=True,
           hood=False, hair="3A2A20", trousers="141416", under_bust=False):
    """A standing figure. Poses: down, waltz (arms out to a partner), raise (right hand up with a glass),
    hold (both hands at the waist), draw (right hand reaching forward into the drum)."""
    s = height / 1.75
    S = lambda *v: tuple(x * s for x in v)
    sh = {-1: Vector(S(-0.19, 0, 1.43)), 1: Vector(S(0.19, 0, 1.43))}
    hands = {}
    poses = {
        "down": {-1: (S(-0.23, 0.0, 1.13), S(-0.24, -0.02, 0.86)), 1: (S(0.23, 0.0, 1.13), S(0.24, -0.02, 0.86))},
        "waltz": {-1: (S(-0.3, -0.12, 1.25), S(-0.18, -0.34, 1.38)), 1: (S(0.24, -0.15, 1.18), S(0.06, -0.32, 1.2))},
        "raise": {-1: (S(-0.23, 0.0, 1.13), S(-0.22, -0.06, 0.88)), 1: (S(0.27, -0.16, 1.2), S(0.2, -0.3, 1.52))},
        "hold": {-1: (S(-0.25, -0.06, 1.15), S(-0.08, -0.24, 1.08)), 1: (S(0.25, -0.06, 1.15), S(0.08, -0.24, 1.08))},
        "draw": {-1: (S(-0.23, 0.0, 1.13), S(-0.24, -0.02, 0.88)), 1: (S(0.28, -0.18, 1.22), S(0.22, -0.42, 1.18))},
    }[pose]
    cloth = portraits.fabric_mat(coat)
    if under_bust:
        # A portrait bust supplies chest, shoulders and upper arms; add the waist, hips and forearms.
        els = [E(S(0, 0.01, 1.08), 0.15 * s, (1.0, 0.66, 0.95)), E(S(0, 0.0, 0.95), 0.145 * s, (1.0, 0.7, 0.7))]
        bust_elbow = {-1: Vector(S(-0.22, -0.02, 1.16)), 1: Vector(S(0.22, -0.02, 1.16))}
        for sx in (-1, 1):
            _, hand = poses[sx]
            els.append(limb(bust_elbow[sx], hand, 0.045 * s))
            hands[sx] = Vector(hand)
    else:
        els = [E(S(0, 0.0, 1.2), 0.17 * s, (1.08, 0.62, 1.55)),          # torso
               E(S(0, 0.0, 0.98), 0.15 * s, (1.05, 0.72, 0.75)),         # hips
               E(S(0, 0.0, 1.45), 0.07 * s, (2.6, 0.9, 0.6))]            # shoulder line
        for sx in (-1, 1):
            elbow, hand = poses[sx]
            els.append(limb(sh[sx], elbow, 0.05 * s))
            els.append(limb(elbow, hand, 0.042 * s))
            hands[sx] = Vector(hand)
    if dress:
        els.append(E(S(0, 0.0, 0.5), 0.3 * s, (1.0, 0.82, 1.7)))
        els.append(E(S(0, 0.0, 0.05), 0.34 * s, (1.0, 0.85, 0.2)))
    body = metaball("body", els, cloth, res=0.012 * s, smooth_iters=6)
    parts = [body]
    if not dress:
        legs = []
        for sx in (-1, 1):
            legs.append(limb(S(sx * 0.09, 0, 0.95), S(sx * 0.1, 0, 0.06), 0.068 * s))
            legs.append(E(S(sx * 0.1, -0.06, 0.04), 0.05 * s, (0.9, 2.0, 0.7)))     # shoe
        parts.append(metaball("legs", legs, portraits.fabric_mat(trousers), res=0.012 * s, smooth_iters=6))
    sm = portraits.skin_mat(dict(skin=skin))
    for sx in (-1, 1):
        parts.append(sphere("hand", 0.04 * s, hands[sx], sm, seg=12, rings=8, scale=(0.8, 0.9, 1.1)))
    if head:
        hd = metaball("head", [E(S(0, 0, 1.63), 0.105 * s, (0.86, 0.95, 1.1)),
                               E(S(0, -0.075, 1.6), 0.02 * s),                                   # nose
                               E(S(0, 0.0, 1.5), 0.05 * s, (1, 1, 1.4))], sm, res=0.008 * s, smooth_iters=4)
        parts.append(hd)
        if hair and not hood:
            parts.append(metaball("hair", [E(S(0, 0.012, 1.67), 0.11 * s, (0.9, 1.0, 0.85)),
                                           E(S(0, -0.12, 1.6), 0.1 * s, (1.1, 0.9, 0.85), neg=True)],
                                  portraits.hair_mat(hair), res=0.008 * s, smooth_iters=4))
    if hood:
        parts.append(metaball("hood", [E(S(0, 0.01, 1.66), 0.135 * s, (0.92, 1.0, 1.05)),
                                       E(S(0, -0.14, 1.6), 0.1 * s, (0.8, 0.9, 0.9), neg=True)], cloth, res=0.01 * s, smooth_iters=4))
    root = lib.parent_all("figure", parts)
    root.location = loc
    root.rotation_euler = (0, 0, yaw)
    root["hand_r"] = list(hands[1])
    root["hand_l"] = list(hands[-1])
    return root


def hand_world(fig, side=1):
    bpy.context.view_layer.update()
    h = Vector(fig["hand_r" if side == 1 else "hand_l"])
    return fig.matrix_world @ h


def flute(at):
    # Eevee renders thin transmissive glass nearly black, so the flute is a glossy pale champagne solid.
    glass = material("glass_flute", "EEF0EC", rough=0.05)
    fizz = material("col_E9D08A", "E9CF7E", rough=0.1)
    at = Vector(at)
    return [cylinder("stem", 0.004, 0.09, at + Vector((0, 0, -0.06)), glass, seg=8),
            cylinder("bowl", 0.02, 0.15, at + Vector((0, 0, 0.03)), glass, seg=16, r2=0.03),
            cylinder("wine", 0.018, 0.1, at + Vector((0, 0, 0.035)), fizz, seg=16, r2=0.026)]


def chandelier(loc, mat_glow, scale=1.0):
    brass = material("brass", "C9A24A", metallic=0.9, rough=0.3)
    crystal = material("glass_E8EEF0", "E8EEF0", rough=0.05)
    parts = [torus("ring", 0.45 * scale, 0.02 * scale, (0, 0, 0), brass), cylinder("stem", 0.02 * scale, 0.8 * scale, (0, 0, 0), brass),
             torus("ring2", 0.28 * scale, 0.015 * scale, (0, 0, 0.22 * scale), brass)]
    for i in range(10):
        a = i / 10 * math.pi * 2
        parts.append(sphere("bulb", 0.05 * scale, (math.cos(a) * 0.45 * scale, math.sin(a) * 0.45 * scale, 0.06 * scale), mat_glow))
    for i in range(36):
        a = i / 36 * math.pi * 2
        drop = 0.1 + 0.06 * (i % 3)
        parts.append(sphere("crystal", 0.018 * scale, (math.cos(a) * 0.36 * scale, math.sin(a) * 0.36 * scale, -drop * scale), crystal,
                            seg=8, rings=6, scale=(1, 1, 1.6)))
    r = lib.parent_all("chandelier", parts)
    r.location = loc
    return r


def camera(loc, look, lens=35):
    cd = bpy.data.cameras.new("cam")
    cd.lens = lens
    cam = bpy.data.objects.new("cam", cd)
    cam.location = loc
    cam.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    link(cam)
    bpy.context.scene.camera = cam
    return cam


def light(kind, loc, energy, color=(1, 0.9, 0.75), size=1.0, look=(0, 0, 1)):
    L = bpy.data.lights.new("l", kind)
    L.energy = energy
    L.color = color
    if kind == "AREA":
        L.size = size
    if kind == "SPOT":
        L.spot_size = math.radians(60)
    if kind == "POINT":
        L.shadow_soft_size = 0.05
    o = bpy.data.objects.new("l", L)
    o.location = loc
    o.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    link(o)
    return o


def flash(cam_loc, energy=900):
    """The press flash: a hard point light just above and beside the lens."""
    p = Vector(cam_loc) + Vector((0.15, 0.05, 0.25))
    return light("POINT", p, energy, (1.0, 0.97, 0.92))


def setup(res=640):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.eevee.taa_render_samples = 64
    try:
        sc.eevee.use_raytracing = True
    except AttributeError:
        pass
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Punchy"
    except TypeError:
        pass
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.012, 0.012, 0.016, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.5


def ballroom(dim=1.0):
    box("floor", (14, 14, 0.05), (0, 0, -0.025), material("wood_floor", "6E4A2C", rough=0.2))
    box("wall", (14, 0.2, 6), (0, 5, 3), material("col_6B2E2A", "6B2E2A", rough=0.8))
    for x in (-4, -1.5, 1, 3.5):
        box("pilaster", (0.5, 0.3, 6), (x, 4.85, 3), material("col_C9B48A", "C9B48A", rough=0.6))
        box("capital", (0.7, 0.4, 0.25), (x, 4.8, 4.6), material("col_D8C69C", "D8C69C", rough=0.5))
    for x in (-2.75, -0.25, 2.25):   # tall windows with drapes between the pilasters
        box("window", (1.2, 0.05, 2.6), (x, 4.88, 2.4), emissive("glow_window_%d" % int(dim * 10), "2A3A55", 0.6 * dim))
        for sx in (-1, 1):
            box("drape", (0.3, 0.12, 3.2), (x + sx * 0.75, 4.8, 2.5), material("fabric_7A1E22", "7A1E22", rough=0.9))
    glow = emissive("glow_bulb_%d" % int(dim * 10), "FFD9A0", 18 * dim)
    chandelier((0, 1.5, 3.6), glow)
    chandelier((-3.5, 2.5, 3.8), glow, 0.8)
    chandelier((3.5, 2.5, 3.8), glow, 0.8)
    light("AREA", (0, 1.8, 3.3), 260 * dim, (1.0, 0.78, 0.5), 3.0, (0, 1.8, 0))
    return glow


COATS = ["141418", "6E2F4A", "2F4A6E", "7A6A3A", "3E5E4A", "5A2A2A"]
HAIRS = ["2A1E16", "5A3A22", "8A6A3A", "1A1412", "B8B0A4"]
SKINS = ["E2B596", "D9A88A", "EBC4A8", "A8714E", "6B4128"]


def couple(x, y, yaw):
    """Two dancers in hold, facing each other."""
    person((x, y, 0), "141418", rnd.choice(SKINS), 1.78, yaw, pose="waltz", hair=rnd.choice(HAIRS))
    off = Vector((math.sin(yaw), -math.cos(yaw), 0)) * 0.42
    person((x + off.x, y + off.y, 0), rnd.choice(COATS[1:]), rnd.choice(SKINS), 1.62, yaw + math.pi, dress=True, pose="waltz",
           hair=rnd.choice(HAIRS))


def scene_flicker():
    setup()
    ballroom(0.3)
    for (x, y) in [(-1.5, 1.0), (0.5, 0.6), (2.1, 1.7), (-0.4, 2.6), (1.3, 3.1), (-2.6, 2.2)]:
        couple(x, y, rnd.uniform(0, math.tau))
    cam = (0, -4.2, 1.7)
    camera(cam, (0, 2, 1.4), 30)
    flash(cam, 700)


def scene_terrace():
    setup()
    stone = material("col_9A958C", "9A958C", rough=0.35)
    box("terrace", (8, 5, 0.1), (0, 0, -0.05), material("wet_stone", "4E4A45", rough=0.08))
    box("rail", (8, 0.25, 0.12), (0, 2.2, 1.0), stone)
    box("plinth", (8, 0.3, 0.12), (0, 2.2, 0.06), stone)
    for i in range(16):
        x = -3.75 + i * 0.5
        lib.lathe("baluster", [(0.0, 0.1), (0.06, 0.1), (0.045, 0.3), (0.08, 0.55), (0.04, 0.85), (0.07, 0.94), (0.0, 0.94)], stone,
                  seg=16, loc=(x, 2.2, 0.0))
    box("sea", (200, 200, 0.1), (0, 100, -6), material("col_10181C", "10181C", rough=0.15))
    box("cliff", (40, 6, 6), (10, 12.3, -1.9), material("col_2E3428", "2E3428", rough=0.9))
    box("path", (40, 0.9, 0.1), (10, 10.0, 1.1), material("col_8A8572", "8A8572", rough=0.9))
    for i in range(7):
        cylinder("post", 0.06, 0.9, (-6 + i * 4, 9.4, 1.1), material("col_3A3328", "3A3328"), seg=6)
    cylinder("lighthouse", 0.8, 9, (24, 60, -3), material("col_D8D3C8", "D8D3C8"), seg=16, r2=0.55)
    sphere("lamp", 0.6, (24, 60, 6.3), emissive("glow_lh", "FFE8B0", 6))
    # The figure: yellow oilskin, hood up, walking east along the cliff path, caught by a path lamp.
    person((3.1, 10.0, 1.15), "E3B62A", height=1.78, yaw=math.radians(-80), hood=True, trousers="1E2228")
    cylinder("lampPost", 0.05, 2.6, (4.3, 9.5, 1.15), material("col_1E1E1E", "1E1E1E"), seg=8)
    sphere("lampGlass", 0.12, (4.3, 9.5, 3.8), emissive("glow_path", "FFD08A", 10))
    light("POINT", (3.9, 9.2, 3.4), 320, (1.0, 0.85, 0.55))
    light("SPOT", (2.0, 4.0, 6.0), 400, (0.7, 0.8, 1.0), look=(3.0, 10.0, 1.2))   # moonlight picking out the headland
    rain = emissive("glow_rain", "C8D4DA", 0.9)
    for i in range(220):
        x, y, z = rnd.uniform(-3, 3), rnd.uniform(0.6, 4), rnd.uniform(0.0, 2.6)
        st = cylinder("rain", 0.0025, rnd.uniform(0.12, 0.3), (x, y, z), rain, seg=4)
        st.rotation_euler = (math.radians(rnd.uniform(-8, -4)), math.radians(rnd.uniform(3, 9)), 0)
    cylinder("pot", 0.3, 0.5, (-2.7, 1.2, 0), material("col_7A4A32", "7A4A32"), seg=16, r2=0.38)
    sphere("bush", 0.5, (-2.7, 1.2, 0.85), material("col_2F3E2A", "2F3E2A"))
    cylinder("table", 0.45, 0.04, (2.4, 0.6, 0.72), material("col_EDE6D6", "EDE6D6"), seg=24)
    cylinder("tleg", 0.04, 0.72, (2.4, 0.6, 0), stone, seg=8)
    cam = (0, -2.5, 1.65)
    camera(cam, (1.4, 10, 1.6), 42)
    flash(cam, 320)
    light("SUN", (0, 0, 10), 0.6, (0.55, 0.65, 0.85), look=(0.3, 1, -0.6))


def scene_toast(draw_raffle=False):
    setup()
    ballroom(1.0)
    # Capt. Rook (his portrait bust on a body), raising a glass or reaching into the raffle drum.
    rook_body = person((-0.32, 0.65, 0), "141416", height=1.72, yaw=math.radians(20), head=False,
                       pose="draw" if draw_raffle else "raise", under_bust=True)
    rook = portraits.build_character("rook")
    rook.location = (-0.32, 0.65, 1.58)
    rook.scale = (1.12, 1.12, 1.12)
    rook.rotation_euler = (0, 0, math.radians(20))
    # The Mayor: a generic bust with a chain of office.
    portraits.CHARACTERS["mayor"] = dict(skin="E7B9A0", hair="bald_sides", hair_col="7A726A", mustache="pencil", clothes="suit",
                                         cloth_col="1E1E24", shirt_col="F1EFE9", tie="bow", tie_col="141414", brow=2, mouth=0.3,
                                         age=0.8, face_w=1.12, jaw=1.1, eye_col="4A3A2A", lid=0.5, nose=1.2, cheeks=1.4)
    mayor_body = person((0.5, 0.8, 0), "1E1E24", height=1.7, yaw=math.radians(-25), head=False, pose="hold", under_bust=True)
    mayor = portraits.build_character("mayor")
    mayor.location = (0.5, 0.8, 1.56)
    mayor.scale = (1.12, 1.12, 1.12)
    mayor.rotation_euler = (0, 0, math.radians(-25))
    gold = material("brass", "C9A24A", metallic=0.9, rough=0.3)
    yaw = math.radians(-25)
    fwd = Vector((math.sin(yaw), -math.cos(yaw), 0))
    # Chain of office: a loop lying round the neck on the shoulders, dipping at the front.
    chain = torus("chain", 0.17, 0.011, (0, 0, 0), gold, seg=64)
    chain.scale = (1.2, 1.0, 1.0)
    chain.rotation_euler = (math.radians(40), 0, yaw)
    chain.location = Vector((0.5, 0.8, 1.335)) + fwd * 0.05
    medal = cylinder("medal", 0.036, 0.008, (0, 0, 0), gold, seg=24)
    medal.rotation_euler = (math.radians(70), 0, yaw)
    medal.location = Vector((0.5, 0.8, 1.22)) + fwd * 0.17
    if not draw_raffle:
        flute(hand_world(rook_body, 1) + Vector((0, 0, 0.06)))
        flute(hand_world(mayor_body, 1) + Vector((0, 0, 0.06)))
    else:
        drum = cylinder("drum", 0.35, 0.6, (0, 0, -0.3), gold, seg=32)   # centred so it turns on its rims
        drum.rotation_euler = (0, math.radians(90), 0)
        drum.location = (-0.3, 0.0, 1.0)
        for sx in (-1, 1):
            torus("drumRim", 0.36, 0.015, (-0.3 + sx * 0.3, 0.0, 1.0), gold, rot=(0, math.radians(90), 0))
        box("stand", (0.8, 0.4, 0.75), (-0.3, 0.0, 0.37), material("wood_4A2E1A", "4A2E1A", rough=0.4))
        hw = hand_world(rook_body, 1)
        box("ticket", (0.06, 0.004, 0.04), hw + Vector((0.0, -0.03, 0.02)), material("col_E05A4A", "E05A4A"))
        for i in range(6):
            person((rnd.uniform(-2.5, 2.5), rnd.uniform(2.0, 3.5), 0), rnd.choice(COATS), rnd.choice(SKINS), rnd.uniform(1.58, 1.82),
                   yaw=rnd.uniform(-0.6, 0.6), dress=rnd.random() < 0.5, hair=rnd.choice(HAIRS))
    cam = (0.1, -1.9, 1.62)
    camera(cam, (0.08, 0.7, 1.42), 38)
    flash(cam, 520)


SCENES = {
    "flicker": scene_flicker,
    "terrace": scene_terrace,
    "toast": lambda: scene_toast(False),
    "raffle": lambda: scene_toast(True),
}


def build_all(only, preview):
    for pid, fn in SCENES.items():
        if only and pid not in only:
            continue
        lib.reset()
        fn()
        out = os.path.join(lib.RENDERS, f"photo_{pid}.png")
        lib.render_preview(out)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, "ArtSource", f"photo_{pid}.blend"))
        print("[photos] rendered", pid)
