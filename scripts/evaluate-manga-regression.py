"""Offline OCR comparison for the user's page-172 regression image; no provider calls."""
import argparse
import json
import time
from pathlib import Path
import cv2
from rapidocr import RapidOCR, OCRVersion, ModelType, LangRec

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('image')
args = parser.parse_args()
out = root / 'artifacts/manga-regression-172'
out.mkdir(parents=True, exist_ok=True)
cv2.setNumThreads(2)
image = cv2.imread(args.image)
if image is None:
    raise ValueError('Cannot read supplied regression image')
cv2.imwrite(str(out / 'input.png'), image)
reports = []
for side in (960, 1440):
    engine = RapidOCR(params={
        'Global.model_root_dir': str(root / 'models/ocr-evaluation'),
        'Global.log_level': 'warning',
        'Det.ocr_version': OCRVersion.PPOCRV5, 'Det.model_type': ModelType.MOBILE,
        'Rec.ocr_version': OCRVersion.PPOCRV5, 'Rec.model_type': ModelType.MOBILE,
        'Rec.lang_type': LangRec.EN,
        'Det.limit_type': 'max', 'Det.limit_side_len': side,
        'EngineConfig.onnxruntime.intra_op_num_threads': 2,
        'EngineConfig.onnxruntime.inter_op_num_threads': 1,
    })
    for scale in (1, 2):
        current = image if scale == 1 else cv2.resize(image, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
        times = []
        for _ in range(2):
            start = time.perf_counter()
            result = engine(current)
            times.append(round((time.perf_counter() - start) * 1000, 2))
        blocks = []
        if result.boxes is not None:
            for box, text, score in zip(result.boxes, result.txts, result.scores):
                blocks.append({'text': text, 'score': float(score), 'box': (box / scale).tolist()})
        reports.append({'side': side, 'scale': scale, 'milliseconds': times, 'blocks': blocks})
        print(f'side={side}, scale={scale}: {times} ms, {len(blocks)} lines', flush=True)
        print(' | '.join(b['text'] for b in blocks), flush=True)
(out / 'rapid-comparison.json').write_text(json.dumps(reports, ensure_ascii=False, indent=2), encoding='utf-8')
