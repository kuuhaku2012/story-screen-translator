# Hướng dẫn thử dịch màn hình

Đã có bản thử tiếng Anh → tiếng Việt qua Groq. Xem hướng dẫn từng bước ở [SCREEN_TRIAL.md](SCREEN_TRIAL.md). Khi bật dịch, giao diện tự thu xuống taskbar trước khi chụp; mở lại giao diện sẽ tạm dừng và ẩn bản dịch. Không còn cần tự đặt tool ngoài vùng chọn.

Mở `E:\tool_dich_man_hinh\Thu-dich-man-hinh.cmd`, nhập Groq key vào ô che ký tự, chọn vùng chữ nhỏ trên manga/trang web rồi bật dịch. Ứng dụng tự đọc vùng đó và cập nhật bản dịch khi chữ mới ổn định; không phải bấm chụp mỗi lần. Khi bật dịch, tool tự thu xuống taskbar; mở lại sẽ tạm dừng. Bản dịch được đặt riêng theo từng cụm chữ gốc: chữ trắng rõ trên ô nền tối cục bộ, phần còn lại trong suốt. Có thanh chỉnh độ đậm nền và mục đọc bản dịch đầy đủ nếu ô quá nhỏ; lớp dịch giữ hiện liên tục nếu Windows hỗ trợ loại cửa sổ khỏi ảnh chụp. Ảnh không đổi sẽ dùng lại OCR. Nếu API Windows thất bại, fallback tạm ẩn khi quét có thể còn nháy. Model mặc định là `openai/gpt-oss-20b`, chưa xác nhận được request thật bằng key người dùng.

Key và cache dịch của phiên màn hình chỉ giữ trong RAM, không lưu xuống đĩa. Chỉ văn bản OCR được gửi tới Groq khi bật, có thể tính phí/hạn mức. Ảnh không gửi lên Groq. Vùng chọn được lưu dưới `E:\tool_dich_man_hinh\data`. Tắt checkbox để dừng capture/OCR, hủy yêu cầu đang chờ và ẩn bản dịch; đóng app để launcher gỡ đăng ký MSIX tạm.

Bản chạy thử dùng runtime tự chứa để không cài .NET toàn hệ thống. Gói FDD nhỏ vẫn là bản chính nhưng cần .NET 10 Desktop Runtime và quy trình cài/ký phù hợp. Launcher không thay đổi Developer Mode hoặc cài/trust certificate.

Đã có 78 kiểm thử offline đạt; API thật chưa được thử bằng key người dùng. Chưa đo CPU/RAM, độ trễ, chất lượng OCR manga hoặc khả năng chạy trên Windows tối thiểu. Hãy thử chữ rõ và một ô thoại trước; nhiều ô thoại có thể bị OCR xếp sai thứ tự. Glossary bảo vệ tên đã khai báo; giữ tên mới chỉ qua prompt thì không có bảo đảm tuyệt đối.

Chưa chạy Wuthering Waves và không có kết quả nào chứng minh an toàn anti-cheat. Chỉ thử ngoài game ở giai đoạn này. Xem [báo cáo kiểm thử](TEST_REPORT.md) để phân biệt bằng chứng hiện tại với các kết quả lịch sử.
