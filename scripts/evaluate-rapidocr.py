from pathlib import Path
import argparse
import importlib.metadata
import json
import time
import cv2
import psutil
from rapidocr import RapidOCR, OCRVersion, ModelType, LangRec

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "artifacts/multimodal-evaluation"
parser = argparse.ArgumentParser()
parser.add_argument("--variant",choices=["v6-small","v5-en-mobile","v5-en-rec-only","v6-medium-dml"],default="v6-small")
parser.add_argument("--ids",help="Optional comma-separated manifest IDs to evaluate")
args = parser.parse_args()
cv2.setNumThreads(2)
params = {
    "Global.model_root_dir":str(ROOT / "models/ocr-evaluation"),
    "EngineConfig.onnxruntime.intra_op_num_threads":2,
    "EngineConfig.onnxruntime.inter_op_num_threads":1,
    "Global.log_level":"warning", "Det.limit_type":"max", "Det.limit_side_len":960,
}
if args.variant in {"v5-en-mobile","v5-en-rec-only"}:
    params.update({"Det.ocr_version":OCRVersion.PPOCRV5,"Det.model_type":ModelType.MOBILE,
                   "Rec.ocr_version":OCRVersion.PPOCRV5,"Rec.model_type":ModelType.MOBILE,"Rec.lang_type":LangRec.EN})
if args.variant == "v5-en-rec-only":
    params.update({"Global.use_det":False,"Global.use_cls":False})
if args.variant == "v6-medium-dml":
    import yaml
    import onnxruntime as ort
    folder=ROOT / "models/ocr-evaluation/ppocrv6-medium"
    dictionary=yaml.safe_load((folder / "inference.yml").read_text(encoding="utf8"))["PostProcess"]["character_dict"]
    keys=folder / "characters.txt"
    keys.write_text("\n".join(dictionary)+"\n",encoding="utf8")
    params.update({
        "Det.model_path":str(ROOT / "models/ocr-evaluation/PP-OCRv6_det_small.onnx"),
        "Cls.model_path":str(ROOT / "models/ocr-evaluation/ch_ppocr_mobile_v2.0_cls_mobile.onnx"),
        "Rec.model_path":str(folder / "inference.onnx"),
        "Rec.rec_keys_path":str(keys),
        "EngineConfig.onnxruntime.use_dml":True,
    })
    if "DmlExecutionProvider" not in ort.get_available_providers():
        raise RuntimeError("v6-medium-dml requested but DmlExecutionProvider is unavailable")
started = time.perf_counter()
engine = RapidOCR(params=params)
load_ms = (time.perf_counter()-started)*1000
inputs = json.loads((DATA / "manifest.json").read_text(encoding="utf8"))
if args.variant == "v5-en-rec-only":
    inputs=[x for x in inputs if x["id"].startswith("video-")]
if args.ids:
    requested=[value.strip() for value in args.ids.split(",") if value.strip()]
    by_id={item["id"]:item for item in inputs}
    unknown=[value for value in requested if value not in by_id]
    if unknown:
        raise ValueError(f"Unknown evaluation IDs: {unknown}")
    inputs=[by_id[value] for value in requested]
process = psutil.Process()
reports = []
for item in inputs:
    image = cv2.imread(item["path"])
    times = []
    cpu_start = process.cpu_times()
    output = None
    for repeat in range(3):
        started = time.perf_counter()
        output = engine(image)
        times.append(round((time.perf_counter()-started)*1000,3))
    cpu_end = process.cpu_times()
    memory = process.memory_info()
    raw_boxes=getattr(output,"boxes",None)
    boxes = [] if raw_boxes is None else raw_boxes.tolist()
    texts = [] if output.txts is None else list(output.txts)
    scores = [] if output.scores is None else list(output.scores)
    if raw_boxes is None and texts and args.variant == "v5-en-rec-only":
        h,w=image.shape[:2]
        boxes=[[[0,0],[w,0],[w,h],[0,h]] for _ in texts]
    records = []
    for box,text,score in zip(boxes,texts,scores):
        xs=[p[0] for p in box]; ys=[p[1] for p in box]
        records.append({"Text":text,"X":min(xs),"Y":min(ys),"Width":max(xs)-min(xs),"Height":max(ys)-min(ys),"Score":float(score)})
    reports.append({"Id":item["id"],"Milliseconds":times,"CpuMilliseconds":round((cpu_end.user+cpu_end.system-cpu_start.user-cpu_start.system)*1000,3),
                    "PeakWorkingSetMiB":getattr(memory,"peak_wset",memory.rss)/1048576,"Blocks":records})
    print(f"{args.variant}: {item['id']} {times[-1]:.1f} ms, {len(records)} lines",flush=True)
(DATA / ("rapid-"+args.variant+("-followup" if args.ids else "")+".json")).write_text(json.dumps({"Status":"completed","LoadMilliseconds":load_ms,
    "Packages":{n:importlib.metadata.version(n) for n in ["rapidocr","onnxruntime","opencv-python-headless"]},
    "ExecutionProviders":(engine.text_det.session.session.get_providers() if args.variant=="v6-medium-dml" else []),
    "Parameters":{"threads":2,"det_limit_type":"max","det_limit_side_len":960,"variant":args.variant,"ids":[item["id"] for item in inputs]},"Reports":reports},ensure_ascii=False,indent=2),encoding="utf8")
