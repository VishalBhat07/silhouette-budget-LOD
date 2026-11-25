#!/usr/bin/env python3
"""Generate the dock's four-level OBJ ladders and isolated binary silhouette tables.

Run with Python 3 and Pillow from any directory. Geometry uses metres and the
same pivot at every detail level. Masks use an orthographic camera and one
fixed frame around the finest mesh for every direction and detail level.
"""
import hashlib
import json
import math
import re
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[1] / "SilhouetteWorkbench" / "Assets" / "Workbench" / "Parts"
RESOLUTION = 256


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def unit(v):
    length = math.sqrt(dot(v, v))
    return tuple(x / length for x in v)


class Mesh:
    def __init__(self):
        self.vertices = []
        self.faces = []

    def loops(self, rings, closed=False):
        """Join polygon rings; ring normals point in the sweep direction."""
        start = len(self.vertices)
        width = len(rings[0])
        for ring in rings:
            self.vertices.extend(ring)
        for i in range(len(rings) if closed else len(rings)-1):
            lower = start + i * width
            upper = start + (i+1) % len(rings) * width
            for j in range(width):
                k = (j+1) % width
                self.faces.extend(((lower+j, lower+k, upper+k), (lower+j, upper+k, upper+j)))
        if not closed:
            end = start + (len(rings)-1) * width
            for j in range(1, width-1):
                self.faces.extend(((start, start+j+1, start+j), (end, end+j, end+j+1)))

    def box(self, pos, size):
        x, y, z = pos
        w, h, d = (s/2 for s in size)
        self.loops([[(x-w,y-h,k), (x+w,y-h,k), (x+w,y+h,k), (x-w,y+h,k)] for k in (z-d,z+d)])

    def tube(self, a, b, radius, sides):
        axis = unit(tuple(y-x for x, y in zip(a, b)))
        u = unit(cross(axis, (0,1,0) if abs(axis[1]) < .9 else (1,0,0)))
        v = cross(axis, u)
        rim = [tuple(radius*(math.cos(i*math.tau/sides)*x + math.sin(i*math.tau/sides)*y)
                     for x, y in zip(u,v)) for i in range(sides)]
        self.loops([[tuple(x+y for x,y in zip(p,offset)) for offset in rim] for p in (a,b)])

    def annulus(self, profile, sides):
        # Open-centre surface of revolution, looking along +z.
        self.loops([[(r*math.cos(i*math.tau/sides), r*math.sin(i*math.tau/sides), z)
                     for i in range(sides)] for r,z in profile], closed=True)


def crane(level):
    m = Mesh()
    sides = (4, 5, 6, 8)[level]
    m.box((0,.23,0), (2.1,.46,2.1))
    for x in (-.72,.72):
        for z in (-.65,.65):
            m.tube((x,.45,z),(x,7.7,z),.13,sides)
    bays = (2,3,5,7)[level]
    for i in range(bays):
        low, high = .5+7.15*i/bays, .5+7.15*(i+1)/bays
        sign = 1 if i % 2 else -1
        for x in (-.72,.72):
            m.tube((x,low,sign*.65),(x,high,-sign*.65),.072,sides)
        for z in (-.65,.65):
            m.tube((sign*.72,low,z),(-sign*.72,high,z),.072,sides)
        if level >= 2:
            m.box((0,high,0),(1.55,.105,1.42))
    m.box((0,7.45,0),(2.05,.49,2.1))
    m.box((0,8.02,-1.36),(1.48,1.04,1.55))
    # The overhead truss has a triangular section and a cantilevered hoist.
    for x in (-.59,.59):
        m.tube((x,7.75,-2.25),(x,7.75,6.5),.105,sides)
    m.tube((0,8.8,-2.25),(0,8.8,6.5),.125,sides)
    jib_bays = (2,4,7,10)[level]
    for i in range(jib_bays+1):
        z = -2.25 + 8.75*i/jib_bays
        for x in (-.59,.59):
            m.tube((x,7.75,z),(0,8.8,z),.069,sides)
            if i < jib_bays and level:
                m.tube((x,7.75,z),(0,8.8,z+8.75/jib_bays),.055,sides)
    m.box((0,7.62,5.75),(1.32,.38,.84))
    for x in (-.22,.22):
        m.tube((x,7.5,5.75),(x,5.35,5.75),.039,sides)
    m.box((0,5.28,5.75),(.69,.32,.46))
    if level:
        for x in (-.28,.28):
            m.tube((x,5.25,5.75),(x*1.7,4.85,5.75),.075,sides)
            m.tube((x*1.7,4.85,5.75),(x*.68,4.68,5.75),.075,sides)
    return m


def radiator(level):
    m = Mesh()
    sides = (4,6,8,10)[level]
    m.box((0,.17,0),(1.55,.34,1.24))
    m.tube((0,.32,0),(0,4.4,0),.21,sides)
    for x in (-2.12,2.12):
        m.tube((x,2.6,0),(x,6.6,0),.12,sides)
    for y in (2.62,6.58):
        m.box((0,y,0),(4.28,.18,.27))
    if level == 0:
        m.box((0,4.6,0),(4.0,3.82,.12))
    else:
        # Keep the pitch of surviving fins fixed so refinements fill true gaps.
        count = 15
        stride = (0,3,2,1)[level]
        for i in range(0,count,stride):
            y = 2.81 + i*.255
            for s in (-1,1):
                m.box((s*1.11,y,0),(1.96,.112,.39))
        for x in (-.17,.17):
            m.tube((x,2.64,-.16),(x,6.57,-.16),.078,sides)
        if level >= 2:
            for s in (-1,1):
                m.tube((0,1.4,0),(s*2.0,2.62,0),.095,sides)
        if level == 3:
            for s in (-1,1):
                m.tube((s*.22,2.64,.19),(s*2.05,6.52,.19),.051,sides)
    return m


def engine(level):
    m = Mesh()
    sides = (8,12,20,32)[level]
    m.annulus([(1.42,-.9),(1.46,-.71),(.91,.39),(.91,.82),(.57,.82),(.57,.39),(1.29,-.71),(1.31,-.9)],sides)
    m.tube((0,0,.42),(0,0,.86),.34,sides)
    blades = (3,5,9,13)[level]
    for i in range(blades):
        t = i*math.tau/blades
        m.tube((.25*math.cos(t),.25*math.sin(t),.48),
               (.86*math.cos(t+.39),.86*math.sin(t+.39),.3),.087,4)
    if level:
        count = (0,6,10,16)[level]
        for i in range(count):
            t = math.tau*i/count
            c,s = math.cos(t),math.sin(t)
            m.tube((1.44*c,1.44*s,-.72),(.93*c,.93*s,.38),.076,sides//2)
    if level >= 2:
        m.annulus([(.98,.43),(.98,.62),(.87,.62),(.87,.43)],sides)
    return m


def cooling_fan(level):
    mesh = Mesh()
    sides = (12, 16, 24, 40)[level]
    mesh.annulus([(1.31, -.22), (1.31, .22), (1.13, .22), (1.13, -.22)], sides)
    mesh.tube((0, 0, -.19), (0, 0, .24), .28, sides)
    for blade in range((3, 5, 7, 9)[level]):
        angle = blade * math.tau / (3, 5, 7, 9)[level]
        mesh.tube((.22*math.cos(angle), .22*math.sin(angle), -.06),
                  (1.08*math.cos(angle+.42), 1.08*math.sin(angle+.42), .06), .115, 4)
    for bar in range(-level, level+1):
        y = bar * .29
        x = math.sqrt(1.12**2-y*y)
        mesh.tube((-x, y, -.24), (x, y, -.24), .024, 4)
    mesh.box((0, -1.37, 0), (2.7, .14, .72))
    return mesh


def pipe_skid(level):
    mesh = Mesh()
    sides = (6, 8, 12, 18)[level]
    mesh.box((0, .13, 0), (3.35, .26, 1.25))
    for index in range((2, 3, 4, 5)[level]):
        z = -.5 + index * .24
        radius = .92 - index * .095
        steps = (3, 5, 8, 14)[level]
        points = [(radius*math.cos(i*math.pi/steps), 1.16 + radius*math.sin(i*math.pi/steps), z)
                  for i in range(steps+1)]
        points = [(radius, .25, z)] + points + [(-radius, .25, z)]
        for a, b in zip(points, points[1:]):
            mesh.tube(a, b, .112, sides)
        mesh.tube((radius, .49, z), (radius, .61, z), .18, sides)
    for x in (-1.48, 1.48):
        mesh.box((x, .65, 0), (.16, 1.03, .99))
    return mesh


def dish_antenna(level):
    mesh = Mesh()
    sides = (8, 12, 20, 32)[level]
    mesh.box((0, .09, 0), (1.2, .18, 1.05))
    mesh.tube((0, .12, 0), (0, 1.63, 0), .15, sides)
    center = 1.8
    for radius in (.29, .7, 1.13) if level >= 2 else (.37, 1.13):
        depth = -.23 + .45 * radius**2
        mesh.annulus([(radius, depth-.035), (radius, depth+.035),
                      (radius-.045, depth+.035), (radius-.045, depth-.035)], sides)
        # The ring profile is generated around the local Z axis.
        for i in range(len(mesh.vertices)-4*sides, len(mesh.vertices)):
            x, y, z = mesh.vertices[i]
            mesh.vertices[i] = (x, y+center, z)
    for strut in range((3, 4, 8, 12)[level]):
        angle = strut * math.tau / (3, 4, 8, 12)[level]
        mesh.tube((0, center, -.21), (1.13*math.cos(angle), center+1.13*math.sin(angle), .33), .049, 4)
    mesh.tube((0, center, -.2), (0, center, .9), .075, sides)
    return mesh


def pressure_vessel(level):
    mesh = Mesh()
    sides = (10, 14, 22, 36)[level]
    rings = [(.11,.54), (.31,.77), (.58,.87), (1.38,.87), (1.64,.77), (1.84,.54)]
    mesh.loops([[(radius*math.sin(i*math.tau/sides), y, radius*math.cos(i*math.tau/sides))
                 for i in range(sides)] for y, radius in rings])
    mesh.tube((0, 1.8, 0), (0, 2.14, 0), .22, sides)
    mesh.box((0, .08, 0), (1.64,.16,1.64))
    for band in range(level):
        y = .65 + band * .27
        mesh.tube((0, y, 0), (0, y+.045, 0), .885, sides)
    return mesh



def sweep(mesh, path, radius, sides):
    """A pipe elbow with connected rings around the local tangent."""
    axes = []
    for index in range(len(path)):
        a = path[max(0, index - 1)]
        b = path[min(len(path) - 1, index + 1)]
        axes.append(unit(tuple(y - x for x, y in zip(a, b))))
    reference = min(((1, 0, 0), (0, 1, 0), (0, 0, 1)),
                    key=lambda normal: max(abs(dot(normal, axis)) for axis in axes))
    rings = []
    for point, axis in zip(path, axes):
        u = unit(cross(reference, axis))
        v = cross(axis, u)
        ring = []
        for side in range(sides):
            angle = side * math.tau / sides
            ring.append(tuple(p + radius * (math.cos(angle) * x + math.sin(angle) * y)
                              for p, x, y in zip(point, u, v)))
        rings.append(ring)
    mesh.loops(rings)


def fasteners(mesh, radius, z, count, sides, size=.047):
    for index in range(count):
        angle = index * math.tau / count
        x, y = radius * math.cos(angle), radius * math.sin(angle)
        mesh.tube((x, y, z), (x, y, z + .09), size, sides)


def turbine_cage(level):
    """Cutaway reaction chamber with exposed cooling tubes and rotor stages."""
    mesh = Mesh()
    sides = (12, 24, 36, 64)[level]
    flanges = [-1.07, -.54, .62, 1.03]
    for z in flanges:
        mesh.annulus([(1.25, z-.08), (1.29, z-.04), (1.29, z+.04),
                      (1.25, z+.08), (1.09, z+.08), (1.09, z-.08)], sides)
        fasteners(mesh, 1.20, z-.12, (4, 8, 12, 24)[level], (4, 6, 6, 8)[level])
    mesh.annulus([(1.1, .64), (.97, 1.18), (.78, 1.85), (.61, 1.85),
                  (.65, 1.18), (.83, .64)], sides)
    mesh.tube((0, 0, -1.02), (0, 0, 1.41), .27, sides)
    for z in (-.91, -.36, .28):
        blades = (6, 12, 18, 26)[level]
        for blade in range(blades):
            angle = blade * math.tau / blades
            points = [(.31 * math.cos(angle), .31 * math.sin(angle), z),
                      (.62 * math.cos(angle + .17), .62 * math.sin(angle + .17), z + .06),
                      (.96 * math.cos(angle + .42), .96 * math.sin(angle + .42), z + .16)]
            sweep(mesh, points, .065, (4, 6, 8, 10)[level])
    conduits = (6, 10, 16, 24)[level]
    for conduit in range(conduits):
        angle = conduit * math.tau / conduits
        c, s = math.cos(angle), math.sin(angle)
        path = [(1.22*c, 1.22*s, -1.19), (1.36*c, 1.36*s, -.85),
                (1.36*c, 1.36*s, -.61), (1.47*c, 1.47*s, -.28),
                (1.47*c, 1.47*s, .33), (1.36*c, 1.36*s, .61),
                (1.36*c, 1.36*s, .88), (1.2*c, 1.2*s, 1.11)]
        sweep(mesh, path[::2] + path[-1:] if level == 0 else path, .062, (4, 6, 10, 16)[level])
    for x in (-.94, .94):
        mesh.box((x, -1.39, 0), (.43, .24, 2.4))
        mesh.box((x, -1.18, .52), (.25, .44, .30))
    return mesh


def docking_clamp(level):
    """A pair of stepped clamp jaws, hydraulic ram, and mounting plate."""
    mesh = Mesh()
    sides = (6, 10, 16, 28)[level]
    mesh.box((0, -.63, .32), (1.45, .25, 1.38))
    for sign in (-1, 1):
        points = [(sign * .34, -.51, .8), (sign * .60, -.23, .8),
                  (sign * .67, .49, .8), (sign * .58, .76, .8),
                  (sign * .31, .78, .8), (sign * .18, .58, .8)]
        sweep(mesh, points, .13, sides)
        mesh.tube((sign*.59, -.42, .43), (sign*.59, .43, .43), .14, sides)
        mesh.tube((sign*.59, .30, .43), (sign*.4, .78, .72), .078, sides)
        for tooth in range((1, 2, 3, 4)[level]):
            mesh.box((sign*(.32+.04*tooth), .62+.04*tooth, .61+.09*tooth), (.13,.10,.13))
        for z in (.06, .65):
            mesh.tube((sign*.49, -.6, z), (sign*.49, -.43, z), .078, sides)
    if level >= 1:
        mesh.annulus([(.39, -.42), (.41, -.36), (.39, -.27),
                      (.28, -.27), (.28, -.42)], sides)
    return mesh


def heat_exchanger(level):
    """Fin cassette with curved return pipes and open structural framing."""
    mesh = Mesh()
    sides = (6, 8, 12, 20)[level]
    for x in (-1.03, 1.03):
        for z in (-.46, .46):
            mesh.box((x, .59, z), (.12, 1.22, .13))
    for y in (.09, 1.16):
        for z in (-.46, .46):
            mesh.box((0, y, z), (2.21, .14, .14))
    for y in (.23, 1.01):
        mesh.tube((-1.18, y, .45), (1.18, y, .45), .088, sides)
    fins = (5, 9, 15, 24)[level]
    bend_steps = (3, 4, 6, 9)[level]
    for index in range(fins):
        x = -.93 + 1.86 * index / (fins - 1)
        path = [(x, .24, -.50)]
        path += [(x, .62 - .39 * math.cos(t * math.pi / bend_steps),
                  .47 + .32 * math.sin(t * math.pi / bend_steps))
                 for t in range(bend_steps + 1)]
        path.append((x, 1.0, -.50))
        sweep(mesh, path, .044, sides)
    if level >= 2:
        for x in (-1.025, 1.025):
            mesh.tube((x, .17, -.45), (x, 1.10, .45), .042, sides)
    return mesh


def compressor(level):
    """Horizontal compressor with motor fins, inlet and braced drive shafts."""
    mesh = Mesh()
    sides = (8, 12, 20, 32)[level]
    mesh.tube((0, 0, -.65), (0, 0, .75), .47, sides)
    mesh.tube((0, 0, .68), (0, 0, 1.06), .24, sides)
    fins = (3, 6, 10, 16)[level]
    for i in range(fins):
        z = -.54 + 1.13 * i / (fins - 1)
        mesh.annulus([(.58, z-.032), (.60, z+.006), (.58, z+.032),
                      (.47, z+.032), (.47, z-.032)], sides)
    for z in (-.73, .78):
        mesh.annulus([(.62,z-.045),(.62,z+.045),(.43,z+.045),(.43,z-.045)], sides)
        fasteners(mesh, .54, z-.09, (5, 8, 12, 18)[level], (4, 6, 6, 8)[level], .028)
    for x in (-.48, .48):
        mesh.box((x, -.62, 0), (.30, .20, 1.98))
        mesh.box((x, -.39, -.32), (.19, .40, .44))
        path = [(x*.7,.32,-.4),(x*1.25,.56,-.4),(x*1.65,.46,-.4),
                (x*1.68,.16,-.4),(x*1.68,-.35,-.4)]
        sweep(mesh, path, .081, sides)
    return mesh


def service_grille(level):
    """A bolted panel carrying individually modeled slanted louvers."""
    mesh = Mesh()
    sides = (4, 6, 8, 12)[level]
    for x in (-.94, .94):
        mesh.box((x, 0, 0), (.14, 1.35, .13))
    for y in (-.60,.60):
        mesh.box((0, y, 0), (2.02, .14, .13))
        for x in (-.92, .92):
            mesh.tube((x, y, -.07), (x, y, -.13), .03, sides)
    blades = (4, 7, 11, 17)[level]
    for blade in range(blades):
        y = -.49 + .98 * blade / (blades - 1)
        mesh.loops([[(x, y + y_offset, z) for x, z in
                     ((-.87,-.26),(.87,-.26),(.87,.26),(-.87,.26))]
                    for y_offset in (.013, -.013)])
    if level >= 1:
        for x in (-.33, .33):
            mesh.tube((x, -.54, -.15), (x, .54, .15), .024, sides)
    if level == 3:
        for i in range(7):
            x = -.68 + .23 * i
            mesh.tube((x, -.54, -.21), (x, .54, -.21), .009, 6)
    return mesh


def directions():
    samples = []
    for i in range(32):
        y = 1 - 2*(i+.5)/32
        r = math.sqrt(1-y*y)
        phi = i*math.pi*(3-math.sqrt(5))
        samples.append((r*math.cos(phi),y,r*math.sin(phi)))
    return samples + [(1,0,0),(-1,0,0),(0,0,1),(0,0,-1),(0,1,0),(0,-1,0)]


def mask(mesh, direction, center, radius):
    right = unit(cross((0,1,0) if abs(direction[1]) < .99 else (1,0,0), direction))
    up = cross(direction,right)
    scale = (RESOLUTION-4)/(2*radius)
    projected = []
    for point in mesh.vertices:
        delta = tuple(x-y for x,y in zip(point,center))
        projected.append((RESOLUTION/2 + scale*dot(right,delta), RESOLUTION/2 - scale*dot(up,delta)))
    image = Image.new("1",(RESOLUTION,RESOLUTION))
    draw = ImageDraw.Draw(image)
    for face in mesh.faces:
        draw.polygon([projected[i] for i in face],fill=1)
    return image


def vector(values):
    return dict(zip("xyz", values))


def table(name, meshes):
    bounds = [(min(a),max(a)) for a in zip(*meshes[-1].vertices)]
    center = [(lo+hi)/2 for lo,hi in bounds]
    radius = math.sqrt(sum(((hi-lo)/2)**2 for lo,hi in bounds))
    views = []
    for direction in directions():
        masks = [mask(mesh,direction,center,radius) for mesh in meshes]
        errors = []
        for candidate in masks:
            union = ImageChops.logical_or(candidate,masks[-1]).histogram()[255]
            changed = ImageChops.logical_xor(candidate,masks[-1]).histogram()[255]
            errors.append(round(changed/union,8))
        views.append(dict(direction=vector(direction),errorByLevel=errors))
    return dict(schemaVersion=1,partId=name,baker="Pillow orthographic binary union",resolution=RESOLUTION,
                referenceLevel=3,referenceCenter=vector(center),referenceRadius=radius,
                triangles=[len(m.faces) for m in meshes],views=views)


def metadata(asset, template):
    guid = hashlib.sha256(("orbital-salvage/"+asset.relative_to(ROOT).as_posix()).encode()).hexdigest()[:32]
    text = template.read_text()
    Path(str(asset)+".meta").write_text(re.sub(r"(?m)^guid: \w+", "guid: "+guid, text))


def generate():
    for name, function in (("crane",crane),("radiator",radiator),("engine_bell",engine), ("cooling_fan",cooling_fan),
                           ("pipe_skid",pipe_skid), ("dish_antenna",dish_antenna),
                           ("pressure_vessel",pressure_vessel),
                           ("turbine_cage", turbine_cage), ("docking_clamp", docking_clamp),
                           ("heat_exchanger", heat_exchanger), ("compressor", compressor),
                           ("service_grille", service_grille)):
        folder = ROOT / name
        folder.mkdir(exist_ok=True)
        metadata(folder, ROOT/"gear.meta")
        meshes = [function(level) for level in range(4)]
        for i,mesh in enumerate(meshes):
            obj = folder / f"{name}_level{i}.obj"
            with obj.open("w") as f:
                f.write(f"# Generated by Tools/generate_salvage_meshes.py\no {obj.stem}\n")
                for v in mesh.vertices:
                    f.write("v %.8f %.8f %.8f\n" % v)
                for face in mesh.faces:
                    f.write("f %d %d %d\n" % tuple(i+1 for i in face))
            metadata(obj, ROOT/"gear"/"gear_level0.obj.meta")
        output = folder / "silhouettes.json"
        baked = table(name,meshes)
        output.write_text(json.dumps(baked,indent=2)+"\n")
        metadata(output,ROOT/"gear"/"silhouettes.json.meta")
        print(name, baked["triangles"])


if __name__ == "__main__":
    generate()
