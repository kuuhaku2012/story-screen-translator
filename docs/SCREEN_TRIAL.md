# Thử dịch vùng màn hình tiếng Anh → tiếng Việt

1. Mở `E:\tool_dich_man_hinh\Thu-dich-man-hinh.cmd`. Giữ cửa sổ console mở trong lúc thử; đóng ứng dụng để script gỡ đăng ký MSIX tạm. Script không cài runtime, chứng chỉ hoặc đổi Developer Mode. Nếu Developer Mode chưa bật, script sẽ báo lỗi và dừng.
2. Nhập Groq API key vào ô che ký tự trong ứng dụng, không nhập vào chat hoặc script. Key chỉ giữ trong bộ nhớ của phiên chạy. Model mặc định `openai/gpt-oss-20b`; có thể đổi theo quyền model trong Groq Console. Đổi mặc định chưa xác minh được request thật bằng key của bạn.
3. Mở manga tiếng Anh hoặc trang web. Phóng to chữ rõ nét. Chọn một ô thoại hay đoạn chữ nhỏ bằng nút **Chọn vùng chữ…**. Cửa sổ tool sẽ tạm ẩn trong lúc chọn.
4. Không cần di chuyển cửa sổ công cụ ra ngoài vùng chọn nữa: khi bật dịch, giao diện tự thu xuống taskbar trước khi quét. Bấm **Xem vùng bản dịch** để xem vị trí lớp phủ. Mở lại cửa sổ từ taskbar sẽ tự tạm dừng, ẩn bản dịch và cho chỉnh cấu hình; bật lại để tiếp tục. Lỗi API/OCR cũng tự mở lại cửa sổ để báo lỗi.
5. Bật **Bật dịch — gửi văn bản OCR tới Groq**. Ứng dụng quét mỗi 0,5 giây (1 giây với vùng lớn), đợi từng cụm chữ ổn định qua hai mẫu rồi gửi chữ mới tới Groq. Trên Windows 10 2004+ nếu SetWindowDisplayAffinity thành công, lớp dịch được loại khỏi ảnh chụp và giữ hiện liên tục khi quét. Nếu API không hỗ trợ/thất bại thì dùng cách tạm ẩn/hiện cũ, có thể còn nháy. Ảnh vùng chọn giống mẫu trước sẽ dùng lại kết quả OCR trong RAM; chỉ OCR lại khi ảnh thay đổi. Bạn không cần bấm chụp từng lần. Cuộn trang/đổi trang; nếu chữ vẫn nằm trong vùng chọn thì bản dịch tự cập nhật. Chọn lại vùng nếu bố cục thay đổi.
6. Đối chiếu ô **Chữ OCR gần nhất** với chữ gốc. OCR sai thì thử phóng to hoặc chọn vùng nhỏ hơn. Tắt checkbox trước khi chuyển sang nội dung riêng tư.

Ảnh chỉ được xử lý trên máy; văn bản OCR được gửi tới Groq khi bật và có thể phát sinh phí/hạn mức. Cache dịch chỉ giữ trong bộ nhớ của mỗi lần bật. Key, ảnh và nội dung OCR không được ghi log/file. Vùng chọn được lưu ở `E:\tool_dich_man_hinh\data\settings.json`; glossary (nếu tự tạo) nằm cùng thư mục, dạng dữ liệu giống `GlossaryStore` hiện có. Tên có trong glossary được bảo vệ bằng token; tên chưa khai báo chỉ được yêu cầu giữ nguyên trong prompt, không có bảo đảm tuyệt đối.

Lỗi key/model, lỗi mạng hoặc OCR sẽ dừng phiên. HTTP 429 tạm thời sẽ chờ và thử lại có giới hạn như mô tả bên dưới. Khắc phục rồi bật lại; không tự gửi lặp vô hạn. API đã được kiểm thử bằng HTTP giả, chưa xác nhận với key thật của bạn. Bản thử này chưa đánh giá tốc độ/độ chính xác manga và chưa được xác nhận an toàn khi chạy cùng game.

Bản chạy thử self-contained launcher dùng nằm ở `artifacts\screen-trial\StoryTranslator-1.0.0.0-x64-self-contained.msix`; launcher `Thu-dich-man-hinh.cmd` đăng ký MSIX tạm và tự gỡ khi đóng ứng dụng. Gói chính framework-dependent nằm ở `artifacts\msix\StoryTranslator-1.0.0.0-x64.msix` và cần .NET 10 Desktop Runtime toàn hệ thống. Hai gói đã được cập nhật cùng mã hybrid ngày 2026-10-20.

## Vùng lớn và bản dịch theo vị trí

OCR giữ tọa độ từng từ, gom các dòng gần nhau thành cụm rồi đặt bản dịch tương ứng ngay trên cụm gốc. Các cột/nhãn cách xa được tách riêng. Nền ngoài các ô dịch trong suốt; mỗi ô có nền tối mặc định 95%, chữ trắng đậm vừa, không dùng bóng mờ. Thanh **Độ đậm nền** cho chỉnh từ 35% đến 100%. Các ô không chặn chuột.

Mỗi cụm chờ hai mẫu ổn định riêng: một vùng đang thay đổi không chặn các vùng đã đứng yên. Chữ biến mất sẽ xóa ô tương ứng; kết quả cũ không được đưa lại lên vùng mới. Ghép tối đa 8 cụm/2.800 ký tự mỗi lượt dịch, kiểm tra ID để trả đúng vị trí, cache trong RAM. Lỗi API không phải giới hạn tốc độ vẫn dừng phiên.

OCR hiện chỉ hỗ trợ tiếng Anh. Chữ nhỏ sau khi thu ảnh toàn màn hình, chữ trang trí và các cụm quá sát nhau có thể nhận diện/gom sai. Đây là gom cụm theo hình học, chưa phải phân tích bong bóng manga. Khi bản dịch dài hơn chỗ trống, chữ co đến cỡ tối thiểu rồi có thể bị cắt; mở lại công cụ và bung **Bản dịch đầy đủ gần nhất** để đọc/copy. Phóng to nội dung hoặc chọn vùng nhỏ hơn sẽ cải thiện OCR. Vẫn cần bạn thử trang thật để đánh giá bố cục.

Tham khảo cách hiển thị trực tiếp của Bubble Translate: https://bubbletranslate.niven.studio/ ; không sử dụng hay khẳng định thuật toán nội bộ của ứng dụng đó. Groq JSON mode: https://console.groq.com/docs/structured-outputs . Phiên bản dịch theo cụm mới chưa được gọi bằng key thật trong kiểm tra tự động.
## Điều tiết Groq khi trang có nhiều chữ

Bản hiện tại không chèn khoảng chờ cố định sau mỗi request. Sau phản hồi thành công, bộ giới hạn đọc `x-ratelimit-remaining-requests`/`x-ratelimit-reset-requests` và `x-ratelimit-remaining-tokens`/`x-ratelimit-reset-tokens`; khi request đã hết hoặc token còn thấp hơn ước tính batch kế tiếp, nó chờ tới reset do Groq công bố. Nếu thiếu header reset, dùng dự phòng ngắn cho request hoặc 60 giây cho token. Bộ đếm chỉ giữ trong RAM và được dùng chung khi tắt/bật lại trong cùng cửa sổ ứng dụng; không chứa key/chữ OCR.

Khi gặp HTTP 429, chờ theo `Retry-After`, nếu thiếu thì reset request/token, rồi mới dùng dự phòng 30 giây; thêm 1 giây dự phòng và thử lại tối đa hai lần liên tiếp. Sau chờ, chọn lại các cụm chữ đang còn trên màn hình; nội dung cũ đã lướt qua không được gửi lại. OFF hủy thời gian chờ. Nếu Groq yêu cầu chờ hơn 2 phút, dừng và hướng dẫn xem Limits. Không tự đổi key, tài khoản hay model để vượt hạn mức. Khởi động lại ứng dụng không xóa hạn mức bên Groq.

Batch gửi tối đa 8 cụm/2.800 ký tự mỗi request; câu trùng trong cùng batch chỉ gửi một lần và kết quả được ánh xạ lại cho các vị trí. Cache dịch vẫn chỉ nằm trong RAM của phiên. Trang nhiều nội dung có thể hiện bản dịch dần từng nhóm. Giới hạn token/request/ngày thực tế phụ thuộc organization và model. Công cụ không thể bảo đảm hết mọi lỗi 429, nhất là khi key còn được dùng ở nơi khác. Nguồn: https://console.groq.com/docs/rate-limits . Kiểm thử HTTP giả đạt; chưa gọi API thật bằng key người dùng.
## Giữ bản dịch khi màn hình đứng yên

Không đổi lựa chọn provider. Lớp phủ của chính ứng dụng dùng WDA_EXCLUDEFROMCAPTURE (Windows 10 2004+) để bản dịch vẫn hiện trên màn hình nhưng không có trong ảnh GDI gửi vào OCR. Không hook hoặc sửa cửa sổ ứng dụng khác. Nếu Set/GetWindowDisplayAffinity không xác nhận được, fallback ẩn/hiện khi quét như cũ để không OCR nhầm bản dịch. Giới hạn: lớp dịch cũng không xuất hiện trong các công cụ chụp/quay tuân thủ display affinity.

Mỗi ảnh vùng chọn được băm SHA-256 từ bộ đệm BMP đã có; chỉ giữ dấu băm và layout OCR gần nhất trong RAM, không ghi ảnh/chữ xuống file. Ảnh trùng dùng lại layout để tiếp tục đếm mẫu ổn định ban đầu. Pipeline đã có cache và bỏ qua khung hiển thị giống hệt nên không dịch hay dựng lại ô khi nội dung không đổi. Vẫn quét ảnh nhẹ định kỳ để biết lúc cần cập nhật; không thể ngừng chụp hoàn toàn rồi tự biết màn hình đã đổi. Nếu video/ảnh động hoặc con trỏ nhấp nháy thay đổi pixel, OCR có thể chạy lại nhưng chữ không đổi vẫn không gửi dịch lại.

Khi cuộn trang/đổi chữ, layout mới cập nhật hoặc xóa các cụm cũ và chờ chữ mới ổn định như trước. Không có cam kết thời gian phản ứng tức thì; còn phụ thuộc chu kỳ lấy mẫu và OCR. Source API: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity .
## Thử truyện tranh tiếng Anh

Trước khi bật dịch, chọn **Chế độ truyện — OCR ảnh phóng 2×, gom dòng căn giữa**. Ảnh vẫn xử lý trên máy; phóng trong RAM theo giới hạn kích thước Windows OCR, tọa độ từ được quy đổi về ảnh gốc. Chế độ thường giữ nguyên cho web/game. Ảnh không đổi vẫn dùng lại OCR nên không phóng/nhận diện lặp khi trang đứng yên.

Đây là cách hỗ trợ chữ nhỏ, chưa phải engine phát hiện bong bóng manga hay OCR chữ nghiêng chuyên dụng. Không cam kết sửa mọi lỗi nhận dạng hoặc tên riêng. Chưa có đo OCR trước/sau trên trang người dùng. Tên OCR sai không được thay tự động bằng bảng hardcode.

Groq GPT-OSS dùng JSON schema strict, tăng mức output tối thiểu 2048 để giảm cắt cụt. Khi phản hồi kết thúc vì length hoặc mã lỗi json_validate_failed/json_generation_failed, nhóm nhiều item được chia đôi và xử lý tuần tự đến từng item, không lặp vô hạn. Có thể tăng số request và thời gian chờ trong tình huống này; rate limiter vẫn áp dụng. Các lỗi HTTP 400 khác không bị gán mặc định là lỗi model. Chỉ đọc mã lỗi có trong danh sách cho phép; không ghi/hiển thị raw error.

Prompt dịch mới dùng ngữ cảnh các item cùng nhóm và nối dòng OCR thành câu tự nhiên, nhưng giữ ID riêng, không đoán tên riêng và không sáng tác phần bị cắt. Các nhóm khác nhau chưa chia sẻ ngữ cảnh toàn trang; chữ bị thiếu do OCR không thể được bảo đảm phục hồi chỉ bằng dịch. Nguồn JSON schema: https://console.groq.com/docs/structured-outputs .
