"""Offline stage measurements. This does not simulate a live overlay or include capture/stability/queue delay."""
from pathlib import Path
import json
import time
import ctranslate2
import psutil
import sentencepiece

PROJECT=Path(__file__).resolve().parents[1]
ROOT=PROJECT / "artifacts/multimodal-evaluation"
refs=json.loads((ROOT / "references.json").read_text(encoding="utf8"))
scores=json.loads((ROOT / "ocr-scores.json").read_text(encoding="utf8"))
model=PROJECT / "models/nllb-en-vi/ct2-int8"
tokenizer=sentencepiece.SentencePieceProcessor(model_file=str(model / "sentencepiece.bpe.model"))
translator=ctranslate2.Translator(str(model),device="cpu",compute_type="int8",intra_threads=2,inter_threads=1)
process=psutil.Process()

def translate(texts):
    source=[["eng_Latn"]+tokenizer.encode(text,out_type=str)+["</s>"] for text in texts]
    result=translator.translate_batch(source,target_prefix=[["vie_Latn"] for _ in texts],beam_size=1,max_decoding_length=192)
    return [tokenizer.decode(r.hypotheses[0][1:]).replace("▁"," ").strip() for r in result]

translate(["Hello."])
cases=[]
ids={"m5-gripes","m5-loses","m5-back","m5-haru","m5-crap","video-01-roi","video-06-roi","video-07-roi"}
for ref in refs:
    if ref["id"] not in ids: continue
    engine="windows-2x" if "frame" in ref else "rapid-v6-small"
    group="video-roi" if "frame" in ref else "manga-full-730"
    actual=next(x["actual"] for x in scores["details"] if x["engine"]==engine and x["group"]==group and x["id"]==ref["id"])
    outputs=[]
    for label,text in [("manual_clean",ref["text"]),("ocr_actual",actual)]:
        started=time.perf_counter()
        translated=translate([text])[0] if text else ""
        outputs.append({"kind":label,"source":text,"translation":translated,"translation_ms":round((time.perf_counter()-started)*1000,3)})
    cases.append({"id":ref["id"],"ocr_engine":engine,"results":outputs})
    print(f"Translated comparison {ref['id']}",flush=True)

batch_results=[]
all_bubbles=[r["text"] for r in refs if r.get("page") in {"manga-001","manga-002","manga-005"}]
for size in [1,6,22]:
    times=[]
    cpu_start=process.cpu_times()
    for repeat in range(3):
        started=time.perf_counter(); translated=translate(all_bubbles[:size]); times.append(round((time.perf_counter()-started)*1000,3))
    cpu_end=process.cpu_times()
    memory=process.memory_info()
    batch_results.append({"sentences":size,"milliseconds":times,"cpu_milliseconds":round(1000*(cpu_end.user+cpu_end.system-cpu_start.user-cpu_start.system),3),
                          "peak_working_set_mib":getattr(memory,"peak_wset",memory.rss)/1048576,"translations":translated})
    print(f"NLLB batch {size}: {times}",flush=True)
(ROOT / "local-chain.json").write_text(json.dumps({"scope":"Offline OCR-text-to-NLLB and warmed text-only batches; no capture/stability/queue/overlay timing",
    "model":"facebook/nllb-200-distilled-600M INT8, beam1, CPU2threads","cases":cases,"batches":batch_results},ensure_ascii=False,indent=2),encoding="utf8")
