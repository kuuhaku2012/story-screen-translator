# Đặc tả dự án — Live Story Screen Translator

## Cập nhật 2026-10-20: bản thử ngoài game

Yêu cầu hiển thị mới: bản dịch phủ ngay trên rectangle người dùng chọn với từng ô theo tọa độ cụm OCR: nền tối cục bộ điều chỉnh được, chữ Việt trắng rõ, nền ngoài ô trong suốt, tự co chữ, không lấy focus và cho chuột đi qua. Lớp phủ của app được loại khỏi capture bằng display affinity và giữ hiển thị khi Windows hỗ trợ; fallback tạm ẩn/khôi phục theo guard nếu API thất bại. Ảnh trùng dùng lại OCR; OFF luôn ẩn. Không còn cần đặt một cửa sổ kết quả tách rời. Cửa sổ công cụ tự thu xuống taskbar khi bật; mở lại giao diện sẽ tạm dừng. Model mặc định mới là `openai/gpt-oss-20b`.

Đã tích hợp Groq text-only và nối giao diện bật/tắt với capture vùng chọn → Windows English OCR → ổn định chữ → glossary → dịch tiếng Việt → overlay. Key nhập trong PasswordBox và chỉ giữ trong RAM; không lưu key bằng DPAPI ở bản thử này. Cache của phiên dịch màn hình dùng RAM; vùng chọn và glossary thuộc `E:\tool_dich_man_hinh\data`. Có bản self-contained thử riêng để không cài runtime toàn hệ thống. Gói FDD vẫn là bản phát hành chính.

Chế độ dịch chỉ chạy sau khi người dùng nhập key, chọn vùng và chủ động bật. Chưa thử request Groq bằng key thật, chưa đo hiệu năng trên manga và không mở game. Xem [SCREEN_TRIAL.md](SCREEN_TRIAL.md). Các mô tả trạng thái bên dưới ghi lại mốc MSIX ngày 2026-10-15; phần “provider để sau/Story Mode khóa” ở mốc đó đã được thay thế bằng bản thử này.

## Mục tiêu
Ứng dụng Windows dịch câu thoại đang hiển thị sang tiếng Việt khi người dùng chủ động bật Story Mode. Người dùng chọn vùng chữ, xem bản dịch trong overlay riêng và tắt chế độ khi chuyển sang combat hoặc phần chơi khác.

## Rủi ro và cổng sử dụng
[Chính sách Wuthering Waves Fair Play](https://wutheringwaves.kurogames.com/en/main/news/detail/742) nói chung về công cụ bên thứ ba trái phép dùng để tạo lợi thế không công bằng, nhưng không nêu rõ quy trình OCR desktop thủ công + overlay riêng đang thiết kế có được phép hay không. [Thỏa thuận người dùng Kuro](https://wutheringwaves.kurogames.com/p/agreement_public.html) cũng dành quyền đánh giá chương trình chạy cùng game và xử lý tài khoản/thiết bị. Vì vậy không thể kết luận workflow này được phép hoặc cam kết không bị ban. Không chạy live với client Kuro cho đến khi Kuro xác nhận bằng văn bản rằng đúng workflow này được phép. Acc phụ không chứng minh an toàn và không dùng để dò rủi ro ban. Trước xác nhận, chỉ thử ảnh/video tổng hợp hoặc ứng dụng không có anti-cheat.

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
Ứng dụng WPF bằng C#/.NET 10 trên Windows; xử lý nghiệp vụ nằm trong class library để test không cần desktop. OCR đã chọn là `Windows.Media.Ocr`, được đóng gói bằng MSIX x64 để cung cấp package identity. Gói chính framework-dependent (khoảng 6,93 MiB) và yêu cầu .NET 10 Desktop Runtime; script có lựa chọn self-contained. Gói mặc định chưa ký. Overlay là cửa sổ riêng, không vẽ/attach vào tiến trình game. Bộ dịch vẫn được trừu tượng hóa nhưng chưa chọn provider; Story Mode vẫn bị khóa, không có endpoint, API key hoặc request mạng.

**Cảnh báo mức Windows:** Manifest MSIX khai báo `MinVersion=10.0.17763.0`, và project khai báo `SupportedOSPlatformVersion=10.0.17763.0`; nhưng TFM là `net10.0-windows10.0.19041.0` (API contract mục tiêu 19041) và Windows SDK package là `10.0.26100.87`. Các mức này không đồng nghĩa. Build/MakeAppx thành công trên SDK đã cài không chứng minh chạy được trên Windows 17763. Chưa xác định lỗi API cụ thể cần đổi code; cần audit API và kiểm thử máy đích trước khi cam kết hỗ trợ mức tối thiểu 17763.

## Quyền riêng tư
Không ghi frame ảnh hoặc text thoại vào log mặc định. Chỉ log mã lỗi, trạng thái và thời gian xử lý. Không lưu thông tin đăng nhập game. Chỉ gửi dòng OCR cần dịch; cần chốt nhà cung cấp, chi phí và cách giữ API key trước khi bật dịch mạng.

## Trạng thái phiên bản hiện tại
- Đã chọn MSIX x64 framework-dependent + Windows.Media.Ocr; gói unsigned được tạo bằng MakeAppx tại `E:\tool_dich_man_hinh\artifacts\msix\StoryTranslator-1.0.0.0-x64.msix` (7,263,431 bytes, khoảng 6.93 MiB). Cần .NET 10 Desktop Runtime trên máy đích.
- `dotnet test StoryTranslator.sln`: 74/74 đạt; `dotnet build StoryTranslator.sln -c Release`: 0 cảnh báo, 0 lỗi; MakeAppx tạo gói thành công.
- Production Windows OCR đã PASS trên ảnh tổng hợp `STORY TEXT 123` qua MSIX self-contained cùng source/manifest. Bản chính framework-dependent chưa tạo kết quả smoke trên máy này; máy thiếu runtime .NET 10 nên khả năng chạy của gói chưa được xác minh. Đăng ký MSIX tạm và tiến trình đã được gỡ/dừng, xác minh không còn. Developer Mode đã bật từ trước và không đổi; một HTTPS development certificate chưa trust do .NET first-run tạo đã được gỡ theo thumbprint, không có trust change; không cài runtime.
- Provider dịch chưa chọn; không network/API key; Story Mode disabled. Không chạy game/tài khoản và không có bảo đảm không bị ban. Chi tiết nằm trong [báo cáo kiểm thử](TEST_REPORT.md).

## Tiêu chí nghiệm thu
- OFF: không capture/OCR/dịch và overlay ẩn.
- ON: chỉ đọc vùng chọn; câu mới được dịch khi đang hiện.
- Text không ổn định/trống không được dịch; một câu không gửi lặp.
- Glossary giữ nguyên tên chính xác kể cả khi OCR nhận alias.
- Tắt chế độ dừng capture và tác vụ chờ.
- Không cần admin, không chạm tiến trình/file game, không giả lập input.
- Kiểm thử trên clip/ảnh offline; không tuyên bố an toàn ban tuyệt đối.

## Mặc định ban đầu
Ngôn ngữ OCR English, đích Vietnamese. Dịch mạng chưa bật cho tới khi người dùng chọn nhà cung cấp. Story Mode vẫn OFF/disabled. Lấy mẫu ban đầu 2 khung/giây; ổn định sau 2 lần nhận diện liên tiếp. Đo thực tế trước khi cố định thông số.


