"""Local text-detector gating; no recognition/provider/gold data in fusion."""
from pathlib import Path
import json,time,base64,html
import cv2
from rapidocr import RapidOCR
from bubble_text_regions import fuse_regions
root=Path(__file__).resolve().parents[1];out=root/'artifacts/bubble-text-fusion';out.mkdir(exist_ok=True)
proposals=json.loads((root/'artifacts/bubble-proposals/assessment.json').read_text(encoding='utf8'))['reports']
latest=max((root/'artifacts/ocr-diagnostics').glob('*/ocr.json'),key=lambda p:p.stat().st_mtime).parent
paths={'actual-page23':latest/'source.png','page172':root/'artifacts/manga-regression-172/input.png'}
for name in ['manga-001','manga-002','manga-005']:paths[name]=root/f'artifacts/multimodal-evaluation/inputs/{name}-730.png'
cv2.setNumThreads(2)
engine=RapidOCR(params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),'Global.log_level':'warning',
 'Det.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
 'Rec.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_rec_small.onnx'),
 'Cls.model_path':str(root/'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
 'Det.limit_type':'max','Det.limit_side_len':960,
 'EngineConfig.onnxruntime.intra_op_num_threads':2,'EngineConfig.onnxruntime.inter_op_num_threads':1})
reports=[];cards=[]
for page in proposals:
    name=page['page'];image=cv2.imread(str(paths[name]));times=[]
    for _ in range(3):
        start=time.perf_counter();r=engine(image,use_rec=False,use_cls=False)
        lines=[{'text':'','box':b.tolist()} for b in r.boxes] if r.boxes is not None else []
        result=fuse_regions(page['proposals'],lines);times.append(round((time.perf_counter()-start)*1000,2))
    drawn=image.copy()
    for i,g in enumerate(result['regions']):
        x,y,rr,b=map(round,g['regionBounds']);color=(0,190,0) if g['kind']=='bubble' else (255,180,0) if g['bubble'] is not None else (0,180,255)
        cv2.rectangle(drawn,(x,y),(rr,b),color,2);cv2.putText(drawn,str(i),(x,max(12,y-2)),cv2.FONT_HERSHEY_SIMPLEX,.4,color,1)
    cv2.imwrite(str(out/(name+'.png')),drawn)
    report={'page':name,'detectorPlusFusionMs':times,'rawBubbles':len(page['proposals']),'detectedLines':len(lines),**result}
    reports.append(report)
    encoded=base64.b64encode((out/(name+'.png')).read_bytes()).decode()
    cards.append(f'<h2>{html.escape(name)}</h2><p>Raw bubbles: {report["rawBubbles"]}; removed without text-line support: {result["removedBubbleCount"]}; final clusters: {len(result["regions"])}. No correctness percentage.</p><img src="data:image/png;base64,{encoded}">')
    print(f'{name}: remove {result["removedBubbleCount"]}/{report["rawBubbles"]} bubbles, {len(result["regions"])} regions, {times[-1]}ms',flush=True)
payload={'reports':reports,'scope':'Detector-only line evidence plus cached independent bubble proposals. No recognizer/translation. Removed proposals may include real speech with missed text detection; retained proposals may be art false positives. No precision/recall or runtime app claim.'}
(out/'assessment.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf8')
(out/'preview.html').write_text('<meta charset="utf-8"><title>Bubble and text fusion</title><style>body{font:16px Segoe UI;background:#222;color:#eee}img{width:90%;max-width:900px}</style><h1>Vùng thoại + detector dòng chữ — chưa tích hợp</h1><p>Xanh lá: một cụm trong vùng sáng. Xanh lam: cụm tách trong vùng nối. Cam: chữ ngoài vùng sáng. Các vùng vẫn cần chấm độc lập.</p>'+''.join(cards),encoding='utf8')
