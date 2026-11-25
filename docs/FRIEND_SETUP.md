# Hướng dẫn cho người dùng máy mới

Repo này hiện là **mã nguồn thử nghiệm**, không phải ứng dụng tải về chạy ngay. `artifacts/` (bản MSIX), `.tools/` (SDK cục bộ), `models/` và `data/` không được đưa lên GitHub. Ứng dụng còn ghi dữ liệu vào đường dẫn cố định `E:\tool_dich_man_hinh`, nên quy trình dưới đây chỉ áp dụng cho máy Windows x64 **có ổ E:** và có thể clone đúng đường dẫn đó. Nếu không có ổ E:, hãy chờ bản sửa đường dẫn và bản phát hành dành cho người dùng; đừng tự đổi ký tự ổ đĩa chỉ để chạy công cụ.

## Chuẩn bị một lần

1. Cài Git, [Windows SDK](https://learn.microsoft.com/en-us/windows/apps/windows-sdk/downloads) có `makeappx.exe`, và bật Developer Mode trong Windows Settings theo [hướng dẫn Microsoft](https://learn.microsoft.com/en-us/windows/apps/get-started/developer-mode-features-and-debugging). Đây là quy trình dựng/chạy thử ứng dụng MSIX, không cần cài một file `.msix` chưa ký bằng cách nhấp đúp.
2. Cài English OCR language pack trong Windows nếu máy chưa có. Ứng dụng hiện chỉ nhận dạng chữ tiếng Anh; Windows OCR cần gói ngôn ngữ OCR tương ứng.
3. Tạo Groq API key trong **tài khoản Groq của chính bạn**. Không gửi key cho người khác hoặc lưu trong repo. API có hạn mức và có thể phát sinh chi phí.

## Lấy mã nguồn và dựng bản chạy

Mở PowerShell. Các lệnh này giả định ổ `E:` đã tồn tại và thư mục `E:\tool_dich_man_hinh` chưa tồn tại:

```powershell
git clone https://github.com/kuuhaku2012/story-screen-translator.git E:\tool_dich_man_hinh
Set-Location E:\tool_dich_man_hinh
New-Item -ItemType Directory -Force .tools | Out-Null
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile .tools\dotnet-install.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.tools\dotnet-install.ps1 -Channel 10.0 -InstallDir 'E:\tool_dich_man_hinh\.tools\dotnet'
& .\.tools\dotnet\dotnet.exe --version
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1 -SelfContained -OutputDirectory 'E:\tool_dich_man_hinh\artifacts\screen-trial'
```

Script của Microsoft cài .NET 10 SDK vào đúng vị trí mà `package.ps1` yêu cầu. Lệnh đóng gói cần mạng để tải các gói NuGet ở lần chạy đầu. Sau khi hoàn tất, `artifacts\screen-trial\layout\AppxManifest.xml` phải tồn tại.

## Chạy ứng dụng

Mở `E:\tool_dich_man_hinh\Thu-dich-man-hinh.cmd`. Giữ cửa sổ PowerShell mà file `.cmd` mở ra cho đến khi đóng ứng dụng; script đăng ký MSIX tạm rồi tự gỡ khi ứng dụng thoát. Nhập Groq key **trong ứng dụng**, chọn vùng chữ và bật dịch. Bản thử chỉ gửi văn bản OCR đến Groq khi bạn bật dịch. Ảnh chẩn đoán chỉ được lưu khi bạn chủ động bấm nút xuất.

Các file `.cmd` khác là công cụ thử nghiệm, không phải cách chạy thông thường:

- `Thu-bo-cuc-the-moi.cmd` và `Thu-chan-doan-OCR.cmd` đòi hỏi layout thử riêng không có trong repo.
- `Thu-danh-gia-60-cau.cmd` và `Thu-glossary-ngu-canh.cmd` đòi hỏi dữ liệu/model đánh giá không có trong repo.

Nếu gặp lỗi thiếu `dotnet.exe`, kiểm tra bước cài SDK; nếu thiếu `makeappx.exe`, kiểm tra Windows SDK; nếu thiếu `AppxManifest.xml`, bước đóng gói chưa hoàn thành. Nếu gặp lỗi gói đã đăng ký, đóng Story Translator đang chạy trước khi mở lại. HTTP 404/429 là vấn đề model/quyền truy cập/hạn mức Groq, không khắc phục bằng cách chạy lại file `.cmd` liên tục.

**Lưu ý khi chơi game:** chưa có xác nhận từ nhà phát hành rằng cách dùng công cụ này cùng Wuthering Waves được phép. Không thể cam kết tài khoản sẽ không bị khóa. Hãy thử trên manga, trang web hoặc video ngoài game; đọc [đặc tả rủi ro](PROJECT_SPEC.md) trước khi cân nhắc dùng trong game. Bản thử vẫn có thể bỏ sót phụ đề xuất hiện nhanh.
