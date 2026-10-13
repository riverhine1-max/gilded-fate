"""Measured character/word alignment for final narration WAVs; no typing-speed estimates.

Dependencies: numpy, onnxruntime, huggingface_hub, faster-whisper (audio decoding + independent ASR).
Input: list of {lineId,text,url}; final mono conversion exactly matches prepare_recordings.py.
The CTC model measures at approximately 20 ms resolution. Listening review remains separate.
"""
import hashlib,json,re,sys,tempfile,urllib.request,wave
from pathlib import Path
import numpy as np
import onnxruntime as ort
from huggingface_hub import hf_hub_download
from faster_whisper import WhisperModel
from faster_whisper.audio import decode_audio

REPO='onnx-community/wav2vec2-base-960h-ONNX'
vocab=json.loads(Path(hf_hub_download(REPO,'vocab.json')).read_text())
config=json.loads(Path(hf_hub_download(REPO,'config.json')).read_text())
options=ort.SessionOptions();options.intra_op_num_threads=4;options.inter_op_num_threads=1
session=ort.InferenceSession(hf_hub_download(REPO,'onnx/model.onnx'),sess_options=options,providers=['CPUExecutionProvider'])
asr=WhisperModel('small.en',device='cpu',compute_type='int8',cpu_threads=4)
blank=vocab['<pad>'];reverse={value:key for key,value in vocab.items()}
stride=int(np.prod(config['conv_stride']))
orthographic={'TRAVELLER':'TRAVELER','TRAVELLED':'TRAVELED'}
def normalized(word):
    value=''.join(c.upper() for c in word if c.isalnum())
    return orthographic.get(value,value)

def align(logits,text,duration):
    canonical=text.split()
    letters=[''.join(c for c in word.upper() if c in vocab and len(c)==1) for word in canonical]
    assert all(letters), 'Canonical token has no alignable spoken letters.'
    target='|'.join(letters);tokens=np.array([vocab[c] for c in target],dtype=np.int32)
    extended=np.full(2*len(tokens)+1,blank,dtype=np.int32);extended[1::2]=tokens
    shifted=logits-np.max(logits,axis=1,keepdims=True)
    logp=shifted-np.log(np.exp(shifted).sum(axis=1,keepdims=True))
    frames,states=len(logp),len(extended)
    assert frames>=len(tokens), 'Recording too short for canonical text.'
    previous=np.full(states,-np.inf,dtype=np.float32)
    previous[0]=logp[0,blank];previous[1]=logp[0,extended[1]]
    back=np.zeros((frames,states),dtype=np.uint8)
    skip=np.zeros(states,dtype=bool)
    skip[2:]=(extended[2:]!=blank)&(extended[2:]!=extended[:-2])
    for frame in range(1,frames):
        stay=previous
        next_state=np.concatenate(([-np.inf],previous[:-1]))
        jump=np.concatenate(([-np.inf,-np.inf],previous[:-2]))
        jump=np.where(skip,jump,-np.inf)
        choices=np.stack((stay,next_state,jump))
        best=np.argmax(choices,axis=0)
        back[frame]=best
        previous=np.max(choices,axis=0)+logp[frame,extended]
    state=states-1 if previous[-1]>previous[-2] else states-2
    assert np.isfinite(previous[state]), 'No valid CTC alignment path.'
    path=np.empty(frames,dtype=np.int32)
    for frame in range(frames-1,-1,-1):
        path[frame]=state
        if frame: state-=int(back[frame,state])
    words=[];characters=[];offset=0
    for original,spelling in zip(canonical,letters):
        indices=[];scores=[];word_characters=[]
        for index,char in enumerate(spelling):
            character_index=offset+index
            observed=np.flatnonzero(path==2*character_index+1)
            assert observed.size, 'Canonical character has no measured frame.'
            start=float(observed[0]*stride/16000);end=min(duration,float((observed[-1]+1)*stride/16000))
            score=float(np.exp(logp[observed,tokens[character_index]]).mean())
            word_characters.append(dict(letter=char,startSeconds=start,endSeconds=end,confidence=score))
            indices.extend(observed.tolist());scores.append(score)
        words.append(dict(text=original,startSeconds=min(indices)*stride/16000,
                          endSeconds=min(duration,(max(indices)+1)*stride/16000),confidence=float(np.mean(scores))))
        characters.append(word_characters)
        offset+=len(spelling)+1
    greedy=logits.argmax(axis=1)
    decoded=[];last=None
    for token in greedy:
        token=int(token)
        if token!=last and token!=blank: decoded.append(reverse[token])
        last=token
    decoded=''.join(decoded).replace('|',' ').strip()
    return words,characters,decoded

requests=json.loads(Path(sys.argv[1]).read_text());results=[]
with tempfile.TemporaryDirectory() as work:
    for request in requests:
        name=request['lineId'];source=Path(work)/(name+'_source.wav');final=Path(work)/(name+'.wav')
        with urllib.request.urlopen(request['url'],timeout=30) as response:source.write_bytes(response.read())
        with wave.open(str(source),'rb') as wav:
            channels=wav.getnchannels();rate=wav.getframerate();width=wav.getsampwidth();count=wav.getnframes()
            assert rate==48000 and width==2
            samples=np.frombuffer(wav.readframes(count),dtype='<i2').reshape(-1,channels)
        mono=np.rint(samples.astype(np.float64).mean(axis=1)).astype(np.int32)*256
        if request.get('cropFrames'):
            start,end=request['cropFrames']
            mono=mono[start:end]
            count=len(mono)
        packed=np.column_stack((mono&255,(mono>>8)&255,(mono>>16)&255)).astype(np.uint8).tobytes()
        with wave.open(str(final),'wb') as wav:
            wav.setnchannels(1);wav.setsampwidth(3);wav.setframerate(rate);wav.writeframes(packed)
        duration=count/rate
        audio=decode_audio(str(final),sampling_rate=16000).astype(np.float32)
        values=(audio-audio.mean())/np.sqrt(audio.var()+1e-7)
        values=np.pad(values,(16000,16000))
        logits=session.run(None,{'input_values':values[None,:]})[0][0]
        words,characters,decoded=align(logits,request['text'],duration+2)
        for word in words:
            word['startSeconds']=max(0,word['startSeconds']-1)
            word['endSeconds']=min(duration,word['endSeconds']-1)
        segments,_=asr.transcribe(str(final),language='en',beam_size=5,word_timestamps=False,vad_filter=True)
        recognized=' '.join(s.text.strip() for s in segments)
        canonical=request['text'].split()
        lexical_match=[normalized(w) for w in recognized.split()]==[normalized(w) for w in canonical]
        ctc_match=[normalized(w) for w in decoded.split()]==[normalized(w) for w in canonical]
        previous=0;valid=True
        for word in words:
            valid=valid and 0<=previous<=word['startSeconds']<word['endSeconds']<=duration
            previous=word['endSeconds']
        results.append(dict(lineId=name,durationSeconds=duration,audioSHA256=hashlib.sha256(final.read_bytes()).hexdigest(),
            words=words,characterAlignmentMeasured=True,recognizedText=recognized,ctcDecodedText=decoded,
            lexicalMatch=lexical_match,ctcMatch=ctc_match,canonicalCoverage=len(words)==len(canonical),validIntervals=valid,
            minimumWordConfidence=min(w['confidence'] for w in words),
            method='wav2vec2-base-960h full precision ONNX CTC Viterbi forced alignment; independent small.en ASR',
            frameResolutionSeconds=stride/16000,manualListeningReview='PENDING',
            orthographicASREquivalents=orthographic,
            readyForAutomaticSync=valid and (lexical_match or ctc_match)))
print(json.dumps(results,separators=(',',':')))

