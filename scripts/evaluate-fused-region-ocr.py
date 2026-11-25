"""Compare full-page OCR with automatically proposed-region OCR, local only."""
from pathlib import Path
import json,time,re,html,base64
import cv2
from rapidocr import RapidOCR
from bubble_text_regions import fuse_regions
root=Path(__file__).resolve().parents[1];out=root/'artifacts/fused-region-ocr';out.mkdir(exist_ok=True)
old=root/'artifacts/multimodal-evaluation'
proposals=json.loads((root/'artifacts/bubble-proposals/assessment.json').read_text(encoding='utf8'))['reports']
latest=max((root/'artifacts/ocr-diagnostics').glob('*/ocr.json'),key=lambda p:p.stat().st_mtime).parent
paths={'actual-page23':latest/'source.png','page172':root/'artifacts/manga-regression-172/input.png'}
for name in ['manga-001','manga-002','manga-005']:paths[name]=old/f'inputs/{name}-730.png'
oldrefs=json.loads((old/'references.json').read_text(encoding='utf8'))
refs23=json.loads((root/'artifacts/manga-regression-23/references.json').read_text(encoding='utf8'))
refs172=json.loads((root/'artifacts/manga-regression-172/region-comparison.json').read_text(encoding='utf8'))['regions']
cv2.setNumThreads(2)
engine=RapidOCR(params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),'Global.log_level':'warning',
 'Det.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
 'Rec.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_rec_small.onnx'),
 'Cls.model_path':str(root/'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
 'Det.limit_type':'max','Det.limit_side_len':960,
 'EngineConfig.onnxruntime.intra_op_num_threads':2,'EngineConfig.onnxruntime.inter_op_num_threads':1})
def recognize(image,scale=1,offset=(0,0)):
    inp=cv2.resize(image,None,fx=scale,fy=scale,interpolation=cv2.INTER_CUBIC) if scale!=1 else image
    r=engine(inp,use_rec=True,use_cls=True,use_det=True)
    if r.boxes is None:return []
    return [{'text':t,'box':[[float(x)/scale+offset[0],float(y)/scale+offset[1]] for x,y in b]} for t,b in zip(r.txts,r.boxes)]
def center(b):return sum(p[0] for p in b['box'])/4,sum(p[1] for p in b['box'])/4
def norm(t):return re.sub('[^A-Z0-9]','',t.upper())
def distance(a,b):
    row=list(range(len(b)+1))
    for i,x in enumerate(a,1):
        nxt=[i]
        for j,y in enumerate(b,1):nxt.append(min(nxt[-1]+1,row[j]+1,row[j-1]+(x!=y)))
        row=nxt
    return row[-1]
def score(lines,truth):
    details=[]
    for rid,b,text in truth:
        selected=sorted([l for l in lines if b[0]<=center(l)[0]<=b[2] and b[1]<=center(l)[1]<=b[3]],key=lambda l:(center(l)[1],center(l)[0]))
        actual=' '.join(l['text'] for l in selected)
        details.append({'id':rid,'reference':text,'actual':actual,'exact':norm(actual)==norm(text),'errors':distance(norm(text),norm(actual)),'chars':len(norm(text))})
    return details
recognize(cv2.imread(str(paths['actual-page23'])))
reports=[];cards=[]
for page in proposals:
    name=page['page'];image=cv2.imread(str(paths[name]));h,w=image.shape[:2]
    start=time.perf_counter();baseline=recognize(image);baseline_ms=(time.perf_counter()-start)*1000
    start=time.perf_counter();det=engine(image,use_rec=False,use_cls=False)
    lines=[{'text':'','box':b.tolist()} for b in det.boxes] if det.boxes is not None else []
    fused=fuse_regions(page['proposals'],lines);det_ms=(time.perf_counter()-start)*1000
    recognized=[];regions=[];start=time.perf_counter()
    for i,g in enumerate(fused['regions']):
        b=g['regionBounds'];x=max(0,int(b[0])-2);y=max(0,int(b[1])-2);rr=min(w,int(b[2])+3);bb=min(h,int(b[3])+3)
        line_height=(g['textBounds'][3]-g['textBounds'][1])/max(1,g['lineCount'])
        scale=min(3,max(1,32/max(1,line_height)))
        begin=time.perf_counter();result=recognize(image[y:bb,x:rr],scale,(x,y));elapsed=(time.perf_counter()-begin)*1000
        text=' '.join(l['text'] for l in sorted(result,key=lambda l:(center(l)[1],center(l)[0])))
        recognized.extend(result);regions.append({**g,'id':i,'text':text,'crop':[x,y,rr,bb],'scale':scale,'ocrMs':round(elapsed,2)})
    crop_ms=(time.perf_counter()-start)*1000
    if name=='actual-page23':truth=refs23
    elif name=='page172':truth=[(r['id'],r['bounds'],r['reference']) for r in refs172]
    else:truth=[(r['id'],[r['box'][0]*w,r['box'][1]*h,r['box'][2]*w,r['box'][3]*h],r['text']) for r in oldrefs if r.get('page')==name]
    report={'page':name,'baselineMs':round(baseline_ms,2),'detectorFusionMs':round(det_ms,2),'regionRecognitionMs':round(crop_ms,2),
       'detectorAndRecognitionMs':round(det_ms+crop_ms,2),'contourProposalMsExcluded':page['milliseconds'][-1],
       'regions':regions,'baseline':score(baseline,truth),'regionOcr':score(recognized,truth)}
    reports.append(report);(out/(name+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    rows=''.join(f'<tr><td>{i}</td><td>{html.escape(g["kind"])}</td><td>{html.escape(g["text"])}</td></tr>' for i,g in enumerate(regions))
    png=base64.b64encode((root/'artifacts/bubble-text-fusion'/f'{name}.png').read_bytes()).decode()
    cards.append(f'<h2>{html.escape(name)}</h2><p>Detector+crop OCR: {report["detectorAndRecognitionMs"]}ms, contour proposal excluded. Full-page OCR baseline: {report["baselineMs"]}ms.</p><img src="data:image/png;base64,{png}"><table>{rows}</table>')
    print(f'{name}: full {sum(d["exact"] for d in report["baseline"])}/{len(truth)}, regions {sum(d["exact"] for d in report["regionOcr"])}/{len(truth)}, {report["detectorAndRecognitionMs"]}ms',flush=True)
summary={}
for mode in ['baseline','regionOcr']:
    details=[d for r in reports for d in r[mode]]
    summary[mode]={'regions':len(details),'exact':sum(d['exact'] for d in details),'cerPercent':round(100*sum(d['errors'] for d in details)/sum(d['chars'] for d in details),2)}
summary['improved']=sum(a['errors']>b['errors'] for r in reports for a,b in zip(r['baseline'],r['regionOcr']))
summary['regressed']=sum(a['errors']<b['errors'] for r in reports for a,b in zip(r['baseline'],r['regionOcr']))
summary['scope']='Development approximate ROI-center scoring, one warmed run/page. No API/live overlay/end-to-end claim. Detector+recognition excludes cached contour proposals, capture/stability/translation/rendering.'
(out/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf8')
(out/'preview.html').write_text('<meta charset="utf-8"><title>Fused region OCR</title><style>body{font:16px Segoe UI;background:#222;color:#eee}td{border:1px solid #666;padding:8px}img{max-width:700px;width:90%}table{border-collapse:collapse}</style><h1>Chữ nhận dạng từ vùng mới — chưa dịch/API</h1>'+''.join(cards),encoding='utf8')
print(json.dumps(summary),flush=True)
