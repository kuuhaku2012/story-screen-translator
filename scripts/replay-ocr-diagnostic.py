"""Compare stored actual Windows diagnostic with local Rapid on identical pixels."""
from pathlib import Path
import json,time,html,base64
import cv2
from rapidocr import RapidOCR
from manga_line_regions import group_lines
root=Path(__file__).resolve().parents[1]
folders=list((root/'artifacts/ocr-diagnostics').glob('*/ocr.json'))
source=max(folders,key=lambda p:p.stat().st_mtime).parent
out=source/'rapid-replay';out.mkdir(exist_ok=True)
original=json.loads((source/'ocr.json').read_text(encoding='utf8'))
image=cv2.imread(str(source/'source.png'));h,w=image.shape[:2];cv2.setNumThreads(2)
engine=RapidOCR(params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),'Global.log_level':'warning',
 'Det.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
 'Rec.model_path':str(root/'models/ocr-evaluation/PP-OCRv6_rec_small.onnx'),
 'Cls.model_path':str(root/'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
 'Det.limit_type':'max','Det.limit_side_len':960,
 'EngineConfig.onnxruntime.intra_op_num_threads':2,'EngineConfig.onnxruntime.inter_op_num_threads':1})
times=[]
for _ in range(3):
    start=time.perf_counter();r=engine(image)
    lines=[{'text':t,'box':b.tolist(),'score':float(s)} for t,b,s in zip(r.txts,r.boxes,r.scores)]
    groups=group_lines(lines)
    for g in groups:
        a=g['box'];inside=[]
        for b in lines:
            x=sum(p[0] for p in b['box'])/4;y=sum(p[1] for p in b['box'])/4
            if a[0]<=x<=a[2] and a[1]<=y<=a[3]:inside.append((y,x,b['text']))
        g['text']=' '.join(t for _,_,t in sorted(inside))
    times.append(round((time.perf_counter()-start)*1000,2))
encoded=base64.b64encode((source/'source.png').read_bytes()).decode()
def layer(boxes):
    svg=f'<svg viewBox="0 0 {w} {h}"><image href="data:image/png;base64,{encoded}" width="{w}" height="{h}"/>'
    for i,b in enumerate(boxes,1):
        x,y,rr,bb=b;svg+=f'<rect x="{x}" y="{y}" width="{rr-x}" height="{bb-y}" fill="none" stroke="red" stroke-width="1.5"/><text x="{x}" y="{max(12,y)}" fill="red" font-size="12">{i}</text>'
    return svg+'</svg>'
win=original['Blocks']
winboxes=[[b['SourceBounds']['X'],b['SourceBounds']['Y'],b['SourceBounds']['X']+b['SourceBounds']['Width'],b['SourceBounds']['Y']+b['SourceBounds']['Height']] for b in win]
def rows(items):return '<ol>'+''.join('<li>'+html.escape(b.get('Text',b.get('text',''))).replace('\n','<br>')+'</li>' for b in items)+'</ol>'
page='<meta charset="utf-8"><title>Same-pixel OCR comparison</title><style>body{font:16px Segoe UI;background:#222;color:#eee}.columns{display:flex;gap:24px}.col{width:48%}svg{width:100%}li{margin-bottom:8px}</style><h1>Cùng ảnh capture: Windows OCR / Rapid cục bộ</h1><p>Chỉ đối chiếu, không gọi dịch/API. Số thứ tự thuộc từng engine, không ánh xạ tương đương.</p><div class="columns"><div class="col"><h2>Windows OCR</h2>'+layer(winboxes)+rows(win)+'</div><div class="col"><h2>Rapid + gom cụm</h2>'+layer([g['box'] for g in groups])+rows(groups)+'</div></div>'
(out/'comparison.html').write_text(page,encoding='utf8')
report={'sourceDirectory':str(source),'windowsOcrMs':original['OcrMilliseconds'],'windowsBlocks':len(win),'rapidOcrGroupingMs':times,
 'rapidBlocks':len(groups),'minimum90pxPanels':sum(abs(b['DisplayBounds']['Width']-90)<.01 for b in win),
 'groups':groups,'lines':lines,'scope':'Same stored actual capture, local replay only. No recognition accuracy score or live DPI measurement. Windows time excludes panel layout; Rapid time includes grouping.'}
(out/'assessment.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k not in ['groups','lines']},indent=2))
