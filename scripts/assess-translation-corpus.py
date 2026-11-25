"""Offline structural audit plus explicit single-reviewer semantic annotations."""
import json,statistics,math,hashlib,csv
from pathlib import Path
root=Path(__file__).resolve().parents[1]
out=root/'artifacts/translation-corpus-study'
corpus_path=root/'data/evaluation/translation-cases.json'
cases=json.loads(corpus_path.read_text(encoding='utf8'))['cases']
plan=json.loads((out/'study-plan.json').read_text(encoding='utf-8-sig'))
assert hashlib.sha256(corpus_path.read_bytes()).hexdigest().upper()==plan['corpusSha256']
outputs={'original':{},'normalized':{}};times={k:[] for k in outputs}
for group in plan['groups']:
    folder=out/group['name']
    record=json.loads((folder/'response.json').read_text(encoding='utf-8-sig'))
    request=json.loads((folder/'request-body.json').read_text(encoding='utf-8-sig'))
    assert request['model']==plan['model'] and record['finishReason']=='stop'
    source=json.loads(request['messages'][1]['content'])
    assert [s['id'] for s in source]==group['ids']
    truth={c['id']:c['text'] for c in cases}
    assert all(s['text'].lower()==truth[s['id']].lower() for s in source)
    translations=json.loads(record['content'])['translations']
    assert len(translations)==len(group['ids']) and {t['id'] for t in translations}==set(group['ids'])
    assert all(t['text'].strip() for t in translations)
    condition=group['name'].split('-',1)[1]
    times[condition].append(record['elapsedMilliseconds'])
    for t in translations:
        assert t['id'] not in outputs[condition]
        outputs[condition][t['id']]=t['text']
assert all(len(v)==60 for v in outputs.values())
# Every output was manually reviewed. Unlisted cases preserve core meaning (2),
# without asserting polished dialogue or consistency of character relationships.
# 0=wrong meaning/nonsense; 1=material repair needed; 2=usable meaning/stiff; 3=natural.
issues={
 'm1-beer':(2,1,'Normalized changes we/us to I; group perspective weakened.'),
 'm2-dorks':(0,2,'Original adds meaningless TAI DAU; normalized recovers core question.'),
 'm2-haru':(1,2,'Original awkward ownership wording; normalized closer to self-centered complaint.'),
 'm2-listening':(0,2,'Original listening becomes feeling; normalized restores hearing/addressee.'),
 'm5-hear':(0,2,'Original hearing becomes feeling; normalized restores hearing.'),
 'm5-loses':(0,0,'Both literal falling; romantic meaning needs scene context.'),
 'm5-back':(0,0,'Original reverses behind/in front; normalized invents going up and literal dying.'),
 'm5-haru':(0,2,'Original meaningless phrasing; normalized closer, still imprecise narcissism.'),
 'm5-conclusion':(0,2,'Original meaningless NHU NGAY; normalized preserves same conclusion.'),
 'm5-crap':(0,3,'Original nonsense; normalized restores indifference.'),
 'video-01-roi':(1,1,'Both awkward poetic image; normalized rung rac is ungrammatical.'),
 'video-07-roi':(2,1,'Normalized omits comparison with speaker ability; original preserves it.'),
 'p23-dumb':(0,2,'Original nonsense packaging; normalized conveys feigning ignorance.'),
 'p23-serious':(1,2,'Original THAT DAU is malformed; normalized preserves seriousness.'),
 'p23-dry':(1,1,'Both literal not wet, misses abstaining from alcohol in source scene.'),
 'p23-detective':(0,1,'Original malformed detective/network; normalized intelligence still wrong as intellect.'),
 'p23-clue':(1,2,'Original no hint rather than no idea; normalized acceptable.'),
 'synthetic-05':(1,1,'Both omit recipient you from giving her message.'),
 'synthetic-08':(1,2,'Original repeated/awkward phrase; normalized understandable end-of-day idiom.'),
 'synthetic-11':(1,1,'Both translate organization and place names, violating explicit name requirement.'),
 'synthetic-12':(1,1,'Both translate Ashen Tower; Riven preserved.'),
 'synthetic-17':(2,1,'Normalized omits waiting from the stay/wait option.')
}
reviews=[]
for c in cases:
    a,b,note=issues.get(c['id'],(2,2,'Core meaning retained in both; no naturalness guarantee.'))
    reviews.append({'id':c['id'],'source':c['text'],'original':outputs['original'][c['id']],
                    'normalized':outputs['normalized'][c['id']],'originalScore':a,'normalizedScore':b,'reviewNote':note})
metrics={}
for condition in outputs:
    scores=[r[condition+'Score'] for r in reviews]
    t=sorted(times[condition])
    metrics[condition]={'usableScore2or3':sum(s>=2 for s in scores),'total':60,
        'usablePercent':round(sum(s>=2 for s in scores)/60*100,2),'apiBatchMedianMs':statistics.median(t),
        'apiBatchP95NearestRankMs':t[math.ceil(.95*len(t))-1],'batchCount':len(t)}
summary={'acceptedForProduction':False,'manualReview':'single reviewer, not blinded; all development cases inspected; scores are judgments, not automated semantic accuracy',
 'metrics':metrics,'improvedCases':sum(r['normalizedScore']>r['originalScore'] for r in reviews),
 'regressedCases':sum(r['normalizedScore']<r['originalScore'] for r in reviews),
 'equalScoreCases':sum(r['normalizedScore']==r['originalScore'] for r in reviews),
 'limitations':['One response per condition, order alternated by group; cannot establish repeatability.',
 'No causal scene context; some idioms underdetermined from isolated text.',
 'Names were kept in source casing, not protected using production glossary placeholders.',
 'Batch latency excludes cooldown, OCR/capture/overlay; eight batches per condition is not live subtitle p95.',
 'No held-out dataset; all60 are development data.'], 'newApiCallsDuringReview':0}
(out/'assessment.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
(out/'semantic-review.json').write_text(json.dumps(reviews,ensure_ascii=False,indent=2),encoding='utf8')
with (out/'semantic-review.tsv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=list(reviews[0]),delimiter='\t');writer.writeheader();writer.writerows(reviews)
print(json.dumps(summary,ensure_ascii=True,indent=2))
