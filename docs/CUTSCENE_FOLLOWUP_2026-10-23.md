# Cutscene: giữ bản dịch qua cảnh đổi — 2026-10-23

Phản hồi bản thử đầu: đôi khi phụ đề hiện và dịch được nhưng overlay biến mất khi hoạt ảnh đổi cảnh dù câu thoại vẫn giữ nguyên; các câu chuyển quá nhanh đôi khi không kịp dịch.

Nguyên nhân xác định trong mã: `SpatialTranslationPipeline.ObserveAsync` thay toàn bộ trạng thái bằng các box OCR của khung mới. Khi OCR không trả box, `PublishLocked` lập tức gửi danh sách rỗng và overlay ẩn. Khi câu cũ được OCR ở vị trí khác với độ giao thấp, pipeline coi đó là câu mới và lại chờ hai mẫu. Với câu mới, chờ hai mẫu trước khi gọi Groq thêm khoảng một chu kỳ chụp.

Bản thử này chỉ trong **chế độ cutscene**: giữ bản dịch hiện có qua tối đa hai khung không có box; nối lại trạng thái nếu OCR nhận đúng cùng câu ở box khác; câu có ít nhất 20 ký tự Latin bắt đầu dịch từ mẫu OCR đầu, câu ngắn vẫn cần hai mẫu. Bộ chọn vùng không nhận dòng chỉ có chữ Hán là phụ đề tiếng Anh. Chế độ truyện giữ hành vi cũ.

Build ứng dụng thành công, 0 cảnh báo và 0 lỗi. Gói thử mới: `artifacts/cutscene-fix-trial-20261023/StoryTranslator-1.0.0.0-x64-self-contained.msix`. Đang chờ quan sát lại trên video: chưa có số đo end-to-end, và Groq vẫn có thể mất 1–2 giây nên phụ đề quá ngắn có thể biến mất trước khi dịch xong. Khi đóng ứng dụng, script trial tự gỡ đăng ký.
