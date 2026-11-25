"""Offline grouping comparison on cached OCR. Reference boxes are scoring-only."""
from pathlib import Path
import argparse
import json
import time
from PIL import Image, ImageDraw
from manga_line_regions import group_lines

root=Path(__file__).resolve().parents[1]
data=root/'artifacts/multimodal-evaluation'
parser=argparse.ArgumentParser()
parser.add_argument('--label',default='baseline')
parser.add_argument('--production-report',default=str(data/'rapid-production-layout.json'))
args=parser.parse_args()
out=root/'artifacts/line-grouping'/args.label
out.mkdir(parents=True,exist_ok=True)
refs=json.loads((data/'references.json').read_text(encoding='utf8'))
reports=json.loads((data/'rapid-v6-small.json').read_text(encoding='utf8'))['Reports']
production=json.loads(Path(args.production_report).read_text(encoding='utf8'))['Reports']
results=[]
for report in reports:
    page=report['Id'].removesuffix('-730')
    selected=[r for r in refs if r.get('page')==page and page in {'manga-001','manga-002','manga-005'}]
    if not selected: continue
    image=Image.open(data/'inputs'/(report['Id']+'.png')).convert('RGB')
    w,h=image.size
    blocks=[]
    for b in report['Blocks']:
        x,y=b['X'],b['Y'];r=x+b['Width'];bottom=y+b['Height']
        blocks.append({'text':b['Text'],'box':[[x,y],[r,y],[r,bottom],[x,bottom]]})
    started=time.perf_counter()
    candidate=group_lines(blocks)
    elapsed=(time.perf_counter()-started)*1000
    old=next(r for r in production if r['Id']==report['Id'] and r['ComicMode'])
    modes={'production':[{'box':[b['X'],b['Y'],b['X']+b['Width'],b['Y']+b['Height']]} for b in old['Blocks']], 'candidate':candidate}
    for mode,regions in modes.items():
        memberships=[]
        for region in regions:
            a=region['box']; matched=set()
            for ref in selected:
                x1,y1,x2,y2=ref['box']
                # Line centers shared by region and reference. Coordinates never enter grouping.
                for b in report['Blocks']:
                    cx=b['X']+b['Width']/2;cy=b['Y']+b['Height']/2
                    if a[0]<=cx<=a[2] and a[1]<=cy<=a[3] and x1*w<=cx<=x2*w and y1*h<=cy<=y2*h:
                        matched.add(ref['id']);break
            memberships.append(matched)
        counts={r['id']:sum(r['id'] in m for m in memberships) for r in selected}
        item={'page':report['Id'],'mode':mode,'references':len(selected),'splitReferences':sum(n>1 for n in counts.values()),
              'missingReferences':sum(n==0 for n in counts.values()),'crossReferenceRegions':sum(len(m)>1 for m in memberships),
              'regionCount':len(regions),'counts':counts,'groupingMs':round(elapsed,3) if mode=='candidate' else None}
        results.append(item)
        if mode=='candidate':
            draw=ImageDraw.Draw(image)
            for i,r in enumerate(regions): draw.rectangle(r['box'],outline='red',width=2);draw.text((r['box'][0],max(0,r['box'][1]-12)),str(i+1),fill='red')
            image.save(out/(report['Id']+'.png'))
summary={}
for mode in modes:
    rows=[r for r in results if r['mode']==mode]
    summary[mode]={k:sum(r[k] for r in rows) for k in ['references','splitReferences','missingReferences','crossReferenceRegions']}
payload={'summary':summary,'details':results,'scope':'Cached Rapid v6 lines on three pages at two sizes (44 reference observations, 22 distinct text regions). Center membership is a proxy; does not prove perfect boundaries, OCR quality, or live latency.'}
(out/'assessment.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(summary,indent=2))
