# Thử riêng dịch văn bản với transcript Vision đã lưu

## Phạm vi

Người dùng yêu cầu thực hiện bước tiếp theo sau lượt ảnh ghép ID. Dùng 14 transcript nguyên văn trong `artifacts/manga-regression-172/vision-response.json`; không thay bằng bản chép tay, không sửa lỗi thiếu A, không thêm rule riêng cho WAITER. Giữ các vùng đã bị Vision gộp để so sánh cùng đầu vào. Lượt này không gửi ảnh, không chụp màn hình và không mở game.

Model thử: `openai/gpt-oss-20b`, model mặc định hiện tại trong app. Prompt yêu cầu giữ nghĩa, chủ thể, thì và tên riêng, trả dịch theo ID. JSON schema strict; reasoning_effort=low, include_reasoning=false, max_completion_tokens=4096 theo cách adapter hiện tại. Không tự retry/đổi model. Kiểm tra endpoint models trước khi gửi để tránh gọi model không có quyền.

## Công cụ và dữ liệu

`scripts/test-clean-text-translation.ps1`: nhận key tại console bằng SecureString, chỉ giữ RAM; một batch 14 item; chặn ID thiếu/thừa/trùng/lạ/rỗng. Nếu đã có kết quả hoàn tất thì mở lại trang so sánh, không gửi thêm request. Trang HTML escape nội dung và giữ nguyên cả bản dịch Vision cũ lẫn bản dịch model text mới.

`-ValidateOnly` chạy bằng Windows PowerShell: PASS — 14 ID duy nhất, messages chỉ chứa string, không có image_url/base64; chưa gọi API ở lượt validation.

Đầu ra tại `artifacts/manga-regression-172/text-trial`: `clean-source.json`, `text-response.json`, `text-result.json`, `text-metrics.json`, `comparison.html`. Dữ liệu chỉ là mẫu manga công khai đã cung cấp và bản dịch; không lưu API key hoặc raw provider error.

## Cách chấm sau khi có kết quả

Đối chiếu ít nhất các lỗi cũ: ai tỉnh táo ở câu đầu; sự kiện người nói đến nơi; nghĩa WAITER; tính đầy đủ, không chuyển nội dung qua ID; Nazuna giữ nguyên. Không dùng exact-match với một câu tiếng Việt duy nhất để chấm mọi cách diễn đạt tự nhiên. Lưu ví dụ sai cụ thể, không biến kiểm tra đủ ID thành tỷ lệ dịch đúng.

Thời gian chỉ là request dịch text. Không cộng ngầm hoặc coi nó là độ trễ tổng: nếu tương lai chạy Vision đọc chữ rồi model text dịch, phải cộng chi phí đọc, dịch, chờ ổn định và hiển thị. Phép thử này nhằm xác định nút thắt chất lượng; chưa xác nhận dùng được cho cutscene hoặc đáp ứng cuộn nhanh.

## Trạng thái

Người dùng đã nhập key trực tiếp và báo hoàn tất. Một request text-only trả finish_reason=stop trong **995 ms**, đủ 14/14 ID. Script đánh giá xác nhận nguồn khớp nguyên văn transcript Vision đã lưu và tên Nazuna giữ nguyên. Không gửi ảnh hoặc gọi lại khi chấm.

## Kết quả chất lượng

Sửa được chủ thể ở T001 (bạn tỉnh táo) và từ WAITER ở T002 (nhân viên phục vụ). Tuy nhiên còn các lỗi nghĩa rõ trên đầu vào sạch:

| ID | Ý gốc | Kết quả model | Nhận xét |
|---|---|---|---|
| T003 | Khi tôi đến, họ đã say khướt | Khi tôi đến, họ đã hết | Mất nghĩa say rượu của wasted |
| T010 | Những chầu đồ uống miễn phí | Có những vòng miễn phí | Dịch sát từ rounds sai ngữ cảnh quán rượu |
| T012 | Thán từ SHEESH rồi nhớ lại một đêm nọ | Đồ. Nó nhắc tôi nhớ một đêm kia | Thán từ sai và câu thiếu tự nhiên |
| T013 | Cười như ngốc chẳng vì gì | Cười như những kẻ ngốc vì điều gì đó | Thay đổi nghĩa over nothing |

Các câu khác có cách diễn đạt cứng như Như, nhìn những chàng trai đó, được cho là vui, một vài... của bạn bè. Đây là nhận xét thủ công có ví dụ, không quy thành điểm accuracy/BLEU tự động hoặc tỷ lệ dịch đúng toàn bộ trang.

**Không chấp nhận gpt-oss-20b làm kết luận chất lượng cốt truyện dựa trên phép thử này.** Có cả lỗi OCR/phân vùng ở luồng cũ lẫn lỗi riêng của translator mặc định trên transcript sạch. Tách Vision rồi text bằng đúng model này chưa giải quyết yêu cầu; không tích hợp vào app chính. Cần so sánh model dịch khác trên cùng input trước khi thay pipeline, không chỉ tiếp tục đổi layout hoặc prompt riêng cho các từ đã biết.

995 ms chỉ là một lượt dịch batch text, không phải latency của toàn bộ hệ thống. Nếu phải gọi Vision để lấy transcript trước, thời gian đọc ảnh là chi phí bổ sung. Chưa có đo lặp hoặc chứng minh đáp ứng cutscene/cuộn manga nhanh.

`scripts/assess-clean-text-trial.py`: PASS cho tính nguyên vẹn nguồn, tập ID và tên riêng; chất lượng dịch bị đánh dấu không đạt qua các kiểm tra nghĩa đã nêu. Tệp máy đọc: `artifacts/manga-regression-172/text-trial/assessment.json`; bảng đối chiếu nguyên văn: `comparison.html`. Không chỉnh sửa các bản dịch model.
