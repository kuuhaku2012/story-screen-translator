"""Offline bright enclosed-region proposals. No OCR seeds/gold coordinates in detection."""
from pathlib import Path
import json,time,html,base64
import cv2
import numpy as np
root=Path(__file__).resolve().parents[1];out=root/'artifacts/bubble-proposals';out.mkdir(parents=True,exist_ok=True)
latest=max((root/'artifacts/ocr-diagnostics').glob('*/ocr.json'),key=lambda p:p.stat().st_mtime).parent
fixtures=[('actual-page23',latest/'source.png'),('page172',root/'artifacts/manga-regression-172/input.png')]
for name in ['manga-001','manga-002','manga-005']:
    fixtures.append((name,root/f'artifacts/multimodal-evaluation/inputs/{name}-730.png'))
refs=json.loads((root/'artifacts/multimodal-evaluation/references.json').read_text(encoding='utf8'))
refs23=json.loads((root/'artifacts/manga-regression-23/references.json').read_text(encoding='utf8'))
refs172=json.loads((root/'artifacts/manga-regression-172/region-comparison.json').read_text(encoding='utf8'))['regions']
def detect(image):
    gray=cv2.cvtColor(image,cv2.COLOR_BGR2GRAY);h,w=gray.shape
    # No morphology across outlines: crossing a thin balloon border merges background/art.
    binary=(gray>210).astype(np.uint8)*255
    contours,hierarchy=cv2.findContours(binary,cv2.RETR_CCOMP,cv2.CHAIN_APPROX_SIMPLE)
    proposals=[]
    if hierarchy is None:return proposals
    tree=hierarchy[0]
    for i,c in enumerate(contours):
        if tree[i][3]!=-1:continue
        x,y,bw,bh=cv2.boundingRect(c);a=cv2.contourArea(c)
        if a<400 or a>w*h*.15 or bw<25 or bh<18:continue
        if x<=0 or y<=0 or x+bw>=w or y+bh>=h:continue
        if a/(bw*bh)<.42:continue
        hull=cv2.contourArea(cv2.convexHull(c))
        if a/max(1,hull)<.65:continue
        child=tree[i][2];holes=[]
        while child!=-1:
            ha=cv2.contourArea(contours[child])
            if ha>=2:holes.append(ha)
            child=tree[child][0]
        dark=sum(holes)/max(1,a)
        if len(holes)<2 or not .008<=dark<=.40 or max(holes,default=0)>a*.3:continue
        proposals.append({'box':[x,y,x+bw,y+bh],'area':round(a,1),'holeCount':len(holes),'darkFraction':round(dark,4)})
    return sorted(proposals,key=lambda p:(p['box'][1],p['box'][0]))
reports=[];cards=[]
for name,path in fixtures:
    image=cv2.imread(str(path));h,w=image.shape[:2];times=[]
    for _ in range(3):
        start=time.perf_counter();proposals=detect(image);times.append(round((time.perf_counter()-start)*1000,3))
    if name=='actual-page23':truth=[{'id':i,'box':b,'text':t} for i,b,t in refs23]
    elif name=='page172':truth=[{'id':r['id'],'box':r['bounds'],'text':r['reference']} for r in refs172]
    else:truth=[{'id':r['id'],'box':[r['box'][0]*w,r['box'][1]*h,r['box'][2]*w,r['box'][3]*h],'text':r['text']} for r in refs if r.get('page')==name]
    coverage=[]
    for ref in truth:
        a=ref['box'];aa=(a[2]-a[0])*(a[3]-a[1]);rank=[]
        for i,p in enumerate(proposals):
            b=p['box'];intersection=max(0,min(a[2],b[2])-max(a[0],b[0]))*max(0,min(a[3],b[3])-max(a[1],b[1]))
            rank.append((intersection/max(1,aa),i))
        fraction,index=max(rank,default=(0,None))
        cx=(a[0]+a[2])/2;cy=(a[1]+a[3])/2
        centers=[i for i,p in enumerate(proposals) if p['box'][0]<=cx<=p['box'][2] and p['box'][1]<=cy<=p['box'][3]]
        coverage.append({'id':ref['id'],'coverage':round(fraction,3),'proposal':index,'centerProposals':centers})
    touched={c['proposal'] for c in coverage if c['coverage']>=.8}
    report={'page':name,'milliseconds':times,'proposals':proposals,'referenceCoverage80':sum(c['coverage']>=.8 for c in coverage),'referenceCentersCovered':sum(bool(c['centerProposals']) for c in coverage),'references':len(truth),
      'proposalsWithoutReferenceMatch':len(proposals)-len(touched),'coverage':coverage}
    reports.append(report)
    drawn=image.copy()
    for i,p in enumerate(proposals):
        x,y,r,b=p['box'];cv2.rectangle(drawn,(x,y),(r,b),(0,0,255),1);cv2.putText(drawn,str(i),(x,y+12),cv2.FONT_HERSHEY_SIMPLEX,.4,(0,0,255),1)
    cv2.imwrite(str(out/(name+'.png')),drawn)
    data=base64.b64encode((out/(name+'.png')).read_bytes()).decode()
    cards.append(f'<h2>{html.escape(name)}</h2><p>{report["referenceCoverage80"]}/{len(truth)} text references covered ≥80%; {len(proposals)} proposals. Coverage is not precision or bubble IoU.</p><img src="data:image/png;base64,{data}">')
    print(f'{name}: {report["referenceCoverage80"]}/{len(truth)} coverage, {len(proposals)} proposals, {times[-1]}ms',flush=True)
payload={'method':'bright enclosed component + shape/hole filters, fixed thresholds, no OCR or reference input','reports':reports,
 'limits':'Approximate text reference coverage only; not balloon boundaries. Connected/open/dark balloons, floating captions and art false positives need review. Same supplied-page development data; no held-out or production claim.'}
(out/'assessment.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf8')
(out/'preview.html').write_text('<meta charset="utf-8"><title>Bubble proposals</title><style>body{background:#222;color:#eee;font:16px Segoe UI}img{max-width:900px;width:90%}</style><h1>Đề xuất vùng sáng kín — thử nghiệm, chưa tích hợp</h1>'+''.join(cards),encoding='utf8')
