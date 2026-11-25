# So sánh model dịch trên cùng transcript sạch

Theo yêu cầu bước tiếp theo: so sánh `openai/gpt-oss-120b` và `qwen/qwen3.8-27b` với kết quả `openai/gpt-oss-20b` đã lưu. Hai ứng viên nhận cùng 14 item nguyên văn từ `text-trial/clean-source.json`, cùng instruction, temperature 0,1 và giới hạn output 4096. GPT-OSS dùng strict JSON schema + reasoning low/include_reasoning=false; Qwen dùng JSON object mode. Không thêm rule riêng cho WAITER hoặc đáp án tham chiếu.

Mốc 20B là lượt trước, không gọi lại. Hướng dẫn output của mốc cũ hơi khác vì schema gửi qua response_format; prompt hai ứng viên hiện tại dùng chung contract tường minh. Vì vậy đây là so sánh thực dụng có ghi điều kiện, không phải benchmark kiểm soát tuyệt đối hoặc chứng minh thống kê.

Llama 3.3 70B không được chọn cho phép thử gói thường: tài liệu Groq ghi ngừng phục vụ free/developer ngày 2026-08-16, có ngoại lệ enterprise. Qwen 3.8 đã chạy được với key của người dùng ở lượt trước. Công cụ vẫn kiểm tra danh sách models hiện tại trước khi gọi từng ứng viên.

Nguồn đã đọc: https://console.groq.com/docs/deprecations ; https://console.groq.com/docs/models . Tài liệu nói khả dụng/cấu hình model, không chứng minh chất lượng dịch mẫu này.

## Công cụ và kiểm tra

`scripts/compare-clean-text-models.ps1`: nhận key ẩn một lần, chỉ giữ RAM. Một GET metadata và tối đa hai POST text, mỗi model tối đa một lần. Không ảnh, không screenshot. Gặp 401/429 dừng vòng; lỗi không in/lưu raw provider error. Mỗi lần thử ghi attempt marker trước request; chạy lại không tự retry model đã được thử, kể cả timeout.

`-ValidateOnly` bằng Windows PowerShell PASS: hai payload text-only, 14 source item giống nhau, không ảnh/API call. Đã sửa cách đọc mảng JSON theo hành vi Windows PowerShell trong bước kiểm tra này.

Đầu ra: `artifacts/manga-regression-172/model-comparison/`: source, request-body từng model (không Authorization/key), response thành công, status, metrics và comparison.html. Bảng giữ nguyên bản dịch; chấm chủ thể, wasted, rounds, SHEESH, over nothing, tên Nazuna và các câu còn lại trước khi chọn model.

## Trạng thái

Người dùng đã nhập key một lần và báo hoàn tất. Cả hai candidate đã gửi đúng một request, finish_reason=stop; không retry. Kiểm tra cục bộ xác nhận 14/14 ID hợp lệ, nguồn và instruction hai candidate giống nhau, Nazuna giữ nguyên.

## Kết quả

| Model | Thời gian HTTP | Nhận xét thủ công |
|---|---:|---|
| GPT-OSS 20B, mốc cũ | 995 ms | Sai wasted, rounds, SHEESH, over nothing; một số câu rất cứng |
| GPT-OSS 120B | 1.390 ms | Hiểu ngữ cảnh tốt hơn 20B nhưng T002 đổi cậu muốn thêm bia thành tôi muốn; T011 dịch sai cụm vài người bạn |
| Qwen 3.8 27B, text-only | 1.206 ms | Tự nhiên hơn trên mẫu này; sửa wasted/rounds/SHEESH/friends, nhưng T002 dịch WAITER thành Đồ uống và thêm lon không có trong nguồn |

120B còn câu là thật là bị lặp và cụm vòng uống thiếu tự nhiên. Qwen T006 thêm ý hiểu ý nhau thay vì chỉ họ hiểu. Không chỉnh tay đầu ra hoặc quy các nhận xét này thành tỷ lệ chính xác tự động.

**Ứng viên tạm chọn để thử rộng hơn: Qwen 3.8 27B. Chưa chấp nhận production.** Đánh giá hiện có một trang/một lượt mỗi candidate; không chứng minh ổn định trên truyện khác hoặc cutscene. Không thay mặc định app chỉ dựa trên mẫu này, không coi đổi model là đã sửa OCR/phân vùng manga. Main app và gói phát hành chưa đổi trong lượt so sánh.

Thời gian trên chỉ là HTTP dịch text, không gồm capture, chờ ổn định, OCR/Vision hoặc render. Chưa chứng minh mục tiêu đầu-cuối 1–1,5 giây. Nếu phải đọc ảnh bằng Vision trước thì có thêm chi phí đọc ảnh.

`scripts/assess-text-model-comparison.py`: PASS cho nguồn/instruction/ID; ghi các lỗi nghĩa cụ thể và selection tạm trong `artifacts/manga-regression-172/model-comparison/assessment.json`. Bảng nguyên văn tại `comparison.html`; request-body/response từng model được giữ để tái lập. Không có API call mới trong bước chấm.
