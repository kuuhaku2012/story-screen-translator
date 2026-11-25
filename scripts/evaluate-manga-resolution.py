"""Offline full-page OCR+grouping gate; no manual crops fed to engine."""
import json,time,re,argparse,statistics
from pathlib import Path
import cv2
from rapidocr import RapidOCR
from manga_line_regions import group_lines
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--config',choices=['native960','double960','double1440'],required=True)
args=parser.parse_args();scale=1 if args.config=='native960' else 2;limit=1440 if args.config=='double1440' else 960
out=root/'artifacts/manga-resolution-gate'/args.config;out.mkdir(parents=True,exist_ok=True)
refs23=json.loads((root/'artifacts/manga-regression-23/references.json').read_text(encoding='utf8'))
refs172=json.loads((root/'artifacts/manga-regression-172/region-comparison.json').read_text(encoding='utf8'))['regions']
old=root/'artifacts/multimodal-evaluation'
oldrefs=json.loads((old/'references.json').read_text(encoding='utf8'))
fixtures=[('page23',root/'artifacts/manga-regression-23/input.png',[(i,b,t) for i,b,t in refs23]),
          ('page172',root/'artifacts/manga-regression-172/input.png',[(r['id'],r['bounds'],r['reference']) for r in refs172])]
for page in ['manga-001','manga-002','manga-005']:
    path=old/'inputs'/(page+'-730.png');im=cv2.imread(str(path));h,w=im.shape[:2]
    fixtures.append((page,path,[(r['id'],[r['box'][0]*w,r['box'][1]*h,r['box'][2]*w,r['box'][3]*h],r['text']) for r in oldrefs if r.get('page')==page]))
cv2.setNumThreads(2)
engine=RapidOCR(params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),'Global.log_level':'warning',
 'Det.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
 'Rec.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_rec_small.onnx'),
 'Cls.model_path':str(root/'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
 'Det.limit_type':'max','Det.limit_side_len':limit,
 'EngineConfig.onnxruntime.intra_op_num_threads':2,'EngineConfig.onnxruntime.inter_op_num_threads':1})
def norm(t):return re.sub('[^A-Z0-9]','',t.upper())
def distance(a,b):
    row=list(range(len(b)+1))
    for i,x in enumerate(a,1):
        nxt=[i]
        for j,y in enumerate(b,1):nxt.append(min(nxt[-1]+1,row[j]+1,row[j-1]+(x!=y)))
        row=nxt
    return row[-1]
def area(b):return max(0,b[2]-b[0])*max(0,b[3]-b[1])
def iou(a,b):
    hit=area([max(a[0],b[0]),max(a[1],b[1]),min(a[2],b[2]),min(a[3],b[3])]);return hit/max(1,area(a)+area(b)-hit)
reports=[]
for name,path,refs in fixtures:
    image=cv2.imread(str(path));times=[]
    for repeat in range(3):
        start=time.perf_counter();inp=cv2.resize(image,None,fx=scale,fy=scale,interpolation=cv2.INTER_CUBIC) if scale!=1 else image
        result=engine(inp)
        blocks=[{'text':t,'box':(box/scale).tolist()} for t,box in zip(result.txts,result.boxes)]
        groups=group_lines(blocks)
        for g in groups:
            a=g['box'];lines=[]
            for b in blocks:
                cx=sum(p[0] for p in b['box'])/4;cy=sum(p[1] for p in b['box'])/4
                if a[0]<=cx<=a[2] and a[1]<=cy<=a[3]:lines.append((cy,cx,b['text']))
            g['text']=' '.join(t for _,_,t in sorted(lines))
        times.append(round((time.perf_counter()-start)*1000,2))
    # Greedy one-to-one matching for scoring only, no gold text used for assignment.
    pairs=sorted([(iou(b,g['box']),i,j) for i,(_,b,_) in enumerate(refs) for j,g in enumerate(groups)],reverse=True)
    matches={};used=set()
    for overlap,i,j in pairs:
        if overlap>=.3 and i not in matches and j not in used:matches[i]=(j,overlap);used.add(j)
    details=[]
    for i,(rid,b,text) in enumerate(refs):
        match=matches.get(i);actual=groups[match[0]]['text'] if match else ''
        details.append({'id':rid,'reference':text,'actual':actual,'iou':round(match[1],3) if match else 0,
          'exact':norm(actual)==norm(text),'errors':distance(norm(text),norm(actual)),'chars':len(norm(text))})
    reports.append({'page':name,'ocrResizeGroupingMs':times,'regions':groups,'details':details})
    (out/(name+'.json')).write_text(json.dumps(reports[-1],ensure_ascii=False,indent=2),encoding='utf8')
    print(f'{args.config} {name}: {sum(d["exact"] for d in details)}/{len(details)} exact, {times[-1]}ms',flush=True)
details=[d for r in reports for d in r['details']]
summary={'config':args.config,'distinctRegions':len(details),'exact':sum(d['exact'] for d in details),
 'exactPercent':round(100*sum(d['exact'] for d in details)/len(details),2),
 'cerPercent':round(100*sum(d['errors'] for d in details)/sum(d['chars'] for d in details),2),
 'warmMs':[t for r in reports for t in r['ocrResizeGroupingMs'][1:]],
 'scope':'Development pages, approximate manual rectangles; automatic full-page OCR/grouping, one-to-one IoU scoring. No API, no live app or held-out claim.'}
(out/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf8');print(json.dumps(summary),flush=True)
