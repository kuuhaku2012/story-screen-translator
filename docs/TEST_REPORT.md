# Báo cáo kiểm thử offline (bản nháp)

Ngày ghi nhận: 2026-10-15

## Phạm vi

Các bài kiểm thử chạy bằng frame tổng hợp, OCR giả xác định, translator giả cục bộ, glossary ví dụ, bộ lọc ổn định câu, coordinator/cache và presentation gate. Không mở desktop, không chụp màn hình thật, không gửi yêu cầu mạng và không khởi chạy Wuthering Waves.

## Kết quả

- `OfflinePipelineTests`: 3/3 đạt.
- Luồng frame giả → OCR giả → đợi câu ổn định → bảo vệ tên riêng → dịch giả → khôi phục tên → overlay gate được kiểm tra.
- Tên `Rover` và `Academy` được giữ nguyên trong kết quả giả.
- Câu lặp không làm gọi translator lần hai; cache trả kết quả đã có.
- OFF ẩn overlay và chặn lần hiển thị sau đó.
- Lỗi translator được truyền về caller và không mở overlay.
- Worker giải phóng frame giả trong luồng thành công.

Các kết quả trên kiểm chứng logic ứng dụng với thành phần giả. Chúng không kiểm chứng độ chính xác OCR thật, chất lượng dịch, tốc độ, tính tương thích của capture trên máy khác hay trạng thái anti-cheat.

## Build và test

Đã chạy sau khi thêm các bài kiểm thử offline, bằng SDK local .NET 10:

```powershell
dotnet test StoryTranslator.sln
dotnet build StoryTranslator.sln -c Release
```

- `dotnet test StoryTranslator.sln`: PASS, 54 passed, 0 failed, 0 skipped.
- `dotnet build StoryTranslator.sln -c Release`: PASS, 0 warning, 0 error.

## Chưa đo / chưa xác minh

- CPU và RAM khi OFF hoặc ON: chưa đo.
- Thời gian OCR đến overlay: chưa đo.
- Số request dịch tới nhà cung cấp thật: chưa có provider; chỉ đếm lời gọi fake trong test.
- OCR Windows: app hiện chưa có MSIX package identity. `Windows.Media.Ocr` yêu cầu identity, nên OCR thật chưa chạy trong cấu hình desktop hiện tại. Phương án đóng gói MSIX hoặc OCR thay thế chưa được chọn.
- Dịch thật: chưa tích hợp provider; không có API key hoặc request mạng.
- Giao diện/overlay khi dùng thật, nhận dạng câu từ hình game và kiểm thử đa màn hình: chưa xác minh thủ công.
- Wuthering Waves: chưa chạy. Không dùng với client Kuro cho tới khi Kuro xác nhận bằng văn bản rằng chính xác quy trình OCR vùng chọn thủ công + overlay ngoài tiến trình này được phép. Acc phụ không phải phép thử an toàn.

## Cổng an toàn

Không có kết quả offline nào chứng minh “không bị ban”. Nếu không có xác nhận bằng văn bản cho đúng workflow, không chạy đồng thời ứng dụng và client game Kuro.
