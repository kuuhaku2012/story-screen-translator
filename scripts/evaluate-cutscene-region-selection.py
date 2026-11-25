"""Offline probe of top/bottom cutscene text selection on saved OCR blocks.

This is development data, not a live player or production app feature.
"""
from pathlib import Path
import json
import re
import statistics

ROOT = Path(r'E:\tool_dich_man_hinh')
FOLDER = ROOT / 'artifacts/cutscene-new-video-20261022'
SOURCE = json.loads((FOLDER / 'ocr-results-valid.json').read_text(encoding='utf8'))
EXPECTED = {
    'iOV-a7O3IkI-p10.png': [],
    'iOV-a7O3IkI-p20.png': [],
    'iOV-a7O3IkI-04m29s.png': ['bottom'],
    'iOV-a7O3IkI-p50.png': ['top'],
    'iOV-a7O3IkI-10m28s-clean.png': ['bottom'],
    'iOV-a7O3IkI-13m27s-clean.png': ['bottom'],
}

def bounds(block):
    points = block['box']
    return min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)

def select(blocks, width, height):
    with_geometry = [(block, bounds(block)) for block in blocks]
    def latin(block):
        return len(re.findall(r'[A-Za-z]', block['text']))
    def anchor(block, box, zone):
        x0, y0, x1, y1 = box
        cx = (x0 + x1) / 2
        cy = (y0 + y1) / 2
        if latin(block) < 20 or x1 - x0 < width * .25 or not width * .3 < cx < width * .8:
            return False
        return cy < height * .13 if zone == 'top' else height * .7 < cy < height * .95

    selected = {}
    for zone in ('top', 'bottom'):
        if not any(anchor(block, box, zone) for block, box in with_geometry):
            continue
        if zone == 'top':
            picked = [(block, box) for block, box in with_geometry
                      if box[1] < height * .12 and box[2] > width * .11 and latin(block) >= 2]
            picked.sort(key=lambda pair: pair[1][0])
        else:
            picked = [(block, box) for block, box in with_geometry
                      if height * .7 < (box[1] + box[3]) / 2 < height * .95
                      and width * .15 < (box[0] + box[2]) / 2 < width * .85
                      and latin(block) >= 2]
            picked.sort(key=lambda pair: (pair[1][1], pair[1][0]))
        selected[zone] = [{'text': block['text'], 'box': list(box)} for block, box in picked]
    return selected

reports = []
for item in SOURCE['reports']:
    filename = item['file']
    width, height = item['size']
    selected = select(item['zones']['full']['blocks'], width, height)
    predicted = sorted(selected)
    expected = sorted(EXPECTED[filename])
    reports.append({'file': filename, 'expectedZones': expected, 'predictedZones': predicted,
                    'zoneMatch': predicted == expected, 'selected': selected,
                    'fullOcrMilliseconds': item['zones']['full']['milliseconds']})

summary = {'source': 'ocr-results-valid.json', 'scope': 'Six inspected development frames; rules tuned after viewing them. No blind holdout, live capture or translation.',
           'correctZoneSets': sum(r['zoneMatch'] for r in reports), 'frames': len(reports),
           'fullOcrMedianMilliseconds': statistics.median(r['fullOcrMilliseconds'] for r in reports),
           'reports': reports}
(FOLDER / 'region-selection.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf8')
print(f"zone match: {summary['correctZoneSets']}/{summary['frames']}; full OCR median: {summary['fullOcrMedianMilliseconds']:.1f} ms")
for report in reports:
    print(report['file'], report['predictedZones'], report['zoneMatch'],
          {zone: [b['text'] for b in blocks] for zone, blocks in report['selected'].items()})
