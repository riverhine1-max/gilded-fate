"""Author reproducible PNG PBR sets. Optional tool; Blender build uses included PNGs.
Requires numpy + Pillow + scipy. No photos, reference art, text or lighting baked in.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, distance_transform_edt
ROOT=Path(__file__).resolve().parents[1]/'Textures'
RNG=np.random.default_rng(74017)
def noise(n,scale):
 a=RNG.random((max(2,n//scale),max(2,n//scale)))
 im=Image.fromarray(np.uint8(a*255)).resize((n,n),Image.Resampling.BICUBIC)
 a=np.asarray(im,dtype=np.float32)/255
 return a

def save_set(name,col,metal,rough,height,ao=None,emission=None,strength=1.):
 folder=ROOT/name;folder.mkdir(parents=True,exist_ok=True)
 n=col.shape[0]
 def scalar(x): return np.full((n,n),x,np.float32) if np.isscalar(x) else x
 metal,rough,height=map(scalar,[metal,rough,height]);ao=scalar(1. if ao is None else ao)
 gy,gx=np.gradient(height)
 normals=np.stack([-gx*strength,gy*strength,np.ones_like(gx)],-1)
 normals/=np.linalg.norm(normals,axis=-1,keepdims=True)
 def write(kind,data):
  Image.fromarray(np.uint8(np.clip(data,0,1)*255+.5)).save(folder/f'CHR_{name}_{kind}.png',optimize=True)
 write('BaseColor',col);write('Metallic',metal);write('Roughness',rough)
 write('Normal',normals*.5+.5);write('AO',ao);write('Height',height)
 write('UnityMask',np.stack([metal,ao,np.zeros_like(ao),1-rough],-1))
 if emission is not None:write('Emission',emission)

n=2048; fine=noise(n,1);grain=noise(n,6);cloud=noise(n,170)
# Uneven fine leather-cell boundaries, varying width, subdued edge wear.
seeds=RNG.random((n,n))>.9994
d=distance_transform_edt(~seeds)
# Voronoi nearest seed labels produce natural crack networks.
_,indices=distance_transform_edt(~seeds,return_indices=True)
labels=indices[0]*n+indices[1]
cracks=((labels!=np.roll(labels,1,0))|(labels!=np.roll(labels,1,1))).astype(np.float32)
cracks=gaussian_filter(cracks,.8)
edge=np.minimum.reduce(np.meshgrid(np.minimum(np.arange(n),n-1-np.arange(n)),np.minimum(np.arange(n),n-1-np.arange(n))))/n
wear=np.exp(-edge*36)*(.4+.6*cloud)
leather=np.stack([.080+.023*grain+.038*wear,.064+.019*grain+.027*wear,.050+.014*grain+.017*wear],-1)
leather+=cracks[...,None]*np.array([.014,.009,.005]); leather+=(cloud[...,None]-.5)*.02
save_set('Leather',leather,0.,.65+.18*cloud-.04*wear,.40+.10*grain-.27*cracks,1-.17*cracks,strength=9.)
goldcloud=noise(n,100);scr=noise(n,2)
patina=np.clip((goldcloud-.48)*2,0,.55)
gold=np.zeros((n,n,3),np.float32)+[.55,.365,.135]
gold*=1-patina[...,None]*.72;gold+=(scr[...,None]-.5)*.022
save_set('Gold',gold,1.,.27+.24*goldcloud,.45+.11*scr,1-.16*patina,strength=2.)
# One portrait parchment face, then two independently addressable atlas regions.
w,h=860,1840
yy,xx=np.mgrid[0:h,0:w];edge=np.minimum.reduce([xx/w,(w-1-xx)/w,yy/h,(h-1-yy)/h])
base_noise=np.asarray(Image.fromarray(np.uint8(noise(2048,85)*255)).resize((w,h)),np.float32)/255
fibers=np.asarray(Image.fromarray(np.uint8(noise(2048,2)*255)).resize((w,h)),np.float32)/255
burn=np.exp(-edge*26)*(.65+.35*base_noise)
p=np.zeros((h,w,3),np.float32)+[.88,.803,.643]
p+=(base_noise[...,None]-.5)*.062
p-=burn[...,None]*np.array([.15,.19,.23]);p+=(fibers[...,None]-.5)*.014
# Decorative borders stay out of the 24 x 34 cm field.
mask=Image.new('L',(w,h));draw=ImageDraw.Draw(mask)
gem=Image.new('L',(w,h));dg=ImageDraw.Draw(gem)
def line(points,width=2):draw.line(points,fill=215,width=width,joint='curve')
def star(cx,cy,r):
 points=[(cx,cy-r),(cx+r*.22,cy-r*.22),(cx+r,cy),(cx+r*.22,cy+r*.22),(cx,cy+r),(cx-r*.22,cy+r*.22),(cx-r,cy),(cx-r*.22,cy-r*.22),(cx,cy-r)]
 line(points,3); dg.polygon([(cx,cy-r*.27),(cx+r*.15,cy),(cx,cy+r*.27),(cx-r*.15,cy)],fill=255)
marginx,marginy=44,55
for inset in [0,7]:draw.rounded_rectangle((marginx+inset,marginy+inset,w-marginx-inset,h-marginy-inset),radius=24,outline=190,width=2)
for cx,cy in [(w/2,marginy),(w/2,h-marginy),(marginx,h/2),(w-marginx,h/2)]:star(cx,cy,20)
for sx in [-1,1]:
 for sy in [-1,1]:
  cx=marginx if sx==1 else w-marginx;cy=marginy if sy==1 else h-marginy
  star(cx+sx*18,cy+sy*18,26)
  # Scrollwork twists in the narrow perimeter only.
  for radius in [12,20,29]:
   pts=[]
   for t in np.linspace(0,math_pi:=4*np.pi,120):
    rr=radius*(1-t/math_pi);pts.append((cx+sx*(25+rr*np.cos(t)),cy+sy*(50+rr*np.sin(t))))
   line(pts,2)
  for offset in [70,104,135]:
   line([(cx+sx*3,cy+sy*(offset-12)),(cx+sx*19,cy+sy*offset),(cx+sx*3,cy+sy*(offset+12))],2)
m=np.asarray(mask,np.float32)/255;g=np.asarray(gem,np.float32)/255
p=p*(1-m[...,None])+m[...,None]*np.array([.46,.30,.12])
p=p*(1-g[...,None])+g[...,None]*np.array([.024,.15,.11])
col=np.full((n,n,3),[.83,.74,.59],np.float32);met=np.zeros((n,n),np.float32);height=np.full((n,n),.45,np.float32);rough=np.full((n,n),.86,np.float32)
# Panels match uv_front/back below, image y inverted from Blender UV convention.
for start in [82,1106]:
 col[102:1942,start:start+w]=p
 met[102:1942,start:start+w]=m*.63
 height[102:1942,start:start+w]=.44+.035*(fibers-.5)+m*.025
 rough[102:1942,start:start+w]=.89-.25*m
save_set('Parchment',col,met,rough,height,strength=2.)
from age_parchment import age_parchment
age_parchment(ROOT/'Parchment')
# Page-edge texture runs length horizontally and leaf layers vertically.
y,x=np.mgrid[0:n,0:n];rows=RNG.random(n)
layers=gaussian_filter(rows,1.)[:,None];jitter=.008*np.sin(x/52)+.003*np.sin(x/13)
edgecol=np.zeros((n,n,3),np.float32)+[.52,.357,.147]
edgecol*=.77+.39*layers[...,None]
edgecol+=(noise(n,12)[...,None]-.5)*.03
edgecol[...,1]+=.012*np.sin(y/67)
save_set('GildedEdges',edgecol,.86,.34+.18*noise(n,70),.30+.32*np.broadcast_to(layers,(n,n))+jitter, .85+.15*np.broadcast_to(layers,(n,n)),strength=3.)
n=1024;y,x=np.mgrid[0:n,0:n];weave=(np.sin(x*1.9)*np.sin(y*1.8))*.04
cloth=np.zeros((n,n,3),np.float32)+[.022,.104,.073];cloth+=weave[...,None]*.12
save_set('Bookmark',cloth,0.,.80+.06*noise(n,5),.5+weave,strength=2.)
n=512;gemnoise=noise(n,45);gemcol=np.zeros((n,n,3),np.float32)+[.012,.115,.087];gemcol*=.70+.50*gemnoise[...,None]
save_set('Emerald',gemcol,.10,.17+.06*gemnoise,.5,1.,gemcol*.12)
print('Authored texture sets:',len(list(ROOT.glob('*/*BaseColor.png'))))
