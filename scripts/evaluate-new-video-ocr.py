from pathlib import Path
import hashlib
import json
import statistics
import time

import cv2
import onnxruntime as ort
import yaml
from rapidocr import RapidOCR

root = Path(r'E:\tool_dich_man_hinh')
folder = root / 'artifacts/cutscene-new-video-20261022'
model = root / 'models/ocr-evaluation/ppocrv6-medium'
dictionary = yaml.safe_load((model / 'inference.yml').read_text(encoding='utf8'))['PostProcess']['character_dict']
keys = model / 'characters.txt'
if not keys.exists():
    keys.write_text('\n'.join(dictionary) + '\n', encoding='utf8')
if 'DmlExecutionProvider' not in ort.get_available_providers():
    raise RuntimeError('DirectML unavailable')
cv2.setNumThreads(2)
engine = RapidOCR(params={
    'Global.model_root_dir': str(root / 'models/ocr-evaluation'),
    'Global.log_level': 'warning',
    'Det.model_path': str(root / 'models/ocr-evaluation/PP-OCRv6_det_small.onnx'),
    'Rec.model_path': str(model / 'inference.onnx'),
    'Rec.rec_keys_path': str(keys),
    'Cls.model_path': str(root / 'models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx'),
    'Det.limit_type': 'max',
    'Det.limit_side_len': 960,
    'EngineConfig.onnxruntime.intra_op_num_threads': 2,
    'EngineConfig.onnxruntime.inter_op_num_threads': 1,
    'EngineConfig.onnxruntime.use_dml': True,
})

def recognize(image):
    begin = time.perf_counter()
    result = engine(image)
    elapsed = round((time.perf_counter() - begin) * 1000, 1)
    blocks = []
    if result.boxes is not None:
        for box, txt, score in zip(result.boxes, result.txts, result.scores):
            blocks.append({'text': txt, 'score': round(float(score), 3), 'box': box.tolist()})
    return {'milliseconds': elapsed, 'blocks': blocks}

files = [folder / name for name in [
    'iOV-a7O3IkI-p10.png',
    'iOV-a7O3IkI-p20.png',
    'iOV-a7O3IkI-04m29s.png',
    'iOV-a7O3IkI-p50.png',
    'iOV-a7O3IkI-10m28s-clean.png',
    'iOV-a7O3IkI-13m27s-clean.png',
]]
warm = cv2.imread(str(files[0]))
recognize(warm)
reports = []
for path in files:
    image = cv2.imread(str(path))
    h, w = image.shape[:2]
    # These are fixed diagnostic zones for this 1920x1080 YouTube player.
    zones = {
        'full': image,
        'top_lyrics': image[20:130, 100:1820],
        'bottom_caption': image[790:1015, 250:1670],
    }
    outputs = {name: recognize(crop) for name, crop in zones.items()}
    reports.append({
        'file': path.name,
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'size': [w, h],
        'zones': outputs,
    })
    print(path.name, {name: {'ms': value['milliseconds'], 'text': [b['text'] for b in value['blocks']]} for name, value in outputs.items()}, flush=True)
result = {
    'source': 'https://www.youtube.com/watch?v=iOV-a7O3IkI',
    'capture': 'Codex in-app browser, YouTube fullscreen player, paused; first four screenshots include player controls, last two were captured after controls faded. Ad frames excluded.',
    'network': 'No network calls for OCR; only user-provided YouTube video viewed in browser',
    'model': 'PP-OCRv6 medium recognition + small detection, ONNX Runtime DirectML',
    'zones': {'top_lyrics': [100, 20, 1820, 130], 'bottom_caption': [250, 790, 1670, 1015]},
    'reports': reports,
}
(folder / 'ocr-results-valid.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
for zone in ['full', 'top_lyrics', 'bottom_caption']:
    print(zone, 'median ms', statistics.median(report['zones'][zone]['milliseconds'] for report in reports))
