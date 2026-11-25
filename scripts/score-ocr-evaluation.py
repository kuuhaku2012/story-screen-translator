from pathlib import Path
import json
import math
import re
import statistics
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "artifacts/multimodal-evaluation"
refs = json.loads((ROOT / "references.json").read_text(encoding="utf8"))

def words(text):
    return re.findall(r"[A-Z0-9]+(?:'[A-Z]+)?", text.upper().replace("’", "'"))

def chars(text):
    return re.sub(r"[^A-Z0-9]", "", text.upper())

def distance(a,b):
    row=list(range(len(b)+1))
    for i,left in enumerate(a,1):
        updated=[i]
        for j,right in enumerate(b,1):
            updated.append(min(updated[-1]+1,row[j]+1,row[j-1]+(left != right)))
        row=updated
    return row[-1]

def ordered_text(blocks):
    return " ".join(b["Text"].replace("\n"," ") for b in sorted(blocks,key=lambda b:(b["Y"],b["X"])))

def percentile(values,p):
    values=sorted(values)
    return round(values[max(0,math.ceil(len(values)*p)-1)],3) if values else None

sources={}
win=json.loads((ROOT / "windows-ocr.json").read_text(encoding="utf8"))
for comic in (False,True):
    sources["windows-2x" if comic else "windows-normal"]=[r for r in win["Reports"] if r["ComicMode"] == comic]
for name in ("v6-small","v5-en-mobile"):
    sources["rapid-"+name]=json.loads((ROOT / ("rapid-"+name+".json")).read_text(encoding="utf8"))["Reports"]
if (ROOT / "rapid-v5-en-rec-only.json").exists():
    sources["rapid-v5-en-rec-only"]=json.loads((ROOT / "rapid-v5-en-rec-only.json").read_text(encoding="utf8"))["Reports"]
layout_path=ROOT / "rapid-production-layout.json"
if layout_path.exists():
    grouped=json.loads(layout_path.read_text(encoding="utf8"))["Reports"]
    raw={r["Id"]:r for r in sources["rapid-v6-small"]}
    for comic in (False,True):
        sources["rapid-v6-layout-2x-rule" if comic else "rapid-v6-layout-normal"]=[
            {**raw[r["Id"]],"Blocks":r["Blocks"]} for r in grouped if r["ComicMode"] == comic]

summary=[]
details=[]
for engine,reports in sources.items():
    lookup={r["Id"]:r for r in reports}
    for group in ("manga-crops","manga-full-1200","manga-full-730","video-roi","video-text-roi","scroll-screen"):
        if engine == "rapid-v5-en-rec-only" and group not in {"video-roi","video-text-roi"}: continue
        selected=[]
        timings=[]
        peaks=[]
        for ref in refs:
            if group in {"video-roi","video-text-roi"}:
                if "frame" not in ref: continue
                if group == "video-text-roi" and not ref["text"]: continue
                report=lookup[ref["id"]]
                actual=ordered_text(report["Blocks"])
            elif group == "manga-crops":
                if "page" not in ref or ref["page"].startswith("manga-scroll"): continue
                report=lookup[ref["id"]]
                actual=ordered_text(report["Blocks"])
            elif group == "scroll-screen":
                if ref.get("page") != "manga-scroll-01": continue
                report=lookup[ref["page"]]
                image=Image.open(ROOT / "inputs" / (ref["page"]+".png"))
                box=ref["box"]
                actual=ordered_text([b for b in report["Blocks"] if box[0] <= (b["X"]+b["Width"]/2)/image.width <= box[2] and
                                     box[1] <= (b["Y"]+b["Height"]/2)/image.height <= box[3]])
            else:
                if "page" not in ref or ref["page"].startswith("manga-scroll"): continue
                suffix="-730" if group.endswith("730") else ""
                report=lookup[ref["page"]+suffix]
                image=Image.open(ROOT / "inputs" / (ref["page"]+suffix+".png"))
                box=ref["box"]
                actual=ordered_text([b for b in report["Blocks"] if box[0] <= (b["X"]+b["Width"]/2)/image.width <= box[2] and
                                     box[1] <= (b["Y"]+b["Height"]/2)/image.height <= box[3]])
            truth=ref["text"]
            item={"engine":engine,"group":group,"id":ref["id"],"reference":truth,"actual":actual,
                  "char_errors":distance(chars(truth),chars(actual)),"reference_chars":len(chars(truth)),
                  "word_errors":distance(words(truth),words(actual)),"reference_words":len(words(truth)),
                  "exact_letters_digits":chars(truth)==chars(actual)}
            selected.append(item); details.append(item)
        ids=set()
        for r in reports:
            name=r["Id"]
            choose=(group=="manga-crops" and name.startswith(("m1-","m2-","m5-"))) or \
                   (group=="manga-full-1200" and name in {"manga-001","manga-002","manga-005"}) or \
                   (group=="manga-full-730" and name in {"manga-001-730","manga-002-730","manga-005-730"}) or \
                   (group in {"video-roi","video-text-roi"} and name.startswith("video-") and (group=="video-roi" or name in {r["id"] for r in refs if "frame" in r and r["text"]})) or \
                   (group=="scroll-screen" and name in {"manga-screen","manga-scroll-01","manga-scroll-02"})
            if choose and name not in ids:
                ids.add(name)
                # First of each three calls is excluded from warmed timing summaries.
                timings.extend(r["Milliseconds"][1:]); peaks.append(r["PeakWorkingSetMiB"])
        positive=[x for x in selected if x["reference_chars"]]
        blank=[x for x in selected if not x["reference_chars"]]
        names=[]
        for x in positive:
            expected={w.split("'")[0] for w in words(x["reference"])} & {"KABURA","HARU","HARUKA","NAZUNA"}
            observed={w.split("'")[0] for w in words(x["actual"])}
            names.extend(n in observed for n in expected)
        summary.append({"engine":engine,"group":group,"positive_regions":len(positive),
            "char_error_percent":round(100*sum(x["char_errors"] for x in positive)/max(1,sum(x["reference_chars"] for x in positive)),2),
            "word_error_percent":round(100*sum(x["word_errors"] for x in positive)/max(1,sum(x["reference_words"] for x in positive)),2),
            "exact_regions":sum(x["exact_letters_digits"] for x in positive),"names_correct":sum(names),"names_total":len(names),
            "blank_false_positives":sum(bool(chars(x["actual"])) for x in blank),"blank_samples":len(blank),
            "warm_p50_ms":round(statistics.median(timings),3) if timings else None,"warm_p95_ms":percentile(timings,.95),
            "peak_process_working_set_mib":round(max(peaks),2) if peaks else None})

(ROOT / "ocr-scores.json").write_text(json.dumps({"notes":["CER uses uppercased ASCII letters/digits only; WER retains word boundaries and apostrophes.",
    "Whole-page scores include current Windows paragraph grouping and ROI association errors; Rapid results are line boxes, not final speech bubbles.",
    "Warmed timing excludes first repeat per fixture. Small fixture set; percentiles are descriptive, not population guarantees.",
    "Video inputs are selected still frames including 1x-playback captures, not a full live OCR/translation run."],"summary":summary,"details":details},ensure_ascii=False,indent=2),encoding="utf8")
print(json.dumps(summary,ensure_ascii=False,indent=2))

fragmentation=[]
for engine,reports in sources.items():
    if engine not in {"windows-normal","windows-2x","rapid-v6-layout-normal","rapid-v6-layout-2x-rule"}: continue
    for suffix in ("","-730"):
        counts=[]
        for ref in refs:
            if ref.get("page") not in {"manga-001","manga-002","manga-005"}: continue
            report=next(r for r in reports if r["Id"]==ref["page"]+suffix)
            image=Image.open(ROOT / "inputs" / (ref["page"]+suffix+".png"))
            box=ref["box"]
            parts=[b for b in report["Blocks"] if box[0] <= (b["X"]+b["Width"]/2)/image.width <= box[2] and
                   box[1] <= (b["Y"]+b["Height"]/2)/image.height <= box[3]]
            counts.append({"id":ref["id"],"parts":len(parts)})
        fragmentation.append({"engine":engine,"width":730 if suffix else 1200,"regions":len(counts),
                              "one_block":sum(c["parts"]==1 for c in counts),"fragmented":sum(c["parts"]>1 for c in counts),
                              "missing":sum(c["parts"]==0 for c in counts),"details":counts})
(ROOT / "layout-fragmentation.json").write_text(json.dumps(fragmentation,indent=2),encoding="utf8")
