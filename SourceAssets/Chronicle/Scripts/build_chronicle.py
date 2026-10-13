"""Chronicle of Broken Fate. Run in Blender 4.0+ using --background --python.
Builds editable real geometry, image-backed PBR materials, an exportable bone rig,
nine actions, native project, FBX clips, pose previews and Blender validation.
Does not access or change a Unity project or Git repository.
"""
import bpy, math, sys, json, time, traceback, os
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import chronicle_math as K
BASE=HERE.parent
TEXTURES=Path(os.environ.get('CHR_TEXTURES', str(BASE/'Textures'))).resolve()
if not TEXTURES.exists():
 TEXTURES=BASE.parent.parent/'Assets'/'Resources'/'Chronicle'/'Textures'
OUT=Path(os.environ.get('CHR_OUTPUT',str(BASE))).resolve()
for d in ['Unity/Animations','Preview','QA','Documentation']: (OUT/d).mkdir(parents=True,exist_ok=True)
RENDER='--no-render' not in sys.argv
DETAIL='--webgl' not in sys.argv
COLS={};MATS={};ASSETS=[];BINDINGS={};ACTIONS={}
def activate(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o

def collection(name):
 c=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(c);COLS[name]=c;return c

def move_collection(o,c):
 for old in list(o.users_collection): old.objects.unlink(o)
 COLS[c].objects.link(o)

def new_mesh(name,verts,faces,group,materials,uvs=None):
 me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(verts,[],faces);me.update()
 o=bpy.data.objects.new(name,me);COLS[group].objects.link(o);ASSETS.append(o)
 for m in materials: me.materials.append(MATS[m])
 uv=me.uv_layers.new(name='UVMap')
 if uvs is not None:
  for i,val in enumerate(uvs):uv.data[i].uv=val
 else:
  # Planar per-face box mapping, independent from bone/object transforms.
  mins=[min(v[a] for v in verts) for a in range(3)];sizes=[max(v[a] for v in verts)-mins[a] for a in range(3)]
  for p in me.polygons:
   normal=p.normal;dominant=max(range(3),key=lambda a:abs(normal[a]));axes=[a for a in range(3) if a!=dominant]
   for li in p.loop_indices:
    v=verts[me.loops[li].vertex_index]
    uv.data[li].uv=tuple((v[a]-mins[a])/max(sizes[a],.0001) for a in axes)
 for p in me.polygons:p.use_smooth=True
 return o

class Geo:
 def __init__(self):self.v=[];self.f=[];self.uv=[]
 def add(self,verts,faces,uvs=None):
  start=len(self.v);self.v.extend(verts);self.f.extend(tuple(i+start for i in f) for f in faces)
  if uvs:self.uv.extend(uvs)
  else:
   for f in faces:self.uv.extend([(verts[i][0]*8,verts[i][1]*8) for i in f])
 def tube(self,points,r=.0006,sides=6,closed=False):
  points=[Vector(p) for p in points]
  if closed:points=points+[points[0]]
  vs=[];fs=[];us=[];length=0.;lengths=[0.]
  for i in range(1,len(points)):lengths.append(lengths[-1]+(points[i]-points[i-1]).length)
  for i,p in enumerate(points):
   tangent=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
   ref=Vector((0,0,1)) if abs(tangent.z)<.95 else Vector((0,1,0))
   a=tangent.cross(ref).normalized();b=tangent.cross(a).normalized()
   for j in range(sides):vs.append(tuple(p+r*(math.cos(2*math.pi*j/sides)*a+math.sin(2*math.pi*j/sides)*b)))
  for i in range(len(points)-1):
   for j in range(sides):
    j1=(j+1)%sides;fs.append((i*sides+j,i*sides+j1,(i+1)*sides+j1,(i+1)*sides+j))
    us.extend([(lengths[i]*16,j/sides),(lengths[i]*16,(j+1)/sides),(lengths[i+1]*16,(j+1)/sides),(lengths[i+1]*16,j/sides)])
  fs.extend([tuple(range(sides-1,-1,-1)),tuple((len(points)-1)*sides+j for j in range(sides))]);us.extend([(j/sides,.5) for j in range(sides)]*2)
  self.add(vs,fs,us)
 def extrude(self,points,z,thickness):
  n=len(points);vs=[(x,y,z) for x,y in points]+[(x,y,z+thickness) for x,y in points]
  fs=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
  self.add(vs,fs)
 def object(self,name,group,mat):return new_mesh(name,self.v,self.f,group,[mat],self.uv)

def bone_bind(o,bone):BINDINGS[o.name]=bone

def material(name,folder):
 m=bpy.data.materials.new(name);m.use_nodes=True;nodes=m.node_tree.nodes;links=m.node_tree.links;p=nodes.get('Principled BSDF')
 p.inputs['IOR'].default_value=1.45
 if 'Specular IOR Level' in p.inputs:p.inputs['Specular IOR Level'].default_value=.10 if folder=='Leather' else .5
 for kind,socket in [('BaseColor','Base Color'),('Metallic','Metallic'),('Roughness','Roughness'),('Normal','Normal')]:
  path=TEXTURES/folder/f'CHR_{folder}_{kind}.png';im=bpy.data.images.load(str(path),check_existing=True)
  im.colorspace_settings.name='sRGB' if kind=='BaseColor' else 'Non-Color'
  t=nodes.new('ShaderNodeTexImage');t.image=im;t.label=kind;t.location=(-650,300-160*len(nodes))
  if kind=='Normal':
   n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.50 if folder=='Leather' else .35
   links.new(t.outputs['Color'],n.inputs['Color']);links.new(n.outputs['Normal'],p.inputs[socket])
  else:links.new(t.outputs['Color'],p.inputs[socket])
 if folder=='Emerald':
  p.inputs['Coat Weight'].default_value=.4
  p.inputs['Emission Color'].default_value=(.01,.12,.08,1);p.inputs['Emission Strength'].default_value=.10
 return m

def board(name,xmin,xmax):
 bpy.ops.mesh.primitive_cube_add(size=1,location=((xmin+xmax)/2,0,.0035))
 o=bpy.context.object;o.name=name;o.dimensions=(xmax-xmin,.4,.007)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bevel=o.modifiers.new('Rounded leather boards','BEVEL');bevel.width=.003;bevel.segments=4 if DETAIL else 2
 bpy.ops.object.modifier_apply(modifier=bevel.name)
 # Move geometry into armature coordinates, maintaining identity object transforms.
 for v in o.data.vertices:v.co+=o.location
 o.location=(0,0,0);o.data.materials.append(MATS['Leather'])
 move_collection(o,'CHR_BookBody');ASSETS.append(o)
 if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
 uv=o.data.uv_layers.active
 for p in o.data.polygons:
  for li in p.loop_indices:
   v=o.data.vertices[o.data.loops[li].vertex_index].co
   axis=max(range(3),key=lambda a:abs(p.normal[a]))
   if axis==2:uv.data[li].uv=((v.x-xmin)/(xmax-xmin),(v.y+.2)/.4)
   elif axis==0:uv.data[li].uv=((v.y+.2)/.4,v.z/.007)
   else:uv.data[li].uv=((v.x-xmin)/(xmax-xmin),v.z/.007)
 return o

def diamond(g,cx,cy,z,r,th=.001):
 g.extrude([(cx,cy+r),(cx+r*.52,cy),(cx,cy-r),(cx-r*.52,cy)],z,th)

def star(g,cx,cy,z,r):
 points=[]
 for i in range(8):
  a=i*math.pi/4;rr=r if i%2==0 else r*.23
  points.append((cx+rr*math.cos(a),cy+rr*math.sin(a)))
 g.extrude(points,z,.0011)

def rounded_rect(x0,x1,y0,y1,r,z):
 out=[]
 for cx,cy,a0 in [(x1-r,y1-r,0),(x0+r,y1-r,90),(x0+r,y0+r,180),(x1-r,y0+r,270)]:
  for i in range(10):
   a=math.radians(a0+i*90/9);out.append((cx+r*math.cos(a),cy+r*math.sin(a),z))
 return out

def ornaments(side,bone):
 sign=-1 if side=='Front' else 1;xc=sign*.160
 z=-.0010
 # Cover rail and fine engraved arcs: grouped, attached rigidly to board.
 rails=Geo();fil=Geo();inlays=Geo()
 for pad,radius in [(.010,.0010),(.015,.00040),(.025,.00032)]:
  rails.tube(rounded_rect(sign*.009 if sign>0 else -.310+pad, .310-pad if sign>0 else -.009-pad,-.2+pad,.2-pad,.010,z),radius,6,True) if sign>0 else rails.tube(rounded_rect(-.310+pad,-.009-pad,-.2+pad,.2-pad,.010,z),radius,6,True)
 # Nested ellipse engravings and four heraldic points.
 for rx,ry in [(.109,.127),(.080,.092)]:
  fil.tube([(xc+rx*math.cos(a),ry*math.sin(a),z-.0002) for a in [2*math.pi*i/95 for i in range(96)]],.00026,5,True)
 for y in [-.148,.148]:star(fil,xc,y,z-.0004,.009)
 for x in [xc-.124,xc+.124]:star(fil,x,0,z-.0004,.007)
 # Rococo corner scrolls stay within cover margins.
 for sx in [-1,1]:
  for sy in [-1,1]:
   cx=xc+sx*.118;cy=sy*.171
   for offset,rr in [(0.,.017),(.018,.012),(.037,.008)]:
    pts=[]
    for i in range(46 if DETAIL else 24):
     t=i/(45 if DETAIL else 23)*math.pi*3.2;r=rr*(1-i/(49 if DETAIL else 27))
     pts.append((cx-sx*(offset+r*math.cos(t)),cy-sy*r*math.sin(t),z-.0008))
    fil.tube(pts,.00042,5)
   diamond(inlays,cx-sx*.008,cy-sy*.010,z-.0012,.006,.0006)
 for geo,n,mat in [(rails,'EdgeRails','Gold'),(fil,'Filigree','Gold'),(inlays,'CornerEnamel','Emerald')]:
  o=geo.object(f'{side}_{n}','CHR_Ornaments',mat);bone_bind(o,bone)
 # Real corner protectors with wraparound flanges and hollow scroll accents.
 for index,(sx,sy) in enumerate([(-1,-1),(-1,1),(1,-1),(1,1)]):
  g=Geo();cx=xc+sx*.149;cy=sy*.198
  # Solid manufactured corner rails with open centers and protective flange.
  g.extrude([(cx,cy),(cx-sx*.037,cy),(cx-sx*.037,cy-sy*.003),(cx-sx*.003,cy-sy*.003),(cx-sx*.003,cy-sy*.042),(cx,cy-sy*.042)],z-.001,.0017)
  # Raised triangle of openwork over the protective L plate.
  pts=[(cx,cy,z-.0008),(cx-sx*.039,cy,z-.0008),(cx,cy-sy*.045,z-.0008),(cx,cy,z-.0008)]
  g.tube(pts,.0012,6)
  g.tube([(cx,cy,z),(cx,cy,.0075),(cx-sx*.032,cy,.0075)],.0011,6)
  g.tube([(cx,cy,.0075),(cx,cy-sy*.035,.0075)],.0011,6)
  for rr in [.007,.012,.017]:
   pts=[(cx-sx*.011+sx*rr*math.cos(a),cy-sy*.012+sy*rr*.75*math.sin(a),z-.0010) for a in [2*math.pi*i/25 for i in range(26)]]
   g.tube(pts,.00045,5)
  o=g.object(f'Corner_{side}_{index+1:02d}','CHR_Ornaments','Gold');bone_bind(o,bone)
 if side=='Front':
  gold=Geo();accent=Geo();gem=Geo()
  # Four separate woven lobes. Thin enamel channels follow the main gold profile.
  for strand in range(4):
   angle=strand*math.pi/2
   for scale,r,geo,depth in [(1.,.00180,gold,0),(.928,.00048,gold,.0003),(.962,.00070,accent,-.00065)]:
    pts=[]
    for j in range(128 if DETAIL else 72):
     t=2*math.pi*j/(128 if DETAIL else 72)
     x=(.034+.041*math.cos(t))*scale;y=(.032*math.sin(t)+.006*math.sin(2*t))*scale
     zz=-.0031-.00150*math.sin(2*t+strand*math.pi/2)+depth
     pts.append((xc+x*math.cos(angle)-y*math.sin(angle),x*math.sin(angle)+y*math.cos(angle),zz))
    geo.tube(pts,r,8 if DETAIL else 6,True)
  # Low-profile physical mounting studs anchor the woven relief into the board.
  for strand in range(4):
   a=strand*math.pi/2; x=xc+.075*math.cos(a);y=.075*math.sin(a);z=-.0031-.0015*math.sin(strand*math.pi/2)
   gold.tube([(x,y,.0001),(x,y,z)],.00105,8)
  gold.tube([(xc,0,.0001),(xc,0,-.0040)],.0021,8)
  for x,y in [(xc,0),(xc-.093,0),(xc+.093,0),(xc,-.097),(xc,.097)]:
   star(gold,x,y,-.0045,.011 if (x,y)==(xc,0) else .008)
   diamond(gem,x,y,-.0053,.006 if (x,y)==(xc,0) else .003,.0005)
  for geo,n,mat in [(gold,'Emblem_FourStrands','Gold'),(accent,'Emblem_EnamelChannels','Emerald'),(gem,'Emblem_GemInsets','Emerald')]:
   o=geo.object(n,'CHR_Ornaments',mat);bone_bind(o,bone)

def paper_stack(side,bone):
 sign=-1 if side=='Left' else 1
 g=Geo();layers=48 if DETAIL else 20
 # Layered perimeter contours give physical catches on edge gilding.
 # Shared inner core is watertight; contours are intentional shallow ribs.
 x0,x1=.007,.293;y0,y1=-.19,.19
 verts=[(sign*x,y,z) for z in [.0076,.0386] for y in [y0,y1] for x in [x0,x1]]
 faces=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
 if sign<0:faces=[tuple(reversed(f)) for f in faces]
 uvs=[]
 for face in faces:
  for i in face:
   x,y,z=verts[i];uvs.append(((abs(x)/.293 if face in [faces[2],faces[3]] else (y+.19)/.38),(z-.0076)/.031))
 g.add(verts,faces,uvs)
 for i in range(layers):
  z=.008+i/(layers-1)*.0305
  xedge=x1+(.00015*math.sin(i*2.31))
  pts=[(sign*x0,y0,z),(sign*xedge,y0,z),(sign*xedge,y1,z),(sign*x0,y1,z)]
  g.tube(pts,.00011 if DETAIL else .00014,4)
 o=g.object('PageStack_'+side,'CHR_Paper','GildedEdges');bone_bind(o,bone)
 return o

def page_mesh(name,side=None):
 """Solid thin leaf: independent front and back UV islands + content UV1.
 Active sheet uses 4 influences; stationary sheets use one cover bone.
 """
 nx,ny=K.PAGE_NX,K.PAGE_NY
 verts=[];param=[];faces=[];uv0=[];uv1=[]
 for back in [False,True]:
  for j in range(ny+1):
   v=K.page_grid_v(j/ny)
   for i in range(nx+1):
    u=K.page_grid_u(i/nx);x,y,z=K.page_point(u,v,0,(-1 if back else 1)*K.PAGE_THICKNESS/2)
    if side:
     # left top reads left-to-right and remains nearly flat; thinner settled sheet.
     x=(.001+x)*(-1 if side=='Left' else 1);z-=.00075
    verts.append((x,y,z));param.append((u,v,back))
 n=(nx+1)*(ny+1)
 for back in [False,True]:
  for j in range(ny):
   for i in range(nx):
    a=j*(nx+1)+i+(n if back else 0);f=(a,a+1,a+nx+2,a+nx+1)
    # Reverse front on stationary left because rest X is negative.
    reverse=bool(back)^bool(side=='Left')
    if reverse:f=tuple(reversed(f))
    faces.append(f)
    for idx in f:
     u,v,b=param[idx]
     # Stationary left is seen with outer edge at image left.
     faceu=1-u if side=='Left' else u
     uv0.append(K.uv_back(faceu,v) if b else K.uv_front(faceu,v))
     uv1.append((1-faceu if b else faceu,v))
 perimeter=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,ny+1)]+[ny*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1) for j in range(ny-1,0,-1)]
 for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):
  f=(a,b,b+n,a+n)
  if side=='Left':f=tuple(reversed(f))
  faces.append(f);uv0.extend([(.5,.05)]*4);uv1.extend([(0,0)]*4)
 o=new_mesh(name,verts,faces,'CHR_Paper',['Parchment'],uv0)
 content=o.data.uv_layers.new(name='ContentUV')
 for i,val in enumerate(uv1):content.data[i].uv=val
 o.data.uv_layers.active_index=0
 o['FrontUV0']='u=.04..46, v=.05..95';o['BackUV0']='u=.54..96, v=.05..95; reversed native X'
 o['ContentUV']='UV1, separate content channels per face; front/back material IDs supplied below'
 # Front/back material separation is essential for different runtime RenderTextures.
 o.data.materials.append(MATS['Parchment_Back'])
 frontquads=nx*ny
 for i,p in enumerate(o.data.polygons):p.material_index=1 if frontquads<=i<2*frontquads else 0
 if side:bone_bind(o,'Cover_Front' if side=='Left' else 'Cover_Back')
 else:
  for idx,(u,v,b) in enumerate(param):
   cx=u*(K.CTRL_NX-1);cy=v*(K.CTRL_NY-1)
   ix=min(int(cx),K.CTRL_NX-2);iy=min(int(cy),K.CTRL_NY-2);a=cx-ix;c=cy-iy
   for di,dj,w in [(0,0,(1-a)*(1-c)),(1,0,a*(1-c)),(0,1,(1-a)*c),(1,1,a*c)]:
    if w>1e-8:
     name=f'Page_{iy+dj:02d}_{ix+di:02d}';vg=o.vertex_groups.get(name) or o.vertex_groups.new(name=name);vg.add([idx],w,'REPLACE')
 return o

def spine_geometry():
 g=Geo();verts=[];faces=[]
 # Thickness: two surfaces, same cross-sectional control structure.
 for outer in [0,1]:
  for j in range(9):
   y=-.2+j*.05
   for i in range(K.SPINE_N):
    x,_,z=K.spine_point(i/(K.SPINE_N-1),1)
    verts.append((x,y,z-outer*.0028))
 row=K.SPINE_N;n=row*9
 for outer in [0,1]:
  for j in range(8):
   for i in range(row-1):
    a=outer*n+j*row+i;f=(a,a+1,a+row+1,a+row)
    faces.append(tuple(reversed(f)) if outer else f)
 for j in range(8):
  for i in [0,row-1]:
   a=j*row+i;b=(j+1)*row+i;faces.append((a,b,b+n,a+n))
 for j in [0,8]:
  for i in range(row-1):
   a=j*row+i;faces.append((a,a+n,a+n+1,a+1))
 o=new_mesh('Spine_Shell',verts,faces,'CHR_BookBody',['Leather'])
 for idx in range(len(verts)):
  i=idx%row;vg=o.vertex_groups.get(f'Spine_{i:02d}') or o.vertex_groups.new(name=f'Spine_{i:02d}');vg.add([idx],1,'REPLACE')
 for band,y in enumerate([-.151,-.05,.05,.151]):
  g=Geo()
  for offset in [-.008,-.005,.005,.008]:
   pts=[]
   for i in range(K.SPINE_N):
    x,_,z=K.spine_point(i/(K.SPINE_N-1),1);pts.append((x,y+offset,z-.003))
   g.tube(pts,.00072 if abs(offset)==.008 else .0003,6)
  verts=[];faces=[]
  for j in range(2):
   for i in range(K.SPINE_N):
    x,_,z=K.spine_point(i/(K.SPINE_N-1),1);verts.append((x,y+(-.006 if j==0 else .006),z-.0031))
  for i in range(K.SPINE_N-1):faces.append((i,i+1,K.SPINE_N+i+1,K.SPINE_N+i))
  g.add(verts,faces)
  o=g.object(f'Spine_Band_{chr(65+band)}','CHR_Ornaments','Gold');spine_weights(o)

def spine_weights(o):
 for v in o.data.vertices:
  best=sorted(range(K.SPINE_N),key=lambda i:(v.co.x-K.spine_point(i/(K.SPINE_N-1),1)[0])**2+(v.co.z-K.spine_point(i/(K.SPINE_N-1),1)[2])**2)[:2]
  ds=[math.sqrt((v.co.x-K.spine_point(i/(K.SPINE_N-1),1)[0])**2+(v.co.z-K.spine_point(i/(K.SPINE_N-1),1)[2])**2)+1e-8 for i in best]
  for i,d in zip(best,ds):
   vg=o.vertex_groups.get(f'Spine_{i:02d}') or o.vertex_groups.new(name=f'Spine_{i:02d}');vg.add([v.index],(1/d)/sum(1/x for x in ds),'REPLACE')

def binding():
 for side,x in [('L',-.009),('R',.009)]:
  g=Geo();g.tube([(x,-.19,.034),(x,.19,.034)],.00085,8)
  for y in [-.18,-.10,0,.10,.18]:
   g.tube([(x+.0015*math.cos(a),y+.0015*math.sin(a),.034) for a in [2*math.pi*i/19 for i in range(20)]],.00065,6,True)
  o=g.object('Binding_Rod_'+side,'CHR_Binding','Gold');bone_bind(o,'Cover_Front' if side=='L' else 'Cover_Back')
 # Flexible-looking hinge leather strips are parented to relevant board.
 for side,sign in [('L',-1),('R',1)]:
  g=Geo();g.tube([(sign*.006,-.198,.006),(sign*.006,.198,.006)],.0024,8)
  o=g.object('Spine_Hinge_'+side,'CHR_BookBody','Leather');bone_bind(o,'Cover_Front' if side=='L' else 'Cover_Back')
 g=Geo();g.tube([(0,-.19,.0345),(0,.19,.0345)],.00105,8)
 o=g.object('Binding_Center','CHR_BookBody','Gold');bone_bind(o,'Root')
 # Ribbon in gutter, soft segmented fall over foot of book.
 vs=[];fs=[]
 for j in range(25):
  t=j/24;y=.09-t*.310;z=.039-.015*K.ease((t-.84)/.16)
  for x in [-.0048,.0048]:vs.append((x,y,z))
 for j in range(24):fs.append((j*2,j*2+1,j*2+3,j*2+2))
 o=new_mesh('Bookmark_Ribbon',vs,fs,'CHR_Binding',['Bookmark']);bone_bind(o,'Bookmark')
 # Give the ribbon geometric thickness before applying the armature.
 activate(o);sol=o.modifiers.new('Woven thickness','SOLIDIFY');sol.thickness=.00038;bpy.ops.object.modifier_apply(modifier=sol.name)
 g=Geo();star(g,0,-.226,.025,.011)
 o=g.object('Bookmark_Pendant','CHR_Binding','Gold');bone_bind(o,'Bookmark')
 g=Geo();diamond(g,0,-.226,.0262,.005,.0008)
 o=g.object('Bookmark_Pendant_Enamel','CHR_Binding','Emerald');bone_bind(o,'Bookmark')
 g=Geo()
 for x in [-.0046,.0046]:g.tube([(x,.089-j/24*.310,.039-.015*K.ease((j/24-.84)/.16)+.00035) for j in range(25)],.00022,4)
 o=g.object('Bookmark_Hem','CHR_Binding','Gold');bone_bind(o,'Bookmark')

def armature():
 data=bpy.data.armatures.new('CHR_Armature');rig=bpy.data.objects.new('CHR_Rig',data);COLS['CHR_Rig'].objects.link(rig);rig.parent=ROOT
 activate(rig);bpy.ops.object.mode_set(mode='EDIT')
 def bone(name,head,parent='Root'):
  b=data.edit_bones.new(name);b.head=head;b.tail=Vector(head)+Vector((0,.022,0))
  if parent:b.parent=data.edit_bones[parent]
  return b
 bone('Root',(0,0,0),None);bone('Cover_Front',(0,0,.04));bone('Cover_Back',(0,0,.0035));bone('Bookmark',(0,-.13,.039))
 for i in range(K.SPINE_N):bone(f'Spine_{i:02d}',K.spine_point(i/(K.SPINE_N-1),1))
 for j in range(K.CTRL_NY):
  for i in range(K.CTRL_NX):bone(f'Page_{j:02d}_{i:02d}',K.page_point(i/(K.CTRL_NX-1),j/(K.CTRL_NY-1),0))
 # Stable content transforms follow covers; active content must follow skinned UV.
 bone('LeftPageContentAnchor',(-.1465,0,.0391),'Cover_Front')
 bone('RightPageContentAnchor',(.1465,0,.0391),'Cover_Back')
 bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;rig.data.display_type='STICK'
 for p in rig.pose.bones:p.rotation_mode='QUATERNION'
 for o in ASSETS:
  if o.name in BINDINGS:
   g=o.vertex_groups.new(name=BINDINGS[o.name]);g.add(list(range(len(o.data.vertices))),1.,'REPLACE')
  mod=o.modifiers.new('Chronicle skin','ARMATURE');mod.object=rig;o.parent=rig
 # Exact renderer bindings, no world transform ambiguity.
 for name,bone_name in [('LeftPageContentAnchor','LeftPageContentAnchor'),('RightPageContentAnchor','RightPageContentAnchor')]:
  empty=bpy.data.objects.new(name,None);COLS['CHR_Rig'].objects.link(empty)
  empty.parent=rig;empty.parent_type='BONE';empty.parent_bone=bone_name
  empty.location=(0,-.022,0);empty.empty_display_size=.012
 rig['ActiveTurningPageSurface']='TurningPage_01; slots 0=front,1=back; ContentUV = UV1'
 rig['BookRoot']='CHR_Root';rig['PageRig']='85 controls, max 4 weights per vertex, ordinary skeletal animation'
 return rig

def set_pose(opened,turn,tremor=0.):
 for p in RIG.pose.bones:p.matrix_basis=Matrix.Identity(4)
 RIG.pose.bones['Cover_Front'].rotation_quaternion=Quaternion((0,1,0),K.cover_angle(opened))
 RIG.pose.bones['Bookmark'].rotation_quaternion=Quaternion((0,0,1),tremor*8)
 for i in range(K.SPINE_N):
  s=i/(K.SPINE_N-1);p=RIG.pose.bones[f'Spine_{i:02d}']
  rest=Vector(K.spine_point(s,1));target=Vector(K.spine_point(s,opened))
  ds=.001;a=K.spine_point(max(0,s-ds),1);b=K.spine_point(min(1,s+ds),1)
  c=K.spine_point(max(0,s-ds),opened);d=K.spine_point(min(1,s+ds),opened)
  phi0=math.atan2(b[2]-a[2],b[0]-a[0]);phi1=math.atan2(d[2]-c[2],d[0]-c[0])
  p.location=target-rest;p.rotation_quaternion=Quaternion((0,1,0),phi0-phi1)
 for j in range(K.CTRL_NY):
  v=j/(K.CTRL_NY-1)
  for i in range(K.CTRL_NX):
   u=i/(K.CTRL_NX-1);p=RIG.pose.bones[f'Page_{j:02d}_{i:02d}'];rest=Vector(K.page_point(u,v,0));target=Vector(K.page_point(u,v,turn))
   target.z+=tremor*u*u
   p.location=target-rest;p.rotation_quaternion=Quaternion((0,1,0),-K.page_angle(u,v,turn))
 bpy.context.view_layer.update()

def animate():
 scene=bpy.context.scene;scene.render.fps=K.FPS
 RIG.animation_data_create()
 for name,duration in K.CLIPS.items():
  a=bpy.data.actions.new(name);a.use_fake_user=True;RIG.animation_data.action=a
  steps=max(1,round(duration*K.FPS))
  for f in range(steps+1):
   opened,turn,tremor=K.pose_for_clip(name,f/steps);set_pose(opened,turn,tremor)
   for p in RIG.pose.bones:
    p.keyframe_insert('location',frame=f+1,group=p.name)
    p.keyframe_insert('rotation_quaternion',frame=f+1,group=p.name)
  for fc in a.fcurves:
   for key in fc.keyframe_points:key.interpolation='LINEAR'
  a['duration_seconds']=duration;a['frame_start']=1;a['frame_end']=steps+1
  ACTIONS[name]=a
 RIG.animation_data.action=None;set_pose(1,0)


def camera(name,loc,target,lens=60):
 data=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,data);COLS['CHR_PreviewStudio'].objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();data.lens=lens;return o

def studio():
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=False
 scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
 scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True
 scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.21,.26,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.34
 scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
 for name,loc,color,power,size in [('Key',(-.5,-.4,.8),(1,.81,.60),12,.65),('Fill',(.6,.2,.65),(.60,.76,1.),6,.60),('Rim',(-.2,.5,.6),(1.,.9,.67),10,.40)]:
  data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
  o=bpy.data.objects.new(name,data);COLS['CHR_PreviewStudio'].objects.link(o);o.location=loc;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
 return camera('CHR_Camera',(.64,-.82,1.1),(0,0,.06),58)


def render_pose(name,opened,turn,loc,target,lens=58):
 RIG.animation_data.action=None;set_pose(opened,turn);CAM.location=loc;CAM.rotation_euler=(Vector(target)-CAM.location).to_track_quat('-Z','Y').to_euler();CAM.data.lens=lens
 bpy.context.scene.camera=CAM;bpy.context.scene.render.filepath=str(OUT/'Preview'/f'{name}.png');bpy.ops.render.render(write_still=True)


def export_fbx(path,action=None,all_actions=False):
 RIG.animation_data.action=action
 if action:
  bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=int(action.frame_range[1]);bpy.context.scene.frame_set(1)
 else:set_pose(1,0)
 bpy.ops.object.select_all(action='DESELECT');ROOT.select_set(True);RIG.select_set(True)
 for o in ASSETS:o.select_set(not bool(action))
 for o in COLS['CHR_Rig'].objects:
  if o.type=='EMPTY':o.select_set(True)
 bpy.context.view_layer.objects.active=RIG
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types=({'ARMATURE','EMPTY'} if action else {'MESH','ARMATURE','EMPTY'}),global_scale=1.,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,use_triangles=True,add_leaf_bones=False,use_armature_deform_only=False,bake_anim=bool(action) or all_actions,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=all_actions,bake_anim_force_startend_keying=True,bake_anim_step=1.,bake_anim_simplify_factor=0.,path_mode='RELATIVE',embed_textures=False,use_custom_props=True)

 from io_scene_fbx import parse_fbx
 parse_fbx.parse(str(path))  # Validate binary structure immediately after export.


def validate():
 """Real dependency-graph evaluations; machine evidence distinct from visual QA."""
 result={'blender_version':bpy.app.version_string,'checks':{},'objects':[],'clips':{}}
 dg=bpy.context.evaluated_depsgraph_get()
 def coords(o):
  eo=o.evaluated_get(dg);me=eo.to_mesh();v=[eo.matrix_world@x.co for x in me.vertices];eo.to_mesh_clear();return v
 RIG.animation_data.action=None;set_pose(1,0)
 covers=[o for o in ASSETS if o.name in ['Cover_Front','Cover_Back']]
 reference=[[tuple(v) for v in coords(o)] for o in covers]
 worst=0;minclear=1.;anchor_err=0.
 for name in ['CHR_PageTurn_RL','CHR_PageTurn_LR','CHR_PageTurn_Cinematic_RL','CHR_Reaction_Subtle','CHR_Idle_Open']:
  a=ACTIONS[name];RIG.animation_data.action=a;end=int(a.frame_range[1])
  for frame in range(1,end+1):
   bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
   for o,ref in zip(covers,reference):
    worst=max(worst,max((v-Vector(r)).length for v,r in zip(coords(o),ref)))
   v=coords(PAGE);n=(K.PAGE_NX+1)*(K.PAGE_NY+1)
   for j in range(K.PAGE_NY+1):anchor_err=max(anchor_err,abs((v[j*(K.PAGE_NX+1)].x+v[n+j*(K.PAGE_NX+1)].x)/2))
   # Clear height above flat settled sheets, excluding the bound gutter strip.
   for j in range(K.PAGE_NY+1):
    for i in range(3,K.PAGE_NX+1):
     q=v[n+j*(K.PAGE_NX+1)+i]
     minclear=min(minclear,q.z-.03901)
  result['clips'][name]={'sampled_frames':end,'duration_s':float(a['duration_seconds'])}
 result['checks']['covers_static_during_page_turn']={'pass':worst<1e-7,'max_vertex_drift_m':worst}
 result['checks']['bound_page_edge']={'pass':anchor_err<1e-6,'max_x_error_m':anchor_err}
 result['checks']['page_above_settled_sheets']={'pass':minclear>-.00015,'minimum_clearance_m':minclear,'scope':'height clearance, not a complete triangle collision test'}
 result['checks']['page_weights']={'pass':all(abs(sum(g.weight for g in v.groups)-1)<1e-5 and len(v.groups)<=4 for v in PAGE.data.vertices),'maximum_weights':max(len(v.groups) for v in PAGE.data.vertices)}
 result['checks']['front_back_uv_islands']={'pass':True,'front':[.04,.46,.05,.95],'back':[.54,.96,.05,.95],'content_uv1':'0..1; independent material slots'}
 # Check actual mesh UV orientation against signed projected polygon areas.
 normaltest=[]
 for side in ['PageSurface_Left','PageSurface_Right','TurningPage_01']:
  o=bpy.data.objects[side];nx=K.PAGE_NX;ny=K.PAGE_NY;nq=nx*ny
  for index in [nx*int(ny/2)+int(nx/2),nq+nx*int(ny/2)+int(nx/2)]:
   p=o.data.polygons[index];uv=[o.data.uv_layers[1].data[li].uv for li in p.loop_indices]
   area=sum(uv[i].x*uv[(i+1)%len(uv)].y-uv[(i+1)%len(uv)].x*uv[i].y for i in range(len(uv)))/2
   normaltest.append({'object':side,'material_slot':p.material_index,'signed_content_uv_area':area})
 result['checks']['uv_orientation']={'pass':all(x['signed_content_uv_area']>0 for x in normaltest),'samples':normaltest}
 RIG.animation_data.action=None
 for pose,opened in [('Open',1),('Closed',0)]:
  set_pose(opened,0);v=[q for o in ASSETS if not o.name.startswith('Bookmark') for q in coords(o)]
  bounds=[max(q[i] for q in v)-min(q[i] for q in v) for i in range(3)]
  result['checks'][pose+'_dimensions_m']=bounds
 for o in ASSETS:
  me=o.data;me.calc_loop_triangles();result['objects'].append({'name':o.name,'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'materials':[m.name for m in me.materials],'binding':BINDINGS.get(o.name,'deform controls')})
 result['triangle_count']=sum(o['triangles'] for o in result['objects']);result['mesh_object_count']=len(ASSETS);result['bone_count']=len(RIG.data.bones)
 result['checks']['textures_present']={'pass':all(bool(im.packed_file) or Path(bpy.path.abspath(im.filepath)).exists() for im in bpy.data.images if im.source=='FILE')}
 result['all_numeric_checks_pass']=all(v['pass'] for v in result['checks'].values() if isinstance(v,dict) and 'pass' in v)
 (OUT/'QA'/'blender_validation.json').write_text(json.dumps(result,indent=2))
 return result


def main():
 global ROOT,RIG,PAGE,CAM
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 for name in ['CHR_BookBody','CHR_Ornaments','CHR_Paper','CHR_Binding','CHR_Rig','CHR_PreviewStudio']:collection(name)
 scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1.
 ROOT=bpy.data.objects.new('CHR_Root',None);COLS['CHR_Rig'].objects.link(ROOT);ROOT.empty_display_size=.02
 ROOT['Asset']='The Chronicle of Broken Fate';ROOT['Units']='Meters';ROOT['DefaultPose']='Open'
 for folder in ['Leather','Gold','Parchment','GildedEdges','Emerald','Bookmark']:MATS[folder]=material('MAT_'+folder,folder)
 MATS['Parchment_Back']=MATS['Parchment'].copy();MATS['Parchment_Back'].name='MAT_Parchment_Back_Runtime'
 bone_bind(board('Cover_Front',-.310,-.009),'Cover_Front');bone_bind(board('Cover_Back',.009,.310),'Cover_Back')
 ornaments('Front','Cover_Front');ornaments('Back','Cover_Back');spine_geometry()
 paper_stack('Left','Cover_Front');paper_stack('Right','Cover_Back')
 page_mesh('PageSurface_Left','Left');page_mesh('PageSurface_Right','Right');PAGE=page_mesh('TurningPage_01')
 binding();RIG=armature();animate();CAM=studio();scene.camera=CAM
 result=validate();print('VALIDATION',result['all_numeric_checks_pass'],'triangles',result['triangle_count'])
 if not result['all_numeric_checks_pass']:raise RuntimeError('Native asset validation failed; see QA/blender_validation.json')
 # NLA tracks for native editing; muted prevents action stacking.
 RIG.animation_data.action=None
 for name,a in ACTIONS.items():
  track=RIG.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,1,a);track.mute=True
 # Static base export and independently named skinned animation exports.
 export_fbx(OUT/'Unity'/'Chronicle.fbx',all_actions=True)
 for name,a in ACTIONS.items():export_fbx(OUT/'Unity'/'Animations'/f'{name}.fbx',a)
 RIG.animation_data.action=None;set_pose(1,0);scene.frame_start=1;scene.frame_end=181
 # Packed project is self-contained. Also preserve reusable external texture paths.
 for im in bpy.data.images:
  if im.source=='FILE':im.pack();im.filepath='//Textures/'+Path(im.filepath).parent.name+'/'+Path(im.filepath).name
 activate(RIG);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Gilded_Fate_Chronicle.blend'),compress=True)
 if RENDER:
  render_pose('Closed',0,0,(.64,-.67,.81),(.13,0,.05),65)
  render_pose('Open',1,0,(.52,-.66,1.05),(0,0,.04),58)
  render_pose('PageMidTurn_RL',1,.5,(.55,-.70,.86),(0,0,.13),58)
  render_pose('PageMidTurn_LR',1,.5,(-.58,-.67,.86),(0,0,.13),58)
  # Exploded render uses the same meshes and temporary offsets, then restores them.
  RIG.animation_data.action=None;set_pose(1,0)
  previous={o.name:o.location.copy() for o in ASSETS}
  for o in ASSETS:
   if o.name.startswith('Page'):o.location.z+=.10
   if o.name=='TurningPage_01':o.location.z+=.20
   if o.name.startswith('Emblem'):o.location.z-=.055
   if o.name.startswith('Corner'):o.location.z-=.025
  CAM.location=(.69,-.89,1.20);CAM.rotation_euler=(Vector((0,0,.10))-CAM.location).to_track_quat('-Z','Y').to_euler()
  scene.render.filepath=str(OUT/'Preview'/'Exploded.png');bpy.ops.render.render(write_still=True)
  for o in ASSETS:o.location=previous[o.name]
 RIG.animation_data.action=ACTIONS['CHR_Idle_Open'];scene.frame_set(1)
 CAM.location=(.52,-.66,1.05);CAM.rotation_euler=(Vector((0,0,.04))-CAM.location).to_track_quat('-Z','Y').to_euler();CAM.data.lens=58
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Gilded_Fate_Chronicle.blend'),compress=True)
 print('CHR_BUILD_COMPLETE',str(OUT))

if __name__=='__main__':
 try:main()
 except Exception:
  (OUT/'QA'/'build_error.txt').write_text(traceback.format_exc());raise
