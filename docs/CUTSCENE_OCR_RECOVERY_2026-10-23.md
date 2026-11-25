# Cutscene OCR recovery — 2026-10-23

Người dùng thử video https://youtu.be/AvS_afduMds ở chế độ YouTube toàn màn hình. Bản `cutscene-ocr-stability-trial` giữ tốt câu 1:39–1:46 và có cải thiện tốc độ cảm nhận. Các mốc 2:13 vẫn không hiện dịch; 3:19 đôi khi còn tiếng Anh; 3:26 có thể không hiện; 4:00 khá tốt. Người dùng bổ sung 1:14 là nơi overlay vẫn mất.

Đã lưu khung fullscreen tại `artifacts/cutscene-AvS-20261023/fullscreen-followup`. Chạy chính `WindowsOcrEngine` của ứng dụng trên khung 2:13: chế độ chuẩn trả **0 block**; chế độ upscale/comic đọc `How they must've hated you...` trong khoảng 192 ms (trung bình 3 lượt). Trên ba khung 3:24–3:29, upscale cũng đọc câu trên và câu dưới chính xác hơn; kết quả tại `artifacts/multimodal-evaluation/cutscene-AvS-20261023-fullscreen/windows-ocr-followup.json`. Kết quả trên ảnh tĩnh không chứng minh toàn bộ luồng live.

Đã áp dụng: chế độ cutscene dùng OCR upscale; câu dài đang chờ Groq được giữ qua tối đa bốn lần OCR trống để bản dịch đến sau vẫn có thể hiện. Ngoài ra giữ sửa trước đó về sai số OCR tối đa hai ký tự trên cùng vị trí. Mục đích là khắc phục hai nguyên nhân cụ thể của mốc 2:13: OCR hụt và kết quả đến sau khi OCR đã trống. Chế độ manga và thường không thay đổi đường OCR.

Kiểm tra lõi: 131/131 đạt; hai ca hồi quy cutscene liên quan trực tiếp đạt. Đóng gói MSIX tự chứa thành công, 71.468.230 byte: `artifacts/cutscene-ocr-recovery-trial-20261023/StoryTranslator-1.0.0.0-x64-self-contained.msix`. Chưa xác nhận kết quả live của bản này; đang chờ người dùng đóng bản cũ để mở bản mới. Chi phí OCR khi bật cutscene tăng khoảng 100–150 ms ở các ảnh đo và có thể tốn CPU hơn. Hạn mức/độ trễ Groq và các câu quá ngắn vẫn là giới hạn.
