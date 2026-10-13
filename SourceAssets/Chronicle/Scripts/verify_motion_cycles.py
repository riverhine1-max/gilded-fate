"""Check opening/closing endpoints and reversal against evaluated Blender geometry."""
import bpy, sys, json, math
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import build_chronicle as B
B.RIG=bpy.data.objects['CHR_Rig']
meshes=[o for o in bpy.data.objects if o.type=='MESH']

def action(name,t):
    a=bpy.data.actions[name]
    B.RIG.animation_data.action=a
    frame=a.frame_range[0]+t*(a.frame_range[1]-a.frame_range[0])
    bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame))
    bpy.context.view_layer.update()

def vertices():
    result=[]
    dg=bpy.context.evaluated_depsgraph_get()
    for o in meshes:
        eo=o.evaluated_get(dg);me=eo.to_mesh()
        result.extend(eo.matrix_world@v.co for v in me.vertices)
        eo.to_mesh_clear()
    return result

def drift(a,b):
    return max((p-q).length for p,q in zip(a,b))

checks={}
for label,opened in [('Closed',0),('Open',1)]:
    B.RIG.animation_data.action=None;B.set_pose(opened,0)
    reference=vertices()
    endpoints=[('CHR_Open',opened),('CHR_Close',1-opened),('CHR_Pose_'+label,0)]
    errors=[]
    for name,t in endpoints:
        action(name,t);errors.append({'clip':name,'progress':t,'maximum_vertex_error_m':drift(reference,vertices())})
    checks[label+'_endpoints']={'pass':all(x['maximum_vertex_error_m']<1e-6 for x in errors),'samples':errors}

maximum=0.;finite=True
for i in range(41):
    t=i/40
    action('CHR_Open',t);v=vertices()
    action('CHR_Close',1-t);w=vertices()
    maximum=max(maximum,drift(v,w))
    finite &= all(math.isfinite(c) for p in v+w for c in p)
checks['opening_closing_reverse_path']={'pass':maximum<.00015,'maximum_vertex_error_m':maximum,
                                      'tolerance_m':.00015,'samples_per_clip':41}
checks['finite_animated_geometry']={'pass':finite}

maximum=0.
page=bpy.data.objects['TurningPage_01']
for i in range(41):
    t=i/40
    action('CHR_PageTurn_RL',t);v=vertices()
    action('CHR_PageTurn_LR',1-t);w=vertices()
    maximum=max(maximum,drift(v,w))
checks['page_turn_reverse_path']={'pass':maximum<1e-6,'maximum_vertex_error_m':maximum,'samples_per_clip':41}
report={'blender':bpy.app.version_string,'checks':checks,
        'all_checks_pass':all(c['pass'] for c in checks.values()),
        'scope':'Evaluated vertex endpoints and reverse trajectories; no exhaustive triangle collision test.'}
(HERE.parent/'QA'/'motion_cycles_validation.json').write_text(json.dumps(report,indent=2))
print('MOTION_CYCLE_VALIDATION',report['all_checks_pass'])
if not report['all_checks_pass']:raise RuntimeError('Motion cycle validation failed')
