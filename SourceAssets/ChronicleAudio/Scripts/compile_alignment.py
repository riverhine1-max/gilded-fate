"""Compile measured word intervals; never infer timestamps or create audio.

Usage: python compile_alignment.py Opening.json recordings/ measured_words.json ready_index.json
measured_words.json: {"OPENING_001": {"words": [{"startSeconds": ..., "endSeconds": ...}, ...]}}
One measured interval per whitespace-delimited canonical word, punctuation included.
The input WAVs must be the final mono, 48 kHz PCM recordings. Record automatic
alignment separately from the final human listening review; never mark that review
complete until someone has actually checked the sound.
"""
import hashlib, json, math, re, sys, wave
from pathlib import Path

def utf16_len(text):
    return len(text.encode('utf-16-le')) // 2

def compile_index(template, recordings, measured):
    ready=[]
    for line in template['lines']:
        if line['lineId'] not in measured:
            continue
        filename=line['lineId']+'.wav'
        candidates=[recordings/filename, recordings/line['speaker'].title()/filename, recordings/'Dialogue'/line['speaker'].title()/filename]
        path=next((candidate for candidate in candidates if candidate.is_file()), candidates[0])
        with wave.open(str(path),'rb') as clip:
            assert clip.getnchannels()==1, f'{path}: dialogue must be mono'
            assert clip.getframerate()==48000, f'{path}: use the final 48 kHz recording'
            assert clip.getsampwidth() in (2,3), f'{path}: use 16/24-bit PCM'
            duration=clip.getnframes()/clip.getframerate()
        intervals=measured[line['lineId']]['words']
        tokens=list(re.finditer(r'\S+',line['text']))
        assert len(intervals)==len(tokens), f'{path}: canonical word coverage differs'
        words=[];previous=0
        for token,timing in zip(tokens,intervals):
            start=float(timing['startSeconds']);end=float(timing['endSeconds'])
            assert math.isfinite(start) and math.isfinite(end) and 0<=previous<=start<end<=duration, f'{path}: invalid measured interval'
            if 'text' in timing:
                assert timing['text']==token.group(), f'{path}: words must match exactly'
            words.append(dict(text=token.group(),charStart=utf16_len(line['text'][:token.start()]),charLength=utf16_len(token.group()),startSeconds=start,endSeconds=end))
            previous=end
        result=dict(line,ready=True,durationSeconds=duration,words=words,audioSHA256=hashlib.sha256(path.read_bytes()).hexdigest(),textSHA256=hashlib.sha256(line['text'].encode('utf-8')).hexdigest())
        result.pop('status',None)
        ready.append(result)
    assert ready, 'No measured lines supplied. Missing timings must remain unavailable.'
    return dict(schemaVersion=1,lines=ready)

if __name__=='__main__':
    source,recordings,alignment,output=map(Path,sys.argv[1:])
    data=compile_index(json.loads(source.read_text(encoding='utf-8')),recordings,json.loads(alignment.read_text(encoding='utf-8')))
    output.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print(f'Compiled {len(data["lines"])} real recordings using supplied measured intervals.')
