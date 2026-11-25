# Chế độ cutscene thử nghiệm — 2026-10-23

Ứng dụng hiện có hộp chọn **Chế độ cutscene**. Khi chọn một vùng ngang lớn, các box OCR ở gần giữa cạnh trên hoặc cạnh dưới được đưa vào luồng ổn định 2 mẫu, cache, dịch Groq và overlay hiện có. Vùng nhỏ và trang dọc vẫn dùng toàn bộ box OCR, nên chế độ truyện không bị thay đổi. Bộ lọc chỉ dùng tọa độ và chữ OCR; không truy cập tiến trình game.

Đây là bộ lọc thực nghiệm. Nó có thể bỏ sót phụ đề lệch mép hoặc nằm giữa màn hình, và không khắc phục lỗi nhận dạng của Windows OCR. Bộ khung video trước đó được đánh giá bằng PP-OCRv6 medium/DirectML ở luồng ngoại tuyến; ứng dụng chạy thực tế vẫn dùng Windows.Media.Ocr. Vì vậy các số đo 175–405 ms của phép thử trước **không phải** tốc độ OCR đã xác nhận trong ứng dụng này.

Đã xác nhận: build 0 cảnh báo/0 lỗi, 129/129 kiểm tra lõi đạt, OCR tổng hợp trong MSIX đọc đúng `STORY TEXT 123`. Gói thử tự chứa ở `artifacts/cutscene-trial-20261023/StoryTranslator-1.0.0.0-x64-self-contained.msix`; script `run-screen-trial.ps1 -LayoutDirectory ...` đăng ký tạm và gỡ khi đóng ứng dụng. Chưa có phép đo trực tiếp từ lúc phụ đề xuất hiện đến lúc overlay hiển thị. Cần ghi thời gian trên hai câu chuyển tiếp khi người dùng thử video và kiểm tra box nào bị thiếu trước khi coi là đáp ứng cutscene.
