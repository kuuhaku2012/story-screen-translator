# Báo cáo kiểm thử offline

## So sánh hai model text bổ sung — 2026-10-21

Người dùng chạy tối đa hai request đã chuẩn bị. GPT-OSS 120B: 1.390 ms; Qwen 3.8 27B: 1.206 ms; cả hai đủ 14 ID, nguồn/instruction giống nhau và giữ Nazuna. Qwen tự nhiên hơn trên trang này nhưng còn sai WAITER thành Đồ uống; 120B sai chủ thể ở BET YOU và cụm COUPLE OF FRIENDS. Chọn Qwen làm ứng viên thử tiếp, chưa coi đạt production hoặc đổi app. Một lượt/trang không phải benchmark lặp hay bằng chứng tốc độ cutscene. Chi tiết `docs/TEXT_MODEL_COMPARISON_2026-10-21.md`; chấm cục bộ không gọi thêm API.

## Dịch text sạch với model mặc định — 2026-10-21

Một request `openai/gpt-oss-20b` dịch 14 transcript Vision nguyên văn, không ảnh, hoàn tất 995 ms và đủ ID/Nazuna. Sửa được WAITER và chủ thể tỉnh táo, nhưng sai wasted (đã hết), free rounds (vòng miễn phí), SHEESH (Đồ), over nothing (vì điều gì đó). Chất lượng cốt truyện chưa đạt dù nguồn chữ sạch. Đánh giá cục bộ xác nhận nguồn không bị sửa và ID hợp lệ; không gọi lại API. Chưa thay app/gói chính hoặc chấp nhận pipeline hai giai đoạn. Chi tiết tại `docs/CLEAN_TEXT_TRIAL_2026-10-21.md` và `artifacts/manga-regression-172/text-trial/assessment.json`.

## Lượt manga ID cục bộ thực — 2026-10-21

Người dùng cho phép riêng ảnh ghép 17 crop, tự nhập key và hoàn tất một request. API trả trong 2.338 ms, đủ 17/17 ID không trùng/lạ; kiểm tra cục bộ xác nhận mọi tọa độ render lấy từ manifest. Tên Nazuna giữ nguyên. Vẫn sai nghĩa rõ ở R001/R002/R006 và tạo bản dịch tự tin cho false positive R015, nên chưa đưa vào app chính. Chưa có phép đo capture-to-overlay hoặc cutscene. Đánh giá chỉ đọc dữ liệu đã lưu, không gọi lại API. Chi tiết: `docs/LOCAL_REGION_ID_TRIAL_2026-10-21.md` và `artifacts/manga-regression-172/id-trial/assessment.json`.

## Đối chiếu manga trang 172 và một lượt Vision thực — 2026-10-20

Người dùng báo bản hybrid còn dịch manga tệ. Đã đo lại đúng ảnh cung cấp: RapidOCR cả trang và crop thủ công vẫn có lỗi chữ, không thay engine trong app. Sau khi người dùng cho phép riêng ảnh này và tự nhập key vào công cụ thử, một request Vision trả trong 4.153 ms. Transcript cải thiện (1/383 ký tự A-Z/0-9 sai sau ghép đối chiếu thủ công), nhưng có dịch sai WAITER thành Đầu bếp, trộn hai vùng, và 8/14 box sai giao thức tọa độ; diễn giải xyxy cũng không có box đạt IoU 0,5. Không chấp nhận kết quả này làm overlay production hoặc bằng chứng đáp ứng cutscene. Chi tiết, giới hạn phép chấm và đường dẫn tệp tại `docs/MANGA_172_REGRESSION.md`. Không có lần gửi API thứ hai; preview đối chiếu được tạo từ phản hồi đã lưu. Mã app/gói chính chưa thay bằng Vision.

## Hybrid Groq: gom câu mới, cache và cooldown — 2026-10-20

- OCR, nhận dạng vùng, glossary và overlay vẫn chạy cục bộ. Chỉ vùng chữ ổn định qua hai mẫu được xếp hàng; cache dịch trong phiên tiếp tục nằm trong RAM.
- Bỏ chờ cố định 4 giây giữa mọi request. Bộ giới hạn dùng header reset còn lại cho request/token; HTTP 429 ưu tiên `Retry-After`, rồi reset header, sau đó dự phòng 30 giây. Tối đa hai lần thử lại; sau cooldown pipeline chọn lại các cụm hiện còn trên màn hình. Hàng đợi kiểm tra còn việc trước khi ngủ và hủy được khi tắt.
- Câu trùng trong một batch chỉ gửi một lần. Batch giữ giới hạn 8 cụm/2.800 ký tự; đầu ra vẫn được ánh xạ về từng vị trí. Tên riêng và token glossary tiếp tục được giữ nguyên qua JSON schema.
- Kiểm thử có HTTP handler giả, không dùng key và không gửi mạng: **18/18 focused tests PASS; 112/112 toàn bộ offline suite PASS**.
- Tạo lại gói unsigned FDD `artifacts/msix/StoryTranslator-1.0.0.0-x64.msix`: **7,317,126 bytes**, SHA256 `6BD4FA05D5D768006D766CF45EC6BDA4C6DFC3FFBAA896AB57C57C4889A3C416`. Cập nhật gói self-contained launcher dùng tại `artifacts/screen-trial/StoryTranslator-1.0.0.0-x64-self-contained.msix`: **71,455,716 bytes**, SHA256 `7E4DD5EA4C57D6E7A01D953D4B0A477E0401D96867D40526BA8E2A71F15D9DF5`.
- `scripts/run-screen-trial.ps1 -SpatialSmokeTest`: **PASS** với Windows OCR production trên nội dung tổng hợp (`STORY TEXT 123`, `RIGHT TEXT 456`, các đoạn mẫu); script xác nhận đã gỡ đăng ký MSIX tạm. Không dùng Groq/key/mạng. Chưa chạy app với key thật, chưa đo độ trễ/độ chính xác trực tiếp trên manga hoặc YouTube, và không mở client game.

## Đánh giá manga/cutscene theo nguồn thật — 2026-10-20

Xem docs/EVALUATION_MANGA_CUTSCENE_2026-10-20.md. Đã đo 42 ảnh với Windows OCR hai chế độ và RapidOCR hai cấu hình (504 lượt), thêm phép thử recognizer-only bị loại vì nhận nhầm khung trống, chấm 22 ô thoại riêng/2 vùng lặp sau cuộn và 3 phụ đề/4 khung trống. Đã dùng chính Core.Group để chứng minh thay OCR vẫn không sửa phân mảnh. Đã đo NLLB từ OCR thật và batch 1/6/22 câu. Chưa có app cục bộ tích hợp hoặc benchmark realtime toàn video; chất lượng dịch nghĩa vẫn chưa đạt. Gói đánh giá có identity riêng đã gỡ.


## OCR truyện và Groq 400/phản hồi chưa hoàn chỉnh — 2026-10-20

- Bằng chứng do người dùng cung cấp: OCR KABURA thành KASURA, HARU thành HAZU, YEAH thành YEAR; nhiều dòng bị phân mảnh; ảnh riêng báo HTTP 400 và phản hồi chưa hoàn chỉnh. Không có raw provider error/finish_reason, nên không khẳng định nguyên nhân chính xác của từng lần 400.
- Thêm checkbox chế độ truyện (mặc định OFF): resize ảnh 2× trong giới hạn Windows OCR, ánh xạ tọa độ lại; gom dòng theo căn giữa bổ sung. Không thay engine hoặc hardcode tên sai.
- GPT-OSS chuyển JSON mode sang schema strict; output min 2048/max 4096. Nếu finish_reason length hoặc mã json_validate_failed/json_generation_failed thì chia nhóm đệ quy hữu hạn đến từng item. Prompt dùng ngữ cảnh các item nhưng không gộp/đổi ID; tên riêng được yêu cầu giữ nguyên. Giữ kiểm tra ID/glossary và cooldown đã có.
- Build solution: 0 warning/0 error. Không thêm/chạy test trong lượt này; chưa so OCR trước/sau trên ảnh truyện thật và chưa gọi Groq bằng key người dùng. Không khẳng định đã giải quyết triệt để chất lượng dịch manga. Các số test bên dưới thuộc phiên bản trước.

## Giữ lớp dịch khi vùng chọn đứng yên — 2026-10-20

- Nguyên nhân chớp: BeginCapture luôn Hide mỗi mẫu dù nội dung không đổi. Thay bằng SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE) và GetWindowDisplayAffinity kiểm tra trên HWND top-level của chính overlay, có kiểm tra phiên bản Windows. Khi hỗ trợ, Begin/EndCapture không ẩn/hiện; fallback cũ nếu API không được xác nhận.
- WindowsCapturedFrame giữ SHA-256 tính từ byte BMP sẵn có. CaptureWorker dùng lại layout OCR khi dấu băm không đổi, vẫn phát quan sát để vượt hai mẫu ổn định. Không lưu ảnh/nội dung xuống file và không thêm provider request. Pipeline có sẵn SequenceEqual/cache giữ view và kết quả dịch.
- Solution build thành công: 0 warning/0 error. Không thêm/chạy test suite trong lượt này. Chưa quan sát mức nhấp nháy trên trang thật; cần người dùng thử bản mới. Việc API trả thành công không thay bằng chứng kiểm tra capture trên từng cấu hình Windows/driver.
- Ảnh động vẫn làm thay đổi fingerprint; các nguồn capture tuân thủ display affinity sẽ không thấy lớp bản dịch. Không đụng process/game khác hoặc thay cơ chế dịch đã chạy được.

## Sửa JSON Array/Object — 2026-10-20

- Ảnh lỗi cho thấy bộ đọc gọi thao tác Object trên Array. Parser batch trước đó chỉ nhận object có trường translations. Bản sửa nhận cả object bọc translations và mảng trực tiếp, kiểm tra loại mỗi item/ID/text trước khi đọc; tiếp tục yêu cầu đầy đủ ID, không trùng, không lạ và giữ token glossary.
- Không thêm/chạy test trong lượt sửa này. Cần thử lại Groq thật với key do người dùng nhập trong app; không đọc key hay raw phản hồi riêng tư.
- Chưa thay nhà cung cấp hoặc hứa tốc độ bằng Google Lens. Đề xuất tiếp theo: Google Cloud Translation NMT cho dịch nhanh theo danh sách chuỗi, Groq tùy chọn cho ngữ cảnh cốt truyện. Cần cấu hình dịch vụ riêng nếu người dùng chọn.

## Sửa điều tiết 429 — 2026-10-20

- Bằng chứng: ảnh người dùng báo HTTP 429; code gửi các nhóm nối tiếp không giãn cách và chưa đọc rate-limit headers. Không đọc raw error, key hoặc thông tin tài khoản nên chưa xác định giới hạn cụ thể đã chạm.
- Thêm GroqRateLimiter dùng chung trong MainWindow qua các lần bật/tắt, tối thiểu 4 giây/request, đọc ngân sách token/reset, output budget theo độ dài. Rate-limited pipeline chọn lại cụm hiện tại sau cooldown, thử lại tối đa hai lần; OFF hủy chờ. Hết request ngày hoặc Retry-After trên 2 phút thì dừng với hướng dẫn Limits.
- Biên dịch solution thành công, 0 cảnh báo/0 lỗi; đóng gói lại bản trial self-contained và bản chính framework-dependent thành công với source cuối. Không thêm/chạy lại test suite trong lần sửa này. Con số 107 PASS bên dưới thuộc bản trước; chưa kiểm thử tự động cơ chế chờ và chưa gọi Groq thật bằng key người dùng.
- Không cam kết tránh mọi 429: hạn mức do Groq áp dụng cho organization; giữ nguyên tên model và không truy cập key.

## Cập nhật mới nhất 2026-10-20 — bản dịch theo từng cụm

- Thay hiển thị một khối bằng các ô riêng theo tọa độ từ Windows OCR, gom dòng thành đoạn và tách các cột/nhãn xa nhau. Chữ trắng Segoe UI SemiBold, nền tối cục bộ mặc định 95%, chỉnh 35–100%; phần ngoài ô trong suốt. Có mục bản dịch đầy đủ trong giao diện chính nếu ô nhỏ bị cắt.
- Ổn định riêng từng cụm, cache RAM, gửi tuần tự tối đa 8 cụm/2.800 ký tự mỗi batch. JSON ID được xác thực trước ánh xạ bản dịch về vị trí gốc; kết quả cũ bị chặn khi đổi chữ/OFF.
- Offline suite: **107/107 PASS**, gồm gom cụm, cột xa nhau, ổn định độc lập, kết quả chậm bị loại, OFF, cache khi di chuyển, ánh xạ JSON đảo thứ tự/thiếu ID, vị trí ô và capture layout. Build solution, trial self-contained và gói chính framework-dependent thành công.
- Native **SpatialSmokeTest PASS**: Windows OCR thật trên cửa sổ do ứng dụng tự tạo, nhận 4 cụm ở hai cột; dịch giả cục bộ qua pipeline mới, render từng ô riêng. Đã xem ảnh `artifacts/screen-trial/spatial-result.png` để kiểm tra độ rõ/vị trí. Không gọi Groq/key/mạng trong phép thử này.
- Native **OverlaySmokeTest PASS** và **LargeOcrSmokeTest PASS** sau cập nhật. Mỗi lượt gỡ đăng ký MSIX tạm thành công. Không mở game hoặc tài khoản.
- Giới hạn: mới kiểm chứng bằng mẫu có kiểm soát và HTTP giả, chưa xác nhận Groq batch bằng key thật. OCR tiếng Anh, gom cụm hình học có thể sai với chữ nhỏ, font trang trí, manga hoặc vùng quá dày. Chưa benchmark tài nguyên/độ trễ và chưa thử thực tế trong game. Các mục bên dưới mô tả những bản trước.
## Cập nhật 2026-10-20 — Groq và thử màn hình

### Nền trong và vùng toàn màn hình

- Ảnh người dùng là preview (`Bản dịch sẽ hiển thị...`), không phải kết quả dịch. Đã đổi nhãn preview thành “chưa dịch” và bỏ nền trắng: lớp phủ có nền trong suốt, chữ trắng/bóng đen.
- Không còn chặn ở 4.000 ký tự. Tự chia tối đa 1.800 ký tự/request, ưu tiên newline/space và tránh cắt token glossary; gửi tuần tự, cache từng đoạn trong RAM. Có giới hạn 32.000 ký tự cho một lần quan sát để tránh số request không giới hạn. Không tự retry lỗi API.
- GPT-OSS dùng reasoning_effort=low, include_reasoning=false và output budget 4096 theo API Groq. Không thay đổi quyền truy cập model của key.
- Ảnh vượt OcrEngine.MaxImageDimension được thu theo tỉ lệ trong bộ nhớ trước OCR; ảnh nhỏ giữ nguyên. Giữ newline từ OCR. Vùng trên 1920×1080 pixels lấy mẫu mỗi giây thay vì nửa giây để giảm tần suất xử lý; chưa benchmark CPU/RAM. Thu ảnh có thể làm chữ nhỏ khó nhận dạng hơn.
- RED: chưa có OcrImageSizing và long text bị chặn. GREEN: **95/95 offline tests** đạt, gồm text trên 4.000 ký tự, cache đoạn, token sát ranh giới và các kích thước landscape/portrait.
- Chưa có bằng chứng dịch Groq thật toàn màn hình của người dùng; static full-screen phải giữ chữ ổn định qua hai mẫu. Không có bố trí bản dịch theo từng bong bóng/từ; kết quả vẫn là một khối trong vùng chọn.
- Native smoke mới **PASS**: điểm màu trên cửa sổ LightSkyBlue do app tạo vẫn giữ màu qua nền overlay (không còn tấm trắng), vị trí overlay khớp rectangle và OCR đọc đúng sample khi ẩn lớp phủ. `-LargeOcrSmokeTest` cũng **PASS** với bitmap tổng hợp rộng hơn MaxImageDimension 128 pixels, nhận `STORY TEXT 123`. Cả hai lượt gỡ đăng ký MSIX tạm thành công, không key/mạng/game.

### Tự thu giao diện khi bật dịch

- Bật dịch sẽ thu MainWindow xuống taskbar trước khi Start worker. Guard kiểm tra trạng thái minimized trước capture, thay cho hạn chế giao diện chồng vùng. Mở lại giao diện tự tạm dừng, hủy request và ẩn overlay; lỗi capture/Groq khôi phục cửa sổ để báo lỗi.
- Key vẫn trong RAM khi chỉ thu/mở lại cửa sổ; chỉ cần nhập lại khi app bị đóng để cập nhật. Thay đổi không đọc nội dung key hoặc phát sinh request thử tự động.
- Existing offline suite: **89/89 PASS**. Đây không phải bằng chứng đã click thử luồng minimize/restore với key thật; kiểm tra build/overlay mẫu được ghi riêng.

### Bản dịch ngay tại vùng chọn

- Theo yêu cầu mới, thay cửa sổ kết quả tách rời bằng lớp nền trắng phủ đúng rectangle được chọn, co chữ xuống khi vùng nhỏ, không lấy focus và đặt mouse-through trên cửa sổ riêng của app. Không sửa trang/ảnh hay can thiệp tiến trình game.
- Capture tạm ẩn overlay trước khi chụp, DwmFlush đồng bộ render, finally khôi phục overlay ngay cả khi capture hủy/lỗi. State giữ guard của dòng dịch: kết quả mới không hiện giữa lúc chụp; OFF và dòng stale không được hồi sinh sau capture.
- Thêm 5 regression tests: ẩn/hiện khi capture, kết quả đến giữa capture, OFF giữa capture, guard dòng cũ hết hiệu lực, finally chạy sau hủy. Đã có RED vì chưa có state/afterCapture; sau triển khai **89/89** đạt. Guard chặn cửa sổ công cụ vẫn giữ; overlay nay được phép ở trong vùng vì có cơ chế ẩn khi chụp.
- Model mặc định đổi `openai/gpt-oss-20b`; không khẳng định lỗi 404 đã hết khi chưa có request thật bằng key người dùng.
- Bài integration `run-screen-trial.ps1 -OverlaySmokeTest` **PASS** trên cửa sổ mẫu riêng do app tạo: GetWindowRect khớp pixel rectangle, OCR đọc `STORY TEXT 123` từ chữ gốc khi lớp `OVERLAY ONLY 999` tạm ẩn, và overlay được khôi phục sau capture. Bài thử không dùng key, không gọi Groq; đăng ký MSIX tạm đã gỡ. Không suy ra độ chính xác manga hay hiệu năng thực từ mẫu này.

### Sửa tự đọc cửa sổ công cụ

- Người dùng cung cấp đầu ra OCR có cả nhãn giao diện Việt và đoạn chữ Anh, chứng minh vùng đọc bị trộn giao diện của tool. Code ẩn tool khi chọn nhưng hiện lại tại vị trí cũ, không kiểm tra chồng vùng trước khi capture.
- Thêm kiểm tra vùng cửa sổ riêng bằng GetWindowRect trước mỗi lần chụp. Nếu tool hoặc overlay đang hiển thị chồng vùng: dừng worker/pipeline và báo người dùng di chuyển cửa sổ; không chụp mẫu bị chặn. Không dùng injection/hook hay API tiến trình game.
- Thêm 6 regression cases (đè vùng, bao vùng, sát mép, tách vùng, tọa độ âm, guard hủy trước native screen read). RED thiếu API guard; GREEN toàn bộ **84/84** đạt. UI có nút đặt vị trí overlay khi OFF; chưa xác minh trực quan thao tác kéo/vùng của người dùng trên màn hình thật.
- Đây là sửa đầu vào OCR bị lẫn tool; chưa có bằng chứng độ chính xác OCR trên ô thoại manga của người dùng sau sửa.

- Thêm Groq HTTP adapter chỉ gửi chữ tới endpoint cố định; không dùng SDK/dependency mới. Key chỉ được đưa vào Authorization header, không có trong payload/log/file. UI dùng key trong RAM của phiên chạy.
- Đã chứng kiến bước RED: 4 kiểm thử Groq không biên dịch vì adapter chưa tồn tại; sau triển khai, toàn bộ **78/78 kiểm thử offline đạt**, không skip/fail.
- Kiểm thử mới xác minh payload/header, kết quả dịch, hủy yêu cầu HTTP, HTTP 429 không tự retry/không lộ raw error body, và chặn bản dịch mất token glossary.
- Giao diện đã nối capture/OCR/pipeline/overlay; tự quét 2 lần/giây sau khi bật. OFF hủy yêu cầu và ẩn overlay. Cache của phiên màn hình chỉ trong RAM; cấu hình vùng chọn ở thư mục data trong project. API key chỉ nhập trong app.
- Chưa có request Groq thật: chưa nhập key của người dùng, nên chất lượng dịch thật và end-to-end từ màn hình đến Groq đang chờ người dùng thử.
- Đóng gói Release/MakeAppx đã thành công cho cả hai loại: FDD **7,272,842 bytes**, bản thử self-contained **71,411,439 bytes**. Cả hai unsigned. Script dùng SDK home/NuGet cache trong project, tắt tạo HTTPS dev certificate và telemetry trong tiến trình build.
- `scripts/run-screen-trial.ps1 -OcrSmokeTest`: **PASS**, nhận đúng `STORY TEXT 123`; script xác nhận gỡ đăng ký tạm. Không đọc màn hình hay gửi Groq ở bài này.
- Đã mở launcher tương tác và quan sát tiến trình ứng dụng có cửa sổ **Story Translator — Thử dịch màn hình**, không thoát khi khởi động. App mở ở trạng thái OFF; chưa kiểm tra trực quan toàn bộ layout hoặc click tương tác. Launcher giữ đăng ký MSIX khi app mở và tự gỡ sau khi đóng app; chưa gọi bước đó là “đã cleanup” trong lúc người dùng đang thử.
- Hướng dẫn chạy: [SCREEN_TRIAL.md](SCREEN_TRIAL.md). Các kết quả và lựa chọn “provider để sau” bên dưới là lịch sử ngày 2026-10-15, được cập nhật bởi phần này.

Ngày ghi nhận: 2026-10-15

## Phạm vi

Các bài kiểm thử logic chạy bằng frame tổng hợp, OCR giả xác định, translator giả cục bộ, glossary ví dụ, bộ lọc ổn định câu, coordinator/cache và presentation gate. Ngoài ra đã chạy production Windows OCR trên một ảnh tổng hợp trong tiến trình MSIX có package identity. Không chụp màn hình thật, không gửi yêu cầu mạng và không khởi chạy Wuthering Waves.

## Kết quả

- `OfflinePipelineTests`: 3/3 đạt.
- `StoryTranslationPipelineTests`: 3/3 đạt; kịch bản `A,A,B,B,A,A` chỉ gọi translator cho hai câu khác nhau và câu A trở lại được lấy từ cache; cùng câu được ổn định lại sau OFF/ON.
- `StaleTranslationPresentationTests`: 4/4 đạt với coordinator giả cố ý trả kết quả muộn; A chậm không ghi đè B nhanh, candidate B làm vô hiệu A ngay ở mẫu đầu, A hoàn tất sau OFF/ON không xuất hiện trong phiên mới, và callback cũ đã xếp hàng bị chặn khi flush sau OFF/ON.
- `TextStabilityFilterTests`: có kiểm tra candidate normalized thay đổi riêng với lúc ổn định, cùng reset candidate/dedupe ở ranh giới session.
- Token glossary ổn định giữa hai lần bảo vệ cùng một câu; tên vẫn được khôi phục đúng.
- Pipeline Core sản phẩm dùng chung trong integration test, tuần tự hóa cập nhật filter mà không giữ khóa trong lúc dịch; coordinator vẫn có thể hủy dòng cũ khi dòng mới tới.
- Deferred-overlay fake lưu callback rồi gọi guard khi test chủ động flush hàng đợi; các test xác minh guard loại callback cũ sau OFF/ON. Đây là mô phỏng ranh giới callback, không phải xác minh tích hợp hoặc hình ảnh thực tế của WPF Dispatcher.
- Dòng trả về `true` từ lời gọi hiển thị nghĩa là overlay nhận yêu cầu hoặc đã xếp nó lên dispatcher; guard có thể loại bỏ yêu cầu cũ trước khi UI cập nhật. Đây không phải xác nhận rằng pixel đã được vẽ lên màn hình.
- Khi candidate OCR chuẩn hóa thay đổi (ngay từ quan sát đầu tiên của câu mới), generation dòng tăng để vô hiệu hóa kết quả câu cũ. OFF/ON reset candidate và last-emitted, đồng thời tăng generation session; cùng một câu còn trên màn hình có thể ổn định lại và dùng cache.
- Pipeline kiểm tra generation trước khi gửi dịch, khi nhận kết quả và trong callback đã xếp hàng; fake trì hoãn callback để kiểm tra hành vi này. WPF Dispatcher thật chưa được kiểm thử hiển thị.
- Luồng frame giả → OCR giả → đợi câu ổn định → bảo vệ tên riêng → dịch giả → khôi phục tên → overlay gate được kiểm tra.
- Tên `Rover` và `Academy` được giữ nguyên trong kết quả giả.
- Câu lặp không làm gọi translator lần hai; cache trả kết quả đã có.
- OFF ẩn overlay và chặn lần hiển thị sau đó.
- Lỗi translator được truyền về caller và không mở overlay.
- Worker giải phóng frame giả trong luồng thành công.

Các kết quả trên kiểm chứng logic ứng dụng với thành phần giả. Chúng không kiểm chứng độ chính xác OCR thật, chất lượng dịch, tốc độ, tính tương thích của capture trên máy khác hay trạng thái anti-cheat.

## Build và test (ảnh chụp lịch sử của offline slice)

Generation/session validation fix: commit `c68228a0fdb0573b9a2a42f6e9e07da5f18a4b4c`.
Sửa vô hiệu hóa ngay khi candidate đổi, reset bộ lọc theo session và guard callback trì hoãn: commit `de99c4f4b05c22b08aa32857dc53f222eb5a900c`.

Đã chạy sau khi thêm các bài kiểm thử offline, bằng SDK local .NET 10:

```powershell
dotnet test StoryTranslator.sln
dotnet build StoryTranslator.sln -c Release
```

- Tại thời điểm commit offline slice `de99c4f4b05c22b08aa32857dc53f222eb5a900c`, `dotnet test StoryTranslator.sln`: PASS, 65 passed, 0 failed, 0 skipped. Đây là kết quả lịch sử, được cập nhật bởi lần xác minh MSIX mới nhất ở phần dưới (74 test).
- `dotnet build StoryTranslator.sln -c Release`: PASS, 0 warning, 0 error.

## RED/GREEN evidence

- Removing first-candidate generation invalidation made the adversarial “A completes after B's first OCR sample” test fail because stale A was accepted.
- Removing stability reset made the OFF/ON repeat-line test fail because A did not stabilize again.
- Removing the deferred-overlay callback guard made the queued OFF/ON callback test fail because stale A appeared after queue flush.
- Restoring all three behaviors made these regressions pass. The deferred callback is a fake boundary test, not a real WPF Dispatcher integration test.

## Chưa đo / chưa xác minh

- CPU và RAM khi OFF hoặc ON: chưa đo.
- Thời gian OCR đến overlay: chưa đo.
- Số request dịch tới nhà cung cấp thật: chưa có provider; chỉ đếm lời gọi fake trong test.
- OCR Windows: production recognition trên ảnh tổng hợp đã PASS trong MSIX self-contained (chi tiết ở phần MSIX bên dưới). Unpackaged CLI diagnostic thoát mã 1 và ghi JSON báo thiếu package identity, đúng hành vi fail-closed.
- Dịch thật: chưa tích hợp provider; không có API key hoặc request mạng.
- Giao diện/overlay khi dùng thật, nhận dạng câu từ hình game và kiểm thử đa màn hình: chưa xác minh thủ công.
- Wuthering Waves: chưa chạy. Không dùng với client Kuro cho tới khi Kuro xác nhận bằng văn bản rằng chính xác quy trình OCR vùng chọn thủ công + overlay ngoài tiến trình này được phép. Acc phụ không phải phép thử an toàn.
- Task 10 có kiểm thử offline, production OCR smoke và gói MSIX unsigned; provider dịch vẫn chưa chọn. Task 10 còn partial vì provider chưa tích hợp, Windows tối thiểu chưa được audit/runtime-test và quy trình cài/gỡ cuối người dùng chưa hoàn thiện.

## Cổng an toàn

Không có kết quả offline nào chứng minh “không bị ban”. Nếu không có xác nhận bằng văn bản cho đúng workflow, không chạy đồng thời ứng dụng và client game Kuro.

## MSIX + Windows OCR verification (2026-10-15)

### Decisions and scope

- OCR distribution choice is MSIX x64 with `Windows.Media.Ocr`; translation provider remains deferred. No endpoint, API key, translation request or network test was added. Story Mode remains disabled.
- Full solution tests exercised deterministic fake frames/OCR/translator only. The app was launched only for a package smoke invocation against an in-memory synthetic image; it did not capture a live desktop frame. No Wuthering Waves client or game account (primary or secondary) was used.
- Kuro's [official Wuthering Waves Fair Play policy](https://wutheringwaves.kurogames.com/en/main/news/detail/742) speaks generally about unauthorized third-party tools used for unfair advantages; it does not expressly decide this manual desktop OCR + independent overlay workflow. The [Kuro User Agreement](https://wutheringwaves.kurogames.com/p/agreement_public.html) also describes its authority regarding programs running alongside the game. The project therefore retains written confirmation for this exact workflow as a gate. No zero-ban-risk claim is made.

### Fresh command results

The following commands were run with the project-local .NET 10 SDK and project-local NuGet cache:

```powershell
dotnet test StoryTranslator.sln
dotnet build StoryTranslator.sln -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1 -OutputDirectory E:\tool_dich_man_hinh\artifacts\msix
```

- `dotnet test StoryTranslator.sln`: PASS — 74 passed, 0 failed, 0 skipped.
- `dotnet build StoryTranslator.sln -c Release`: PASS — 0 warnings, 0 errors.
- Initial self-contained package (before the user's packaging choice): `E:\tool_dich_man_hinh\artifacts\msix-self-contained\StoryTranslator-1.0.0.0-x64-self-contained.msix` — **71,402,023 bytes (~68.09 MiB)**; `Get-AuthenticodeSignature` reported `NotSigned`.
- Primary unsigned framework-dependent package, freshly recreated with the updated default script: `E:\tool_dich_man_hinh\artifacts\msix\StoryTranslator-1.0.0.0-x64.msix` — **7,263,431 bytes (~6.93 MiB)**, SHA256 `BA579252803625F493BAE22E82BFEE39A5D967472C4B331F5A7316EFF684C330`; `Get-AuthenticodeSignature` reported `NotSigned`. This is 89.83% smaller than self-contained. It requires `Microsoft.NETCore.App` 10.0 and `Microsoft.WindowsDesktop.App` 10.0. Machine-wide runtimes are 3.1.32 and 8.0.23 only; .NET 10 is not installed. `scripts/package.ps1` uses framework-dependent by default; `-SelfContained` opts into the larger package.
- Manifest XML parsed; package identity is `KuuHaku.StoryTranslator`, publisher `CN=KuuHaku`, architecture `x64`; logo dimensions checked as 44x44, 50x50, 150x150 and 310x150.

### Production OCR smoke and unresolved runtime floor

- `--ocr-smoke-test <result-path>` has argument routing and report-writing unit coverage. An unpackaged CLI diagnostic exited with code 1 and wrote JSON identifying the missing MSIX package identity. Production recognition through the self-contained MSIX **passed**: expected and recognized text were both `STORY TEXT 123`. The selected framework-dependent MSIX was activated but did not write a result; this machine lacks the required .NET 10 runtime, so that package's runtime behavior remains unverified here. No runtime was installed. Temporary package registration was removed, and package absence plus process exit were verified. Developer Mode was already enabled before the test and was not changed; the English OCR language pack was available. .NET first-run created one untrusted HTTPS development certificate; that exact certificate was removed by thumbprint and verified absent from both the personal and trusted-root stores. No certificate trust change occurred.
- Windows target declarations differ: the MSIX manifest `MinVersion` and project `SupportedOSPlatformVersion` are `10.0.17763.0`; TFM is `net10.0-windows10.0.19041.0` (Windows API contract baseline 19041); Windows SDK package is `10.0.26100.87`. These are not interchangeable. The current Release build and MakeAppx package do **not** prove runtime compatibility on Windows 17763. No concrete API compatibility failure was identified, so this documentation update does not change code. Audit API usage and test on the declared minimum OS before claiming that support.
- CPU/RAM, real Windows OCR accuracy, OCR-to-overlay latency, live WPF Dispatcher rendering, translation-provider latency/request count and game-client interaction remain unmeasured/unverified.
- Task 10 remains partial: provider selection and integration, minimum-OS runtime verification, and end-user package signing/runtime/install guidance remain open. Do not use a Kuro client until Kuro confirms this exact workflow in writing; no-ban safety cannot be guaranteed.
