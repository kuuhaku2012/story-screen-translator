import json
import re
import sys
from pathlib import Path

source = Path(sys.argv[1])
image_name = "Screenshot 2026-10-22 001909.png"
width, height = 637, 908
regions = [
    {"id": "top-left", "rect": [60, 70, 205, 175], "text": "And you said, 'Mind your own beeswax. You don't know us.'"},
    {"id": "center-quote", "rect": [235, 72, 330, 300], "text": "'Take my advice,' I said. 'Leave the kid alone,' I said."},
    {"id": "top-right", "rect": [495, 60, 585, 160], "text": "Nazuna, Nazuna, Nazuna! What did I tell you?"},
    {"id": "middle-left", "rect": [80, 420, 200, 510], "text": "Ko has friends. Classmates. Study dates at diners."},
    {"id": "middle-right", "rect": [478, 400, 565, 500], "text": "But the truth is, you don't really know him either."},
    {"id": "bottom-left-short", "rect": [90, 640, 195, 715], "text": "Not with monsters."},
    {"id": "bottom-left-long", "rect": [315, 615, 425, 700], "text": "Humans should live human lives with fellow humans."},
    {"id": "bottom-right", "rect": [480, 585, 590, 700], "text": "That's the kind of life he ought to be living."},
]

def normalized(text):
    return re.sub(r"[^A-Z0-9]", "", text.upper())

def tokens(text):
    return [word for part in text.upper().split()
            if (word := re.sub(r"[^A-Z0-9]", "", part))]

def edit_distance(a, b):
    row = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        nxt = [i]
        for j, cb in enumerate(b, 1):
            nxt.append(min(nxt[-1] + 1, row[j] + 1, row[j - 1] + (ca != cb)))
        row = nxt
    return row[-1]

data = json.loads(source.read_text(encoding="utf-8"))
case = next(c for c in data["Cases"] if c["Group"] == "manga" and c["Name"] == image_name)
blocks = case["FullFrame"]["Blocks"]
records = []
assigned = set()
for region in regions:
    x1, y1, x2, y2 = region["rect"]
    chosen = []
    for index, block in enumerate(blocks):
        cx = block["X"] + block["Width"] / 2
        cy = block["Y"] + block["Height"] / 2
        if x1 <= cx <= x2 and y1 <= cy <= y2:
            chosen.append((index, block))
            assigned.add(index)
    chosen.sort(key=lambda pair: (pair[1]["Y"], pair[1]["X"]))
    observed = " ".join(block["Text"] for _, block in chosen)
    ref_norm, obs_norm = normalized(region["text"]), normalized(observed)
    ref_tokens, obs_tokens = tokens(region["text"]), tokens(observed)
    records.append({
        "id": region["id"],
        "rect_px": region["rect"],
        "rect_normalized": [round(x1 / width, 5), round(y1 / height, 5),
                            round((x2 - x1) / width, 5), round((y2 - y1) / height, 5)],
        "reference": region["text"],
        "observed": observed,
        "block_indices": [index for index, _ in chosen],
        "block_count": len(chosen),
        "exact_normalized": ref_norm == obs_norm,
        "character_errors": edit_distance(ref_norm, obs_norm),
        "reference_characters": len(ref_norm),
        "word_errors": edit_distance(ref_tokens, obs_tokens),
        "reference_words": len(ref_tokens),
    })
dialogue_chars = sum(r["reference_characters"] for r in records)
dialogue_words = sum(r["reference_words"] for r in records)
result = {
    "source_image": image_name,
    "source_sha256": case["Sha256"],
    "image_size": [width, height],
    "reference_method": "Manual transcript with approximate dialogue-region rectangles read from the full page. Center association groups OCR lines; this is not an IoU localization score.",
    "normalization": "Case-insensitive; punctuation and spaces ignored for CER and exact-region matching. WER uses alphanumeric tokens after punctuation removal.",
    "metrics": {
        "speech_regions": len(records),
        "regions_with_ocr_lines": sum(r["block_count"] > 0 for r in records),
        "exact_normalized_regions": sum(r["exact_normalized"] for r in records),
        "character_errors": sum(r["character_errors"] for r in records),
        "reference_characters": dialogue_chars,
        "cer": sum(r["character_errors"] for r in records) / dialogue_chars if dialogue_chars else 0,
        "word_errors": sum(r["word_errors"] for r in records),
        "reference_words": dialogue_words,
        "wer": sum(r["word_errors"] for r in records) / dialogue_words if dialogue_words else 0,
        "non_dialogue_detections": len(blocks) - len(assigned),
        "warm_full_page_median_ms": case["FullFrame"]["WarmMedianMilliseconds"],
        "full_page_block_count": len(blocks),
    },
    "regions": records,
    "non_dialogue_blocks": [b for i, b in enumerate(blocks) if i not in assigned],
    "notes": [
        "An ellipsis-only balloon is visible in the top-left panel and is excluded from lexical CER/WER.",
    ],
    "limitations": [
        "This is a manually labelled development page, not a blind holdout after scoring.",
        "Approximate region rectangles only group OCR lines by their centers; no geometric localization accuracy is claimed.",
        "This evaluates English OCR, not Vietnamese translation or overlay quality.",
        "The source is a saved screenshot; no game, API, or network was used."
    ]
}
out_dir = Path(sys.argv[2])
out_dir.mkdir(parents=True, exist_ok=True)
json_path = out_dir / "manga-sample-001909-reference.json"
md_path = out_dir / "manga-sample-001909-score.md"
json_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
m = result["metrics"]
lines = [
    "# Manga sample 001909 OCR score - 2026-10-22",
    "",
    "## Result",
    "",
    f"- Dialogue regions with OCR lines: {m['regions_with_ocr_lines']}/{m['speech_regions']}",
    f"- Exact region matches after case/punctuation normalization: {m['exact_normalized_regions']}/{m['speech_regions']}",
    f"- Dialogue CER: {m['cer'] * 100:.2f}% ({m['character_errors']}/{m['reference_characters']} characters)",
    f"- Dialogue WER: {m['wer'] * 100:.2f}% ({m['word_errors']}/{m['reference_words']} words)",
    f"- Unassigned OCR detections: {m['non_dialogue_detections']}",
    f"- Warm full-page OCR median: {m['warm_full_page_median_ms']:.1f} ms; {m['full_page_block_count']} detected blocks",
    "- One ellipsis-only balloon is visible but excluded from lexical CER/WER.",
    "",
    "| Region | Reference | OCR | Exact |",
    "|---|---|---|---:|",
]
for r in records:
    reference = r["reference"].replace("|", "\\|")
    observed = r["observed"].replace("|", "\\|")
    lines.append(f"| {r['id']} | {reference} | {observed} | {'yes' if r['exact_normalized'] else 'no'} |")
lines += [
    "",
    "## Limits",
    "",
    "This page is manually labelled development data, not a blind holdout. Rectangles only group detections by center; they do not measure bubble localization. This score does not assess Vietnamese translation quality or overlay fit.",
    "",
    "No game, translation API, or network was used."
]
md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps(result["metrics"], ensure_ascii=False))
print(json_path)
print(md_path)
