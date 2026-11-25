"""Review the already-returned approved Vision response; never makes network requests."""
from pathlib import Path
import base64
import html
import json
import re

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/manga-regression-172'
envelope = json.loads((out / 'vision-response.json').read_text(encoding='utf-8-sig'))
result = json.loads(envelope['content'])
references = {r['id']: r for r in json.loads((out / 'region-comparison.json').read_text(encoding='utf-8'))['regions']}
mapping = {1:['being'],2:['waiter','another'],3:['time'],4:['nazuna'],5:['not-only'],6:['look'],7:['going'],
           8:['seen'],9:['worth'],10:['free'],11:['couple','friends'],12:['night'],13:['laugh'],14:['chill']}

def norm(s):
    return re.sub('[^A-Z0-9]', '', s.upper())

def distance(a,b):
    row = list(range(len(b)+1))
    for i,ac in enumerate(a,1):
        nxt=[i]
        for j,bc in enumerate(b,1):
            nxt.append(min(nxt[-1]+1,row[j]+1,row[j-1]+(ac!=bc)))
        row=nxt
    return row[-1]

def iou(a,b):
    area = max(0,min(a[2],b[2])-max(a[0],b[0])) * max(0,min(a[3],b[3])-max(a[1],b[1]))
    union = (a[2]-a[0])*(a[3]-a[1]) + (b[2]-b[0])*(b[3]-b[1]) - area
    return area/union if union>0 else 0

rows=[]
cards=[]
details=[]
errors=characters=exact=invalid=0
for region in result['regions']:
    refs=[references[k] for k in mapping[region['id']]]
    source=' '.join(r['reference'] for r in refs)
    expected=norm(source)
    observed=norm(region['source'])
    difference=distance(expected,observed)
    errors+=difference
    characters+=len(expected)
    exact+=difference==0
    x,y,w,h=region['box']
    xywh_valid = 0<=x<1000 and 0<=y<1000 and w>0 and h>0 and x+w<=1000 and y+h<=1000
    invalid+=not xywh_valid
    # Diagnostic only: the model appears to have used x1,y1,x2,y2 despite the prompt.
    alternative=[x/1000*663,y/1000*927,w/1000*663,h/1000*927]
    ground=[min(r['bounds'][0] for r in refs),min(r['bounds'][1] for r in refs),
            max(r['bounds'][2] for r in refs),max(r['bounds'][3] for r in refs)]
    overlap=iou(alternative,ground)
    issue=[]
    if not xywh_valid: issue.append('Sai box theo giao thức x,y,w,h')
    if len(refs)>1: issue.append('Gộp hai vùng đối chiếu')
    if difference: issue.append(f'Lệch {difference} ký tự chuẩn hóa')
    if region['id']==2: issue.append('Sai nghĩa: WAITER là phục vụ, không phải đầu bếp')
    details.append({'id':region['id'],'referenceRegions':mapping[region['id']], 'characterErrors':difference,
                    'xywhValid':xywh_valid,'diagnosticXyxyIoU':round(overlap,4),'issues':issue})
    rows.append('<tr><td>'+str(region['id'])+'</td><td>'+html.escape(source)+'</td><td>'+html.escape(region['source'])+
                '</td><td>'+html.escape(region['translation'])+'</td><td>'+html.escape('; '.join(issue))+'</td></tr>')
    if w>x and h>y:
        cards.append(f'<div class="box" style="left:{x/10}%;top:{y/10}%;width:{(w-x)/10}%;height:{(h-y)/10}%">{region["id"]}</div>')
report={'model':envelope['model'],'apiElapsedMs':envelope['elapsedMilliseconds'],'responseRegions':len(result['regions']),
        'manuallyReferencedRegions':len(references),'normalizedCharacters':characters,'characterErrors':errors,
        'characterErrorPercent':round(errors/characters*100,3),'exactTranscriptionsAfterManualMerging':exact,
        'invalidXywhBoxes':invalid,'diagnosticXyxyHitsIoU50':sum(r['diagnosticXyxyIoU']>=.5 for r in details),
        'meanDiagnosticXyxyIoU':round(sum(r['diagnosticXyxyIoU'] for r in details)/len(details),4),
        'notes':['One image and one successful API call; not realtime performance.',
                 'CER ignores spaces, case and punctuation; manual pairing/merging is evaluation only, not app capability.',
                 'Translations manually inspected; at least one clear semantic error, not automatically scored.',
                 'Alternative xyxy visualization is diagnostic, not accepted production coordinates.'], 'details':details}
(out/'vision-assessment.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'vision-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
image=base64.b64encode((out/'input.png').read_bytes()).decode('ascii')
preview='''<!doctype html><html lang="vi"><meta charset="utf-8"><title>Đối chiếu Vision – manga 172</title>
<style>body{background:#202124;color:#eee;font:16px/1.5 Segoe UI,sans-serif;margin:24px}h1{font-size:24px}p{max-width:1100px}.compare{display:flex;gap:24px}.page{width:min(45vw,663px);position:relative}.page img{width:100%;display:block}.box{position:absolute;box-sizing:border-box;border:2px solid #ffad33;color:#000;background:#ffad3344;font-size:20px;font-weight:bold}table{border-collapse:collapse;width:100%;margin-top:24px}th,td{border:1px solid #666;padding:10px;vertical-align:top}th{background:#333}td:nth-child(5){color:#ffbd66}</style>
<h1>Vision đọc chữ tốt hơn, chưa đạt để đè bản dịch tự động</h1>'''
preview+=f'<p>{envelope["model"]}: {envelope["elapsedMilliseconds"]/1000:.3f} giây cho một yêu cầu. {invalid}/14 box sai định dạng yêu cầu. Bảng dưới giữ nguyên toàn bộ bản dịch model trả; chưa sửa tay.</p>'
preview+='<p>Trái: ảnh gốc. Phải: thử diễn giải box theo x1,y1,x2,y2 để chẩn đoán. Các khung cam vẫn sai vị trí; đây không phải overlay đã sửa thành công.</p>'
preview+=f'<div class="compare"><div class="page"><img src="data:image/png;base64,{image}"></div><div class="page"><img src="data:image/png;base64,{image}">'+''.join(cards)+'</div></div>'
preview+='<table><thead><tr><th>ID</th><th>Chép tay để đối chiếu</th><th>Vision đọc</th><th>Vision dịch (nguyên văn)</th><th>Vấn đề phát hiện</th></tr></thead><tbody>'+''.join(rows)+'</tbody></table></html>'
(out/'vision-preview.html').write_text(preview,encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='details'},ensure_ascii=False,indent=2))
