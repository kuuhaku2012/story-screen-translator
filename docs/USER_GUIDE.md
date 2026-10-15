# Hướng dẫn người dùng (bản nháp)

## Trạng thái hiện tại

Đây là bản thử nghiệm kỹ thuật, chưa sẵn sàng dùng với game. Story Mode khởi đầu OFF. Trong UI hiện tại, chức năng bật chế độ bị khóa cho tới khi runtime OCR và nguồn dịch được cấu hình. Chưa chọn nhà cung cấp dịch hoặc mô hình phân phối OCR.

Không chạy ứng dụng cùng Wuthering Waves. Chỉ xem xét sử dụng với client Kuro sau khi có xác nhận bằng văn bản của Kuro cho đúng cách hoạt động: người dùng tự chọn vùng chữ, chụp vùng desktop ở tần suất thấp, OCR cục bộ, gửi riêng dòng văn bản OCR cho bộ dịch đã chọn và hiển thị kết quả trong overlay ngoài tiến trình game. Không có cam kết rủi ro ban bằng không; dùng acc phụ không chứng minh an toàn.

## Các bước dự kiến sau khi hoàn thiện

1. Chạy ứng dụng với quyền người dùng thường; không cần quyền quản trị.
2. Chọn vùng thoại trên màn hình thủ công.
3. Bật Story Mode khi muốn dịch thoại; tắt trước khi chuyển sang combat hoặc nội dung khác.
4. Đọc bản dịch trong cửa sổ overlay riêng. Tắt Story Mode để dừng capture, hủy công việc đang chờ và ẩn overlay.

Các bước này mô tả thiết kế dự kiến; hiện không phải quy trình đã kiểm thử end-to-end trên desktop thật.

## Giới hạn đã biết

- Windows OCR đang dùng `Windows.Media.Ocr`, API yêu cầu MSIX package identity cho ứng dụng desktop. Bản hiện tại chưa có identity MSIX nên OCR thật chưa sẵn sàng.
- Chưa chọn giữa đóng gói MSIX và một OCR thay thế có thể chạy portable.
- Chưa chọn nhà cung cấp dịch, chi phí, quyền riêng tư, khu vực xử lý dữ liệu hoặc cách cấp API key. Vì vậy không có dịch mạng.
- Kiểm thử hiện có dùng frame, OCR và translator giả; không phản ánh độ chính xác hay hiệu năng thật.
- Chưa đo CPU/RAM, độ trễ OCR đến overlay hoặc request tới provider thật.
- Không có tự nhận biết combat/story. Người dùng phải tự bật/tắt.
- Không đọc/ghi bộ nhớ, tiến trình hay file game; không hook/inject; không gửi input, click hoặc macro.

## Dữ liệu và quyền riêng tư

Thiết kế hiện tại xử lý ảnh trong bộ nhớ và không ghi frame/thoại vào log theo mặc định. Khi tích hợp provider, chỉ gửi văn bản OCR cùng mã ngôn ngữ; cần chốt rõ provider và chính sách dữ liệu trước khi bật. Không nhập thông tin đăng nhập game vào ứng dụng.

## Gỡ bỏ

Chưa phát hành gói cài đặt để gỡ. Khi đóng ứng dụng, các file dữ liệu thử nghiệm nằm trong thư mục dữ liệu ứng dụng ở LocalApplicationData (cache dịch, settings và glossary). Có thể xóa thư mục `StoryTranslator` sau khi thoát ứng dụng nếu muốn xóa cấu hình/cache cục bộ. Bản portable/MSIX và quy trình gỡ tương ứng sẽ được xác định sau khi chọn mô hình phân phối.
