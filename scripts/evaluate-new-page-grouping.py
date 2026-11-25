"""Score existing generic line grouping on newly labelled saved manga pages."""
from pathlib import Path
import json
import sys
from manga_line_regions import group_lines

root = Path(__file__).resolve().parents[1]
base = root / "artifacts/holdout-evaluation/2026-10-22"
ocr = json.loads((base / "results.json").read_text(encoding="utf-8"))
fixtures = [
    ("page24", "Screenshot 2026-10-22 001824.png", base / "manga-page24/manga-page24-reference.json"),
    ("sample001909", "Screenshot 2026-10-22 001909.png", base / "manga-sample-001909/manga-sample-001909-reference.json"),
]
reports = []
for name, image_name, reference_path in fixtures:
    ref = json.loads(reference_path.read_text(encoding="utf-8"))
    regions = ref.get("regions", ref.get("Bubbles"))
    labels = [(r.get("id", r.get("Id")), r.get("rect_px", r.get("RectPx"))) for r in regions]
    # The OCR transcript groups two visually distinct text islands in each page.
    # Split these manual labels for the grouping check; no OCR output is used to place them.
    if name == "page24":
        labels = [(label, rect) for label, rect in labels if label != "bottom-left"]
        labels += [("kyoko", [195, 575, 285, 625]), ("can-we-bounce", [105, 600, 195, 650])]
    if name == "sample001909":
        labels = [(label, rect) for label, rect in labels if label != "center-quote"]
        labels += [("take-my-advice", [230, 75, 335, 150]), ("leave-kid-alone", [235, 210, 335, 305])]
    case = next(c for c in ocr["Cases"] if c["Group"] == "manga" and c["Name"] == image_name)
    blocks = case["FullFrame"]["Blocks"]
    lines = []
    for block in blocks:
        x, y, w, h = (block[k] for k in ("X", "Y", "Width", "Height"))
        lines.append({"text": block["Text"], "box": [[x, y], [x + w, y], [x + w, y + h], [x, y + h]]})
    groups = group_lines(lines)
    row = []
    for group in groups:
        x1, y1, x2, y2 = group["box"]
        cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
        associated = [label for label, (rx1, ry1, rx2, ry2) in labels
                      if rx1 <= cx <= rx2 and ry1 <= cy <= ry2]
        row.append({"box": [round(v, 1) for v in group["box"]],
                    "line_count": group["detectedLineCount"], "associated_regions": associated})
    by_region = {label: sum(label in g["associated_regions"] for g in row) for label, _ in labels}
    report = {
        "page": name,
        "image": image_name,
        "labelled_dialogue_regions": len(labels),
        "ocr_line_boxes": len(blocks),
        "group_count": len(groups),
        "groups_per_region": by_region,
        "split_regions": sum(n > 1 for n in by_region.values()),
        "missed_regions": sum(n == 0 for n in by_region.values()),
        "groups_spanning_multiple_region_centers": sum(len(g["associated_regions"]) > 1 for g in row),
        "unassigned_groups": sum(len(g["associated_regions"]) == 0 for g in row),
        "groups": row,
    }
    reports.append(report)
out = base / "manga-grouping-replay"
out.mkdir(exist_ok=True)
payload = {
    "algorithm": "Existing manga_line_regions.group_lines, no changes to grouping thresholds.",
    "association": "Approximate manual region rectangles; group center association only. This is a split/merge proxy, not bubble localization IoU.",
    "pages": reports,
}
(out / "results.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
lines = [
    "# Manga line grouping replay - 2026-10-22",
    "",
    "Existing line grouping on two newly labelled saved pages, using the same stored OCR boxes. Manual rectangles associate group centers with text islands; the two transcript regions that combine separate visible islands are split for this check. No algorithm change or translated overlay.",
    "",
    "| Page | Dialogue regions | Groups | Split regions | Missed regions | Unassigned groups |",
    "|---|---:|---:|---:|---:|---:|",
]
for r in reports:
    lines.append(f"| {r['page']} | {r['labelled_dialogue_regions']} | {r['group_count']} | {r['split_regions']} | {r['missed_regions']} | {r['unassigned_groups']} |")
lines += [
    "",
    "A group center inside a manual region is only a grouping proxy. It does not prove the group has the full dialogue, and does not measure text-box or balloon IoU. These pages have been inspected and are development data. There is no cutscene or end-to-end timing result here.",
    "",
    "No network, game, or Groq API call was used."
]
(out / "REPORT.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps([{k:v for k,v in r.items() if k != "groups"} for r in reports], ensure_ascii=False, indent=2))
