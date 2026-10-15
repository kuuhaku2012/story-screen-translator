# Đặc tả dự án — Live Story Screen Translator

## Mục tiêu
Ứng dụng Windows dịch câu thoại đang hiển thị sang tiếng Việt khi người dùng chủ động bật Story Mode. Người dùng chọn vùng chữ, xem bản dịch trong overlay riêng và tắt chế độ khi chuyển sang combat hoặc phần chơi khác.

## Rủi ro và cổng sử dụng
Chính sách công khai của Kuro không xác định rõ OCR overlay có được cho phép hay không. Không thể cam kết không bị ban khi chạy phần mềm ngoài cùng Wuthering Waves. Không chạy live với game cho đến khi Kuro xác nhận bằng văn bản rằng đúng cách hoạt động dưới đây được phép. Acc phụ không chứng minh an toàn và không dùng để dò rủi ro ban. Trước xác nhận, chỉ thử ảnh/video cục bộ hoặc ứng dụng không có anti-cheat.

## Phạm vi phiên bản đầu
- Windows desktop, chạy quyền người dùng thường.
- Người dùng chọn vùng thoại thủ công.
- Story Mode mặc định OFF, bật/tắt rõ ràng.
- Capture vùng chọn ở tần suất thấp chỉ khi bật.
- OCR cục bộ, lọc câu ổn định và lặp, dịch văn bản mới sang tiếng Việt.
- Overlay độc lập hiển thị bản dịch.
- Glossary giữ nguyên tên nhân vật, tổ chức, địa danh và thuật ngữ lore.
- Cache cục bộ để tránh gọi dịch lặp.
- Mặc định không ghi ảnh hoặc nội dung thoại vào log.

## Không thuộc phạm vi
- Đọc/ghi bộ nhớ game, dò/attach tiến trình game, DLL/API injection hoặc hooking.
- Sửa game/resource, tự động nhập liệu, click, macro, né anti-cheat, đổi tên/ẩn tiến trình.
- Tự phát hiện combat/story bằng cách đọc trạng thái game.
- Thử nghiệm live với Wuthering Waves trước xác nhận bằng văn bản.

## Luồng và hành vi
1. OFF: không chạy capture, OCR, queue dịch hoặc overlay nội dung.
2. Người dùng bật Story Mode; bộ capture đọc đúng vùng đã chọn.
3. Bộ lọc đợi text ổn định, bỏ rỗng và bản trùng; ưu tiên câu mới nhất.
4. OCR trả văn bản; glossary bảo vệ tên riêng bằng token.
5. Bộ dịch chỉ nhận văn bản và ngôn ngữ, không nhận ảnh.
6. Khôi phục tên nguyên dạng, hiển thị bản dịch và cache kết quả.
7. Khi tắt: hủy tác vụ chờ, dừng capture và ẩn overlay.

## Thiết kế kỹ thuật
Ứng dụng WPF bằng C#/.NET 10 trên Windows; xử lý nghiệp vụ nằm trong class library để test không cần desktop. Dùng Windows OCR cho nhận dạng và API desktop công khai để chụp vùng crop người dùng chọn. Overlay là cửa sổ riêng, không vẽ/attach vào tiến trình game. Bộ dịch được trừu tượng hóa; SQLite dùng cho cache. Cấu hình glossary nằm trong app data; API key dùng Windows Credential Manager.

## Quyền riêng tư
Không ghi frame ảnh hoặc text thoại vào log mặc định. Chỉ log mã lỗi, trạng thái và thời gian xử lý. Không lưu thông tin đăng nhập game. Chỉ gửi dòng OCR cần dịch; cần chốt nhà cung cấp, chi phí và cách giữ API key trước khi bật dịch mạng.

## Tiêu chí nghiệm thu
- OFF: không capture/OCR/dịch và overlay ẩn.
- ON: chỉ đọc vùng chọn; câu mới được dịch khi đang hiện.
- Text không ổn định/trống không được dịch; một câu không gửi lặp.
- Glossary giữ nguyên tên chính xác kể cả khi OCR nhận alias.
- Tắt chế độ dừng capture và tác vụ chờ.
- Không cần admin, không chạm tiến trình/file game, không giả lập input.
- Kiểm thử trên clip/ảnh offline; không tuyên bố an toàn ban tuyệt đối.

## Mặc định ban đầu
Ngôn ngữ vào English, đích Vietnamese. Dịch mạng chưa bật cho tới khi người dùng chọn nhà cung cấp. Lấy mẫu ban đầu 2 khung/giây; ổn định sau 2 lần nhận diện liên tiếp. Đo thực tế trước khi cố định thông số.

