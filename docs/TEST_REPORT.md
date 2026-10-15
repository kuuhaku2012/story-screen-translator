# Báo cáo kiểm thử offline (bản nháp)

Ngày ghi nhận: 2026-10-15

## Phạm vi

Các bài kiểm thử chạy bằng frame tổng hợp, OCR giả xác định, translator giả cục bộ, glossary ví dụ, bộ lọc ổn định câu, coordinator/cache và presentation gate. Không mở desktop, không chụp màn hình thật, không gửi yêu cầu mạng và không khởi chạy Wuthering Waves.

## Kết quả

- `OfflinePipelineTests`: 3/3 đạt.
- `StoryTranslationPipelineTests`: 3/3 đạt; kịch bản `A,A,B,B,A,A` chỉ gọi translator cho hai câu khác nhau và câu A trở lại được lấy từ cache; cùng câu được ổn định lại sau OFF/ON.
- `StaleTranslationPresentationTests`: 4/4 đạt với coordinator giả cố ý trả kết quả muộn; A chậm không ghi đè B nhanh, candidate B làm vô hiệu A ngay ở mẫu đầu, A hoàn tất sau OFF/ON không xuất hiện trong phiên mới, và callback cũ đã xếp hàng bị chặn khi flush sau OFF/ON.
- `TextStabilityFilterTests`: có kiểm tra candidate normalized thay đổi riêng với lúc ổn định, cùng reset candidate/dedupe ở ranh giới session.
- Token glossary ổn định giữa hai lần bảo vệ cùng một câu; tên vẫn được khôi phục đúng.
- Pipeline Core sản phẩm dùng chung trong integration test, tuần tự hóa cập nhật filter mà không giữ khóa trong lúc dịch; coordinator vẫn có thể hủy dòng cũ khi dòng mới tới.
- Deferred-overlay fake lưu callback rồi gọi guard khi test chủ động flush hàng đợi; các test xác minh guard loại callback cũ sau OFF/ON. Đây là mô phỏng ranh giới callback, không phải xác minh tích hợp hoặc hình ảnh thực tế của WPF Dispatcher.
- Dòng trả về `true` từ lời gọi hiển thị nghĩa là overlay nhận yêu cầu hoặc đã xếp nó lên dispatcher; guard có thể loại bỏ yêu cầu cũ trước khi UI cập nhật. Đây không phải xác nhận rằng pixel đã được vẽ lên màn hình.
- Khi candidate OCR chuẩn hóa thay đổi (ngay từ quan sát đầu tiên của câu mới), generation dòng tăng để vô hiệu hóa kết quả câu cũ. OFF/ON reset candidate và last-emitted, đồng thời tăng generation session; cùng một câu còn trên màn hình có thể ổn định lại và dùng cache.
- Pipeline kiểm tra generation trước khi gửi dịch, khi nhận kết quả và trong callback đã xếp hàng; fake trì hoãn callback để kiểm tra hành vi này. WPF Dispatcher thật chưa được kiểm thử hiển thị.
- Luồng frame giả → OCR giả → đợi câu ổn định → bảo vệ tên riêng → dịch giả → khôi phục tên → overlay gate được kiểm tra.
- Tên `Rover` và `Academy` được giữ nguyên trong kết quả giả.
- Câu lặp không làm gọi translator lần hai; cache trả kết quả đã có.
- OFF ẩn overlay và chặn lần hiển thị sau đó.
- Lỗi translator được truyền về caller và không mở overlay.
- Worker giải phóng frame giả trong luồng thành công.

Các kết quả trên kiểm chứng logic ứng dụng với thành phần giả. Chúng không kiểm chứng độ chính xác OCR thật, chất lượng dịch, tốc độ, tính tương thích của capture trên máy khác hay trạng thái anti-cheat.

## Build và test

Generation/session validation fix: commit `c68228a0fdb0573b9a2a42f6e9e07da5f18a4b4c`.
Sửa vô hiệu hóa ngay khi candidate đổi, reset bộ lọc theo session và guard callback trì hoãn: commit `de99c4f4b05c22b08aa32857dc53f222eb5a900c`.

Đã chạy sau khi thêm các bài kiểm thử offline, bằng SDK local .NET 10:

```powershell
dotnet test StoryTranslator.sln
dotnet build StoryTranslator.sln -c Release
```

- `dotnet test StoryTranslator.sln`: PASS, 65 passed, 0 failed, 0 skipped (fresh exact solution run).
- `dotnet build StoryTranslator.sln -c Release`: PASS, 0 warning, 0 error.

## RED/GREEN evidence

- Removing first-candidate generation invalidation made the adversarial “A completes after B's first OCR sample” test fail because stale A was accepted.
- Removing stability reset made the OFF/ON repeat-line test fail because A did not stabilize again.
- Removing the deferred-overlay callback guard made the queued OFF/ON callback test fail because stale A appeared after queue flush.
- Restoring all three behaviors made these regressions pass. The deferred callback is a fake boundary test, not a real WPF Dispatcher integration test.

## Chưa đo / chưa xác minh

- CPU và RAM khi OFF hoặc ON: chưa đo.
- Thời gian OCR đến overlay: chưa đo.
- Số request dịch tới nhà cung cấp thật: chưa có provider; chỉ đếm lời gọi fake trong test.
- OCR Windows: app hiện chưa có MSIX package identity. `Windows.Media.Ocr` yêu cầu identity, nên OCR thật chưa chạy trong cấu hình desktop hiện tại. Phương án đóng gói MSIX hoặc OCR thay thế chưa được chọn.
- Dịch thật: chưa tích hợp provider; không có API key hoặc request mạng.
- Giao diện/overlay khi dùng thật, nhận dạng câu từ hình game và kiểm thử đa màn hình: chưa xác minh thủ công.
- Wuthering Waves: chưa chạy. Không dùng với client Kuro cho tới khi Kuro xác nhận bằng văn bản rằng chính xác quy trình OCR vùng chọn thủ công + overlay ngoài tiến trình này được phép. Acc phụ không phải phép thử an toàn.
- Task 10 mới hoàn thành phần kiểm thử offline và tài liệu nháp. Script/gói portable hoặc MSIX chưa làm; chờ lựa chọn phương án phân phối OCR và provider dịch rồi mới hoàn tất đóng gói.

## Cổng an toàn

Không có kết quả offline nào chứng minh “không bị ban”. Nếu không có xác nhận bằng văn bản cho đúng workflow, không chạy đồng thời ứng dụng và client game Kuro.
