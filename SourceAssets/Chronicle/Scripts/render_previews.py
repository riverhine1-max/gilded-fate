"""Re-render final poses from an existing project. Blender 4.0+."""
import bpy,sys
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build_chronicle as B
B.OUT=HERE.parent;B.RIG=bpy.data.objects['CHR_Rig'];B.PAGE=bpy.data.objects['TurningPage_01'];B.CAM=bpy.data.objects['CHR_Camera']
B.ASSETS=[o for o in bpy.data.objects if o.type=='MESH' and o.name!='UV_Test']
s=bpy.context.scene;s.cycles.use_denoising=False;s.cycles.samples=48
B.render_pose('Closed',0,0,(.64,-.67,.81),(.13,0,.05),65)
B.render_pose('Open',1,0,(.52,-.66,1.05),(0,0,.04),58)
B.render_pose('PageMidTurn_RL',1,.5,(.55,-.70,.86),(0,0,.13),58)
B.render_pose('PageMidTurn_LR',1,.5,(-.58,-.67,.86),(0,0,.13),58)
B.RIG.animation_data.action=None;B.set_pose(1,0)
previous={o.name:o.location.copy() for o in B.ASSETS}
for o in B.ASSETS:
 if o.name.startswith('Page'):o.location.z+=.10
 if o.name=='TurningPage_01':o.location.z+=.20
 if o.name.startswith('Emblem'):o.location.z-=.055
 if o.name.startswith('Corner'):o.location.z-=.025
from mathutils import Vector
B.CAM.location=(.69,-.89,1.20);B.CAM.rotation_euler=(Vector((0,0,.10))-B.CAM.location).to_track_quat('-Z','Y').to_euler()
s.render.filepath=str(B.OUT/'Preview'/'Exploded.png');bpy.ops.render.render(write_still=True)
for o in B.ASSETS:o.location=previous[o.name]
B.RIG.animation_data.action=bpy.data.actions['CHR_Idle_Open'];s.frame_set(1)
B.CAM.location=(.52,-.66,1.05);B.CAM.rotation_euler=(Vector((0,0,.04))-B.CAM.location).to_track_quat('-Z','Y').to_euler();B.CAM.data.lens=58
bpy.ops.wm.save_as_mainfile(filepath=str(B.OUT/'Gilded_Fate_Chronicle.blend'),compress=True)
