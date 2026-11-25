"""New user-supplied page, offline recognition/grouping regression. No network/API."""
from pathlib import Path
import json,time,shutil,re
import cv2
from rapidocr import RapidOCR
from manga_line_regions import group_lines

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/manga-regression-23'
out.mkdir(parents=True,exist_ok=True)
source=Path('C:/Users/kuuhaku/AppData/Local/Temp/codex-clipboard-b13cb1af-bac4-488f-ac94-fd087d699527.png')
shutil.copyfile(source,out/'input.png')
# Hand-transcribed, approximate reference text rectangles; scoring only.
refs=[
('homework',[65,58,145,118],"I'VE DONE MY HOMEWORK, YOU KNOW."),
('dumb',[267,57,320,132],"DON'T PLAY DUMB WITH ME."),
('serious',[380,108,457,147],"I'M SERIOUS!"),
('nazuna',[511,84,585,113],'NAZUNA!'),
('ew',[62,201,113,251],'EW, HOME-WORK.'),
('drink',[190,303,250,373],'WANNA GO FOR A DRINK?'),
('azami',[290,312,356,399],"HEY, AZAMI. YOU'RE STILL IN TOWN?"),
('sure',[78,349,117,371],'Sure.'),
('dry',[130,319,161,383],"I'll stay dry."),
('detective',[399,372,550,468],"DON'T UNDERESTIMATE THE INTELLIGENCE NETWORK OF A CRACK DETECTIVE!"),
('guess',[265,519,337,598],'GUESS WHAT? NAZUNA CAME TO SEE ME THE OTHER DAY.'),
('oh',[118,549,154,574],'OH?'),
('want',[68,577,129,626],'WHAT DID SHE WANT?'),
('why',[67,714,124,755],'HUH? WHY?'),
('blood',[246,701,318,786],'SHE ASKED ME WHERE I GET MY BLOOD SUPPLY.'),
('clue',[69,819,112,854],'NO CLUE.'),
('hi',[280,440,307,469],'Hi!')]
(out/'references.json').write_text(json.dumps(refs,indent=2),encoding='utf8')
cv2.setNumThreads(2)
engine=RapidOCR(params={'Global.model_root_dir':str(root/'models/ocr-evaluation'),'Global.log_level':'warning',
    'Det.limit_type':'max','Det.limit_side_len':960,
    'EngineConfig.onnxruntime.intra_op_num_threads':2,'EngineConfig.onnxruntime.inter_op_num_threads':1})
image=cv2.imread(str(out/'input.png'))
times=[]
for _ in range(3):
    start=time.perf_counter();result=engine(image);times.append(round((time.perf_counter()-start)*1000,2))
blocks=[{'text':text,'box':box.tolist()} for text,box in zip(result.txts,result.boxes)]
start=time.perf_counter();groups=group_lines(blocks);group_ms=(time.perf_counter()-start)*1000
def center(b):
    return sum(p[0] for p in b['box'])/4,sum(p[1] for p in b['box'])/4
def inside(c,b): return b[0]<=c[0]<=b[2] and b[1]<=c[1]<=b[3]
def norm(s): return re.sub('[^A-Z0-9]','',s.upper())
details=[]
for rid,box,text in refs:
    actual=' '.join(b['text'] for b in sorted(blocks,key=lambda b:(center(b)[1],center(b)[0])) if inside(center(b),box))
    hits=[i for i,g in enumerate(groups) if any(inside(center(b),box) and inside(center(b),g['box']) for b in blocks)]
    details.append({'id':rid,'reference':text,'actual':actual,'normalizedExact':norm(actual)==norm(text),'groups':hits})
merges=[]
for i in range(len(groups)):
    ids=[d['id'] for d in details if i in d['groups']]
    if len(ids)>1: merges.append({'group':i,'references':ids})
drawn=image.copy()
for i,g in enumerate(groups):
    x,y,r,b=map(round,g['box']);cv2.rectangle(drawn,(x,y),(r,b),(0,0,255),1);cv2.putText(drawn,str(i),(x,max(12,y-2)),cv2.FONT_HERSHEY_SIMPLEX,.35,(0,0,255),1)
cv2.imwrite(str(out/'grouped.png'),drawn)
report={'ocrMs':times,'groupingMs':round(group_ms,3),'referenceCount':len(refs),'exactReferenceTranscripts':sum(d['normalizedExact'] for d in details),
 'splitReferences':sum(len(d['groups'])>1 for d in details),'missingReferences':sum(not d['groups'] for d in details),'crossReferenceGroups':merges,
 'details':details,'ocrBlocks':blocks,'groups':groups,'limitations':'Approximate manually annotated text rectangles, center-membership scoring only; Rapid prototype, not Windows OCR/app or live overlay. No translation call.'}
(out/'assessment.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k not in ['details','ocrBlocks','groups']},indent=2))
