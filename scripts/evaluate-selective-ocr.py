"""Bounded automatic crop rerecognition. References used only after inference for scoring."""
import json,time,re,statistics
from pathlib import Path
import cv2
from rapidocr import RapidOCR
from manga_line_regions import group_lines
root=Path(__file__).resolve().parents[1];out=root/'artifacts/selective-ocr-gate';out.mkdir(parents=True,exist_ok=True)
old=root/'artifacts/multimodal-evaluation'
refs=json.loads((old/'references.json').read_text(encoding='utf8'))
fixtures=[]
for name in ['23','172']:
    folder=root/('artifacts/manga-regression-'+name)
    data=json.loads((folder/('references.json' if name=='23' else 'region-comparison.json')).read_text(encoding='utf8'))
    truth=[{'id':i,'box':b,'text':t} for i,b,t in data] if name=='23' else [{'id':r['id'],'box':r['bounds'],'text':r['reference']} for r in data['regions']]
    fixtures.append(('page'+name,folder/'input.png',truth))
for name in ['manga-001','manga-002','manga-005']:
    path=old/'inputs'/(name+'-730.png');image=cv2.imread(str(path));h,w=image.shape[:2]
    truth=[{'id':r['id'],'box':[r['box'][0]*w,r['box'][1]*h,r['box'][2]*w,r['box'][3]*h],'text':r['text']} for r in refs if r.get('page')==name]
    fixtures.append((name,path,truth))
cv2.setNumThreads(2)
engine=RapidOCR(params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),'Global.log_level':'warning',
 'Det.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
 'Rec.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_rec_small.onnx'),
 'Cls.model_path':str(root/'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
 'Det.limit_type':'max','Det.limit_side_len':960,
 'EngineConfig.onnxruntime.intra_op_num_threads':2,'EngineConfig.onnxruntime.inter_op_num_threads':1})
def recognize(image,scale=1,offset=(0,0)):
    inp=cv2.resize(image,None,fx=scale,fy=scale,interpolation=cv2.INTER_CUBIC) if scale!=1 else image
    r=engine(inp)
    if r.boxes is None:return []
    return [{'text':t,'score':float(s),'box':[[float(x)/scale+offset[0],float(y)/scale+offset[1]] for x,y in b]} for t,s,b in zip(r.txts,r.scores,r.boxes)]
def center(b):return (sum(p[0] for p in b['box'])/4,sum(p[1] for p in b['box'])/4)
def inside(p,b):return b[0]<=p[0]<=b[2] and b[1]<=p[1]<=b[3]
def norm(t):return re.sub('[^A-Z0-9]','',t.upper())
def distance(a,b):
    row=list(range(len(b)+1))
    for i,x in enumerate(a,1):
        nxt=[i]
        for j,y in enumerate(b,1):nxt.append(min(nxt[-1]+1,row[j]+1,row[j-1]+(x!=y)))
        row=nxt
    return row[-1]
def score(blocks,truth):
    groups=group_lines(blocks);details=[]
    for ref in truth:
        lines=sorted([b for b in blocks if inside(center(b),ref['box'])],key=lambda b:(center(b)[1],center(b)[0]))
        actual=' '.join(b['text'] for b in lines)
        hits=[i for i,g in enumerate(groups) if any(inside(center(b),g['box']) for b in lines)]
        details.append({'id':ref['id'],'reference':ref['text'],'actual':actual,'exact':norm(actual)==norm(ref['text']),
          'errors':distance(norm(ref['text']),norm(actual)),'chars':len(norm(ref['text'])),'groups':hits,'hasDetectedText':bool(lines)})
    merges=sum(sum(i in d['groups'] for d in details)>1 for i in range(len(groups)))
    return {'details':details,'splitReferences':sum(len(d['groups'])>1 for d in details),'crossReferenceGroups':merges,
            'noDetectedTextReferences':sum(not d['hasDetectedText'] for d in details)}
# Warm engine on existing data, excluded from every page timing.
recognize(cv2.imread(str(fixtures[0][1])))
reports=[]
for name,path,truth in fixtures:
    image=cv2.imread(str(path));h,w=image.shape[:2];start=time.perf_counter();blocks=recognize(image);base_ms=(time.perf_counter()-start)*1000
    groups=group_lines(blocks);assigned=set();candidates=[]
    for index,g in enumerate(groups):
        members=[i for i,b in enumerate(blocks) if i not in assigned and inside(center(b),g['box'])]
        assigned.update(members)
        if members:
            low=min(blocks[i]['score'] for i in members)
            if low<.90:candidates.append((low,index,members))
    # Fixed BEFORE viewing results: three lowest-confidence groups below0.90.
    selected=sorted(candidates)[:3];replaced=set();added=[];attempts=[];start=time.perf_counter()
    for low,index,members in selected:
        b=groups[index]['box'];line_heights=[max(p[1] for p in blocks[i]['box'])-min(p[1] for p in blocks[i]['box']) for i in members]
        pad=max(3,round(statistics.median(line_heights)*.25))
        x=max(0,int(b[0])-pad);y=max(0,int(b[1])-pad);right=min(w,int(b[2])+pad+1);bottom=min(h,int(b[3])+pad+1)
        factor=min(3,max(1,40/max(1,statistics.median(line_heights))))
        begin=time.perf_counter();retry=recognize(image[y:bottom,x:right],factor,(x,y));ms=(time.perf_counter()-begin)*1000
        before=' '.join(blocks[i]['text'] for i in members);after=' '.join(z['text'] for z in sorted(retry,key=lambda z:(center(z)[1],center(z)[0])))
        old_mean=statistics.mean(blocks[i]['score'] for i in members);new_mean=statistics.mean(z['score'] for z in retry) if retry else 0
        length_ratio=len(norm(after))/max(1,len(norm(before)))
        accept=bool(retry) and new_mean>=old_mean+.03 and .65<=length_ratio<=1.5
        if accept:replaced.update(members);added.extend(retry)
        attempts.append({'group':index,'crop':[x,y,right,bottom],'scale':factor,'before':before,'after':after,'oldMean':old_mean,'newMean':new_mean,'accepted':accept,'ms':round(ms,2)})
    extra_ms=(time.perf_counter()-start)*1000
    final=[b for i,b in enumerate(blocks) if i not in replaced]+added
    report={'page':name,'baselineMs':round(base_ms,2),'extraMs':round(extra_ms,2),'totalMs':round(base_ms+extra_ms,2),
      'attempts':attempts,'baseline':score(blocks,truth),'selective':score(final,truth)}
    reports.append(report);(out/(name+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print(f'{name}: {len(attempts)} retries, {sum(a["accepted"] for a in attempts)} accepted, {report["totalMs"]}ms',flush=True)
summary={}
for mode in ['baseline','selective']:
    details=[d for r in reports for d in r[mode]['details']]
    summary[mode]={'regions':len(details),'exact':sum(d['exact'] for d in details),'cerPercent':round(100*sum(d['errors'] for d in details)/sum(d['chars'] for d in details),2),
       **{k:sum(r[mode][k] for r in reports) for k in ['splitReferences','crossReferenceGroups','noDetectedTextReferences']}}
summary['improvedRegions']=sum(b['errors']>s['errors'] for r in reports for b,s in zip(r['baseline']['details'],r['selective']['details']))
summary['regressedRegions']=sum(b['errors']<s['errors'] for r in reports for b,s in zip(r['baseline']['details'],r['selective']['details']))
summary['scope']='ROI-center-associated text; no IoU cut-off. Geometry counts reported separately. Approximate development rectangles, not pure recognition-only accuracy or complete page detection recall. One warmed run/page, no latency percentiles.'
(out/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf8');print(json.dumps(summary),flush=True)
