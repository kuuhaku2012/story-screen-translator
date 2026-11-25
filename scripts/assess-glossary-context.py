"""Audit saved partial study without network or changing attempted requests."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
out=root/'artifacts/translation-context-study'
prepared=json.loads((out/'prepared.json').read_text(encoding='utf8'))
report=[]
for job in prepared['jobs']:
    folder=out/job['name'];path=folder/'response.json'
    if not path.exists():
        report.append({'condition':job['name'],'status':'not attempted' if not (folder/'attempt.json').exists() else 'no response'})
        continue
    response=json.loads(path.read_text(encoding='utf-8-sig'))
    entries=json.loads(response['content'])['translations']
    assert response['finishReason']=='stop'
    assert len(entries)==len(job['items']) and {e['id'] for e in entries}=={i['id'] for i in job['items']}
    errors=[]
    for entry in entries:
        tokens=re.findall(r'⟬STG_[^⟭]*⟭',entry['text'])
        expected=list(job['tokenMaps'][entry['id']])
        if sorted(tokens)!=sorted(expected):
            errors.append({'id':entry['id'],'expectedTokens':expected,'observedTokens':tokens})
    report.append({'condition':job['name'],'apiMs':response['elapsedMilliseconds'],'idsValid':True,'placeholderErrors':errors,
                   'status':'rejected' if errors else 'structurally valid'})
assert next(r for r in report if r['condition']=='glossary-context')['placeholderErrors'][0]['id']=='m5-loses'
assert next(r for r in report if r['condition']=='glossary')['status']=='structurally valid'
assessment={'acceptedForProduction':False,'conditions':report,'conclusion':'Glossary-only preserves known target tokens; combined context leaks unrelated dialogue and context-only token into m5-loses. Fourth condition untested; no valid complete factorial conclusion.',
 'semanticFindings':['Known organization/place names recover correctly with glossary, not plain prompting.',
 'Glossary does not fix literal falling, dying, staying dry or intelligence network.',
 'Combined context returns extraneous dialogue and is rejected; do not trim it heuristically or blindly restore context names.'],
 'newApiCallsDuringReview':0,'mainApplicationChanged':False}
(out/'assessment.json').write_text(json.dumps(assessment,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(assessment,ensure_ascii=True,indent=2))
