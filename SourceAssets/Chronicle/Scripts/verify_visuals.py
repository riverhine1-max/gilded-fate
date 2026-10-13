"""Render actual baked turn action and temporary front/back UV labels."""
import bpy,sys
from pathlib import Path
from mathutils import Vector
H=Path(__file__).resolve().parent;sys.path.insert(0,str(H))
import build_chronicle as B
B.OUT=H.parent;B.RIG=bpy.data.objects['CHR_Rig'];B.CAM=bpy.data.objects['CHR_Camera']
s=bpy.context.scene;s.cycles.samples=12;s.cycles.use_denoising=False;s.render.resolution_x=800;s.render.resolution_y=600
B.CAM.location=(.55,-.77,1.00);B.CAM.rotation_euler=(Vector((0,0,.12))-B.CAM.location).to_track_quat('-Z','Y').to_euler();B.CAM.data.lens=56
folder=B.OUT/'QA'/'TurnFrames';folder.mkdir(exist_ok=True)
B.RIG.animation_data.action=bpy.data.actions['CHR_PageTurn_RL']
for i in range(25):
 frame=1+round(i/24*54);s.frame_set(frame)
 s.render.filepath=str(folder/f'RL_{i:03d}.png');bpy.ops.render.render(write_still=True)
# Temporary debug materials: never save them into the delivered native asset.
for label in ['FRONT','BACK']:
 m=bpy.data.materials.new('QA_'+label);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.9
 im=bpy.data.images.load(str(B.OUT/'QA'/f'UV_Test_{label}.png'))
 n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=im
 uv=m.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='ContentUV'
 m.node_tree.links.new(uv.outputs[0],n.inputs['Vector']);m.node_tree.links.new(n.outputs[0],p.inputs['Base Color'])
for name in ['PageSurface_Left','PageSurface_Right','TurningPage_01']:
 o=bpy.data.objects[name];o.data.materials[0]=bpy.data.materials['QA_FRONT'];o.data.materials[1]=bpy.data.materials['QA_BACK']
B.RIG.animation_data.action=None
B.CAM.location=(.03,-.5,1.1);B.CAM.rotation_euler=(Vector((0,0,.04))-B.CAM.location).to_track_quat('-Z','Y').to_euler();B.CAM.data.lens=53
for name,t in [('UV_Open',0),('UV_Turn_Back',.87)]:
 B.set_pose(1,t);s.render.filepath=str(B.OUT/'QA'/f'{name}.png');bpy.ops.render.render(write_still=True)
print('Visual QA renders complete')
