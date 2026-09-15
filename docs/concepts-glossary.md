# Concepts Glossary

Tài liệu ôn phỏng vấn tích lũy dần — khái niệm nền tảng ngành Backend/.NET, không phụ
thuộc project cụ thể nào trong portfolio. Mỗi khái niệm chỉ viết đầy đủ một lần; lần
gặp lại sau chỉ bổ sung góc nhìn mới và ghi chú "đã gặp ở".

---

## General Concept: Offset-based Pagination

**Định nghĩa tổng quát:** Kỹ thuật chia một tập kết quả lớn thành các trang nhỏ bằng
cách bỏ qua (`OFFSET`/`Skip`) một số bản ghi đầu rồi lấy (`LIMIT`/`Take`) một số lượng
cố định tiếp theo, dựa trên vị trí số học của bản ghi trong tập kết quả.

**Vấn đề nó giải quyết:** Tránh trả về toàn bộ bảng dữ liệu (có thể hàng triệu dòng)
trong một response — giảm băng thông, giảm bộ nhớ, giảm thời gian render phía client.

**Trade-off / khi nào KHÔNG nên dùng:**
- `OFFSET` lớn (trang thứ 10,000) buộc DB phải quét và bỏ qua toàn bộ bản ghi phía
  trước → chậm dần theo số trang, không scale cho dataset cực lớn.
- Nếu dữ liệu bị insert/delete giữa hai lần gọi trang kế tiếp, người dùng có thể thấy
  một bản ghi bị lặp lại hoặc bị bỏ sót giữa hai trang (page drift) — vì vị trí offset
  không cố định khi tập dữ liệu nền thay đổi.
- Với dataset cực lớn hoặc cần độ chính xác cao khi ghi liên tục (feed, log), keyset/
  cursor pagination (dựa trên "lấy bản ghi có Id > cursor cuối cùng") ổn định hơn và
  không chậm dần theo trang.

**Khái niệm dễ nhầm lẫn:** Cursor/keyset pagination (dùng giá trị của bản ghi cuối
làm điểm tiếp tục, không dùng OFFSET); infinite scroll (là UX pattern, có thể implement
bằng offset hoặc cursor pagination).

**Câu hỏi phỏng vấn thường gặp:**
1. Offset pagination có vấn đề gì khi dataset rất lớn hoặc thay đổi liên tục?
2. So sánh offset pagination và cursor/keyset pagination — khi nào chọn cái nào?
3. Làm sao thiết kế response để client biết còn trang tiếp theo hay không?

**Liên hệ OrderFlow:** trong GetOrders tôi dùng `Skip`/`Take` + trả kèm `TotalCount`/
`TotalPages` — chấp nhận trade-off offset cho quy mô dữ liệu v1, chưa cần cursor.

---

## General Concept: Stable Ordering trong Pagination

**Định nghĩa tổng quát:** Yêu cầu bắt buộc phải có một `ORDER BY` tường minh, dựa trên
cột (hoặc tổ hợp cột) duy nhất, khi phân trang — nếu không, thứ tự trả về giữa các lần
gọi không được đảm bảo.

**Vấn đề nó giải quyết:** Đảm bảo `Skip`/`Take` (hay `OFFSET`/`LIMIT`) trả về đúng và
nhất quán bản ghi mong đợi ở mỗi trang.

**Trade-off / khi nào KHÔNG nên dùng:** Không có "khi nào không nên" — đây gần như luôn
bắt buộc khi phân trang; cái cần cân nhắc là **sắp xếp theo cột nào**: cột không unique
(ví dụ `CreatedAt`) có thể có nhiều bản ghi trùng giá trị → vẫn cần thêm cột phụ (thường
là khóa chính) để đảm bảo thứ tự tuyệt đối duy nhất.

**Khái niệm dễ nhầm lẫn:** Nhiều người nghĩ database "tự nhiên" trả kết quả theo thứ tự
insert — điều này **không đúng** với bất kỳ RDBMS nào trừ khi có `ORDER BY` tường minh;
query planner có quyền trả bản ghi theo bất kỳ thứ tự nào nó thấy hiệu quả nhất.

**Câu hỏi phỏng vấn thường gặp:**
1. Điều gì xảy ra nếu phân trang mà không có ORDER BY?
2. Tại sao sort theo một cột không unique vẫn có thể gây lỗi phân trang?

**Liên hệ OrderFlow:** `OrderRepository.GetPagedAsync` sort theo `order.Id` (khóa chính,
unique) trước khi `Skip`/`Take`, đảm bảo trang sau không lặp/thiếu bản ghi.

---

## General Concept: N+1 Query Problem & Eager vs Lazy Loading

**Định nghĩa tổng quát:** N+1 là lỗi hiệu năng khi code chạy 1 query để lấy N bản ghi
cha, rồi chạy thêm N query riêng lẻ (một cho mỗi bản ghi cha) để lấy dữ liệu con liên
quan — thay vì gộp thành 1 (hoặc 2) query duy nhất. Eager loading (`Include` trong EF
Core, `JOIN FETCH` trong JPA...) tải trước dữ liệu liên quan trong cùng query; lazy
loading chỉ tải khi thuộc tính đó thực sự được truy cập, đúng lúc gây ra N+1 nếu dùng
trong vòng lặp.

**Vấn đề nó giải quyết (khi dùng đúng):** Giảm số round-trip tới database.

**Trade-off / khi nào KHÔNG nên dùng eager loading:** Eager loading dữ liệu không thực
sự cần dùng (ví dụ tải toàn bộ chi tiết đơn hàng cho một danh sách chỉ hiển thị tóm tắt)
lại gây **over-fetching** — tốn băng thông, tốn bộ nhớ, chậm hơn so với việc không tải
gì cả. Quy tắc chung: eager load đúng những gì response thực sự cần, không hơn.

**Khái niệm dễ nhầm lẫn:** N+1 khác over-fetching (N+1 là *quá nhiều query*, over-
fetching là *quá nhiều dữ liệu trong một/vài query*) — cả hai đều là vấn đề hiệu năng
nhưng nguyên nhân và cách sửa khác nhau.

**Câu hỏi phỏng vấn thường gặp:**
1. N+1 query là gì, làm sao phát hiện nó (SQL profiler/log)?
2. Eager loading luôn tốt hơn lazy loading đúng không? Vì sao không?
3. Cho một đoạn code load danh sách rồi lặp qua từng item gọi thêm query — chỉ ra vấn đề.

**Liên hệ OrderFlow:** `GetOrderById` dùng `Include(o => o.Items)` vì cần đủ chi tiết
một order; `GetOrders` (list) **cố ý không** `Include(Items)` vì response danh sách chỉ
cần tóm tắt — tránh over-fetch cho N order trong một trang.

---

## General Concept: CQRS (Command Query Responsibility Segregation)

**Định nghĩa tổng quát:** Tách rõ hai loại thao tác trên hệ thống: *Command* (thay đổi
trạng thái, không trả dữ liệu nghiệp vụ) và *Query* (đọc trạng thái, không gây side
effect) — thường bằng hai interface/pipeline riêng biệt, có thể dùng chung một model
dữ liệu (CQRS "nhẹ") hoặc tách hẳn hai model/datastore đọc-ghi riêng (CQRS "đầy đủ").

**Vấn đề nó giải quyết:** Một model/service "làm tất cả" (đọc lẫn ghi) dễ phình to,
khó tối ưu riêng cho từng chiều (ghi cần validate/invariant chặt, đọc cần nhanh/có thể
denormalize) và khó test vì handler ôm quá nhiều trách nhiệm.

**Trade-off / khi nào KHÔNG nên dùng:** Với hệ thống nhỏ/CRUD đơn giản, tách Command
và Query riêng (nhất là CQRS đầy đủ với datastore riêng) là over-engineering — thêm độ
phức tạp (đồng bộ hai model, eventual consistency) không tương xứng lợi ích.

**Khái niệm dễ nhầm lẫn:** CQRS không bắt buộc phải có MediatR/message bus hay Event
Sourcing đi kèm — đó là các lựa chọn triển khai độc lập, hay bị gộp chung một cách sai
lầm. CQRS "nhẹ" (chỉ tách interface, chung 1 DB) là một điểm khởi đầu hợp lý.

**Câu hỏi phỏng vấn thường gặp:**
1. CQRS là gì, và nó có bắt buộc phải tách database đọc/ghi không?
2. CQRS khác gì với việc chỉ đơn giản có Service layer chung cho đọc và ghi?
3. Khi nào KHÔNG nên áp dụng CQRS?

**Liên hệ OrderFlow:** `ICommand`/`ICommandHandler` và `IQuery`/`IQueryHandler` là hai
interface tự viết tay (không dùng MediatR) — CQRS "nhẹ", chung một `OrderFlowDbContext`,
không tách datastore. `GetOrders` là handler Query thứ hai theo pattern này.

---

## General Concept: DTO / Projection — không expose Domain Entity qua API

**Định nghĩa tổng quát:** Domain entity (mang invariant, behavior, có thể có dữ liệu
nội bộ như `RowVersion`) không nên được serialize trực tiếp làm response HTTP; thay vào
đó dùng một kiểu dữ liệu phẳng riêng (DTO/Result) chỉ chứa đúng field client cần.

**Vấn đề nó giải quyết:** Tách rời "hình dạng dữ liệu nghiệp vụ nội bộ" khỏi "hợp đồng
API công khai" — đổi cấu trúc Domain (thêm field nội bộ, đổi tên property) không làm vỡ
API contract, và tránh vô tình lộ dữ liệu nhạy cảm/không cần thiết (over-exposure).

**Trade-off / khi nào KHÔNG nên dùng:** Với API/service thật sự nội bộ, tuổi thọ ngắn,
hoặc prototype — thêm một lớp mapping entity→DTO cho mọi thứ có thể là overhead không
cần thiết. Nhưng đây là trade-off hiếm khi đáng đánh đổi cho một API public lâu dài.

**Khái niệm dễ nhầm lẫn:** DTO khác ViewModel (ViewModel thường gắn với UI rendering,
DTO là hợp đồng truyền dữ liệu giữa các layer/service nói chung); AutoMapper là một
công cụ *tùy chọn* để làm việc map DTO, không phải bản thân khái niệm DTO.

**Câu hỏi phỏng vấn thường gặp:**
1. Tại sao không nên trả thẳng Entity Framework entity ra khỏi API?
2. DTO và ViewModel khác nhau ở điểm nào?

**Liên hệ OrderFlow:** `GetOrdersResult`/`GetOrdersItemResult` là record phẳng riêng,
không phải `Order`/`OrderItem` — entity domain không bao giờ rời khỏi Application layer.
