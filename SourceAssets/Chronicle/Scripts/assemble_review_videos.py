"""Assemble Blender PNGs into iPhone-playable H.264 review movies.

Requires ordinary Python, Pillow and ffmpeg. The Blender clips remain at their
specified durations; the page-turn review slows their playback for inspection.
"""
from pathlib import Path
from PIL import Image
import subprocess, json, tempfile, math

OUT = Path(__file__).resolve().parent.parent
SOURCE = OUT/'QA'/'ReviewFrames'
RATE = 24
cache = {}
def crop_for(folders):
    bounds=[960,720,0,0]
    for folder in folders:
        for path in (SOURCE/folder).glob('*.png'):
            bbox=Image.open(path).getchannel('A').getbbox()
            if bbox:
                bounds=[min(bounds[0],bbox[0]),min(bounds[1],bbox[1]),
                        max(bounds[2],bbox[2]),max(bounds[3],bbox[3])]
    cx=(bounds[0]+bounds[2])/2;cy=(bounds[1]+bounds[3])/2
    width=max(bounds[2]-bounds[0]+44,(bounds[3]-bounds[1]+44)*4/3)
    height=width*3/4
    x=max(0,min(960-width,cx-width/2));y=max(0,min(720-height,cy-height/2))
    return (math.floor(x),math.floor(y),math.ceil(x+width),math.ceil(y+height))

for folder,count in [('Page_RL',72),('Open',61),('Close',44)]:
    assert all((SOURCE/folder/f'{i:03d}.png').exists() for i in range(count)),folder
page_crop=crop_for(['Page_RL']);cover_crop=crop_for(['Open','Close'])
crops={'Page_RL':page_crop,'Open':cover_crop,'Close':cover_crop}

with tempfile.TemporaryDirectory(prefix='chronicle-movie-') as tmp:
    tmp = Path(tmp)
    def flat(folder, i):
        key=(folder,i)
        if key not in cache:
            image=Image.open(SOURCE/folder/f'{i:03d}.png').convert('RGBA')
            image=image.crop(crops[folder]).resize((960,720),Image.Resampling.LANCZOS)
            background=Image.new('RGBA',image.size,(24,25,24,255))
            background.alpha_composite(image)
            path=tmp/f'{folder}_{i:03d}.png'
            background.convert('RGB').save(path)
            cache[key]=path
        return cache[key]

    def indices(folder,count,seconds,reverse=False):
        n=round(seconds*RATE)
        ix=[round(i/(n-1)*(count-1)) for i in range(n)]
        if reverse: ix=ix[::-1]
        return [flat(folder,i) for i in ix]

    def hold(folder,i,seconds):
        return [flat(folder,i)]*round(seconds*RATE)

    def movie(filename,frames):
        folder=tmp/filename.removesuffix('.mp4');folder.mkdir()
        for i,source in enumerate(frames):
            (folder/f'{i:05d}.png').hardlink_to(source)
        target=OUT/'Preview'/filename
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y',
            '-framerate',str(RATE),'-i',str(folder/'%05d.png'),
            '-vf','hqdn3d=1.5:1.5:4.5:4.5',
            '-c:v','libx264','-crf','19','-preset','medium','-pix_fmt','yuv420p',
            '-movflags','+faststart',str(target)],check=True)
        probe=json.loads(subprocess.check_output(['ffprobe','-v','error',
            '-show_entries','format=duration:stream=codec_name,width,height,nb_frames',
            '-of','json',str(target)]))
        assert abs(float(probe['format']['duration'])-len(frames)/RATE)<.001
        return {'file':filename,'duration_seconds':len(frames)/RATE,
                'frames':len(frames),'probe':probe,
                'processing':'Common crop for each fixed camera, Lanczos resize and light temporal noise reduction',
                'crop_pixels':cover_crop if 'Open_Close' in filename else page_crop}

    single=indices('Page_RL',72,3)
    page=(hold('Page_RL',0,2)+single+hold('Page_RL',71,1)
          +indices('Page_RL',72,3,reverse=True)+hold('Page_RL',0,2)
          +single+hold('Page_RL',71,2))
    open_close=(hold('Open',0,2)+indices('Open',61,2.5)
        +hold('Open',60,3.2083333333)+indices('Close',44,43/24)
        +hold('Close',43,2.5))
    result=[movie('PageTurn_RL.mp4',single),
            movie('Chronicle_Page_Turn_Review.mp4',page),
            movie('Chronicle_Open_Close_Review.mp4',open_close)]
    (OUT/'QA'/'review_video_validation.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))
