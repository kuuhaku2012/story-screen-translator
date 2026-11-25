"""Measure offline OCR replay of frames captured during normal video playback."""
from pathlib import Path
import hashlib
import json
import re
import statistics
import time

import cv2
import onnxruntime as ort
from rapidocr import RapidOCR

root = Path(r'E:\tool_dich_man_hinh')
out = root / 'artifacts/cutscene-new-video-20261022/live-replay'
model = root / 'models/ocr-evaluation/ppocrv6-medium'
if 'DmlExecutionProvider' not in ort.get_available_providers():
    raise RuntimeError('DirectML unavailable')
cv2.setNumThreads(2)
engine = RapidOCR(params={
    'Global.model_root_dir': str(root / 'models/ocr-evaluation'),
    'Global.log_level': 'warning',
    'Det.model_path': str(root / 'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
    'Rec.model_path': str(model / 'inference.onnx'),
    'Rec.rec_keys_path': str(model / 'characters.txt'),
    'Cls.model_path': str(root / 'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
    'Det.limit_type': 'max', 'Det.limit_side_len': 960,
    'EngineConfig.onnxruntime.intra_op_num_threads': 2,
    'EngineConfig.onnxruntime.inter_op_num_threads': 1,
    'EngineConfig.onnxruntime.use_dml': True,
})

def choose(blocks, width, height):
    def box(b):
        points = b['box']
        return min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)
    def letters(b):
        return len(re.findall(r'[A-Za-z]', b['text']))
    pairs = [(b, box(b)) for b in blocks]
    selected = {}
    for zone in ('top', 'bottom'):
        anchors = [(b, p) for b, p in pairs if letters(b) >= 20 and p[2]-p[0] >= width*.25
                   and width*.3 < (p[0]+p[2])/2 < width*.8
                   and ((p[1]+p[3])/2 < height*.13 if zone == 'top'
                        else height*.7 < (p[1]+p[3])/2 < height*.95)]
        if not anchors:
            continue
        if zone == 'top':
            picked = [(b, p) for b, p in pairs if p[1] < height*.12 and p[2] > width*.11 and letters(b) >= 2]
            picked.sort(key=lambda pair: pair[1][0])
        else:
            picked = [(b, p) for b, p in pairs if height*.7 < (p[1]+p[3])/2 < height*.95
                      and width*.15 < (p[0]+p[2])/2 < width*.85 and letters(b) >= 2]
            picked.sort(key=lambda pair: (pair[1][1], pair[1][0]))
        selected[zone] = ' '.join(b['text'] for b, _ in picked)
    return selected

def recognize(image):
    start = time.perf_counter()
    result = engine(image)
    ms = round((time.perf_counter()-start)*1000, 1)
    blocks = [] if result.boxes is None else [
        {'box': box.tolist(), 'text': text, 'score': round(float(score), 3)}
        for box, text, score in zip(result.boxes, result.txts, result.scores)]
    return ms, blocks

segments = [out, out / 'segment-b']
warm_image = cv2.imread(str(out / 'frame-00.png'))
recognize(warm_image)
reports = []
for segment in segments:
    log = json.loads((segment / 'capture-log.json').read_text(encoding='utf8'))
    frames = []
    finish_ms = 0
    for sample in log['frames']:
        path = segment / sample['file']
        image = cv2.imread(str(path))
        height, width = image.shape[:2]
        ms, blocks = recognize(image)
        selected = choose(blocks, width, height)
        finish_ms = max(sample['elapsedMs'], finish_ms) + ms
        frames.append({'file': sample['file'], 'captureMs': sample['elapsedMs'],
                       'ocrMs': ms, 'serialReadyMs': round(finish_ms, 1),
                       'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                       'selected': selected, 'blocks': blocks})
    reports.append({'segment': 'a' if segment == out else 'b',
                    'captureSpanMs': log['frames'][-1]['elapsedMs'],
                    'ocrMedianMs': round(statistics.median(f['ocrMs'] for f in frames), 1),
                    'ocrP95Ms': round(sorted(f['ocrMs'] for f in frames)[18], 1),
                    'uniqueSelectedTexts': len({tuple(f['selected'].items()) for f in frames}),
                    'frames': frames})
    print(reports[-1]['segment'], 'frames', len(frames), 'median/p95 OCR ms', reports[-1]['ocrMedianMs'], reports[-1]['ocrP95Ms'])
    for i, f in enumerate(frames):
        print(i, f['captureMs'], f['ocrMs'], f['selected'])

result = {'scope': 'OCR applied to saved frames after capture; serialReadyMs is a replay estimate, not observed live overlay latency.',
          'translation': 'not run: no Groq key was entered for this measurement',
          'capture': 'Codex in-app browser fullscreen while playing at normal speed; sampled approximately every 500 ms',
          'reports': reports}
(out / 'ocr-replay.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
