"""Precise atlas-aware wear authoring. Called once after raw map regeneration.
Uses image data maps, preserves the central content field and decorative ink.
"""
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

def age_parchment(folder):
 folder=Path(folder);prefix='CHR_Parchment_'
 def read(name):return np.asarray(Image.open(folder/(prefix+name+'.png')),dtype=np.float32)/255
 def write(name,data):Image.fromarray(np.uint8(np.clip(data,0,1)*255+.5)).save(folder/(prefix+name+'.png'),optimize=True)
 col=read('BaseColor');metal=read('Metallic');rough=read('Roughness');height=read('Height');ao=read('AO')
 w,h=860,1840; yy,xx=np.mgrid[0:h,0:w]
 distance=np.minimum.reduce([xx/w*291,(w-1-xx)/w*291,yy/h*380,(h-1-yy)/h*380])
 for index,start in enumerate([82,1106]):
  rng=np.random.default_rng(82173+index)
  # Feathered oxidation and fine foxing remain within the perimeter.
  cloud=gaussian_filter(rng.random((h,w),dtype=np.float32),12)
  cloud=(cloud-cloud.min())/max(1e-8,cloud.max()-cloud.min())
  edge=np.exp(-distance/5.2)*(.55+.45*cloud)
  seed=(rng.random((h,w))>.99935).astype(np.float32)
  freckles=gaussian_filter(seed,1.5)*14*np.exp(-distance/9.)
  speckle=gaussian_filter((rng.random((h,w))>.996).astype(np.float32),.55)*np.exp(-distance/3.8)
  # No added wear in the writing-safe rectangle, including soft fade into its margins.
  safe=np.minimum.reduce([xx/w-.085,.915-xx/w,yy/h-.053,.947-yy/h])
  gate=np.clip(-safe/.020,0,1)
  wear=(edge*.50+freckles*.70+speckle*.12)*gate
  region=(slice(102,1942),slice(start,start+w))
  paper=1-metal[region]
  col[region]-=wear[...,None]*paper[...,None]*np.array([.125,.153,.185],np.float32)
  rough[region]=np.clip(rough[region]+wear*.05*paper,0,1)
  height[region]-=(speckle*.02+freckles*.012)*gate*paper
  ao[region]=np.clip(ao[region]-(speckle*.045+freckles*.025)*gate*paper,.90,1)
 gy,gx=np.gradient(height);norm=np.stack([-gx*2,gy*2,np.ones_like(gx)],-1);norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
 write('BaseColor',col);write('Roughness',rough);write('Height',height);write('Normal',norm*.5+.5);write('AO',ao)
 write('UnityMask',np.stack([metal,ao,np.zeros_like(ao),1-rough],-1))
 print('Added feathered edge aging; writing-safe field preserved')
if __name__=='__main__':age_parchment(Path(__file__).resolve().parents[1]/'Textures'/'Parchment')
