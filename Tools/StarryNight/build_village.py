"""Bake the shared metre-scale village mesh for Unity and the offline viewer."""
import json
import math
import random
import struct
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/StarryNight"
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(1889)
vertices = bytearray()
solids = []
sin, cos, exp = math.sin, math.cos, math.exp


def colour(hex_colour, shade):
    return bytes(max(0, min(255, round(int(hex_colour[i:i+2], 16) * shade))) for i in (0, 2, 4))


def face(points, palette, axis, shade=1, uv=None):
    """Every triangle gets an orthonormal tangent frame measured in world metres."""
    if len(points)>3:
        for j in range(1,len(points)-1):
            ids=(0,j,j+1)
            face([points[k] for k in ids],palette,axis,shade,
                 None if uv is None else [uv[k] for k in ids])
        return
    points = np.array(points, dtype=float)
    normal = np.cross(points[1]-points[0], points[2]-points[0])
    normal /= np.linalg.norm(normal)
    tangent = np.array(axis, dtype=float)
    tangent -= normal * np.dot(tangent, normal)
    tangent /= np.linalg.norm(tangent)
    across = np.cross(normal, tangent)
    if uv is None:
        uv = list(zip(points @ tangent, points @ across))
    else:
        # Retain the road/contour flow but restore unit lengths on sloping land.
        chart=np.array(uv,dtype=float)
        delta=chart[1:]-chart[0]
        frame=np.linalg.solve(delta,points[1:]-points[0])
        tangent=frame[0]/np.linalg.norm(frame[0])
        across=frame[1]-tangent*np.dot(frame[1],tangent)
        across/=np.linalg.norm(across)
        offset=points-points[0]
        uv=chart[0]+np.column_stack((offset@tangent,offset@across))
    a, b = (colour(c, shade) for c in palette)
    family = 1 if palette == HILL else 2 if palette == GRASS else 3 if palette == ROAD else 4 if palette in (FOLIAGE,FAR_LEAF) else 0
    for j in range(1, len(points)-1):
        for k in (0, j, j+1):
            vertices.extend(struct.pack("<5f", *points[k], *uv[k]) + a + bytes([family]) + b + b'\xff')


def bend(z):
    return 2.6*sin(z*.063) + .9*sin(z*.141)


def ground(x, z):
    r = math.hypot(x*.94, z)
    angle = math.atan2(x, z)
    ridge = 83 + 5*sin(3*angle) + 7*cos(5*angle)
    hills = (13 + 6*sin(angle*7+.45) + 3*cos(angle*13))*exp(-((r-ridge)/13)**2)
    overlook = 10.2*exp(-((x+1)/21)**2 - ((z+42)/15)**2)
    return hills + overlook + .47*sin(x*.083) + .38*cos(z*.098) + .31*sin((x+z)*.13)


GRASS = ("1e3642","9eab58")
HILL = ("182653","6989d1")
ROAD = ("37465d","c5b988")
WALLS = [("42536b","c5c6b4"),("2a3c62","a3b1b6"),("475e69","bac6ae"),
         ("626261","cfb788"),("344b70","92a5c6")]
ROOFS = [("172b4d","7084a8"),("493a38","ba7752"),("203754","8995a4"),("272e43","77839b")]
DARK = ("28323c","667783")
WOOD = ("4c493e","9a805a")
WINDOW = ("de9e39","ffdf7a")
FOLIAGE = ("101f26","677144")
FAR_LEAF = ("243b40","849979")


def land():
    # Include both edges of every lane as grid lines, so the road follows the terrain.
    lanes = [-28, -14, 0, 14, 28]
    crossings = [-26, -13, 0, 13, 26, 39]
    xs = sorted(set(range(-112,113,4)) | {n+d for n in lanes for d in (-2,2)})
    zs = sorted(set(range(-112,113,4)) | {n+d for n in crossings for d in (-2,2)})
    for z0,z1 in zip(zs,zs[1:]):
        for x0,x1 in zip(xs,xs[1:]):
            x,z = (x0+x1)/2,(z0+z1)/2
            lane = any(abs(x-n)<2 for n in lanes) and -28<=z<=41
            lane |= abs(x)<2 and -52<=z<-26
            cross = any(abs(z-n)<2 for n in crossings) and abs(x)<34
            points = [(u+bend(v),ground(u+bend(v),v),v) for u,v in
                      [(x0,z0),(x0,z1),(x1,z1),(x1,z0)]]
            far = math.hypot(x,z)>59
            palette = ROAD if lane or cross else HILL if far else GRASS
            tone = .95+.09*sin(x*.12+z*.08)+.05*cos(z*.3)
            # Project the street in running metres around its bends. Hill strokes follow contours.
            if lane:
                coords = [(p[2],p[0]-bend(p[2])) for p in points]
            elif cross:
                coords = [(p[0],-p[2]) for p in points]
            else:
                coords = [(p[0],p[2]+1.3*sin(p[0]*.12)+.48*p[1]) for p in points]
            for ids in ((0,1,2),(0,2,3)):
                face([points[k] for k in ids],palette,(1,0,0),tone,[coords[k] for k in ids])


def house(x,z,w,d,h,angle,church=False,tower=False):
    # Local basis, with the front facade facing local -Z.
    ca,sa = cos(angle), sin(angle)
    base = ground(x,z)-.04
    def p(u,v,k):
        return (x+ca*u+sa*k,base+v,z-sa*u+ca*k)
    along = (ca,0,-sa)
    depth = (sa,0,ca)
    wall = WALLS[0] if church else rng.choice(WALLS)
    roof = ROOFS[0] if church else rng.choice(ROOFS)
    left,right,front,back = -w/2,w/2,-d/2,d/2
    solids.append([x,z,w/2,d/2,angle])
    def panel(corners, palette=wall, direction=along, tone=1):
        face([p(*q) for q in corners],palette,direction,tone)
    for side,k in [(1,front),(-1,back)]:
        panel([(left,-.85,k),(right,-.85,k),(right,h,k),(left,h,k)],tone=1 if side==1 else .78)
        def mark(a,b,low,high,offset,ink):
            f = k-side*offset
            panel([(a,low,f),(b,low,f),(b,high,f),(a,high,f)],ink,(0,1,0))
        bays = max(2,round(w/1.65))
        for i in range(bays):
            u = left+(i+.5)*w/bays
            if i==bays//2 and side==1 and not tower:
                door_x,_,door_z = p(u,0,k)
                threshold = ground(door_x,door_z)-base
                mark(u-.50,u+.50,threshold-.10,threshold+2.05,.023,WOOD)
            else:
                mid = h*.57 if not tower else h-.85
                mark(u-.42,u+.42,mid-.53,mid+.55,.02,DARK)
                paint = WINDOW if rng.random()<.67 or church else ("42546c","b0b4a1")
                for a,b in [(-.32,-.035),(.035,.32)]:
                    mark(u+a,u+b,mid-.41,mid+.43,.034,paint)
    for side,u in [(-1,left),(1,right)]:
        panel([(u,-.85,front),(u,-.85,back),(u,h,back),(u,h,front)],wall,depth,.76 if side==-1 else .88)
        for k in [-d*.23,d*.23]:
            surface = u+side*.025
            panel([(surface,1.05,k-.35),(surface,1.05,k+.35),(surface,1.91,k+.35),(surface,1.91,k-.35)],DARK,depth)
            panel([(surface+side*.014,1.14,k-.26),(surface+side*.014,1.14,k+.26),
                   (surface+side*.014,1.82,k+.26),(surface+side*.014,1.82,k-.26)],WINDOW if rng.random()<.46 else WOOD,depth)
    tip = h+(7.3 if tower else d*(.45 if church else .35))
    if tower:
        corners = [(left,h,front),(right,h,front),(right,h,back),(left,h,back)]
        for i in range(4):
            panel([corners[i],corners[(i+1)%4],(.05,tip,0)],("465370","a0afb1"),(0,1,0),.86+.06*i)
        return
    for u in (left,right):
        panel([(u,h,front),(u,h,back),(u,tip,0)],wall,depth,.88)
    for k,shade in [(front-.23,.93),(back+.23,.76)]:
        panel([(left-.23,h-.12,k),(right+.23,h-.12,k),(right+.23,tip,0),(left-.23,tip,0)],roof,depth,shade)
        panel([(left-.23,h-.12,k),(right+.23,h-.12,k),(right+.23,h-.23,k),(left-.23,h-.23,k)],DARK,along,.73)


def tree(x,z,height,width,cypress=False):
    base = ground(x,z)-.23
    solids.append([x,z,width*.29,width*.29,0])
    sides=9 if cypress else 8
    phase=rng.random()*6
    def crown(cx,cz,start,h,r,lean):
        rings=[]
        for y,ratio in [(0,.26),(.15,.81),(.39,1),(.66,.70),(.84,.39),(.97,.09)]:
            ring=[]
            for i in range(sides):
                a=2*math.pi*i/sides+phase
                radius=r*ratio*(1+.12*sin(3*a+5*y))
                ring.append((cx+radius*cos(a)+lean*y+.12*r*sin(y*8),
                             base+start+y*h,cz+radius*sin(a)+.13*r*sin(4*y+phase)))
            rings.append(ring)
        palette=FOLIAGE if cypress else FAR_LEAF
        for j,(a,b) in enumerate(zip(rings,rings[1:])):
            for i in range(sides):
                k=(i+1)%sides
                tangent=np.subtract(b[i],a[i]) if cypress else np.subtract(a[k],a[i])
                face([a[i],a[k],b[k],b[i]],palette,tangent,.8+.17*cos(phase+i*6.28/sides-.6)+.06*j)
        tip=(cx+lean,base+start+h,cz)
        for i in range(sides):
            face([rings[-1][i],rings[-1][(i+1)%sides],tip],palette,(0,1,0))
    crown(x,z,0,height,width, width*.43)
    if cypress:
        for i in range(5):
            a=phase+2.40*i
            crown(x+width*.76*cos(a),z+width*.76*sin(a),0,height*(.42+.10*i),width*.43,width*.42*cos(a))


def bake():
    land()
    terrain_count=len(vertices)//28
    for row,z in enumerate([-20,-6.5,6.5,19.5,32.5]):
        for col,u in enumerate([-35,-21,-7,7,21,35]):
            if (row,col) in [(2,3),(2,2),(0,0),(4,5),(1,5),(3,0)]: continue
            zz=z+rng.uniform(-.7,.7); x=u+bend(zz)+rng.uniform(-.6,.6)
            house(x,zz,rng.uniform(4.0,6.0),rng.uniform(3.44,5.04),rng.uniform(2.52,3.36),
                  rng.uniform(-.15,.15)+(math.pi/2 if (row+col)%3==0 else 0))
    house(8.0,7.1,7.6,6.5,4.7,.04,church=True)
    house(4.7,3.8,2.7,2.7,6.8,.04,church=True,tower=True)
    tree(-7.7,-27.9,15.4,1.68,True)
    for x,z in [(-10,-27),(-33,-9),(28,17),(39,27),(-30,29),(16,47),(-13,37),(33,-22)]:
        tree(x,z,rng.uniform(6.5,10),rng.uniform(1.2,1.7),True)
    # Low orchard treetops break up plot edges and frame openings between lanes.
    for u,z in [(-11,-18),(10,-21),(-4,-11),(17,-8),(-24,5),(-11,11),
                (17,16),(-18,24),(31,29),(-25,35),(4,34),(22,-19)]:
        for j in range(3):
            zz=z+j*1.25
            tree(u+bend(zz)+rng.uniform(-.4,.4),zz,rng.uniform(1.45,2.5),rng.uniform(.77,1.1))
    for i in range(145):
        a=rng.uniform(0,2*math.pi); r=rng.uniform(44,71)
        x,z=r*cos(a),r*sin(a)
        if z<-15 and abs(x-bend(z))<6: continue
        tree(x,z,rng.uniform(2.4,5),rng.uniform(1.1,2.5))
    (OUT/'Village.bytes').write_bytes(vertices)
    info={'stride':28,'terrainVertices':terrain_count,'vertices':len(vertices)//28,
          'spawn':[bend(-40),-40],'eye':1.62,'radius':.31,'limit':101,
          'solids':[{'x':x,'z':z,'halfX':w,'halfZ':d,'angle':a} for x,z,w,d,a in solids]}
    (OUT/'Layout.json').write_text(json.dumps(info,separators=(',',':')),encoding='utf8')
    print(f"{info['vertices']//3:,} triangles, {len(solids)} collision footprints")


if __name__ == '__main__':
    bake()

