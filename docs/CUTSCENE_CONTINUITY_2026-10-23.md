# Cutscene continuity follow-up — 2026-10-23

Người dùng xác nhận bản OCR recovery đã dịch được ở 2:13 và 3:26, 3:19 dịch tốt, 4:00 tốt. Ở 1:14 overlay vẫn ngắt ngắn khi đổi cảnh; `This is my cause` không kịp dịch. 3:26 còn ngắt nhẹ.

Phân tích 11 khung fullscreen đã lưu tại `artifacts/cutscene-AvS-20261023/followup-frames` bằng chính Windows OCR của ứng dụng, kết quả tại `artifacts/multimodal-evaluation/cutscene-AvS-114-sequence/windows-ocr-sequence.json`: cùng câu `A thousand times over, and I'd still see it through to the end` bị nhận thành câu nguyên, hai mảnh cùng dòng, hoặc biến thể sai vài ký tự trong các khung 01–06; khung 07 OCR trống; khung 08–09 là `This iS my cause` rồi `is is my cause`; khung 10 trống. Vì trước đây pipeline chỉ chấp nhận sai tối đa hai ký tự và chờ hai lần quét cho câu ngắn, trạng thái bị thay và bản dịch đến chậm.

Đã sửa trong chế độ cutscene: ghép các mảnh OCR gần nhau trên cùng dòng; giữ trạng thái nếu câu dài cùng vị trí chỉ khác tối đa 10% ký tự (tối đa sáu ký tự); gửi câu có ít nhất 10 chữ Latin và ba từ đi dịch ngay ở lần đọc đầu; giữ bản dịch trước tối đa bốn lần quét khi đang chờ câu mới. Các thay đổi này chỉ ảnh hưởng chế độ cutscene. Khi câu mới thực sự khác, bản dịch cũ có thể còn hiện tối đa khoảng hai giây nếu Groq chậm; đây là giới hạn của cách giữ liên tục, cần kiểm tra trực tiếp về cảm nhận.

Kiểm tra hồi quy mô phỏng chuỗi 1:14 đạt; toàn bộ kiểm tra lõi 135/135 đạt. Gói self-contained đã đóng thành công: `artifacts/cutscene-continuity-trial-20261023/StoryTranslator-1.0.0.0-x64-self-contained.msix` (71.468.739 byte). Chưa kiểm tra live với bản mới. Bản trial trước đang mở; cần đóng để đăng ký bản này.
