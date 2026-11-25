# Cutscene: xử lý OCR nhiễu và giảm thời gian gửi một câu — 2026-10-23

Người dùng xác nhận bản trước vẫn mất overlay rồi hiện lại khi đổi cảnh, và một số câu thoại nhanh vẫn bị bỏ sót.

Kiểm tra cửa sổ của bản thử đang chạy cho thấy overlay có Windows display affinity `17` (`WDA_EXCLUDEFROMCAPTURE`). Trong cấu hình đó, hàm `BeginCapture` không ẩn overlay; nguyên nhân chớp nằm trong trạng thái dịch/nhận dạng, không phải thao tác chụp che phủ.

Luồng cutscene giờ giữ bản dịch đang hiển thị tối đa bốn lần OCR trống, hoặc hai lần OCR trả chữ khác chưa có bản dịch, thay vì xóa ngay. Câu cũ xuất hiện lại dùng cache ngay cả khi ngắn. Khi chỉ có một câu cần gửi, luồng dùng phản hồi văn bản trực tiếp của Groq, bỏ phần JSON ID vốn cần cho nhiều box. Các chế độ không phải cutscene giữ cách xử lý cũ.

Build: 0 cảnh báo, 0 lỗi. Gói thử: `artifacts/cutscene-hold-trial-20261023/StoryTranslator-1.0.0.0-x64-self-contained.msix`.

Giới hạn: thời gian Groq từng được đo 904–2.113 ms cho hai câu mẫu, nên câu thoại tồn tại ngắn hơn thời gian mạng vẫn có thể bị lỡ. Chưa đo được cải thiện tốc độ từ đường phản hồi một câu và chưa xác nhận overlay bằng bản thử mới. Việc tăng nhịp quét toàn màn hình sẽ tăng tải CPU; cần đo OCR thực tế trước khi chỉnh.
