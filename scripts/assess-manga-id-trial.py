"""Audit the completed ID response locally. Manual meaning notes do not alter model output."""
from pathlib import Path
import json

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/manga-regression-172/id-trial'
load=lambda name: json.loads((out/name).read_text(encoding='utf-8-sig'))
manifest=load('manifest.json')
envelope=load('vision-response.json')
provider=json.loads(envelope['content'])['translations']
rendered=load('vision-result.json')['regions']
expected={r['id']:r for r in manifest['regions']}
ids=[r['id'] for r in provider]
assert len(ids)==len(set(ids)), 'Duplicate response ID'
assert set(ids)==set(expected), 'Missing/unknown response ID'
for r in rendered:
    assert max(abs(a-b) for a,b in zip(r['box'],expected[r['id']]['box']))<1e-8, 'Coordinate mismatch'
old=json.loads((out.parent/'vision-response.json').read_text(encoding='utf-8-sig'))
issues=[
    {'id':'R001','type':'clear semantic error','source':"BEING AROUND DRUNK PEOPLE WHEN YOU'RE SOBER IS A DRAG.",
     'finding':'khi họ tỉnh lại changes the sober person and the time meaning; the speaker is sober while others are drunk.'},
    {'id':'R002','type':'clear omission','source':"BY THE TIME I SHOW UP, THEY'RE ALREADY WASTED.",
     'finding':'Vào thời điểm đó omits the speaker arriving.'},
    {'id':'R006','type':'clear lexical error','source':'WAITER! BEER ME!',
     'finding':'Đầu bếp means cook; WAITER addresses serving staff.'},
    {'id':'R007','type':'cross-region meaning risk','source':'ARE THE FREE ROUNDS...',
     'finding':'Adds worth-question content also translated under R008; review continuity rather than counting structural ID completeness as semantic completeness.'},
    {'id':'R010','type':'meaning risk','source':'LIKE, LOOK AT THOSE GUYS. THEY GET IT.',
     'finding':'họ sẽ hiểu thôi introduces future tense; crop also misses part of the original caption.'},
    {'id':'R015','type':'false-positive region and unsupported translation','source':'Tiny visual fragment, not a readable FRIENDS caption.',
     'finding':'Model outputs Bạn bè with uncertain=false for a very small local detector fragment. Needs local quality gating.'},
]
by_id={r['id']:r for r in provider}
for issue in issues: issue['modelTranslation']=by_id[issue['id']]['text']
report={'model':envelope['model'],'apiElapsedMs':envelope['elapsedMilliseconds'],
        'previousApiElapsedMs':old['elapsedMilliseconds'],
        'singleTrialApiReductionPercent':round((old['elapsedMilliseconds']-envelope['elapsedMilliseconds'])/old['elapsedMilliseconds']*100,1),
        'expectedIds':len(expected),'returnedIds':len(ids),'allIdsUniqueAndComplete':True,
        'allRenderedCoordinatesFromLocalManifest':True,
        'uncertainCount':sum(r.get('uncertain') is True for r in provider),
        'nazunaPreserved':'Nazuna' in by_id['R004']['text'],
        'manualFindings':issues,
        'notes':['One call per approach; latency reduction is descriptive, not a repeated performance benchmark.',
                 'Structural ID correctness does not imply correct segmentation, meaning, or sufficient card size.',
                 'No new image request was made during this assessment. Model translations are unchanged.',
                 'No end-to-end capture-to-overlay test or cutscene test was performed.']}
(out/'assessment.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ['manualFindings','notes']},ensure_ascii=False,indent=2))
