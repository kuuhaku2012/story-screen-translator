"""Validate the saved text trial and record manual semantic checks; no network access."""
from pathlib import Path
import json

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/manga-regression-172/text-trial'
load=lambda name:json.loads((out/name).read_text(encoding='utf-8-sig'))
inputs=load('clean-source.json')
envelope=load('text-response.json')
entries=json.loads(envelope['content'])['translations']
expected={r['id']:r['text'] for r in inputs}
translated={r['id']:r['text'] for r in entries}
assert len(entries)==len(translated)==len(expected)==14
assert set(translated)==set(expected)
assert all(isinstance(r['text'],str) and r['text'].strip() for r in entries)
prior=json.loads(json.loads((out.parent/'vision-response.json').read_text(encoding='utf-8-sig'))['content'])['regions']
assert expected=={f'T{r["id"]:03}':r['source'] for r in prior}, 'Transcript was changed'
checks=[
    ('T001','speaker remains sober',True,'Correct viewpoint: bạn tỉnh táo. Wording remains stiff.'),
    ('T002','WAITER means serving staff',True,'Nhân viên phục vụ corrects the cook error.'),
    ('T003','wasted means drunk in this context',False,'họ đã hết loses the intoxication meaning.'),
    ('T010','free rounds means rounds of drinks',False,'vòng miễn phí is literal and wrong in this bar dialogue.'),
    ('T012','SHEESH is an interjection',False,'Đồ is not a suitable rendering of this exclamation.'),
    ('T013','over nothing does not mean over something',False,'vì điều gì đó changes the original meaning.'),
]
report={'model':envelope['model'],'apiElapsedMs':envelope['elapsedMilliseconds'],
        'idsValid':True,'items':len(entries),'sourceUnchangedFromSavedVision':True,
        'nazunaPreserved':'Nazuna' in translated['T004'],'acceptedForStoryTranslation':False,
        'manualChecks':[{'id':i,'criterion':c,'passed':p,'finding':f,'source':expected[i],'translation':translated[i]} for i,c,p,f in checks],
        'notes':['Selected semantic checks are not an overall translation-accuracy percentage.',
                 'One text request timing only; excludes Vision reading, local OCR, capture, stability and rendering.',
                 'A two-stage Vision-plus-text production pipeline has not been benchmarked or accepted.',
                 'This assessment makes no API calls and does not edit model translations.']}
(out/'assessment.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ['manualChecks','notes']},ensure_ascii=False,indent=2))
