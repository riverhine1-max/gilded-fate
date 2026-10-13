"""Blender-independent, deterministic Chronicle deformation kernel (meters)."""
import math
PAGE_WIDTH = 0.291
PAGE_HEIGHT = 0.380
PAGE_Z = 0.0396
PAGE_THICKNESS = 0.00028
PAGE_CORNER_RADIUS = 0.009
PAGE_NX, PAGE_NY = 48, 32
CTRL_NX, CTRL_NY = 17, 5
SPINE_N = 17
FPS = 60
CLIPS = {
 'CHR_Open': 2.5, 'CHR_Close': 1.8,
 'CHR_PageTurn_RL': .9, 'CHR_PageTurn_LR': .9,
 'CHR_PageTurn_Cinematic_RL': 1.5, 'CHR_Idle_Open': 3.,
 'CHR_Reaction_Subtle': .6, 'CHR_Pose_Closed': 1/60,
 'CHR_Pose_Open': 1/60,
}
def clamp(t): return max(0., min(1., t))
def ease(t):
 t=clamp(t); return t*t*(3.-2.*t)
def page_angle(u, v, t):
 """Local tangent rotates with hinge; corner leads, outer edge lags at crossing.
 Angle constrained to [0, pi] keeps every sample above the stationary stacks.
 """
 t=clamp(t)
 if t==0: return 0.
 if t==1: return math.pi
 envelope=math.sin(math.pi*t)**1.4
 hinge=math.pi*ease(t)
 # Outboard width lag creates a curved sheet, top corner initiates lift.
 curl=.38*math.sin(2*math.pi*t)*u**1.15 - 1.25*envelope*u**1.25
 corner=.10*envelope*u*u*(2*v-1)
 return max(0.,min(math.pi,hinge+curl+corner))
def page_point(u,v,t,side_offset=0.):
 """Arc-length integration across width; bound edge stays on x=0."""
 # Gently rounded free corners, with full height retained at the bound edge.
 y=(v-.5)*PAGE_HEIGHT
 corner_y=max(0.,abs(y)-(PAGE_HEIGHT/2-PAGE_CORNER_RADIUS))
 cut=PAGE_CORNER_RADIUS-math.sqrt(max(0.,PAGE_CORNER_RADIUS**2-corner_y**2)) if corner_y else 0.
 u=u*(1.-cut/PAGE_WIDTH)
 steps=max(4,int(u*96)); du=u/steps
 x=z=0.
 for k in range(steps):
  s=(k+.5)*du; a=page_angle(s,v,t)
  x+=math.cos(a)*PAGE_WIDTH*du
  z+=math.sin(a)*PAGE_WIDTH*du
 a=page_angle(u,v,t)
 # A shallow 0.5 mm arch, smoothly vanishing at hinge and outer edge.
 arch=.0005*math.sin(math.pi*u)**2*math.cos(math.pi*t)**2
 return (x-side_offset*math.sin(a), (v-.5)*PAGE_HEIGHT,
         PAGE_Z+z+arch+side_offset*math.cos(a))
def page_grid_u(q):
 # Extra edge sampling gives a smooth quarter-circle without increasing triangles.
 return q/.875*.965 if q<=.875 else .965+(q-.875)/.125*.035
def page_grid_v(q):
 if q<.125: return q/.125*.032
 if q>.875: return 1-(1-q)/.125*.032
 return .032+(q-.125)/.75*.936
def spine_point(s,opened):
 k=ease(opened)
 closed=(-.018*math.sin(math.pi*s), .04+.04*math.cos(math.pi*s))
 spread=(-.014*math.cos(math.pi*s), .0035-.0115*math.sin(math.pi*s))
 return (closed[0]*(1-k)+spread[0]*k, 0.,closed[1]*(1-k)+spread[1]*k)
def cover_angle(opened): return math.pi*(1-ease(opened))
def pose_for_clip(name,t):
 t=clamp(t)
 if name=='CHR_Open': return ease(t),0.,0.
 if name=='CHR_Close': return 1-ease(t),0.,0.
 if name=='CHR_Pose_Closed': return 0.,0.,0.
 if name=='CHR_PageTurn_LR': return 1.,1.-t,0.
 if name.startswith('CHR_PageTurn'): return 1.,t,0.
 if name=='CHR_Reaction_Subtle': return 1.,0.,.0002*math.sin(6*math.pi*t)*math.sin(math.pi*t)**2
 return 1.,0.,.00018*math.sin(2*math.pi*t)
def uv_front(u,v): return (.04+.42*u,.05+.90*v)
def uv_back(u,v): return (.54+.42*(1-u),.05+.90*v)
CONTENT_SAFE_LOCAL = {'width_m':.240,'height_m':.340,'center_y_m':0.,'u_range':[.085,.915],'v_range':[.053,.947]}
