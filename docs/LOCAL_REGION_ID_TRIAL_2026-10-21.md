# Thử tọa độ cục bộ và bản dịch theo ID

## Mục tiêu và phạm vi

Theo yêu cầu thực hiện bước tiếp theo: định vị vùng chữ trên máy và gán ID, để model chỉ trả dịch theo ID thay vì đoán tọa độ. Phép thử chỉ dùng trang manga 172 đã cung cấp. Chưa thay app chính hoặc bật gửi ảnh màn hình liên tục.

## Phép đo đã thực hiện

- Comic Text Detector ONNX, ONNX Runtime CPU hai luồng: 16.931 / 17.120 / 17.036 ms cho ba lượt, khoảng 1.036 MiB peak working set. Chỉ 8 vùng; 6/16 vùng chép tay có một box IoU >=0,5. Loại phương án này cho luồng nhanh trên cấu hình hiện tại. Thời gian khác phép thử OpenCV trước đây; không tráo số liệu giữa backend/trang.
- Gom hình học từ 67 dòng OCR đã lưu: khoảng 2,8 ms riêng khâu gom; không bao gồm 1.868 ms OCR có sẵn. Ngưỡng ghép transcript ban đầu quá rộng, có thể nhận nhầm câu dài vào một vùng chỉ chứa nửa câu. Đã siết ghép replay, không dùng nó thay hợp đồng ID của provider.
- RapidOCR PP-OCRv5 mobile detector-only trên ảnh 2x: không chạy nhận dạng hoặc classifier. Dòng vẫn được phát hiện dù recognizer không đọc đủ tin cậy. Dùng liên kết dòng hai chiều theo khoảng cách/độ chồng và ghép từ đơn nằm trong đoạn. Sau thay đổi cuối: ba lượt 816 / 571 / 587 ms cho detection + grouping, process peak 364 MiB. Lượt đầu có khởi tạo model lười; thời gian load riêng chưa tách, không hiểu trường modelLoadMs=0 là không có chi phí load.
- Cấu hình detector mặc định tìm 18 vùng, bao gồm số trang; 13/16 vùng chép tay đạt IoU >=0,5. Bỏ số trang để tạo 17 ID. Đây chưa phải 16 ô thoại hoàn chỉnh: vẫn có caption thiếu dòng hoặc bị tách. Hạ box threshold từ 0,5 xuống 0,3 tạo thêm vùng nhưng không tăng số hit, nên giữ 0,5 cho thử ID.
- Replay bản dịch Vision cũ ghép được 9/14 transcript ở ngưỡng bảo thủ. Các transcript gộp hai vùng hoặc không đủ tin cậy được để trống, không lấy mapping chép tay gán vào ảnh. Bản dịch cũ không được sửa tay. Kết quả replay không chứng minh chất lượng của request ID mới.

Tọa độ và ID đều được sinh tự động. Box chép tay chỉ dùng chấm IoU. Không dùng vị trí do Vision trả. Các số trên là từng khâu, không phải độ trễ app từ màn hình tới overlay.

## Artifact để xem và tái lập

- Script: `scripts/manga_line_regions.py`, `scripts/evaluate-local-manga-regions.py`, `scripts/prepare-manga-id-trial.py`.
- Detector chậm: `artifacts/manga-regression-172/local-regions/assessment.json`.
- Nhánh mặc định nhẹ: `artifacts/manga-regression-172/local-regions-line-detector/assessment.json`, `detected-regions.png`, `preview.html`.
- So sánh threshold 0,3: `artifacts/manga-regression-172/local-regions-line-detector-0.3/assessment.json`.
- Ảnh ghép 17 crop gắn nhãn: `artifacts/manga-regression-172/id-trial/id-input.png`; bảng ID -> tọa độ: `manifest.json`. Crop có lề ngữ cảnh 10 pixel; tất cả đều từ ảnh gốc đã cung cấp.

Request mới chỉ yêu cầu translations[id,text,uncertain], không yêu cầu chép nguồn hoặc tọa độ. Bộ kiểm tra chặn ID thiếu/thừa/lạ/trùng và ánh xạ tọa độ từ manifest cục bộ. Output budget 2.048 token, chưa biết có đủ cho lượt thật. Không thêm rule dịch riêng WAITER để tránh làm đẹp kết quả chỉ trên ví dụ đã biết.

`powershell.exe -NoProfile -File scripts/test-manga-vision.ps1 -UseLocalIds -ValidateOnly`: PASS cho 17 ID duy nhất, tọa độ hợp lệ và payload một ảnh; không gọi API.

## Trạng thái chờ gửi thử

Xét duyệt tự động ban đầu từ chối lệnh mở lượt thử vì quyền trước chỉ nêu ảnh gốc, chưa nêu rõ ảnh ghép các crop. Sau đó người dùng xác nhận rõ: cho phép gửi ảnh ghép 17 vùng này một lần. Người dùng tự nhập key trong console; một request ảnh ghép đã hoàn tất. Công cụ không lấy lại key từ app/lượt trước và không gửi thêm ảnh ngoài contact sheet đã cho phép.

## Kết quả thực — 2026-10-21

- Một API call hoàn tất với finish_reason=stop trong **2.338 ms**, so với 4.153 ms ở lượt full-page trước: giảm 43,7% trong hai lượt riêng lẻ này. Không gọi đây là benchmark lặp hoặc khẳng định tốc độ chung của provider.
- Trả đủ **17/17 ID**, không trùng/lạ; script đánh giá xác nhận toàn bộ tọa độ render bằng manifest cục bộ trong sai số 1e-8. Provider không trả tọa độ. Tên Nazuna được giữ nguyên.
- Chất lượng nghĩa **chưa đạt**. R001 đổi khi mình tỉnh táo thành khi họ tỉnh lại; R002 bỏ sự kiện người nói đến nơi; R006 tiếp tục dịch WAITER thành Đầu bếp. R007 thêm nội dung hỏi đáng hay không từ cụm kế bên; R010 đổi sắc thái sang tương lai. Đây là chấm thủ công có ví dụ, không phải tỷ lệ dịch đúng tự động.
- Detector có một false positive rất nhỏ (R015, khoảng 5,5 x 7 pixel trên ảnh gốc); model vẫn trả Bạn bè và uncertain=false. Cần chặn vùng thiếu thông tin ở máy, không tin riêng cờ uncertain của model. Không sửa tay kết quả để làm đẹp preview.
- Tốc độ detector khoảng 0,57–0,59 giây được đo riêng trước đó. 2,338 giây API không gồm capture, chờ ổn định, detect hoặc render; chưa có phép đo end-to-end hoặc test cutscene. Chưa đạt mục tiêu cutscene 1–1,5 giây.
- Không thay engine trong app chính và không bật gửi ảnh màn hình liên tục. ID giải quyết quyền sở hữu tọa độ và mapping, chưa giải quyết chất lượng dịch hoặc các caption bị phân mảnh.

Tái kiểm tra cục bộ: `scripts/assess-manga-id-trial.py` — PASS cho tập ID và tọa độ. Tệp thật nằm trong `artifacts/manga-regression-172/id-trial`: `vision-response.json`, `vision-result.json`, `vision-metrics.json`, `vision-preview.html`, `assessment.json`. Không có API call mới khi chấm.

Hướng kỹ thuật tiếp theo nếu tiếp tục thử: giữ detector/ID cục bộ, lọc false positive trước khi gửi; tách bước đọc chữ Vision và bước dịch văn bản để dùng transcript sạch với translator text, rồi đo chất lượng/tổng thời gian. Lượt full-page trước có transcript tốt nhưng dịch vẫn lỗi, nên cần so sánh bằng dữ liệu đã lưu trước khi thêm lượt ảnh hoặc đổi model. Đây là hướng chưa được kiểm chứng, không phải bản sửa đã đạt.
