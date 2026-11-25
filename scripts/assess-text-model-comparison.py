"""Audit saved candidate outputs and record human semantic findings; no provider calls."""
from pathlib import Path
import json

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/manga-regression-172/model-comparison'
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
inputs=load(out/'clean-source.json')
expected={r['id']:r['text'] for r in inputs}
baseline=load(out.parent/'text-trial/clean-source.json')
assert expected=={r['id']:r['text'] for r in baseline}, 'Comparison source changed'
models=['openai/gpt-oss-120b','qwen/qwen3.8-27b']
checks=[]
prompts=[]
for model in models:
    folder=out/model.replace('/','_')
    request=load(folder/'request-body.json')
    prompts.append(request['messages'][0]['content'])
    assert {r['id']:r['text'] for r in json.loads(request['messages'][1]['content'])}==expected
    envelope=load(folder/'response.json')
    assert envelope['finishReason']=='stop'
    entries=json.loads(envelope['content'])['translations']
    translated={r['id']:r['text'] for r in entries}
    assert len(entries)==len(translated)==len(expected)==14 and set(translated)==set(expected)
    assert all(isinstance(v,str) and v.strip() for v in translated.values())
    checks.append({'model':model,'apiElapsedMs':envelope['elapsedMilliseconds'],'idsValid':True,
                   'sourceUnchanged':True,'nazunaPreserved':'Nazuna' in translated['T004']})
assert prompts[0]==prompts[1], 'Candidate instructions differ'
findings={
    'openai/gpt-oss-120b':[
        {'id':'T002','issue':'Changes BET YOU COULD GO FOR ANOTHER BEER into the speaker wanting another beer: tôi muốn. Wrong viewpoint.'},
        {'id':'T011','issue':'của bạn bè is a word-for-word possessive rendering of COUPLE ... OF FRIENDS.'},
        {'id':'T010','issue':'vòng uống is stiff; drinking context is recognized, unlike the older 20B output.'},
        {'id':'T001','issue':'là thật là is ungrammatical repetition; viewpoint is otherwise correct.'},
    ],
    'qwen/qwen3.8-27b':[
        {'id':'T002','issue':'WAITER becomes Đồ uống, so the address to serving staff is lost. Adds a can (lon) not specified in the text.'},
        {'id':'T006','issue':'hiểu ý nhau adds a mutual-understanding interpretation; source only says THEY GET IT.'},
    ],
}
report={'candidates':checks,'sameSourceAndInstructionsForCandidates':True,'manualFindings':findings,
        'provisionalCandidate':'qwen/qwen3.8-27b','acceptedForProduction':False,
        'selectionReason':'More natural Vietnamese and better handling of friends/rounds/interjections on this page, but a clear WAITER error remains.',
        'notes':['One page and one API call per candidate; not a repeated benchmark or overall accuracy score.',
                 '20B baseline is from the earlier run and has a slightly different formatting instruction.',
                 'JSON mode differs: strict schema for GPT-OSS; JSON object mode for Qwen.',
                 'Timings are text HTTP requests, not screen-to-overlay or cutscene latency.',
                 'No model outputs were rewritten and no request was made during this audit.']}
(out/'assessment.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ['manualFindings','notes']},ensure_ascii=False,indent=2))
