import json
import re
import sys
from pathlib import Path

source = Path(sys.argv[1])
out_dir = Path(sys.argv[2])
data = json.loads(source.read_text(encoding="utf-8"))
frames = [case for case in data["Cases"] if case["Group"] == "video"]

def normalize(value):
    return re.sub(r"[^a-z0-9]", "", value.lower())

def score(full_blocks, case, max_area=None, min_conf=None):
    width = case["FullFrame"]["Width"]
    height = case["FullFrame"]["Height"]
    kept = []
    removed = []
    for block in full_blocks:
        area_ratio = (block["Width"] * block["Height"]) / (width * height)
        reason = []
        if max_area is not None and area_ratio > max_area:
            reason.append("box_area")
        if min_conf is not None and block["Score"] < min_conf:
            reason.append("confidence")
        if reason:
            removed.append({"text": block["Text"], "area_ratio": round(area_ratio, 6),
                            "score": block["Score"], "rejected_for": reason})
        else:
            kept.append(block)
    observed = " ".join(block["Text"] for block in kept)
    ref = case["Reference"]
    if ref["Kind"] == "caption":
        correct = normalize(observed) == normalize(ref["Expected"])
        false_positive = False
    else:
        correct = not bool(observed.strip())
        false_positive = bool(observed.strip())
    return {
        "name": case["Name"],
        "kind": ref["Kind"],
        "expected": ref["Expected"],
        "observed": observed,
        "correct": correct,
        "false_positive": false_positive,
        "kept_blocks": len(kept),
        "removed_blocks": removed,
    }

variants = [
    {"name": "raw_full_frame", "max_area": None, "min_conf": None},
    {"name": "area_le_10pct", "max_area": 0.10, "min_conf": None},
    {"name": "confidence_ge_0.75", "max_area": None, "min_conf": 0.75},
    {"name": "area_le_10pct_and_confidence_ge_0.75", "max_area": 0.10, "min_conf": 0.75},
]
out = {"source": str(source), "sample_count": len(frames), "metrics": [], "cases": {}}
for variant in variants:
    rows = [score(case["FullFrame"]["Blocks"], case, variant["max_area"], variant["min_conf"])
            for case in frames]
    captions = [row for row in rows if row["kind"] == "caption"]
    blanks = [row for row in rows if row["kind"] == "blank"]
    out["metrics"].append({
        "name": variant["name"],
        "caption_exact_matches": sum(row["correct"] for row in captions),
        "caption_count": len(captions),
        "blank_false_positives": sum(row["false_positive"] for row in blanks),
        "blank_count": len(blanks),
    })
    out["cases"][variant["name"]] = rows

band_captions = [case["SubtitleBandReference"] for case in frames
                 if case["SubtitleBandReference"]["Kind"] == "caption"]
band_blanks = [case["SubtitleBandReference"] for case in frames
               if case["SubtitleBandReference"]["Kind"] == "blank"]
out["fixed_bottom_band_baseline"] = {
    "caption_exact_matches": sum(ref["Match"] for ref in band_captions),
    "caption_count": len(band_captions),
    "blank_false_positives": sum(ref["FalsePositive"] for ref in band_blanks),
    "blank_count": len(band_blanks),
    "warm_median_ms": [case["SubtitleBand"]["WarmMedianMilliseconds"] for case in frames],
}
out["full_frame_warm_median_ms"] = [case["FullFrame"]["WarmMedianMilliseconds"] for case in frames]
out["limitations"] = [
    "Only the existing five frames are used: three caption frames and two blank frames.",
    "All caption frames in this sample place text near the bottom; arbitrary-position coverage is untested.",
    "The thresholds were selected after inspecting this tiny sample and must be validated on a new holdout.",
    "This is an offline diagnostic, not an app change, live-capture test, translation test, latency-to-overlay test, or game-safety result."
]
out_dir.mkdir(parents=True, exist_ok=True)
json_path = out_dir / "caption-box-filter.json"
md_path = out_dir / "caption-box-filter.md"
json_path.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
lines = [
    "# Cutscene full-frame OCR box-filter diagnostic - 2026-10-22",
    "",
    "## Result",
    "",
    "A post-OCR geometry/confidence filter keeps OCR on the full frame, so it does not assume subtitles stay at the bottom.",
    "On the existing five saved frames, rejecting boxes larger than 10% of frame area or confidence below 0.75 keeps all 3/3 caption strings and removes the single full-frame false positive on blank frames (0/2 false positives).",
    "",
    "| Method | Exact caption matches | Blank false positives |",
    "|---|---:|---:|",
]
for row in out["metrics"]:
    lines.append(f"| {row['name']} | {row['caption_exact_matches']}/{row['caption_count']} | {row['blank_false_positives']}/{row['blank_count']} |")
lines.append(f"| fixed bottom band (existing comparison) | {out['fixed_bottom_band_baseline']['caption_exact_matches']}/{out['fixed_bottom_band_baseline']['caption_count']} | {out['fixed_bottom_band_baseline']['blank_false_positives']}/{out['fixed_bottom_band_baseline']['blank_count']} |")
lines += [
    "",
    "## Timing",
    "",
    "Warm full-frame OCR medians (ms): " + ", ".join(f"{value:.1f}" for value in out["full_frame_warm_median_ms"]) + ".",
    "Warm bottom-band OCR medians (ms): " + ", ".join(f"{value:.1f}" for value in out["fixed_bottom_band_baseline"]["warm_median_ms"]) + ".",
    "The filter adds negligible post-processing; no new OCR pass is involved.",
    "",
    "## Limitation",
    "",
    "This is only a post-hoc check on the same five frames, not a blind evaluation. Three captions and two blank frames are insufficient to accept thresholds or claim generalization. The saved captions all appear near the bottom, so this does not establish arbitrary-position subtitle coverage.",
    "",
    "Next data needed: holdout frames with subtitles near the top/center, bilingual or multi-line subtitles, movement/transition frames, and blank frames. Then verify the filter without tuning on that holdout and measure capture-to-overlay latency.",
    "",
    "No network, translation API, game process, or game account was used."
]
md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json_path)
print(md_path)
print(json.dumps(out["metrics"], ensure_ascii=False))
