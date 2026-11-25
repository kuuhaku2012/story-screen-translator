"""Detect once, batch-recognize source quadrilaterals, then assemble fused regions."""
from pathlib import Path
import json,time,re,html,hashlib,argparse
import cv2
from rapidocr import RapidOCR,LangRec,OCRVersion,ModelType
from bubble_text_regions import fuse_regions
parser=argparse.ArgumentParser();parser.add_argument('--recognizer',choices=['v6','english','medium'],default='v6');parser.add_argument('--provider',choices=['cpu','dml'],default='cpu');args=parser.parse_args()
root=Path(__file__).resolve().parents[1];out_name='direct-line-ocr' if args.recognizer=='v6' else 'direct-line-ocr-'+args.recognizer
if args.provider=='dml':out_name+='-dml'
out=root/'artifacts'/out_name;out.mkdir(exist_ok=True)
old=root/'artifacts/multimodal-evaluation';prior=root/'artifacts/fused-region-ocr'
proposals=json.loads((root/'artifacts/bubble-proposals/assessment.json').read_text(encoding='utf8'))['reports']
latest=max((root/'artifacts/ocr-diagnostics').glob('*/ocr.json'),key=lambda p:p.stat().st_mtime).parent
paths={'actual-page23':latest/'source.png','page172':root/'artifacts/manga-regression-172/input.png'}
for n in ['manga-001','manga-002','manga-005']:paths[n]=old/f'inputs/{n}-730.png'
refs=json.loads((old/'references.json').read_text(encoding='utf8'))
refs23=json.loads((root/'artifacts/manga-regression-23/references.json').read_text(encoding='utf8'))
refs172=json.loads((root/'artifacts/manga-regression-172/region-comparison.json').read_text(encoding='utf8'))['regions']
cv2.setNumThreads(2)
params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),'Global.log_level':'warning',
 'Det.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
 'Rec.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_rec_small.onnx'),
 'Cls.model_path':str(root/'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
 'Det.limit_type':'max','Det.limit_side_len':960,
 'EngineConfig.onnxruntime.intra_op_num_threads':2,'EngineConfig.onnxruntime.inter_op_num_threads':1}
if args.provider=='dml':
    import onnxruntime as ort
    if 'DmlExecutionProvider' not in ort.get_available_providers():
        raise RuntimeError('DML requested but DmlExecutionProvider is unavailable in this environment')
    params.update({'EngineConfig.onnxruntime.use_dml':True})
if args.recognizer=='english':
    params.update({'Rec.model_path':str(root/'models/ocr-evaluation/en_PP-OCRv5_rec_mobile.onnx'),
      'Rec.lang_type':LangRec.EN,'Rec.ocr_version':OCRVersion.PPOCRV5,'Rec.model_type':ModelType.MOBILE})
if args.recognizer=='medium':
    import yaml
    folder=root/'models/ocr-evaluation/ppocrv6-medium'
    dictionary=yaml.safe_load((folder/'inference.yml').read_text(encoding='utf8'))['PostProcess']['character_dict']
    assert all('\n' not in c and '\r' not in c for c in dictionary)
    keys=folder/'characters.txt';keys.write_text('\n'.join(dictionary)+'\n',encoding='utf8')
    params.update({'Rec.model_path':str(folder/'inference.onnx'),'Rec.rec_keys_path':str(keys)})
engine=RapidOCR(params=params)
def center(b):return sum(p[0] for p in b['box'])/4,sum(p[1] for p in b['box'])/4
def norm(t):return re.sub('[^A-Z0-9]','',t.upper())
def distance(a,b):
    row=list(range(len(b)+1))
    for i,x in enumerate(a,1):
        nxt=[i]
        for j,y in enumerate(b,1):nxt.append(min(nxt[-1]+1,row[j]+1,row[j-1]+(x!=y)))
        row=nxt
    return row[-1]
def run(image,bubbles):
    started=time.perf_counter();det=engine(image,use_det=True,use_cls=False,use_rec=False);det_ms=(time.perf_counter()-started)*1000
    if det.boxes is None:return [],[],{'detectorMs':det_ms,'recognizerMs':0,'totalMs':det_ms}
    begin=time.perf_counter()
    crops=engine.crop_text_regions(image,det.boxes.copy())
    crops,_=engine.cls_and_rotate(crops)
    recognized=engine.recognize_txt(crops)
    assert len(recognized.txts)==len(det.boxes),'Recognizer lost line-to-quad mapping'
    lines=[{'text':t,'box':b.tolist(),'score':float(s)} for t,b,s in zip(recognized.txts,det.boxes,recognized.scores)]
    rec_ms=(time.perf_counter()-begin)*1000
    # Text score filtering is explicit, matching the installed engine's configured threshold.
    kept=[b for b in lines if b['text'].strip() and b['score']>=float(engine.text_score)]
    regions=fuse_regions(bubbles,[{'text':'','box':b.tolist()} for b in det.boxes])['regions']
    assigned=set()
    for i,g in enumerate(regions):
        a=g['textBounds'];members=[j for j,l in enumerate(kept) if j not in assigned and a[0]<=center(l)[0]<=a[2] and a[1]<=center(l)[1]<=a[3]]
        assigned.update(members)
        g['id']=i;g['text']=' '.join(kept[j]['text'] for j in sorted(members,key=lambda j:(center(kept[j])[1],center(kept[j])[0])))
    return kept,regions,{'detectorMs':round(det_ms,2),'recognizerMs':round(rec_ms,2),'totalMs':round((time.perf_counter()-started)*1000,2)}
# Excluded warmup loads all three resident models.
run(cv2.imread(str(paths['actual-page23'])),proposals[0]['proposals'])
execution_providers={}
if args.provider=='dml':
    execution_providers={
      'detector':engine.text_det.session.session.get_providers(),
      'recognizer':engine.text_rec.session.session.get_providers()}
reports=[];cards=[]
for page in proposals:
    name=page['page'];path=paths[name];image=cv2.imread(str(path));h,w=image.shape[:2];times=[]
    for _ in range(3):lines,regions,timing=run(image,page['proposals']);times.append(timing)
    if name=='actual-page23':truth=refs23
    elif name=='page172':truth=[(r['id'],r['bounds'],r['reference']) for r in refs172]
    else:truth=[(r['id'],[r['box'][0]*w,r['box'][1]*h,r['box'][2]*w,r['box'][3]*h],r['text']) for r in refs if r.get('page')==name]
    details=[]
    for rid,b,text in truth:
        selected=sorted([l for l in lines if b[0]<=center(l)[0]<=b[2] and b[1]<=center(l)[1]<=b[3]],key=lambda l:(center(l)[1],center(l)[0]))
        actual=' '.join(l['text'] for l in selected)
        details.append({'id':rid,'reference':text,'actual':actual,'exact':norm(actual)==norm(text),'errors':distance(norm(text),norm(actual)),'chars':len(norm(text))})
    report={'page':name,'sourceSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'executionProviders':execution_providers,'timings':times,'details':details,'regions':regions,'lines':lines}
    reports.append(report);(out/(name+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    prior_page=json.loads((prior/(name+'.json')).read_text(encoding='utf8'))
    rows=''.join('<tr><td>'+str(g['id'])+'</td><td>'+html.escape(g['text'])+'</td></tr>' for g in regions)
    cards.append(f'<h2>{html.escape(name)}</h2><p>Latest direct pipeline: {timing["totalMs"]}ms. Prior crop path: {prior_page["detectorAndRecognitionMs"]}ms (separate run).</p><table>{rows}</table>')
    print(f'{name}: {sum(d["exact"] for d in details)}/{len(details)} exact, {timing["totalMs"]}ms',flush=True)
details=[d for r in reports for d in r['details']]
summary={'recognizer':args.recognizer,'provider':args.provider,'executionProviders':execution_providers,'exact':sum(d['exact'] for d in details),'regions':len(details),'cerPercent':round(100*sum(d['errors'] for d in details)/sum(d['chars'] for d in details),2),
 'scope':'Detect once + perspective line crops + batch classify/recognize + fusion. Three warmed observations/page, source hashes saved. Excludes cached bubble contours, capture/debounce/translation/rendering; approximate ROI-center scoring, not held out.'}
(out/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf8')
(out/'preview.html').write_text('<meta charset="utf-8"><title>Direct line OCR</title><style>body{font:16px Segoe UI;background:#222;color:#eee}td{border:1px solid #666;padding:8px}table{border-collapse:collapse}</style><h1>Nhận dạng dòng trực tiếp — chưa dịch/API</h1>'+''.join(cards),encoding='utf8')
print(json.dumps(summary),flush=True)
