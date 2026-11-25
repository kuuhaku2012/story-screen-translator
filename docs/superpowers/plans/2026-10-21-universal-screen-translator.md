# Universal Screen Translator — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking. Execute inline; do not delegate without user authorization.

**Goal:** Một ứng dụng EN→VI dùng được khi đọc manga và xem cutscene: tự dịch nội dung mới, giữ tên riêng, phủ đúng vị trí, ổn định và có giới hạn tài nguyên đo được.

**Architecture:** Capture cục bộ → nhận biết thay đổi trong vùng cần đọc → OCR theo cấu hình manga/phụ đề → cụm chữ có ID và phiên bản → hàng đợi dịch có cache/hạn mức → bố cục overlay theo tọa độ nguồn. Dùng chung điều khiển, cache, glossary và provider; hai cấu hình xử lý được chọn rõ ràng, chưa thêm tự phân loại màn hình. Mọi lựa chọn OCR/provider phải vượt bộ đánh giá riêng trước khi làm mặc định.

**Tech Stack:** .NET 10, WPF, MSIX, Windows capture/OCR hiện có; ứng viên manga RapidOCR/ONNX cục bộ đã cài để đánh giá. Không thêm Python/runtime vào gói chính trước khi kiểm tra chất lượng, giấy phép và tổng dung lượng. Groq hiện có là baseline, chưa có model dịch nào được công nhận đạt.

**Spec:** `E:\tool_dich_man_hinh\docs\PROJECT_SPEC.md`, yêu cầu người dùng trong cuộc trò chuyện và phần tiêu chí dưới đây. Kế hoạch này thay thế quyết định dùng Windows OCR cho manga hoặc dùng một model dịch làm mặc định chỉ dựa trên vài câu tốt. Các báo cáo lịch sử giữ nguyên để đối chiếu.

## 1. Ràng buộc bắt buộc

- Toàn bộ source, model, dependency thử nghiệm, dữ liệu và báo cáo ở `E:\tool_dich_man_hinh`.
- Dịch tự động khi bật; không yêu cầu tự chụp từng ảnh. Chọn vùng một lần, có tùy chọn toàn màn hình.
- Chủ động chọn Manga / Phụ đề; chuyển combat bằng nút/phím tắt tắt dịch. Không hứa tự hiểu cốt truyện/combat.
- OFF: dừng capture/OCR/network, ẩn overlay và hủy tác vụ chờ; model có thể còn trong RAM nếu người dùng chọn giữ để bật nhanh.
- OCR/ảnh mặc định cục bộ. Chỉ chữ mới cần dịch ra mạng; quyền thử Vision trên một ảnh trước đây không cho phép gửi ảnh tự động.
- Key chỉ trong RAM, không log, không đọc key từ ứng dụng khác. Log mặc định chỉ số đo/ID, không ảnh hoặc thoại. Bật lưu fixture chẩn đoán là thao tác riêng của người dùng.
- Không injection/hook/đọc bộ nhớ/sửa file game/tự động input; capture và overlay là cửa sổ Windows độc lập. Không thiết kế né anti-cheat.
- Không thể bảo đảm không bị ban. Chỉ đánh giá ảnh, web và video trước; đối với game Kuro giữ cổng xác nhận bằng văn bản như đặc tả hiện hành. Acc phụ không chứng minh an toàn.
- Overlay ngoài thẻ trong suốt, không lấy focus, chuột đi qua, giao diện tự thu khi bắt đầu; có cách dừng nhanh.
- Tên nhân vật/tổ chức/vùng đất giữ chính tả bằng glossary. OCR alias chỉ từ mapping đã xác nhận; không tự sửa tên gần giống một cách mù quáng.
- FDD là gói chính; self-contained là gói thử thuận tiện. MSIX giữ identity cho Windows OCR. Không bắt người dùng cài môi trường phát triển.

## 2. Vì sao bản cũ không đạt

1. Windows OCR bỏ/sai chữ trên font manga; grouping không khôi phục được phần mất.
2. Nhóm dòng bằng khoảng cách không đồng nghĩa tìm được ranh giới bong bóng. Chỉ số tâm hộp trên 22 vùng không đo chất lượng toàn trang.
3. Thẻ tối thiểu 90px và chiều cao nới tự động có thể che tranh, va nhau; co font + clip/ellipsis làm mất nội dung.
4. Nhiều model dịch sai cả chữ chuẩn. Cache và tốc độ không giải quyết được nghĩa sai.
5. Chưa đo độ trễ đầu-cuối trên phiên chạy liên tục. API 0,73 giây không có nghĩa ứng dụng cập nhật sau 0,73 giây.

Không sửa bằng tọa độ/từ thay thế riêng cho trang đã biết. Các kết quả tốt cũ chỉ là baseline thí nghiệm.

## 3. Tiêu chí nghiệm thu đề xuất

Đo trên máy hiện tại i5-11400H, RAM khoảng 16GB, RTX3050 4GB; CPU OCR tối đa 2 luồng ban đầu, không dùng GPU inference mặc định. Báo riêng cold-start/warmed, median/p95, số mẫu, timeout và câu bỏ sót.

| Hạng mục | Cổng nghiệm thu |
|---|---|
| OCR manga | CER chuẩn hóa ≤3% và ≥90% cụm đúng toàn bộ chữ/số trên tập giữ kín; báo riêng tên, chữ nghiêng, chữ viết tay, caption |
| Tìm/gom vùng | Recall cụm ≥95%, precision ≥95%; chia một câu thành nhiều thẻ ≤3%, ghép hai câu khác nhau ≤1%; ít nhất 90% cụm ghép đúng có text-box IoU ≥0,7 |
| Dịch | Chấm mù 0=sai/ngược nghĩa, 1=mất ý cần sửa, 2=đúng nhưng cứng, 3=tự nhiên; ≥95% đạt 2–3, không có lỗi đảo phủ định/chủ thể hoặc bịa nội dung trong tập nghiệm thu |
| Tên riêng | 100% mục glossary còn nguyên trong bản dịch khi OCR cung cấp đúng; lỗi OCR tên phải báo riêng, không tính là đã bảo toàn |
| Phụ đề | Từ khi câu hoàn chỉnh xuất hiện tới overlay: p50 ≤0,8s, p95 ≤1,5s; ≥95% câu hiện ≥2s được dịch trước khi biến mất; báo số câu trễ/bỏ qua |
| Manga | Từ khi dừng cuộn: phần đang đọc đầu tiên p50 ≤1,2s, p95 ≤2s; cả viewport p95 ≤3s ở kích thước kiểm tra 1920×1080 |
| Cuộn nhanh | Không dịch đuổi khung cũ; chỉ giữ khung mới nhất. Trang chỉ lóe dưới 300ms có thể không dịch. Không cam kết dịch mọi trang khi người dùng liên tục cuộn không dừng |
| Ổn định | Trang đứng yên 30s: không gọi dịch lại, không dựng lại overlay không cần thiết, không chớp do vòng capture |
| Sai thời điểm | Kết quả phiên bản cũ không bao giờ hiện sau đổi trang/OFF; khi phát hiện cuộn, ẩn phần không còn khớp trong ≤150ms |
| Tài nguyên | Mục tiêu warmed tổng RAM ≤700MiB cho app+OCR helper; CPU đứng yên ON ≤3% toàn máy, đang OCR trung bình ≤20%; VRAM inference 0 mặc định; OFF không có công việc định kỳ |
| Hạn mức | Một hàng đợi provider; không vòng lặp 429; chờ theo header, không xếp backlog nội dung cũ; throughput thực tế và thời gian cooldown phải tính vào nghiệm thu |

Đây là mục tiêu kỹ thuật, chưa phải kết quả hay bảo đảm. Nếu không vượt cổng, ghi rõ nguyên nhân và sửa quyết định kiến trúc; không hạ tiêu chí sau khi nhìn tập giữ kín. Không suy FPS/an toàn tài khoản từ số CPU/RAM.

## 4. Bộ dữ liệu và cách chấm

- Dùng tất cả trang cũ, trang 172 và trang mới 23 làm tập phát triển/hồi quy, không coi là dữ liệu chưa thấy.
- Thêm tối thiểu 20 trang: 10 phát triển, 10 giữ kín. Đa dạng font thường/nghiêng/viết tay, lời ngoài bong bóng, nền tối, cột gần nhau, chữ nhỏ. Người dùng cung cấp hoặc chỉ định nguồn được phép dùng; không tự thu ảnh riêng tư.
- Chép ít nhất 150 cụm, đánh dấu polygon chữ, quan hệ các dòng, thứ tự đọc và tên riêng. Polygon bong bóng là nhãn riêng nếu cần đo vùng che tranh.
- Video: ít nhất 3 đoạn tổng 10 phút, ≥100 lần thay phụ đề, có hai dòng, kiểu hiện từng chữ, fade, nền chuyển động và tên riêng. Lưu thời điểm chữ đủ đọc và khi biến mất; không đưa câu tương lai vào dịch câu hiện tại.
- Tập giữ kín do người chấm giữ; không chọn đoạn chỉ vì OCR đọc tốt. Cần đánh giá ở 100%,125%,150% DPI, kích thước cửa sổ khác nhau và nhiều màn hình nếu máy có.
- Nhãn chuẩn không đi vào thuật toán chạy; ảnh resize lặp không tăng số mẫu độc lập. Chấm text đúng vùng, mapping ID, độ trễ và ảnh overlay cuối cùng; không chỉ chấm hộp có tâm đúng.

## 5. Luồng sử dụng sẽ xây

**Manga:** chọn Manga → chọn trang/vùng hoặc toàn màn → Bật. Giao diện thu xuống. Sau cuộn dừng ngắn, OCR viewport mới; ưu tiên cụm ở vùng người dùng đang xem, trả bản dịch theo từng batch nhỏ, giữ cụm hoàn tất đến khi nội dung thay đổi. Không gom hai bong bóng chỉ vì gần nhau. Có tùy chọn chiều đọc L→R/R→L; không tự suy từ ngôn ngữ.

**Phụ đề/cutscene:** chọn Phụ đề → chọn dải phụ đề một lần → Bật. Theo dõi chữ trong dải, không đợi nền phim đứng yên. Gộp dòng thành câu hiện tại, đợi chữ ổn định ngắn, dùng một ít ngữ cảnh đã dịch trước đó nếu provider đạt kiểm chứng. Câu mới thay câu cũ; tuyệt đối không để yêu cầu cũ chen vào sau.

**Hiển thị:** mặc định thẻ gọn ngay tại vùng chữ, nền cục bộ bán trong suốt, chữ có viền, ngoài thẻ trong suốt. Có tùy chọn nền đặc cho manga để che chữ gốc rõ hơn. Chỉ che ranh giới bong bóng khi có vùng bao được xác định đủ tin cậy; nếu không dùng text box kèm padding. Không inpaint/erase ảnh nền ở phiên bản này.

## 6. Hợp đồng dữ liệu bổ sung tối thiểu

Thêm `src/StoryTranslator.Core/TextRegion.cs`:

```csharp
public enum ReadingMode { Manga, Subtitles }
public sealed record TextRegion(string Id, long FrameVersion,
    string Text, System.Drawing.RectangleF SourceBounds,
    float LineHeight, float? OcrConfidence);
public sealed record RegionTranslation(string Id, long FrameVersion,
    string SourceText, string Text);
```

`SourceBounds` luôn là pixel vật lý tương đối vùng capture ban đầu; không DIP, không tọa độ sau upscale. `OcrConfidence=null` là không có score, không được coi là 1. Không thêm interface provider mới khi `IBatchTextTranslator` đủ; adapter ánh xạ ID/version tại pipeline trước/sau lệnh dịch. Hợp đồng helper dùng JSON một dòng/một message qua stdin/stdout, gồm `requestId`, `width`, `height`, danh sách vùng và lỗi có mã; stdout không có log lẫn vào protocol.

## 7. Các phần triển khai

### Task 1 — Bộ đo và regression toàn đường chạy

**Files:** thêm `scripts/evaluate-screen-session.py`, `tests/StoryTranslator.Core.Tests/FrameVersionTests.cs`; sửa `FileOcrEvaluation.cs` để nhận đường output nằm trong project, không ghi đè baseline; thêm `docs/ACCEPTANCE_MATRIX.md`.

- [ ] Viết ca hỏng trước: A đang dịch, chuyển B, A trả sau B; kết quả chỉ được chứa B. Ca OFF ngay trước response không được hiển thị.
- [ ] Chạy ca đó để xác nhận lỗi thiếu kiểm soát phiên bản nếu có; giữ ca đã đạt làm regression, không cố sửa khi chưa có lỗi.
- [ ] Ghi mốc monotonic `capture/ocr-start/ocr-end/stable/enqueue/send/receive/render` bằng ID, không ghi nội dung.
- [ ] Bổ sung cách replay ảnh/chuỗi khung qua recognizer/pipeline/layout thật; báo lỗi/timeouts vào mẫu, không bỏ khỏi p95.
- [ ] Lập tập phát triển/giữ kín theo mục 4, chốt tiêu chí trước khi chạy. Xác nhận báo cáo phân biệt microbenchmark với end-to-end.

**Deliverable:** một báo cáo tái lập bản cũ trên dữ liệu mới, đủ truy lỗi từng khâu.

### Task 2 — Cổng chất lượng dịch, làm trước tích hợp OCR lớn

**Files:** thêm `scripts/evaluate-translation-quality.ps1`, `data/evaluation/translation-cases.json`, `docs/TRANSLATION_PROVIDER_GATE.md`; sửa `GroqTextTranslator.cs`, `GroqRateLimiter.cs` chỉ sau khi chọn được cấu hình vượt cổng.

- [ ] Tạo 60 câu chuẩn trong đó ít nhất 20 giữ kín; gồm phủ định, đại từ, tên, lời nói lóng, câu phụ đề có lịch sử và không lịch sử. Không cho đáp án Việt vào prompt.
- [ ] So sánh cách giữ nguyên ALL CAPS với chuẩn hóa hoa/thường thận trọng, giữ nguyên tên và dấu câu; đây là một giả thuyết cần đo, không phải mặc định đúng.
- [ ] So sánh câu đơn và ngữ cảnh cùng cảnh; không ghép ngẫu nhiên nhiều cảnh thành lịch sử. Tên/quan hệ chỉ từ glossary hoặc ngữ cảnh có thật.
- [ ] Dùng tối đa hai cấu hình có lý do rõ trên provider hiện có trước; giới hạn số call/chi phí, lưu payload không chứa auth. Không gọi lại hàng loạt các model đã thất bại mà không có thay đổi giả thuyết.
- [ ] Chấm mù theo mục 3; đo p50/p95 tối thiểu 30 request riêng lẻ và batch nhỏ; tính cả quota/cooldown trong phát lại phiên.
- [ ] Nếu vẫn không đạt: tạo so sánh một dịch vụ EN→VI khác sau khi kiểm tra tài liệu chính thức, quyền truy cập và phí; người dùng tự nhập key cục bộ. Không bắt buộc mua dịch vụ hoặc lấy key thay người dùng.
- [ ] Chỉ đặt mặc định sau khi vượt cổng; nếu tất cả thất bại, công khai kết luận mục tiêu chất lượng/latency chưa khả thi với cấu hình và quota hiện tại. Tiếp tục phần offline độc lập, không phát hành bản dịch “đã tốt”.

**Deliverable:** cấu hình dịch đạt hoặc quyết định không đạt có dữ liệu; không cần sửa overlay để kiểm chứng bước này.

### Task 3 — OCR manga trong ứng dụng thật

**Files:** thêm `src/StoryTranslator.App/MangaOcrEngine.cs`, `tools/manga-ocr/worker.py`, `scripts/package-manga-ocr.ps1`; sửa `AppComposition.cs`, `MainWindow.xaml.cs`; thêm `MangaOcrProtocolTests.cs` trong thư mục test hiện tại.

- [ ] Viết test protocol: ID sai, output trống, helper chết, timeout, hủy, tọa độ ngoài ảnh, scale1x/2x đều không tạo vùng sai âm/thừa.
- [ ] Thử engine hiện có với cả trang và nhận dạng lại crop chỉ cho vùng kém tin cậy; không chạy hai lượt cả trang mỗi chu kỳ. Đo trên cả trang23 lẫn trang chưa dùng phát triển.
- [ ] Chỉ tích hợp nếu vượt cổng OCR và ngân sách. Không coi Rapid hiện có là đã đạt chỉ vì hơn Windows. Không triển khai CTD CPU đã đo chậm ~8–17s làm realtime.
- [ ] Dùng helper resident, một instance/phiên, hai luồng; khởi động một lần, không tạo Python process mỗi frame. Nếu tổng RAM hoặc đóng gói không đạt, đánh giá .NET ONNX trước khi thay runtime; chỉ port khi đã cố định preprocessing/postprocessing/model và có golden outputs.
- [ ] Kiểm tra giấy phép từng model, mã, runtime; pin version và SHA256; thử khởi động khi mất mạng để bảo đảm không tự tải model lúc đang dịch.
- [ ] UI Manga chọn recognizer mới đủ điều kiện; Subtitles vẫn dùng recognizer thắng đánh giá riêng. Engine lỗi phải hiện lỗi, không âm thầm fallback sang OCR đã biết kém rồi giả vờ hoạt động tốt.

**Deliverable:** app đọc đúng các fixture cùng đường recognizer thực tế, kèm số tổng app+helper. Không bắt người dùng cài Python thủ công.

### Task 4 — Cụm chữ và tọa độ có kiểm soát

**Files:** sửa `SpatialTextLayout.cs`, `WindowsOcrEngine.cs`, `MangaOcrEngine.cs`; thêm `TextRegion.cs`, mở rộng `SpatialTextLayoutTests.cs`.

- [ ] Kiểm thử từng case: tiêu đề dài, một từ ngắn giữa cụm, hai cột sát, balloon nối, caption nghiêng, vùng bị crop ở mép, chữ rời ngoài balloon. Bao gồm lỗi mới trang23.
- [ ] Giữ quad/box gốc đến cuối OCR; quy đổi một lần về pixel nguồn. Dùng cạnh dòng/reciprocal neighbor làm baseline, bổ sung bằng chứng hình ảnh ranh giới nếu nó cải thiện tập giữ kín; không thêm heuristic vô hạn.
- [ ] Phân biệt text bounding box và balloon polygon; chỉ dùng vùng nào đã có bằng chứng. Chưa đủ tin cậy thì giữ box chữ gọn, không che cả bong bóng.
- [ ] Chấm recall/precision/IoU, số cụm split/merge và lỗi ngoài nhãn; kiểm tra ảnh cuối ở từng DPI thay vì chỉ đếm tâm hộp.

**Deliverable:** vùng nguồn và ID đúng trên app; không buộc model dịch tự đoán tọa độ ảnh.

### Task 5 — Bộ điều phối theo nội dung, không theo chuyển động cả màn

**Files:** sửa `CaptureWorker.cs`, `SpatialTranslationPipeline.cs`, `TextStabilityFilter.cs`; thêm `FrameChangeTracker.cs`, `FrameVersionTests.cs`, mở rộng `StaleTranslationPresentationTests.cs`.

- [ ] Viết test nền video chuyển động nhưng chữ giữ nguyên: không dịch lại; chữ mới trên nền tương tự: phải dịch; cuộn liên tục: chỉ frame mới nhất.
- [ ] Manga: lấy mẫu thay đổi nhẹ, debounce dừng cuộn khởi điểm 200–300ms; ổn định một vùng không bị vùng động bên cạnh hủy. Chỉ chạy OCR nặng khi viewport thay đổi đủ mức.
- [ ] Subtitles: giới hạn ROI, bắt đầu khảo sát 5–8 mẫu nhẹ/giây, OCR tối đa 4 lần/giây; ổn định chữ 100–200ms. Điều chỉnh bằng số đo, không tăng tần suất toàn màn.
- [ ] Một OCR job active + một latest-frame pending; một provider request active; không xếp hàng mọi frame. Khi nhận frame mới, response cũ kiểm tra version trước khi present/cache áp dụng vào vị trí.
- [ ] Cache theo source text chuẩn hóa an toàn + ngôn ngữ + provider/model + glossary version; vị trí không nằm trong khóa bản dịch nhưng luôn lấy từ frame hiện tại. Tên, số và phủ định không bị chuẩn hóa mất.
- [ ] Dùng header hạn mức, bounded retry đã có; bỏ câu không còn hiển thị sau cooldown. Trạng thái chờ hiện rõ, không hiển thị bản dịch cũ tại vị trí mới.

**Deliverable:** không nhấp nháy/không dịch đuổi, số call và backlog đo được trên replay.

### Task 6 — Overlay vừa vùng, không cắt nội dung im lặng

**Files:** sửa `SpatialPanelLayout.cs`, `TranslationOverlay.xaml.cs`, `MainWindow.xaml`; mở rộng `SpatialPanelLayoutTests.cs`; sửa `SpatialOverlaySmoke.cs`.

- [ ] Tạo test box chữ hẹp, hai vùng gần nhau, text Việt dài hơn 2×, sát mép và DPI khác nhau. Assert bounds không ra viewport và không chồng vùng chữ khác.
- [ ] Thay minimum width90 bằng đo text theo font/line-height và khoảng trống thực tế. Đặt anchor vào vùng nguồn; mở rộng cân đối trong khu vực an toàn, không kéo xuống tùy ý.
- [ ] Fit text ưu tiên wrap, sau đó điều chỉnh size trong ngưỡng dễ đọc. Không clip/ellipsis âm thầm để tạo ấn tượng đã dịch đủ.
- [ ] Nếu không thể chứa: thẻ có chỉ báo thiếu chỗ và chế độ xem đầy đủ theo yêu cầu; với phụ đề cho phép dải phụ đề cố định do người dùng chọn. Không bắt chuyển sang cửa sổ kết quả riêng.
- [ ] Giữ layer stable; chỉ cập nhật vùng/text thay đổi. Nền ngoài thẻ trong suốt; opacity tùy chọn, chữ viền tương phản. Không inpaint, không thêm model nền nặng.
- [ ] Kiểm tra capture loại trừ overlay; nếu phải hide/capture/show mà chớp rõ thì đánh dấu cấu hình không đạt yêu cầu đứng yên, không tuyên bố đã hết nháy.

**Deliverable:** ảnh overlay thực tế đầy đủ, readable, đúng anchor trên trang23 và tập giữ kín.

### Task 7 — Nối toàn bộ và công tắc sử dụng

**Files:** sửa `MainWindow.xaml`, `MainWindow.xaml.cs`, `AppComposition.cs`, `StoryModeController.cs`, `SettingsStore.cs`; thêm `ReadingModeTests.cs`, mở rộng `CaptureLifecycleTests.cs`.

- [ ] UI một lựa chọn Manga / Phụ đề, thông báo recognizer/provider đang dùng, chọn vùng, độ đậm nền, nút bật/tắt. Không yêu cầu chọn chi tiết model ở luồng bình thường sau khi mặc định đã đạt.
- [ ] Bật tự thu giao diện; phím tắt dừng đăng ký Windows hotkey thông thường, không hook bàn phím game. OFF hủy jobs; mở giao diện tạm dừng như bản cũ.
- [ ] Đổi mode/glossary/provider tăng phiên bản, không dùng cache/ngữ cảnh không tương thích. Không ghi key vào settings.
- [ ] Phát lại ảnh/video qua composition thật; so sánh text và ảnh cuối với expected. Kiểm tra khởi động sạch, helper crash, mất mạng, 401,400,404,429, timeout.

**Deliverable:** một app dùng được hai mode; mặc định OFF, không tự gửi dữ liệu lúc vừa mở.

### Task 8 — Đo phiên liên tục và tối ưu có bằng chứng

**Files:** `scripts/evaluate-screen-session.py`, thêm `scripts/measure-runtime.ps1`; cập nhật `docs/ACCEPTANCE_MATRIX.md`.

- [ ] Chạy đọc manga 10 phút: đứng yên, cuộn chậm, cuộn nhanh, quay lại trang cũ, resize, chuyển cửa sổ; ít nhất 10 trang giữ kín.
- [ ] Chạy video cutscene 1×, ba đoạn tổng ≥10 phút; ghi mọi lần đổi phụ đề, overlay đầu tiên, response bỏ do trễ, queue wait và cooldown.
- [ ] Tính p50/p95 đầu-cuối từ mốc chữ xuất hiện/dừng cuộn, không từ lúc gửi API. Chấm nội dung đúng vùng; câu không xuất hiện vì timeout tính thất bại/coverage thiếu.
- [ ] Đo tổng RSS/peak, CPU toàn máy theo process app+helper, VRAM, cold-start, OFF và đứng yên. Nếu không đạt, tối ưu khâu chậm nhất theo trace; không hạ chất lượng model chỉ để đạt tốc độ.
- [ ] Chỉ khi đủ cổng mới mời người dùng nghiệm thu; nếu quota gây trễ, nói rõ yêu cầu quota/provider và chi phí dựa trên số đo, không hứa cache giải quyết mọi 429.

**Deliverable:** bảng đạt/chưa đạt có raw evidence; không mở game để đo trong giai đoạn này.

### Task 9 — Đóng gói và phát hành thử có rollback

**Files:** sửa `scripts/package.ps1`, `scripts/run-screen-trial.ps1`, `docs/USER_GUIDE.md`, `docs/SCREEN_TRIAL.md`, `docs/TEST_REPORT.md`.

- [ ] Gói version mới có manifest/model hashes và license; lưu bản cũ ở thư mục version riêng trước thay launcher. Không ghi đè baseline kết quả.
- [ ] FDD chính, self-contained tùy chọn; kiểm tra dependency OCR có sẵn trên máy mới/mất mạng. Ghi dung lượng app/model/runtime tách biệt.
- [ ] Launcher kiểm tra process và registration sở hữu đúng path; ngăn hai helper chạy song song, xử lý crash và leftover registration; không gỡ package của đường dẫn khác.
- [ ] Chạy OCR/overlay smoke, toàn Core suite và fixture thực tế của engine mới. Không dùng smoke chữ tổng hợp thay nghiệm thu manga/cutscene.
- [ ] Ghi hướng dẫn bật mode, giới hạn tốc độ, dừng dịch, key/quota, rollback. Phát hành nhãn thử nghiệm nếu còn tiêu chí chưa đạt.

**Deliverable:** gói chạy được, báo cáo trung thực và đường quay lại bản trước.

## 8. Thứ tự, checkpoint và cách thực hiện

1. **Đợt A: Task1–2.** Chốt bộ đo và khả năng dịch đúng. Nếu chưa có provider đạt, dừng mọi tuyên bố chất lượng, nhưng vẫn thực hiện được phần offline OCR/layout.
2. **Đợt B: Task3–4.** OCR và vùng chữ trong app; qua tập giữ kín trước khi làm mặc định.
3. **Đợt C: Task5–7.** Scheduler, overlay và luồng hai mode; kiểm tra replay end-to-end.
4. **Đợt D: Task8–9.** Đo phiên thật ngoài game, đóng gói, người dùng nghiệm thu.

Không ấn định ngày hoàn thành khi provider/OCR còn chưa vượt cổng. Sau mỗi task cập nhật checkbox, số đo và quyết định vào báo cáo; không cần yêu cầu người dùng xác nhận lại các thao tác offline đã nằm trong kế hoạch. Những việc cần đầu vào thật: thêm nội dung đánh giá, nhập key thủ công, lựa chọn dịch vụ có phí hoặc thay đổi phạm vi gửi ảnh. Không lặp yêu cầu cấp quyền đã được chấp thuận trong đúng phạm vi.

Mỗi thay đổi code: viết ca tái hiện thất bại → chạy xác nhận → sửa nhỏ nhất → chạy ca liên quan/toàn suite phù hợp → review kết quả → checkpoint. Nếu repository hoạt động thì commit từng task; hiện trạng Git trước đây không nhận worktree hợp lệ nên phải kiểm tra lại, không giả định đã commit. Không di chuyển project sang thư mục khác để đáp ứng workflow.

## 9. Tự rà soát kế hoạch

- [x] Có luồng manga/cutscene và công tắc OFF cho combat.
- [x] Có glossary, privacy mặc định text-only, capture/overlay độc lập.
- [x] Tách chất lượng dịch khỏi OCR/grouping; không chọn sẵn model đã thất bại.
- [x] Đo đầu-cuối, tập chưa dùng phát triển, false positives/overflow/câu bỏ sót.
- [x] Một ứng dụng, không yêu cầu chụp tay, không chuyển toàn bộ sang Vision khi chưa được phép.
- [x] Có giới hạn khả thi: cuộn không dừng, quota mạng, GPU nhỏ và không bảo đảm ban.
- [x] Có map file, output, checkpoint và rollback; không triển khai tính năng trong lần lập kế hoạch này.
