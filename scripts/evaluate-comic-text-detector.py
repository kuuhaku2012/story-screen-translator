from pathlib import Path
import json
import time
import cv2
import numpy as np
import psutil

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'artifacts/multimodal-evaluation'
MODEL = ROOT / 'models/ocr-evaluation/comic-text-detector/comictextdetector.pt.onnx'
OUT = DATA / 'comic-text-detector'
OUT.mkdir(parents=True, exist_ok=True)
PAGE_IDS = ['manga-001', 'manga-002', 'manga-005']
VIEWS = ['', '-730']
CONF = 0.4
NMS = 0.35
INPUT = 1024
THREADS = 2
cv2.setNumThreads(THREADS)
cv2.ocl.setUseOpenCL(False)
manifest = {x['id']: x for x in json.loads((DATA / 'manifest.json').read_text(encoding='utf8'))}
references = json.loads((DATA / 'references.json').read_text(encoding='utf8'))
net_start = time.perf_counter()
net = cv2.dnn.readNetFromONNX(str(MODEL))
load_ms = (time.perf_counter() - net_start) * 1000
names = net.getUnconnectedOutLayersNames()
process = psutil.Process()
records = []


def intersection_over_detector(a, b):
    x1, y1 = max(a[0], b[0]), max(a[1], b[1])
    x2, y2 = min(a[2], b[2]), min(a[3], b[3])
    inter = max(0, x2-x1) * max(0, y2-y1)
    area = max(1, (b[2]-b[0]) * (b[3]-b[1]))
    return inter / area


def draw_box(img, box, color, label):
    x1, y1, x2, y2 = [int(round(v)) for v in box]
    cv2.rectangle(img, (x1, y1), (x2, y2), color, 2)
    cv2.putText(img, label, (x1, max(18, y1-4)), cv2.FONT_HERSHEY_SIMPLEX, .48, color, 1, cv2.LINE_AA)

for page_id in PAGE_IDS:
    page_refs = [r for r in references if r.get('page') == page_id]
    for view in VIEWS:
        image_id = page_id + view
        item = manifest[image_id]
        image = cv2.imread(item['path'])
        if image is None:
            raise FileNotFoundError(item['path'])
        h, w = image.shape[:2]
        ratio = min(INPUT / h, INPUT / w)
        resized_w, resized_h = round(w * ratio), round(h * ratio)
        resized = cv2.resize(cv2.cvtColor(image, cv2.COLOR_BGR2RGB), (resized_w, resized_h), interpolation=cv2.INTER_LINEAR)
        padded = np.zeros((INPUT, INPUT, 3), dtype=np.uint8)
        padded[:resized_h, :resized_w] = resized
        blob = cv2.dnn.blobFromImage(padded, scalefactor=1 / 255.0, size=(INPUT, INPUT))
        net.setInput(blob)
        started = time.perf_counter()
        outputs = net.forward(names)
        infer_ms = (time.perf_counter() - started) * 1000
        raw = outputs[0][0]
        pred_xywh = raw[:, :4]
        objectness = raw[:, 4]
        class_scores = raw[:, 5:] * objectness[:, None]
        best_class = np.argmax(class_scores, axis=1)
        confidence = class_scores[np.arange(len(raw)), best_class]
        image_boxes = []
        for cls in np.unique(best_class[confidence > CONF]):
            ids = np.flatnonzero((confidence > CONF) & (best_class == cls))
            xywh = pred_xywh[ids]
            boxes = [[float(x-wb/2), float(y-hb/2), float(wb), float(hb)] for x,y,wb,hb in xywh]
            keep = cv2.dnn.NMSBoxes(boxes, confidence[ids].astype(float).tolist(), CONF, NMS, top_k=300)
            keep = np.asarray(keep).reshape(-1) if len(keep) else []
            for k in keep:
                x, y, bw, bh = boxes[int(k)]
                x1, y1 = max(0, x / ratio), max(0, y / ratio)
                x2, y2 = min(w, (x+bw) / ratio), min(h, (y+bh) / ratio)
                image_boxes.append({'Box':[x1,y1,x2,y2], 'Class':int(cls), 'Confidence':float(confidence[ids[int(k)]])})
        matched = []
        for ref in page_refs:
            nx, ny, nw, nh = ref['box']
            gt = [nx*w, ny*h, (nx+nw)*w, (ny+nh)*h]
            overlaps = [intersection_over_detector(gt, p['Box']) for p in image_boxes]
            best = max(overlaps, default=0.0)
            matched.append({'Id':ref['id'], 'DetectorInsideReference':best, 'Hit50':best >= .5, 'Hit80':best >= .8})
        visual = image.copy()
        for p in image_boxes:
            draw_box(visual, p['Box'], (30,30,230), f"D{p['Class']} {p['Confidence']:.2f}")
        for ref in page_refs:
            nx, ny, nw, nh = ref['box']
            gt = [nx*w, ny*h, (nx+nw)*w, (ny+nh)*h]
            draw_box(visual, gt, (40,180,40), ref['id'])
        visual_path = OUT / f'{image_id}-boxes.jpg'
        cv2.imwrite(str(visual_path), visual, [cv2.IMWRITE_JPEG_QUALITY, 92])
        records.append({'Id':image_id,'Width':w,'Height':h,'InferenceMs':infer_ms,'DetectorCount':len(image_boxes),
                        'Detections':image_boxes,'References':matched,'Visual':str(visual_path)})
        print(f'{image_id}: {infer_ms:.0f} ms, {len(image_boxes)} blocks, {sum(x["Hit50"] for x in matched)}/{len(matched)} speech regions covered >=50%', flush=True)

all_refs = [r for page in records for r in page['References']]
report = {'Status':'completed','Model':str(MODEL),'ModelSha256':'1a86ace74961413cbd650002e7bb4dcec4980ffa21b2f19b86933372071d718f',
          'Backend':'OpenCV DNN CPU','InputSize':INPUT,'Threads':THREADS,'ConfidenceThreshold':CONF,'NmsThreshold':NMS,
          'LoadMilliseconds':load_ms,'InferenceMilliseconds':[round(r['InferenceMs'],3) for r in records],
          'P50InferenceMilliseconds':float(np.median([r['InferenceMs'] for r in records])),
          'P95InferenceMilliseconds':float(np.percentile([r['InferenceMs'] for r in records],95)),
          'PeakWorkingSetMiB':getattr(process.memory_info(),'peak_wset',process.memory_info().rss)/1048576,
          'MetricDefinition':'For each manually cropped text region, find the maximum fraction of a predicted detector rectangle that lies inside that crop. This measures region containment, not speech-balloon boundary IoU.',
          'Hit50':sum(x['Hit50'] for x in all_refs),'Hit80':sum(x['Hit80'] for x in all_refs),'ReferenceCount':len(all_refs),
          'Records':records}
(DATA / 'comic-text-detector.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
