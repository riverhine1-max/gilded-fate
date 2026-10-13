"""Render actual baked Chronicle actions for the review movies in Blender 4.0+.

Run against Gilded_Fate_Chronicle.blend. No native data is saved by this script.
Then run assemble_review_videos.py with ordinary Python and ffmpeg installed.
"""
import bpy, sys, json
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
scene = bpy.context.scene
rig = bpy.data.objects['CHR_Rig']
cam = bpy.data.objects['CHR_Camera']
scene.camera = cam
scene.render.resolution_x = 960
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = True
scene.render.use_file_extension = True
scene.render.use_persistent_data = False
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.cycles.samples = 16
scene.cycles.use_denoising = False

def camera(location, target, lens):
    cam.location = location
    cam.rotation_euler = (Vector(target)-cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    bpy.context.view_layer.update()

def set_action(name, progress):
    action = bpy.data.actions[name]
    rig.animation_data.action = action
    frame = float(action.frame_range[0]) + progress * (action.frame_range[1]-action.frame_range[0])
    scene.frame_set(int(frame), subframe=frame-int(frame))
    bpy.context.view_layer.update()

def framing(names):
    """Check the actual deformed geometry throughout each action, not just endpoints."""
    bounds = [1., 1., 0., 0.]
    behind = False
    for name in names:
        for i in range(21):
            set_action(name, i/20)
            dg = bpy.context.evaluated_depsgraph_get()
            for obj in bpy.data.objects:
                if obj.type != 'MESH':
                    continue
                evaluated = obj.evaluated_get(dg)
                for corner in evaluated.bound_box:
                    p = world_to_camera_view(scene, cam, evaluated.matrix_world @ Vector(corner))
                    bounds[0] = min(bounds[0], p.x)
                    bounds[1] = min(bounds[1], p.y)
                    bounds[2] = max(bounds[2], p.x)
                    bounds[3] = max(bounds[3], p.y)
                    behind |= p.z <= 0
    return bounds, behind

def fit_camera(names):
    for attempt in range(20):
        bounds, behind = framing(names)
        if not behind and min(bounds[:2]) >= .045 and max(bounds[2:]) <= .955:
            return {'projected_bounds': bounds, 'lens_mm':cam.data.lens, 'pass':True}
        cam.data.lens *= .96
    raise RuntimeError(f'Unable to frame entire animation: {bounds}')

def render_sequence(name, count, folder):
    target = OUT/'QA'/'ReviewFrames'/folder
    target.mkdir(parents=True, exist_ok=True)
    for i in range(count):
        path=target/f'{i:03d}.png'
        complete = path.exists() and path.stat().st_size > 12
        if complete:
            with path.open('rb') as file:
                file.seek(-12,2)
                complete = file.read() == b'\x00\x00\x00\x00IEND\xaeB`\x82'
        if '--resume' in sys.argv and complete:
            print(f'REVIEW_RESUME {folder} {i+1}/{count}', flush=True)
            continue
        set_action(name, i/(count-1))
        temporary=target/f'{i:03d}.rendering.png'
        scene.render.filepath = str(temporary)
        bpy.ops.render.render(write_still=True)
        temporary.replace(path)
        print(f'REVIEW_FRAME {folder} {i+1}/{count}', flush=True)

report = {'renderer':bpy.app.version_string, 'size':[960,720], 'samples':16,
          'source':'Evaluated baked skeletal actions from the delivered .blend', 'sequences':{}}
camera((.55,-.77,1.0), (0,0,.12), 56)
report['sequences']['Page_RL'] = fit_camera(['CHR_PageTurn_RL','CHR_PageTurn_LR'])
render_sequence('CHR_PageTurn_RL', 72, 'Page_RL')
camera((.62,-.82,1.08), (.025,0,.14), 56)
report['sequences']['Open_Close'] = fit_camera(['CHR_Open','CHR_Close'])
render_sequence('CHR_Open', 61, 'Open')
render_sequence('CHR_Close', 44, 'Close')
(OUT/'QA'/'review_video_render.json').write_text(json.dumps(report,indent=2))
print('REVIEW_RENDER_COMPLETE',flush=True)
