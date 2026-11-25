# Story Translator

Bản thử Windows dịch chữ tiếng Anh trên màn hình sang tiếng Việt và đặt bản dịch lên vị trí chữ gốc. Dùng cho nội dung truyện, trang web và video cutscene khi người dùng chủ động chọn vùng rồi bật dịch. Mã nguồn C#/.NET 10; OCR chạy cục bộ qua Windows Media OCR, phần dịch dùng Groq API.

**Trạng thái:** bản thử nghiệm, chưa phải bản phát hành ổn định. Trường hợp phụ đề ngắn ở mốc 3:17 của video đối chiếu vẫn có thể bị bỏ lỡ. Đọc [báo cáo gần nhất](docs/CUTSCENE_DIALOGUE_PRIORITY_2026-10-23.md) trước khi đánh giá chất lượng.

## Cách hoạt động

- Chỉ chụp vùng màn hình đã chọn khi bật dịch; OCR xử lý trên máy. Ứng dụng thông thường chỉ gửi **văn bản OCR** đến Groq, không gửi ảnh. Một số script đánh giá riêng có thể gửi ảnh và cần được xem kỹ trước khi chạy.
- API key được nhập trong ứng dụng và giữ trong RAM của phiên. Dữ liệu cá nhân và ảnh chẩn đoán chỉ lưu tại máy khi người dùng chủ động xuất; các thư mục này không nằm trong Git.
- Lớp phủ hiển thị bản dịch theo vị trí cụm chữ. Có chế độ manga và cutscene; chất lượng phụ thuộc OCR, bố cục, thời gian tồn tại của câu thoại và độ trễ API.
- Không đọc bộ nhớ, sửa tệp, hook hay inject vào game. **Không có bảo đảm tránh bị khóa tài khoản**; chưa có xác nhận của nhà phát hành game cho cách dùng này. Xem [phạm vi và rủi ro](docs/PROJECT_SPEC.md).

## Cấu trúc

- `src/StoryTranslator.App`: ứng dụng WPF, chụp màn hình, OCR và overlay.
- `src/StoryTranslator.Core`: xử lý câu thoại, glossary, cache và dịch.
- `tests/`: kiểm thử cục bộ.
- `scripts/`: đóng gói, chạy bản thử và các công cụ đánh giá.
- `docs/`: ghi chép thiết kế, thử nghiệm và các giới hạn đã biết.

## Tạo và chạy bản thử trên Windows

Cần Windows x64, .NET 10 SDK đặt tại `.tools/dotnet`, Windows SDK có `makeappx.exe`, và Developer Mode để đăng ký gói MSIX tạm. Bản mã hiện dùng `E:\tool_dich_man_hinh` làm thư mục dữ liệu và chẩn đoán; nếu clone ở vị trí khác, cần chỉnh các đường dẫn này trước khi chạy. Không đưa API key vào script hoặc Git.

Trong PowerShell ở thư mục dự án:

```powershell
.\scripts\package.ps1 -SelfContained -OutputDirectory 'E:\tool_dich_man_hinh\artifacts\screen-trial'
.\scripts\run-screen-trial.ps1
```

Giữ PowerShell mở khi dùng. Đóng ứng dụng để script tự gỡ đăng ký MSIX tạm. Nhập Groq API key trong ứng dụng, chọn vùng chữ, chọn chế độ nếu cần rồi bật dịch. Xem [hướng dẫn sử dụng](docs/USER_GUIDE.md). Các bản `.msix`, model, SDK cục bộ, dữ liệu phiên và ảnh chẩn đoán không được đẩy lên GitHub.
