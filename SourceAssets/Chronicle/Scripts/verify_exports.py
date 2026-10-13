"""Reopen native and reimport every FBX in a clean Blender scene."""
import bpy,json,sys,math
from pathlib import Path
P=Path(__file__).resolve().parents[1]
report={'blender':bpy.app.version_string,'native':{},'exports':[],'unity_editor_tested':False}
bpy.ops.wm.open_mainfile(filepath=str(P/'Gilded_Fate_Chronicle.blend'))
rig=bpy.data.objects.get('CHR_Rig');report['native']['opened']=bool(rig)
report['native']['actions']={a.name: {'frames':list(a.frame_range),'duration_s':a.get('duration_seconds')} for a in bpy.data.actions}
report['native']['all_images_packed']=all(im.packed_file is not None for im in bpy.data.images if im.source=='FILE')
for path in [P/'Unity'/'Chronicle.fbx']+sorted((P/'Unity'/'Animations').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True,ignore_leaf_bones=True)
 meshes=[o for o in bpy.data.objects if o.type=='MESH'];arms=[o for o in bpy.data.objects if o.type=='ARMATURE']
 actions=list(bpy.data.actions)
 item={'filename':path.name,'bytes':path.stat().st_size,'meshes':len(meshes),'armatures':len(arms),'bones':sum(len(o.data.bones) for o in arms),'actions':[a.name for a in actions],'pass':len(arms)==1 and len(arms[0].data.bones)==108}
 if path.parent.name=='Animations':
  item['pass'] &= len(actions)>=1 and len(meshes)==0
  for a in actions:
   if arms:
    arms[0].animation_data_create();arms[0].animation_data.action=a
    for f in [a.frame_range[0],sum(a.frame_range)/2,a.frame_range[1]]:
     bpy.context.scene.frame_set(int(f));bpy.context.view_layer.update()
 elif meshes:
  item['pass'] &= len(meshes)==38 and len(actions)==9
  open_action=next(a for a in actions if 'CHR_Pose_Open' in a.name)
  arms[0].animation_data.action=open_action
  bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
  dg=bpy.context.evaluated_depsgraph_get();vs=[]
  for o in meshes:
   if o.name.startswith('Bookmark'):continue
   eo=o.evaluated_get(dg);me=eo.to_mesh();vs.extend(eo.matrix_world@v.co for v in me.vertices);eo.to_mesh_clear()
  dims=[max(v[i] for v in vs)-min(v[i] for v in vs) for i in range(3)]
  item['bounds_blender_m']=dims;item['pass'] &= .60<dims[0]<.66 and .39<dims[1]<.41
 report['exports'].append(item)
report['all_export_checks_pass']=all(i['pass'] for i in report['exports'])
(P/'QA'/'reopen_and_fbx_validation.json').write_text(json.dumps(report,indent=2))
print('REOPEN_EXPORT_VALIDATION',report['all_export_checks_pass'])
