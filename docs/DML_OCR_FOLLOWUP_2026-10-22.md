# DirectML OCR follow-up — manga scroll and cutscene subtitle samples (2026-10-22)

## Scope

- Ran the local PP-OCRv6 medium RapidOCR pipeline through ONNX Runtime DirectML on saved project samples only. No live screen capture, game process, Groq, translation service, API key, or account was used.
- Samples were already used in earlier project evaluations. Results are supplementary regression evidence, not held-out validation.
- Detector, classifier, and recognizer ran with `DmlExecutionProvider` and CPU fallback. OCR images are the saved manga scroll/page images and subtitle ROIs.
- The first image call includes lazy initialization and is separated from warmed calls. All later times below are full detector+classifier+recognizer calls, not end-to-end translation.

## Results

The video subtitle ROIs matched all three saved English references exactly (3/3). The saved blank ROI returned no text (0 false positives in 1 sample). This validates OCR on clean, cropped subtitles only; it does not cover subtitle detection in a full screen, changing backgrounds, movement, or overlay delay.

The manga outputs remained fragmented into line detections, and no translation was evaluated. The page sample produced 70 lines; this is enough to show the OCR path can return text quickly after warmup, but it is not a speech-bubble grouping or translation-quality score.

| Sample | Detected lines | Warm median (ms) | Three calls (ms) |
|---|---:|---:|---:|
| `manga-scroll-01` | 15 | 299 | 4387, 297, 299 |
| `manga-scroll-02` | 9 | 207 | 207, 223, 193 |
| `manga-005-730` | 70 | 697 | 729, 693, 697 |
| `video-01-roi` | 1 | 206 | 587, 206, 167 |
| `video-06-roi` | 1 | 236 | 236, 233, 283 |
| `video-07-roi` | 1 | 204 | 189, 204, 244 |
| `video-08-roi` | 0 | 158 | 157, 173, 158 |

Cold first-image calls varied substantially by workload: 4387 ms for `manga-scroll-01`, 14432 ms for the first full-screen video frame, and 2597 ms for the first player-region crop. Subsequent warmed calls for those images were 297/299 ms, 321/295 ms, and 122/79 ms respectively. These cold calls include lazy model/provider initialization and are materially slower than warmed medians. Warm medians are not cold-start latency and do not include translation, debounce/stability waiting, caching, overlay composition, or app scheduling. Video subtitle ROI medians were about 204–236 ms; the empty ROI took 158 ms.

## Full-screen video frames (không crop ROI)

Chạy lại cùng PP-OCRv6 medium DirectML trên bốn ảnh khung hình YouTube 730×655 đã lưu. Cả ba câu phụ đề tiếng Anh tham chiếu đều được tìm nguyên câu (3/3); khung không có phụ đề lại cho 15 dòng OCR UI/title. Khung 01 đồng thời nhận cả phụ đề tiếng Trung trên video, nên bộ nhận diện thấy nội dung chữ chồng ngôn ngữ chứ chưa chọn đúng một bản phụ đề.

| Frame | Dòng OCR toàn ảnh | Warm median (ms) | Ba lượt (ms) |
|---|---:|---:|---:|
| `video-01` | 17 | 321 | 14432, 321, 295 |
| `video-06` | 19 | 317 | 334, 316, 317 |
| `video-07` | 16 | 321 | 312, 321, 322 |
| `video-08` | 15 | 288 | 291, 273, 288 |


Đây là OCR toàn ảnh, không tính dịch hay overlay. Toàn ảnh bao gồm toolbar YouTube, tiêu đề video và metadata; khung trống có 15 kết quả UI nên **không dùng toàn màn hình làm nguồn dịch**. Cần vùng capture quanh video/subtitle và kiểm tra song ngữ, rồi thử trên frame mới. Kết quả này hỗ trợ chọn vùng theo dõi nhưng chưa chứng minh một ROI cố định dùng được khi subtitle đổi vị trí.

## User-selected video-player region

Tạo crop lớn quanh player từ bảy screenshot đã lưu (x=0, y=54, 716×414 trên ảnh 730×655). Crop giữ toàn bộ hình video và cả hai vị trí subtitle trên/dưới; không crop sát chữ như bộ ROI chuẩn tham chiếu. Đây mô phỏng người dùng chọn vùng theo dõi video một lần, chưa chạy chọn vùng/overlay trong app.

| Frame | Dòng OCR | Warm median (ms) | Ba lượt (ms) |
|---|---:|---:|---:|
| `video-01-player-roi` | 2 | 122 | 2597, 122, 79 |
| `video-06-player-roi` | 1 | 135 | 167, 135, 124 |
| `video-07-player-roi` | 1 | 124 | 124, 125, 120 |
| `video-08-player-roi` | 1 | 78 | 88, 78, 78 |
| `video-02-player-roi` | 1 | 44 | 2329, 43, 44 |
| `video-03-player-roi` | 3 | 140 | 168, 136, 140 |
| `video-04-player-roi` | 3 | 137 | 407, 136, 137 |


Ba subtitle tiếng Anh khớp nguyên câu (3/3), với warmed median khoảng 122–140ms; bốn frame không có thoại lại cho tổng cộng 8 box như watermark `SUBSCRIBE`, control bar `0:10/13:53`, tiêu đề video, ký hiệu và chữ trên player. Frame đầu mỗi process có khởi tạo model/provider khoảng 2.3–2.6s; đây không đại diện cho warmed call. Frame 01 đồng thời nhận thêm subtitle tiếng Trung; chưa lọc được ngôn ngữ mong muốn hay loại watermark/control.

Đây vẫn là replay trên frame đã dùng trước đây, không có dịch, detector động, đồng bộ phiên bản frame hoặc render overlay. Box tọa độ thuộc crop; app cần offset theo vùng được chọn trước khi vẽ. Chất lượng ở ba câu có chữ tốt, nhưng tám box nhiễu cho thấy OCR toàn player chưa được phép tự dịch mọi kết quả.

## Decision

- DirectML is a useful local OCR acceleration candidate on this machine; do not equate this with lower app latency or lower GPU impact while a game is running.
- Do not integrate into the main application yet: current samples were previously seen, full-screen OCR includes unrelated UI text, and OCR-to-overlay/resource gates have not been measured. First add held-out frames/pages and test a selected video-only region plus bilingual-caption handling; the saved-frame trial suggests this is feasible but leaves watermark and language selection unsolved.
- Manga OCR still needs better grouping and translation evaluation. Full-page OCR line boxes are not yet accurate overlay regions.
- Game/account safety remains unverified. No game client or game account was touched in this evaluation.

## Artifacts

- Raw result: `../artifacts/multimodal-evaluation/rapid-v6-medium-dml-followup.json`
- Visual manga/ROI review: `../artifacts/multimodal-evaluation/rapid-v6-medium-dml-followup.html`
- Full-frame video review: `../artifacts/multimodal-evaluation/rapid-v6-medium-dml-full-screen-followup.html`
- Full-frame raw result: `../artifacts/multimodal-evaluation/rapid-v6-medium-dml-full-screen-followup.json`
- Selected-player-region raw result: `../artifacts/multimodal-evaluation/rapid-v6-medium-dml-player-roi-followup.json`
- Selected-player-region visual review: `../artifacts/multimodal-evaluation/rapid-v6-medium-dml-player-roi-followup.html`
