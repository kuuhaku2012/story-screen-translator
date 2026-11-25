# Cutscene follow-up — AvS_afduMds — 2026-10-23

Video do người dùng đưa: https://youtu.be/AvS_afduMds. Các mốc người dùng báo: 1:39, 1:46, 2:13, 2:19, 2:36, 3:01, 3:19, 3:26, 4:00. Lần này chỉ lấy mẫu khung 1:40–1:46, chưa xác nhận từng mốc còn lại.

Mười khung hình fullscreen 1920×1080 khi video chạy bình thường được lưu tại `artifacts/cutscene-AvS-20261023/frames`. OCR offline PP-OCRv6 medium/DirectML (khác Windows OCR của ứng dụng) đọc câu `Or are you afraid of reliving that day?` ở bốn khung đầu, khung thứ năm trống, rồi đọc `Returning... all by your lonesome?` ở bốn khung tiếp. Khung cuối đọc lệch hai ký tự thành `Returning.. al by your lonesome?`. Thời gian OCR đã làm nóng: 158–388 ms/khung. Đây chỉ là phép đo OCR trên ảnh đã lưu, không phải độ trễ overlay từ đầu đến cuối.

Đã sửa `SpatialTranslationPipeline` trong chế độ cutscene: nếu hai lần OCR cùng vị trí chỉ khác tối đa hai ký tự trên câu dài từ 16 ký tự, giữ cùng trạng thái, bản dịch và cache thay vì dịch lại. Kiểm tra hồi quy đúng biến thể OCR quan sát trên video: 1/1 đạt. Gói tự chứa mới: `artifacts/cutscene-ocr-stability-trial-20261023/StoryTranslator-1.0.0.0-x64-self-contained.msix` (71.468.114 byte), đóng gói thành công; đã mở bản thử.

Người dùng đo bản trước nhanh hơn một chút nhưng chưa đáng kể. Chưa có đo thời gian đầu-cuối trên bản sửa mới. Tần suất lấy ảnh vẫn 500 ms ở tối đa 1080p và 1 giây với vùng lớn hơn. Thời gian gọi Groq vẫn có thể vượt thời lượng một câu ngắn; bản sửa này chủ yếu giảm việc mất rồi hiện lại khi OCR nhiễu, chưa giải quyết triệt để câu thoại quá nhanh. Đang chờ người dùng kiểm tra bản vừa mở ở các mốc đã đưa.
