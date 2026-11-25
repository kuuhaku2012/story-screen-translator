# Manga trang 172: lỗi chất lượng và thử nghiệm Vision

## Bằng chứng từ người dùng

Người dùng cung cấp ảnh gốc 663 x 927 và ảnh chụp kết quả app. Bản dịch chưa đạt: nhiều cụm còn là chữ Anh/rác, các câu bị tách thành nhiều card, một số cụm cạnh nhau bị trộn. Bản hybrid trước đó sửa nhịp gửi và HTTP 429; chưa thay engine Windows OCR hoặc phân vùng bong bóng.

Kiểm tra source xác nhận `WindowsOcrEngine` vẫn dùng Windows English OCR, comic mode chỉ phóng ảnh 2x và thêm cách ghép dòng căn giữa. `SpatialTextLayout` chỉ dựa trên khoảng cách, chồng lấp và chiều cao dòng; không có ranh giới panel/bong bóng. `TranslationOverlay` co font tối thiểu rồi cắt bằng ellipsis nếu ô không đủ chỗ. Ảnh kết quả không đủ để xác định mọi chuỗi rác xuất hiện ở đầu ra OCR, provider hay hiển thị; chưa có dữ liệu Groq thật cho lượt này.

## Đo lại trên đúng ảnh gốc được cung cấp

Dùng RapidOCR PP-OCRv5 mobile EN đã có sẵn, ONNX CPU hai luồng; không tải model hoặc gửi mạng. Thử detector side 960/1440 và ảnh 1x/2x; mỗi cấu hình hai lượt. Lượt thứ hai lần lượt 1.615, 1.573, 1.712 và 1.868 ms mỗi trang. Đây là các lượt đơn, không phải benchmark realtime.

Đối chiếu 16 vùng chép tay, chữ chuẩn hóa về A-Z/0-9; bỏ khoảng trắng và dấu câu. Cấu hình full-page 1440/2x có lỗi ký tự 11,49%. Đọc riêng 16 crop biết trước, phóng 2x và thêm viền, vẫn lỗi 11,23%. Tổng thời gian các lời gọi OCR crop là 2.184 ms, gồm lượt OCR crop đầu nhưng chưa tính chuẩn bị crop, phát hiện vùng tự động, dịch hay hiển thị. Phép thử crop không phải tính năng phân vùng tự động.

Ví dụ crop vẫn làm mất TIME I và WASTED trong câu BY THE TIME I SHOW UP, THEY'RE ALREADY WASTED; câu AND LAUGH LIKE IDIOTS OVER NOTHING mất IDIOTS và đọc sai NOTHING. Crop NAZUNA! YOU LOOK LIKE YOU NEED A DRINK được đọc đúng. Vì vậy thay engine hoặc cắt nhỏ đơn thuần chưa đủ bằng chứng cải thiện tổng thể.

Tệp đo: `artifacts/manga-regression-172/rapid-comparison.json`, `region-comparison.json`. Ảnh đầu vào là `input.png`. Script: `scripts/evaluate-manga-regression.py`, `scripts/score-manga-regression.py`.

## Phạm vi Vision đã được cho phép

Người dùng đồng ý thử Vision với riêng ảnh manga vừa gửi; chưa đồng ý gửi ảnh màn hình liên tục. Công cụ `scripts/test-manga-vision.ps1` chỉ đọc fixture cố định, nhận key qua SecureString tại console, kiểm tra model qua endpoint models, rồi gửi một image request. Không lấy key từ app hoặc lưu key. Model mặc định dựa trên tài liệu chính thức hiện đọc được: `qwen/qwen3.8-27b`; nếu không có trong models list của key thì dừng trước khi gửi ảnh. Không tự retry.

Tài liệu: https://console.groq.com/docs/vision . Tài liệu chứng minh API nhận ảnh/JSON, không chứng minh chất lượng đọc manga hoặc độ trễ của ảnh này.

Đầu ra sau lượt thật: `vision-response.json` chứa content thành công và thời gian; `vision-result.json` chứa vùng/câu/dịch; `vision-metrics.json`; `vision-preview.html` hiển thị ảnh gốc và overlay cạnh nhau, kèm bảng câu đầy đủ. Khung cam đánh dấu model không chắc hoặc card bị tràn; tọa độ do Vision tạo vẫn phải đối chiếu thủ công.

Đã chạy `-ValidateOnly` bằng Windows PowerShell: PASS. Sau khi người dùng nhập key trực tiếp, một image request thực đã hoàn tất: `qwen/qwen3.8-27b`, 4.153 ms từ trước gửi HTTP tới sau nhận response, finish_reason=stop. Không có lần gửi lại.

## Kết quả Vision thực trên trang này

- Đọc chữ cải thiện mạnh: sau ghép đối chiếu thủ công, 13/14 transcript đúng toàn bộ A-Z/0-9; lỗi 1/383 ký tự (0,261%), thiếu A trong A COUPLE. Con số này bỏ dấu câu/khoảng trắng, là chấm transcript, không phải chấm dịch hoặc khả năng phân vùng tự động. Hai transcript gộp hai vùng đối chiếu, nên 14 transcript bao phủ 16 vùng chép tay.
- Dịch vẫn có lỗi nghĩa rõ: WAITER bị dịch thành Đầu bếp thay vì phục vụ. Nhiều câu còn lại đã có nghĩa và đầy đủ hơn đầu ra OCR trước; chưa có chấm độc lập để kết luận đạt chất lượng toàn bộ trang.
- Vị trí không đạt: 8/14 box vượt giới hạn nếu đọc theo x,y,width,height đã yêu cầu. Thử chẩn đoán diễn giải x1,y1,x2,y2 cũng không sửa được vị trí: không box nào đạt IoU >=0,5 với vùng chép tay, IoU trung bình 0,1208. Không dùng phép diễn giải lại này làm tọa độ production.
- Bộ kiểm tra trong script từ chối box sai, vì vậy không tạo preview tự động ở lượt chạy ban đầu. Phản hồi thành công đã được lưu trước kiểm tra; `scripts/review-manga-vision.py` tạo `vision-assessment.json`, `vision-result.json` và `vision-preview.html` hoàn toàn cục bộ từ phản hồi đó, không thêm request. Trang preview giữ nguyên dịch và đánh dấu rõ box chỉ là chẩn đoán. `vision-metrics.json` không được tạo ở script ban đầu; thời gian thật nằm trong `vision-response.json` và assessment.
- 4,153 giây mới là thời gian HTTP/model, chưa tính chờ ổn định ảnh, capture, OCR hay render. Chưa đáp ứng mục tiêu cutscene 1–1,5 giây; không suy kết quả một ảnh sang khả năng theo kịp video hoặc lướt nhanh.

Kết luận kỹ thuật: Vision full-page có ích cho đọc chữ khó nhưng không đủ làm cả đọc/dịch/định vị. Hướng thử tiếp là xác định vùng/tọa độ ở máy, gán ID ổn định rồi yêu cầu model trả dịch theo ID, giảm đầu ra lặp nguồn/tọa độ; cần đo lại, chưa khẳng định sẽ nhanh hoặc đúng hơn. Không tích hợp tự gửi ảnh màn hình từ phép thử một ảnh này.

## Tiêu chí trước khi phát hành bản sửa chất lượng

Trên 16 vùng của trang này: kiểm tra đủ câu, không trộn hai cụm, giữ Nazuna, tiếng Việt đúng ý và không dùng chuỗi OCR rác như bản dịch. Đọc được toàn câu trong card hoặc bản xem đầy đủ; không dùng test hạ tầng thay cho chấm trang thực. Thời gian API riêng phải phân biệt với thời gian từ màn hình đến overlay. Chỉ tích hợp manga Vision sau khi đo được chất lượng thực; cutscene cần đánh giá luồng ROI phụ đề riêng thay vì suy từ một trang ảnh tĩnh.
