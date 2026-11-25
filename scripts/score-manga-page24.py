import json
import re
import sys
from pathlib import Path

source = Path(sys.argv[1])
image_name = "Screenshot 2026-10-22 001824.png"
width, height = 657, 920
regions = [
    {"id": "top-left", "rect": [105, 80, 225, 205], "text": "When you have Ko to feed on?"},
    {"id": "top-middle", "rect": [270, 95, 400, 225], "text": "...Why would you be scouting out new sources of blood..."},
    {"id": "top-right", "rect": [450, 80, 575, 205], "text": "The question this raises is..."},
    {"id": "middle-right", "rect": [465, 300, 580, 425], "text": "Of course, blood is your staple food, so it wouldn't hurt to have a few other veins on tap..."},
    {"id": "middle-left", "rect": [100, 395, 225, 475], "text": "...But still- why now?"},
    {"id": "bottom-left", "rect": [106, 545, 294, 675], "text": "Kyoko? Can we bounce?"},
    {"id": "bottom-right", "rect": [470, 735, 570, 835], "text": "Have you turned into a foodie?"},
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
    char_errors = edit_distance(ref_norm, obs_norm)
    word_errors = edit_distance(ref_tokens, obs_tokens)
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
        "character_errors": char_errors,
        "reference_characters": len(ref_norm),
        "word_errors": word_errors,
        "reference_words": len(ref_tokens),
    })
dialogue_chars = sum(r["reference_characters"] for r in records)
dialogue_words = sum(r["reference_words"] for r in records)
char_errors = sum(r["character_errors"] for r in records)
word_errors = sum(r["word_errors"] for r in records)
result = {
    "source_image": image_name,
    "source_sha256": case["Sha256"],
    "image_size": [width, height],
    "reference_method": "Manual transcript with approximate dialogue-region rectangles read from the full page. Center association groups OCR lines; this is not an IoU localization score.",
    "normalization": "Case-insensitive; punctuation and spaces ignored for CER and exact-region matching. Word errors are computed over alphanumeric tokens.",
    "metrics": {
        "speech_regions": len(records),
        "regions_with_ocr_lines": sum(r["block_count"] > 0 for r in records),
        "exact_normalized_regions": sum(r["exact_normalized"] for r in records),
        "character_errors": char_errors,
        "reference_characters": dialogue_chars,
        "cer": char_errors / dialogue_chars if dialogue_chars else 0,
        "word_errors": word_errors,
        "reference_words": dialogue_words,
        "wer": word_errors / dialogue_words if dialogue_words else 0,
        "non_dialogue_detections": len(blocks) - len(assigned),
        "warm_full_page_median_ms": case["FullFrame"]["WarmMedianMilliseconds"],
        "full_page_block_count": len(blocks),
    },
    "regions": records,
    "non_dialogue_blocks": [b for i, b in enumerate(blocks) if i not in assigned],
    "limitations": [
        "This is one manually labelled page and not a blind holdout after this scoring.",
        "Approximate region rectangles only group OCR lines; no geometric localization accuracy is claimed.",
        "This evaluates English OCR, not Vietnamese translation or overlay quality.",
        "The source is a saved screenshot; no game, API, or network was used."
    ]
}
out_dir = Path(sys.argv[2])
out_dir.mkdir(parents=True, exist_ok=True)
json_path = out_dir / "manga-page24-reference.json"
md_path = out_dir / "manga-page24-score.md"
json_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
m = result["metrics"]
lines = [
    "# Manga page 24 OCR score - 2026-10-22",
    "",
    "## Result",
    "",
    f"- Dialogue regions with OCR lines: {m['regions_with_ocr_lines']}/{m['speech_regions']}",
    f"- Exact region matches after case/punctuation normalization: {m['exact_normalized_regions']}/{m['speech_regions']}",
    f"- Dialogue CER: {m['cer'] * 100:.2f}% ({m['character_errors']}/{m['reference_characters']} characters)",
    f"- Dialogue WER: {m['wer'] * 100:.2f}% ({m['word_errors']}/{m['reference_words']} words)",
    f"- Unassigned OCR detections: {m['non_dialogue_detections']}",
    f"- Warm full-page OCR median: {m['warm_full_page_median_ms']:.1f} ms; {m['full_page_block_count']} detected blocks",
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
    "This is one manually labelled page, not a blind holdout. Rectangles only group line detections by their centers; they do not measure bubble-box localization. This score says nothing about Vietnamese translation quality or overlay fit.",
    "",
    "No game, translation API, or network was used."
]
md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps(result["metrics"], ensure_ascii=False))
print(json_path)
print(md_path)
