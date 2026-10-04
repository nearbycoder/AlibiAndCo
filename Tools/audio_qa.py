#!/usr/bin/env python3
"""Audio QA for Assets/Resources/Audio: loudness (BS.1770, LUFS), peak, DC offset, clipped samples,
and for loops (music_*, amb_*) the jump at the loop point compared with a typical sample step.

    .venv/bin/python Tools/audio_qa.py
"""
import os, wave, numpy as np
from scipy.signal import lfilter
D=os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Assets', 'Resources', 'Audio')
def load(p):
    with wave.open(p) as w:
        sr=w.getframerate(); ch=w.getnchannels()
        a=np.frombuffer(w.readframes(w.getnframes()),dtype=np.int16).astype(np.float64)/32768
    return sr, a.reshape(-1,ch)
def kweight(x, sr):
    # BS.1770 K-weighting (48k coefficients are close enough at 44.1k for a relative check)
    b1=[1.53512485958697,-2.69169618940638,1.19839281085285]; a1=[1,-1.69065929318241,0.73248077421585]
    b2=[1,-2,1]; a2=[1,-1.99004745483398,0.99007225036621]
    return lfilter(b2,a2,lfilter(b1,a1,x,axis=0),axis=0)
def lufs(x, sr):
    y=kweight(x,sr); blk=int(0.4*sr); hop=int(0.1*sr)
    if len(y)<blk: ms=(y**2).mean(0).sum(); return -0.691+10*np.log10(ms+1e-12)
    vals=[]
    for s in range(0,len(y)-blk+1,hop):
        ms=(y[s:s+blk]**2).mean(0).sum(); vals.append(ms)
    vals=np.array(vals); l=-0.691+10*np.log10(vals+1e-12)
    g=vals[l>-70]; 
    if len(g)==0: return -99
    rel=-0.691+10*np.log10(g.mean())-10
    g2=vals[(l>-70)&(l>rel)]
    return -0.691+10*np.log10(g2.mean())
rows=[]
for f in sorted(os.listdir(D)):
    if not f.endswith('.wav'): continue
    sr,a=load(os.path.join(D,f))
    peak=np.abs(a).max(); dc=a.mean(0).max()
    clip=(np.abs(a)>0.999).sum()
    seam=''
    if f.startswith(('music','amb')):
        # jump at loop point relative to typical sample-to-sample step
        jump=np.abs(a[0]-a[-1]).max(); typ=np.median(np.abs(np.diff(a,axis=0)))*1+1e-6
        seam=f'seam {jump:.4f} ({jump/typ:.0f}x typ)'
    rows.append((f, len(a)/sr, lufs(a,sr), 20*np.log10(peak+1e-9), dc, clip, seam))
for r in rows: print(f'{r[0]:28s} {r[1]:6.2f}s  {r[2]:6.1f} LUFS  peak {r[3]:6.1f} dBFS  dc {r[4]:+.4f}  clip {r[5]:3d}  {r[6]}')
