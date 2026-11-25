"""Automatic local region detection and cached Vision replay; no API calls or manual region assignment."""
from pathlib import Path
import argparse
import base64
import difflib
import html
import json
import time
import cv2
import numpy as np
import onnxruntime as ort
import psutil
from manga_line_regions import group_lines

parser=argparse.ArgumentParser()
parser.add_argument('--backend',choices=['comic-detector','line-geometry','line-detector'],default='line-detector')
parser.add_argument('--box-threshold',type=float,default=.5)
args=parser.parse_args()

root = Path(__file__).resolve().parents[1]
data = root / 'artifacts/manga-regression-172'
out = data / ('local-regions-'+args.backend)
if args.box_threshold!=.5: out=data / ('local-regions-'+args.backend+'-'+str(args.box_threshold))
out.mkdir(parents=True, exist_ok=True)
image = cv2.imread(str(data/'input.png'))
h,w = image.shape[:2]
cv2.setNumThreads(2)
options=ort.SessionOptions()
options.intra_op_num_threads=2
options.inter_op_num_threads=1
options.graph_optimization_level=ort.GraphOptimizationLevel.ORT_ENABLE_ALL
ocr=json.loads((data/'rapid-comparison.json').read_text(encoding='utf-8'))[-1]
if args.backend=='comic-detector':
    start=time.perf_counter()
    session=ort.InferenceSession(str(root/'models/ocr-evaluation/comic-text-detector/comictextdetector.pt.onnx'),
                               sess_options=options,providers=['CPUExecutionProvider'])
    load_ms=(time.perf_counter()-start)*1000
    ratio=1024/max(w,h)
    small=cv2.resize(cv2.cvtColor(image,cv2.COLOR_BGR2RGB),(round(w*ratio),round(h*ratio)))
    padded=np.zeros((1024,1024,3),dtype=np.uint8)
    padded[:small.shape[0],:small.shape[1]]=small
    tensor=np.ascontiguousarray(padded.transpose(2,0,1)[None].astype(np.float32)/255)
    times=[]
    for _ in range(3):
        start=time.perf_counter()
        prediction=session.run(['blk'],{'images':tensor})[0][0]
        times.append(round((time.perf_counter()-start)*1000,2))
    scores=(prediction[:,5:]*prediction[:,4:5]).max(axis=1)
    ids=np.flatnonzero(scores>.4)
    rectangles=[[float(x-bw/2),float(y-bh/2),float(bw),float(bh)] for x,y,bw,bh in prediction[ids,:4]]
    keep=cv2.dnn.NMSBoxes(rectangles,scores[ids].astype(float).tolist(),.4,.35)
    keep=np.asarray(keep).reshape(-1) if len(keep) else []
    regions=[]
    for k in keep:
        x,y,bw,bh=rectangles[int(k)]
        box=[max(0,x/ratio),max(0,y/ratio),min(w,(x+bw)/ratio),min(h,(y+bh)/ratio)]
        if box[2]<=box[0] or box[3]<=box[1]: continue
        regions.append({'box':[round(v,2) for v in box],'confidence':round(float(scores[ids[int(k)]]),4),'lines':[]})
elif args.backend=='line-detector':
    from rapidocr import RapidOCR, OCRVersion, ModelType
    engine=RapidOCR(params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),
                           'Global.log_level':'warning','Det.ocr_version':OCRVersion.PPOCRV5,
                           'Det.model_type':ModelType.MOBILE,'Det.limit_type':'max','Det.limit_side_len':1440,
                           'EngineConfig.onnxruntime.intra_op_num_threads':2,
                           'EngineConfig.onnxruntime.inter_op_num_threads':1})
    doubled=cv2.resize(image,None,fx=2,fy=2,interpolation=cv2.INTER_CUBIC)
    load_ms=0
    times=[]
    for _ in range(3):
        start=time.perf_counter()
        detected=engine(doubled,use_rec=False,use_cls=False,box_thresh=args.box_threshold)
        blocks=[{'text':'','box':(box/2).tolist()} for box in detected.boxes] if detected.boxes is not None else []
        regions=group_lines(blocks)
        times.append(round((time.perf_counter()-start)*1000,2))
else:
    load_ms=0
    times=[]
    for _ in range(3):
        start=time.perf_counter()
        regions=group_lines(ocr['blocks'])
        times.append(round((time.perf_counter()-start)*1000,2))
regions.sort(key=lambda r:(r['box'][1],r['box'][0]))
for i,r in enumerate(regions,1): r['id']=f'R{i:03}'

def area(b): return max(0,b[2]-b[0])*max(0,b[3]-b[1])
def intersection(a,b): return area([max(a[0],b[0]),max(a[1],b[1]),min(a[2],b[2]),min(a[3],b[3])])
def iou(a,b):
    hit=intersection(a,b)
    return hit/max(1,area(a)+area(b)-hit)
def norm(s): return ''.join(c for c in s.upper() if c.isascii() and c.isalnum())

# Replay cached independent local OCR. Its latency is reported separately, not hidden in detector timing.
unassigned=[]
for line in ocr['blocks']:
    points=line['box']
    b=[min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)]
    ranked=sorted(((intersection(b,r['box'])/max(1,area(b)),r) for r in regions),key=lambda p:p[0],reverse=True)
    if ranked and ranked[0][0]>=.5:
        ranked[0][1]['lines'].append({'text':line['text'],'box':b})
    else: unassigned.append(line['text'])
for r in regions:
    r['source']=' '.join(t['text'] for t in sorted(r['lines'],key=lambda t:(t['box'][1],t['box'][0])))
vision=json.loads(json.loads((data/'vision-response.json').read_text(encoding='utf-8-sig'))['content'])['regions']
# Conservative one-to-one replay matching. Combined/ambiguous transcripts remain unresolved.
candidates=[]
for v in vision:
    ranked=sorted(((difflib.SequenceMatcher(None,norm(v['source']),norm(r['source'])).ratio(),r['id']) for r in regions),reverse=True)
    if ranked:
        best=ranked[0]
        margin=best[0]-(ranked[1][0] if len(ranked)>1 else 0)
        length_ratio=min(len(norm(v['source'])),len(norm(next(r['source'] for r in regions if r['id']==best[1]))))/max(1,len(norm(v['source'])))
        if best[0]>=.85 and margin>=.12 and length_ratio>=.8: candidates.append((best[0],v,best[1]))
used=set()
matched=set()
for score,v,rid in sorted(candidates,key=lambda c:c[0],reverse=True):
    if rid in used: continue
    r=next(r for r in regions if r['id']==rid)
    r.update(translation=v['translation'],visionId=v['id'],textSimilarity=round(score,4))
    used.add(rid)
    matched.add(v['id'])
references=json.loads((data/'region-comparison.json').read_text(encoding='utf-8'))['regions']
evaluation=[]
for ref in references:
    ranked=sorted(((iou(ref['bounds'],r['box']),r['id']) for r in regions),reverse=True)
    best=ranked[0] if ranked else (0,None)
    evaluation.append({'reference':ref['id'],'bestRegion':best[1],'iou':round(best[0],4)})
report={'backend':args.backend,'modelLoadMs':round(load_ms,2),'inferenceMs':times,
        'cachedOcrSecondPassMs':ocr['milliseconds'][-1], 'regionCount':len(regions),
        'referenceCount':len(references),'hitsIoU50':sum(e['iou']>=.5 for e in evaluation),
        'matchedVisionTranscripts':len(matched),'visionTranscripts':len(vision),
        'unmatchedVision':[v for v in vision if v['id'] not in matched],
        'unassignedOcrLines':unassigned,'regions':regions,'evaluation':evaluation,
        'peakWorkingSetMiB':round(getattr(psutil.Process().memory_info(),'peak_wset',psutil.Process().memory_info().rss)/1048576,2),
        'notes':['Detector coordinates and IDs are automatic; manual reference boxes are used only for evaluation.',
                 'Translation text is replayed unchanged from the prior single API call, including its semantic errors.',
                 'Text-similarity matching is an offline replay experiment, not a substitute for provider ID validation.',
                 'Inference timing excludes OCR, translation, model startup and rendering. No end-to-end speed claim.']}
(out/'assessment.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
drawn=image.copy()
cards=[]
rows=[]
for r in regions:
    x1,y1,x2,y2=r['box']
    cv2.rectangle(drawn,(round(x1),round(y1)),(round(x2),round(y2)),(0,130,255),2)
    cv2.putText(drawn,r['id'],(round(x1),max(12,round(y1)-3)),cv2.FONT_HERSHEY_SIMPLEX,.4,(0,0,200),1,cv2.LINE_AA)
    if 'translation' in r:
        cards.append(f'<div class="card" title="{r["id"]}" style="left:{x1/w*100}%;top:{y1/h*100}%;width:{(x2-x1)/w*100}%;height:{(y2-y1)/h*100}%">{html.escape(r["translation"])}</div>')
    rows.append(f'<tr><td>{r["id"]}</td><td>{html.escape(r["source"])}</td><td>{html.escape(r.get("translation","Chưa ghép đủ tin cậy — giữ ảnh gốc"))}</td></tr>')
cv2.imwrite(str(out/'detected-regions.png'),drawn)
encoded=base64.b64encode((data/'input.png').read_bytes()).decode()
marked=base64.b64encode((out/'detected-regions.png').read_bytes()).decode()
page='''<!doctype html><html lang="vi"><meta charset="utf-8"><title>Vùng cục bộ và ID – thử nghiệm manga</title><style>body{font:16px/1.4 Segoe UI,sans-serif;background:#202124;color:#eee;margin:24px}.compare{display:flex;gap:24px}.page{width:min(45vw,663px);position:relative}.page img{width:100%;display:block}.card{position:absolute;box-sizing:border-box;padding:3px;background:#151515f2;font-size:16px;line-height:1.12;overflow:hidden}.overflow{outline:2px solid orange}td,th{border:1px solid #666;padding:9px}table{border-collapse:collapse;margin-top:24px;width:100%}</style>'''
page+=f'<h2>Detector cục bộ + phát lại kết quả Vision đã lưu</h2><p>{len(regions)} vùng tự động; {report["hitsIoU50"]}/16 vùng đối chiếu đạt IoU ≥ 0,5. Ghép được {len(matched)}/14 transcript. Không gọi API mới; bản dịch và lỗi nghĩa giữ nguyên. Vùng chưa ghép giữ ảnh gốc. Viền cam: ô không đủ chỗ.</p>'
page+=f'<div class="compare"><div class="page"><img src="data:image/png;base64,{marked}"></div><div class="page"><img src="data:image/png;base64,{encoded}">'+''.join(cards)+'</div></div>'
page+='<table><tr><th>ID cục bộ</th><th>OCR cục bộ để ghép thử</th><th>Vision đã lưu</th></tr>'+''.join(rows)+'</table>'
page+='''<script>document.querySelectorAll('.card').forEach(c=>{let s=16;while(s>10&&(c.scrollHeight>c.clientHeight||c.scrollWidth>c.clientWidth)){s-=.5;c.style.fontSize=s+'px'}if(c.scrollHeight>c.clientHeight||c.scrollWidth>c.clientWidth)c.classList.add('overflow')})</script></html>'''
(out/'preview.html').write_text(page,encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ['regions','evaluation','unmatchedVision']},ensure_ascii=False,indent=2),flush=True)
