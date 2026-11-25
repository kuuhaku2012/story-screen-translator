# Đánh giá manga và cutscene — 20/10/2026

## Kết luận

Bản hiện tại **chưa đạt** mục tiêu đa năng: vừa dịch tốt manga khi cuộn nhanh, vừa theo kịp cutscene. Đã xác định ba vấn đề độc lập bằng dữ liệu: Windows OCR kém trên manga; cách gom dòng hiện tại chia vụn ô thoại ngay cả khi OCR tốt; model dịch cục bộ đã thử vẫn sai nghĩa khi được cung cấp chữ đúng.

RapidOCR v6 là ứng viên tốt hơn cho nhận dạng manga. Phụ đề có vị trí cố định có thể dùng đường xử lý khác trong cùng ứng dụng. Chưa có cấu hình toàn bộ pipeline được chứng minh đáp ứng yêu cầu; các OCR/model dưới đây là cấu hình đánh giá, chưa được tích hợp làm bản dịch chính.

## Nội dung và phương pháp

- Manga do người dùng chỉ định: https://specterscans.com/series/yofukashi-no-uta/chapter/179 . Đã mở trang, kiểm tra có 21 trang; tải 4 trang mẫu (001,002,003,005), trong đó 003 là trang tiêu đề. Chấm trên 22 vùng thoại thuộc 001,002,005 có bản chép tay.
- Có thêm 2 vùng lặp lại sau cuộn, không tính là 2 câu mới độc lập. Đã cuộn một màn hình rồi cuộn liên tiếp hai lần, chụp trạng thái sau cuộn. Đây là ảnh sau cuộn, **không phải đo lớp dịch đang chạy theo cuộn**.
- Video: https://www.youtube.com/watch?v=5mCH8zIhQ4s . Quan sát và chụp từ trình duyệt: phụ đề tiếng Anh có cả ở dải trên và dưới; lấy 3 mẫu có chữ (khoảng 00:21, 06:56, 09:13) và 4 mẫu trống. Một số khung lấy khi phát 1×, một số sau khi tua/tạm dừng. Khung 05 chụp ngay sau tua còn hình cũ nên bị loại khỏi chấm điểm. Không coi đây là kiểm thử liên tục toàn video hoặc đo độ trễ đầu-cuối khi phát.
- 42 ảnh đầu vào cuối cùng: 4 trang ở 1200 và 730 pixels chiều rộng, 3 ảnh viewport thực tế, 24 vùng thoại, 7 ROI video. Windows OCR thường/phóng 2× và hai cấu hình RapidOCR chạy 3 lần mỗi ảnh, tổng 504 lượt. Thời gian warmed bỏ lần đầu mỗi ảnh; tập mẫu nhỏ nên p95 chỉ là mô tả trên mẫu, không bảo đảm thống kê tổng quát.
- Ảnh vùng chuẩn đã được xem để loại lỗi cắt thiếu chữ; bản chép tay không lấy từ OCR. Điểm ký tự (CER) chuẩn hóa về chữ cái/số ASCII, không tính hoa/thường/khoảng trắng/dấu câu. WER giữ ranh giới từ và dấu nháy. Không gọi CER là phần trăm câu dịch đúng.
- CPU i5-11400H, 12 luồng logic, khoảng 15,7 GiB RAM, RTX 3050 Laptop. RapidOCR/ONNX và CTranslate2 được giới hạn 2 luồng CPU; OpenCV 2 luồng. Không dùng GPU inference, key, Groq, dịch cloud hay client game. Tải dependency/model ban đầu có mạng; các lượt nhận dạng/dịch dùng dữ liệu cục bộ.

## 1. Nhận dạng manga

### Vùng thoại cắt sẵn — tách lỗi nhận dạng khỏi lỗi tìm vùng

| Cấu hình | CER thấp hơn tốt hơn | WER | Ô đúng toàn bộ chữ/số | Tên đúng trong 9 lần xuất hiện | p50 OCR/vùng | p95 OCR/vùng |
|---|---:|---:|---:|---:|---:|---:|
| Windows OCR thường | 18,47% | 36,55% | 4/22 | 5/9 | 5,23 ms | 10,12 ms |
| Windows OCR 2× | 16,81% | 35,86% | 6/22 | 5/9 | 12,13 ms | 24,84 ms |
| RapidOCR PP-OCRv6 small | **1,16%** | 10,34% | **19/22** | **9/9** | 117,28 ms | 192,67 ms |
| RapidOCR PP-OCRv5 English mobile | 6,82% | 8,97% | 19/22 | 9/9 | 121,69 ms | 195,05 ms |

Đây là ô cắt đúng bằng tay: không chứng minh app tự tìm được ô thoại. Chỉ 9 lần xuất hiện tên; không thể suy ra tên riêng luôn được bảo toàn. CER/WER có thể khác thứ hạng vì mất khoảng trắng, thiếu nguyên từ hoặc một vùng lỗi nhiều.

### Đọc cả trang

| Cấu hình | CER tại các vùng thoại, ảnh 1200px | p50/ p95 OCR cả trang 1200px | CER tại các vùng thoại, ảnh 730px | p50/ p95 OCR cả trang 730px |
|---|---:|---:|---:|---:|
| Windows thường + grouping hiện tại | 22,46% | 127,61 / 145,20 ms | 13,31% | 62,39 / 78,15 ms |
| Windows 2× + grouping hiện tại | 14,81% | 427,02 / 465,32 ms | 26,46% | 154,59 / 188,83 ms |
| Rapid v6, line boxes | **2,50%** | 1073,93 / 1602,64 ms | **1,66%** | 1027,57 / 1565,20 ms |
| Rapid v5 English mobile, line boxes | 10,15% | 1019,43 / 1698,44 ms | 8,99% | 1039,49 / 1841,69 ms |

Điểm cả trang được ghép theo vị trí tâm hộp so với vùng chép tay; với Windows còn bao gồm lỗi grouping, không phải so sánh thuần recognizer. Rapid line boxes chưa phải ô thoại đã gom hoàn chỉnh. Không có chuyện cứ phóng ảnh 2× là tốt hơn: kết quả phụ thuộc kích thước/font, và mẫu 730px bị giảm chất lượng khi phóng.

Đỉnh working set tiến trình trong toàn lượt OCR: Windows thường khoảng 106,55 MiB; Windows 2× khoảng 220,25 MiB; Rapid v6 khoảng 248,01 MiB; Rapid v5 khoảng 226,16 MiB. Đây là high-water mark của mỗi tiến trình trong cả bộ, không phải RAM riêng của một câu phụ đề hoặc tổng RAM của app hoàn chỉnh. Windows OCR có thể dùng thành phần hệ điều hành ngoài tiến trình, chưa tính riêng.

## 2. Gom ô thoại — lỗi không được sửa chỉ bằng đổi OCR

Áp dụng **chính SpatialTextLayout.Group của sản phẩm** lên các dòng Rapid v6, không viết lại heuristic để làm đẹp điểm:

| Đầu vào / quy tắc | Ô bị chia nhiều khối trên 22 ô, ảnh 1200px | Ảnh 730px |
|---|---:|---:|
| Windows thường + grouping | 8 | 4 |
| Windows 2× + grouping | 8 | 3 |
| Rapid v6 + grouping thường | **17** | **19** |
| Rapid v6 + grouping chế độ truyện | **17** | **19** |

Ít khối không đồng nghĩa tốt hơn: OCR có thể bỏ mất chữ, và hộp ghép có thể chứa sai nội dung. Ảnh minh họa: ../artifacts/multimodal-evaluation/layout-comparison.png . Rapid đọc chữ tốt hơn nhưng grouping hiện tại vẫn chia/có hộp chồng nhau, nên đưa từng mảnh đi dịch sẽ tiếp tục tạo câu Việt sai/rời rạc.

### Đã thử detector text-block cho manga

Đã chạy model ONNX chính thức từ [dmMaze/comic-text-detector](https://github.com/dmMaze/comic-text-detector) bằng OpenCV DNN CPU, 2 luồng, input 1024. Trên 22 vùng tham chiếu được lặp ở hai kích thước (44 lượt đối chiếu), 41/44 hộp detector nằm ít nhất 80% bên trong vùng chữ đã khoanh tay. Đây là phép đo chứa hộp text trong ROI tham chiếu, không phải IoU ranh giới bong bóng thoại; các lượt lặp ở hai kích thước không phải 44 mẫu độc lập. Hộp nhận diện chữ, không phải bong bóng; detector bỏ sót hiệu ứng chữ dọc cách điệu “COULDN’T GIVE LESS OF A CRAP”.

Tốc độ không phù hợp cho lướt manga: trang 1200px mất 7,93–8,41 giây, p50 8,23 giây, p95 8,39 giây; peak working set 586 MiB. Kết quả thị giác và số thô: `../artifacts/multimodal-evaluation/comic-text-detector/` và `../artifacts/multimodal-evaluation/comic-text-detector.json`. Mã nguồn detector mang GPL-3.0; cần rà soát riêng giấy phép model/mã trước khi phân phối ứng dụng. Vì chậm nhiều lần so với tốc độ cuộn, **không chọn detector này cho đường chạy realtime**. Cần tiếp tục với phân vùng nhẹ hơn dựa trên dòng OCR/layout hoặc detector khác có tốc độ đo được.

## 3. Phụ đề cutscene

Vùng chỉ chứa dòng tiếng Anh; ảnh nhỏ do video hiển thị ở chiều rộng khoảng 730 CSS pixels. Chưa đánh giá caption hai dòng, font khác, caption có nền biến đổi hoặc toàn màn hình.

| Cấu hình | CER trên 3 mẫu có chữ | Đúng toàn bộ chữ/số | p50/ p95 OCR khi có chữ | Đọc nhầm ra chữ trên 4 mẫu trống |
|---|---:|---:|---:|---:|
| Windows thường | 100% (không nhận được chữ ở ROI thấp) | 0/3 | 0,27 / 0,34 ms — không phải kết quả dùng được | 0/4 |
| Windows 2× | 1,74% | 1/3 | **7,02 / 8,11 ms** | 0/4 |
| Rapid v6 small | 0% | **3/3** | 229,26 / 384,34 ms | 0/4 |
| Rapid v5 English mobile | 0% | **3/3** | 208,23 / 250,85 ms | 0/4 |
| Rapid v5 nhận dạng thẳng, bỏ detector/classifier | 30,23% | 1/3 | 69,29 / 78,32 ms | **3/4** |

Các số p50/p95 trong bảng chỉ tính mẫu có chữ; không trộn khung trống để làm tốc độ trông tốt hơn. Không nhận được chữ ở ROI mỏng không chứng minh Windows OCR thường thất bại trên mọi kích thước vùng. Windows 2× rất nhanh nhưng các lỗi như `of feeling` thành `offeeUng` đã khiến model dịch lệch nghĩa. Bỏ detector không phải tối ưu chấp nhận được trong cấu hình đã thử vì đọc ra chữ trên khung trống.

## 4. Dịch cục bộ sau OCR

NLLB-600M INT8 beam 1, CTranslate2 CPU 2 luồng, model giữ trong RAM; xem ../artifacts/multimodal-evaluation/local-chain.json .

- 1 câu ngắn theo batch thử: 240–244 ms.
- 6 câu cùng batch: 573–587 ms.
- 22 câu cùng batch: 1420–1474 ms.
- Các câu dài/khó riêng trong so sánh OCR: khoảng 300–773 ms/câu. Bộ này có chữ in hoa như manga; model có thể đổi kết quả theo kiểu viết hoa.
- Peak working set tiến trình dịch trong lượt này: khoảng 1019,70 MiB. Chưa đo tổng tài nguyên app + cả hai engine resident cùng lúc hoặc ảnh hưởng FPS.

**Chất lượng chưa đạt dù OCR đúng**:

- `THEY REHASH THE SAME GRIPES...` được Rapid đọc đúng nhưng NLLB cho `Họ lại dùng cùng một thứ đùa.`
- `THE ONE WHO FALLS THE HARDEST...` thành `ai rơi nặng nhất...`, mất nghĩa ngữ cảnh tình cảm.
- `NARCISSISTIC TENDENCIES` thành `xu hướng ngốc` trong bản thử chữ in hoa; lượt trước chữ thường còn cho `tự kỷ`. Đều không đạt.
- Lỗi OCR ở `of feeling` làm câu đúng về cảm xúc biến thành `sinh ra từ phiền toái` trong kết quả dịch.

Không chọn mẫu tốt để tuyên bố đạt, không hardcode sửa tên/thành ngữ theo trang đã biết. Dịch đúng các câu đơn giản không bù cho lỗi nghĩa nặng. Bảng điểm OCR không phải bảng chất lượng dịch.

### Bổ sung: Qwen2.5 Instruct nhỏ trên CPU

Để so sánh model chat đa ngôn ngữ với NMT chuyên dụng, đã chạy Qwen2.5-0.5B và 1.5B Q4_K_M trên cùng 10 câu manga/cutscene. Đây là một lượt mỗi câu, không phải benchmark warmed 48 lượt như các số phía trên.

| Model | p50 / p95 mỗi câu | Peak working set | Chất lượng mẫu |
|---|---:|---:|---|
| Qwen2.5-0.5B Q4_K_M | 583 / 1054 ms | 507 MiB | Sai `gripes`, lẫn tiếng Bồ Đào Nha ở `crap`, từ chối dịch `dorks` |
| Qwen2.5-1.5B Q4_K_M | 1254 / 4830 ms | 1679 MiB | Dịch sai `gripes`, lặp token tới giới hạn ở câu `falls the hardest` |

OPUS-MT chuyên EN→VI nhanh hơn nhiều (44 ms p50 warmed, 162 MiB peak) nhưng vẫn làm sai `gripes` và thành ngữ `I couldn't give less of a crap`. Qwen không khắc phục được vấn đề ngữ cảnh; bản 1.5B còn quá chậm cho lời thoại liên tục trên CPU. Kết quả chi tiết: `../artifacts/local-llm-evaluation/qwen2.5-0.5b-cpu.json`, `../artifacts/local-llm-evaluation/qwen2.5-1.5b-cpu.json`.

### Qwen3-4B trên RTX 3050

Đã chạy model Q4_K_M chính thức qua llama.cpp CUDA, nạp model trong 3,24 giây và dùng 2.763 MiB VRAM trên GPU 4.096 MiB. Với 10 câu gọi riêng từng câu, p50 501 ms, p95 1.219 ms; tổng thời gian gọi 10 câu 6,16 giây. Peak working set tiến trình 2.874 MiB. Đây là một lượt/câu và không phải phát video liên tục.

Chất lượng nhỉnh hơn một số câu nhưng vẫn không đạt: `gripes` lẫn chữ Trung ở cuối câu; `falls the hardest` dịch literal; `I couldn't give less of a crap` sai nghĩa; câu `she up and died on me behind my back` thành vô nghĩa. `narcissistic tendencies` và `Here you go` dùng được hơn. Tài nguyên GPU chiếm gần 2,8 GiB khi không chạy game; không đo FPS khi chơi. Vì vậy model này cũng chưa thể làm bộ dịch mặc định cho cutscene/manga. Báo cáo thô: `../artifacts/local-llm-evaluation/qwen3-4b-gpu.json`; script: `../scripts/evaluate-qwen3-gpu.ps1`; model Qwen3-4B GGUF dùng license Apache-2.0 và SHA-256 `7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5`.

## 5. Đánh giá yêu cầu đa năng

| Yêu cầu | Kết quả đánh giá |
|---|---|
| Nhận dạng manga chính xác hơn hiện tại | Có ứng viên tiến bộ rõ: Rapid v6; chưa giải quyết ô thoại tự động |
| Dịch manga khi cuộn nhanh | Chưa đạt/chưa chứng minh đầu-cuối; OCR cả trang ~1–1,6s cộng dịch nhiều câu, grouping còn lỗi |
| Detector manga thử nghiệm | Tìm được nhiều vùng chữ nhưng mất ~8,2s/trang; không dùng realtime |
| Đọc phụ đề nhanh | Có phương án nhanh và phương án chính xác hơn; cần thử nhiều đoạn hơn và caption hai dòng |
| Dịch cục bộ đúng ngữ cảnh cốt truyện | Chưa đạt với Argos, OPUS-MT, NLLB-600M, Qwen2.5 0.5B/1.5B và Qwen3-4B |
| Giữ tên riêng | Mẫu OCR mới tốt hơn nhưng chưa có bảo đảm xuyên suốt dịch/glossary |
| Không hiện bản dịch cũ khi đổi cảnh/cuộn | Chưa đo tích hợp realtime; ảnh sau cuộn không chứng minh scheduler/overlay đúng |
| Nhẹ khi chơi game | Chưa đo với game, GPU inference không dùng; không suy từ microbenchmark ra FPS hoặc độ an toàn tài khoản |

**Quyết định và bước triển khai tiếp theo (2026-10-20):** dùng hybrid Groq theo lựa chọn của người dùng: OCR/overlay cục bộ; chỉ gửi cụm mới sau hai mẫu ổn định; cache trong RAM; gộp tối đa 8 cụm/2.800 ký tự và bỏ câu trùng trong cùng batch. Đã bỏ chờ cố định 4 giây; bộ giới hạn dựa trên header còn lại/reset của Groq và tự cooldown/retry tối đa hai lần khi HTTP 429, rồi chọn lại các cụm vẫn còn trên màn hình. Toàn bộ 112 kiểm thử ngoại tuyến đạt; gói FDD/self-contained mới đã tạo; Windows OCR + overlay smoke test trên nội dung tổng hợp đạt, đăng ký MSIX đã gỡ. Chưa gửi API request thật hoặc đo end-to-end trên cuộn manga/video. Bước đánh giá tiếp theo là thử một trang manga và một đoạn YouTube ngoài game, ghi nhận thời gian từ chữ ổn định tới overlay và lỗi OCR/429. Đây không chứng minh hiệu năng cutscene liên tục hoặc an toàn khi chạy cùng game.

Mục tiêu đầu-cuối 1–1,5 giây vẫn chỉ là mục tiêu thiết kế. Lượt này đã đo từng khâu và đối chiếu dữ liệu thật, **chưa chạy một bản app cục bộ hoàn chỉnh liên tục trên video hoặc cuộn manga**. Không có số liệu giả về độ trễ end-to-end, số câu bỏ sót toàn video, hoặc FPS.

## Tái lập và trạng thái hệ thống

- Chuẩn bị: scripts/prepare-ocr-evaluation.py (cần ảnh mẫu đã lưu).
- Windows: scripts/evaluate-windows-ocr.ps1 cùng bản build riêng artifacts/ocr-evaluation-package; identity `KuuHaku.StoryTranslator.Evaluation`, đã gỡ sau mỗi lượt. Không đóng app chính, không đọc key, không đổi Developer Mode/certificate/runtime hệ thống.
- Rapid: scripts/evaluate-rapidocr.py, ba variant. `rapidocr 3.10.0`, `onnxruntime 1.31.0`, model cục bộ dưới models/ocr-evaluation, runtime riêng .tools/ocr-eval-venv.
- Grouping: scripts/OcrLayoutEvaluation, tham chiếu trực tiếp Core hiện tại.
- Chấm: scripts/score-ocr-evaluation.py; raw: artifacts/multimodal-evaluation/ocr-scores.json và layout-fragmentation.json.
- Nguồn/frame/ảnh chuẩn/thời điểm và log dưới artifacts/multimodal-evaluation; input frame 05 chỉ lưu để audit, không chấm. Metadata khung là thời điểm đọc ngay trước chụp, không bảo đảm đồng bộ frame chính xác từng millisecond.
- Chỉ thêm harness và nhánh CLI offline `--evaluate-ocr`; chưa thay provider/OCR của bản app chính. Không chạy lại toàn bộ unit suite trong lượt này, không sử dụng con số 107 PASS lịch sử để xác nhận harness mới.
- Nguồn công cụ OCR: https://rapidai.github.io/RapidOCRDocs/main/en/install_usage/rapidocr/install/ ; không suy chất lượng từ benchmark của tác giả.
