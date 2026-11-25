"""Build development fixtures; no network, no provider answers or images in payloads."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
extra=[
('I never said Kabura stole the key.','negation'),
("Don't let anyone leave before Nazuna returns.",'negation'),
('Haru did not tell Azami where the supplies were.','roles'),
("If I hadn't warned you, you would have walked into the trap.",'conditional'),
('She asked me to give you her message, not mine.','roles'),
("I can't afford to lose you again.",'idiom'),
('He gave me the cold shoulder after our argument.','idiom'),
("Let's call it a day and head back.",'idiom'),
('You were right about the map, but wrong about the gate.','contrast'),
('Only two of the seven guards know the password.','numbers'),
('The Northern Alliance has never controlled Glass Harbor.','proper-name'),
('Meet Riven at the Ashen Tower before sunrise.','proper-name'),
("I thought you meant her brother, not her father.",'roles'),
('Keep the medicine for yourself. I already have some.','roles'),
('By the time we arrived, the last boat had left.','tense'),
("I used to trust him. That doesn't mean I still do.",'tense'),
('You can either wait here or come with me.','choice'),
("No, I don't want you to stop trying.",'negation')]
refs=json.loads((root/'artifacts/multimodal-evaluation/references.json').read_text(encoding='utf8'))
page=json.loads((root/'artifacts/manga-regression-23/references.json').read_text(encoding='utf8'))
cases=[]
for r in refs:
    if r.get('page') in {'manga-001','manga-002','manga-005'} or r['id'] in {'video-01-roi','video-06-roi','video-07-roi'}:
        cases.append({'id':r['id'],'text':r['text'],'origin':'previous-user-fixture','mode':'subtitles' if 'frame' in r else 'manga'})
for rid,box,text in page:
    cases.append({'id':'p23-'+rid,'text':text,'origin':'user-page23-manual-transcript','mode':'manga'})
for i,(text,tag) in enumerate(extra,1):
    cases.append({'id':f'synthetic-{i:02}','text':text,'origin':'synthetic-development','mode':'subtitles','category':tag})
assert len(cases)==60 and len({c['id'] for c in cases})==60
assert all(c['text'].strip() for c in cases)
folder=root/'data/evaluation'
folder.mkdir(parents=True,exist_ok=True)
(folder/'translation-cases.json').write_text(json.dumps({'role':'development-only; no held-out claim','cases':cases},ensure_ascii=False,indent=2),encoding='utf8')
rows=['id\tscore_0_to_3\tcritical_error\tname_error\tnotes']
rows += [c['id']+'\t\t\t\t' for c in cases]
(folder/'translation-review.tsv').write_text('\n'.join(rows)+'\n',encoding='utf8')
print('PASS: 60 unique nonempty development cases (42 supplied-fixture, 18 synthetic). No API call.')
