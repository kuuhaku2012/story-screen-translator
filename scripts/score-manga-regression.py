"""Compare whole-page OCR with isolated, manually bounded text crops; not an automatic detector."""
from pathlib import Path
import json
import re
import time
import cv2
from rapidocr import RapidOCR, OCRVersion, ModelType, LangRec

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/manga-regression-172'
image = cv2.imread(str(out / 'input.png'))
references = [
    ('being', (215,66,320,211), "BEING AROUND DRUNK PEOPLE WHEN YOU'RE SOBER IS A DRAG."),
    ('time', (337,69,423,180), "BY THE TIME I SHOW UP, THEY'RE ALREADY WASTED."),
    ('nazuna', (438,112,529,199), 'NAZUNA! YOU LOOK LIKE YOU NEED A DRINK.'),
    ('not-only', (551,64,614,139), 'NOT ONLY THAT'),
    ('waiter', (72,239,141,301), 'WAITER! BEER ME!'),
    ('another', (145,213,225,299), 'BET YOU COULD GO FOR ANOTHER BEER.'),
    ('look', (100,378,205,479), 'LIKE, LOOK AT THOSE GUYS. THEY GET IT.'),
    ('going', (238,378,334,478), 'GOING OUT FOR DRINKS IS SUPPOSED TO BE FUN.'),
    ('seen', (63,482,140,570), "I'VE SEEN THEM AROUND A LOT."),
    ('worth', (367,376,435,434), 'REALLY WORTH IT?'),
    ('free', (548,375,616,453), 'ARE THE FREE ROUNDS...'),
    ('couple', (174,632,277,694), 'A COUPLE...'),
    ('friends', (120,695,225,750), 'OF FRIENDS...'),
    ('night', (66,775,171,890), 'SHEESH. THAT REMINDS ME OF THAT ONE NIGHT...'),
    ('laugh', (369,636,456,750), 'AND LAUGH LIKE IDIOTS OVER NOTHING.'),
    ('chill', (504,634,619,739), 'I JUST WANNA CHILL WITH A COUPLE OF FRIENDS...'),
]

def normalized(s):
    return re.sub('[^A-Z0-9]', '', s.upper())

def distance(a, b):
    row = list(range(len(b)+1))
    for i, ac in enumerate(a, 1):
        nxt = [i]
        for j, bc in enumerate(b, 1):
            nxt.append(min(nxt[-1]+1, row[j]+1, row[j-1]+(ac != bc)))
        row = nxt
    return row[-1]

cv2.setNumThreads(2)
engine = RapidOCR(params={
    'Global.model_root_dir': str(root / 'models/ocr-evaluation'), 'Global.log_level': 'warning',
    'Det.ocr_version': OCRVersion.PPOCRV5, 'Det.model_type': ModelType.MOBILE,
    'Rec.ocr_version': OCRVersion.PPOCRV5, 'Rec.model_type': ModelType.MOBILE, 'Rec.lang_type': LangRec.EN,
    'EngineConfig.onnxruntime.intra_op_num_threads': 2, 'EngineConfig.onnxruntime.inter_op_num_threads': 1,
    'Det.limit_type': 'max', 'Det.limit_side_len': 960,
})
full = json.loads((out / 'rapid-comparison.json').read_text(encoding='utf-8'))[-1]
records = []
for name, (x1,y1,x2,y2), source in references:
    crop = cv2.resize(image[y1:y2,x1:x2], None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC)
    crop = cv2.copyMakeBorder(crop, 16,16,16,16, cv2.BORDER_CONSTANT, value=(255,255,255))
    start = time.perf_counter()
    result = engine(crop)
    ms = round((time.perf_counter()-start)*1000, 2)
    text = ' '.join(result.txts or [])
    full_blocks = [b for b in full['blocks'] if x1 <= sum(p[0] for p in b['box'])/4 <= x2 and y1 <= sum(p[1] for p in b['box'])/4 <= y2]
    full_text = ' '.join(b['text'] for b in sorted(full_blocks, key=lambda b: (min(p[1] for p in b['box']), min(p[0] for p in b['box']))))
    ref = normalized(source)
    records.append({'id':name, 'bounds':[x1,y1,x2,y2], 'reference':source, 'full':full_text, 'crop':text,
                    'milliseconds':ms, 'full_errors':distance(ref,normalized(full_text)), 'crop_errors':distance(ref,normalized(text)), 'characters':len(ref)})
    print(f'{name}: {text} ({ms} ms)', flush=True)
total = sum(r['characters'] for r in records)
report = {'notes':['16 manually bounded regions; crop experiment does not include automatic region detection latency.',
                    'Character score uppercases and excludes spaces/punctuation; quality of Vietnamese translation is not measured.'],
          'full_character_error_percent':round(sum(r['full_errors'] for r in records)/total*100,2),
          'crop_character_error_percent':round(sum(r['crop_errors'] for r in records)/total*100,2),
          'crop_total_ms':round(sum(r['milliseconds'] for r in records),2), 'regions':records}
(out/'region-comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k != 'regions'}), flush=True)
