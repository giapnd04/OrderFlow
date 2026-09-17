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

---

## General Concept: Repository Pattern

**Định nghĩa tổng quát:** Một lớp trừu tượng (interface) đứng giữa business logic và cơ
chế lưu trữ dữ liệu thật (database, file, API ngoài...), biểu diễn các "khả năng" mà
business logic cần (lấy theo id, kiểm tra tồn tại, thêm mới...) chứ không phải các thao
tác của công nghệ lưu trữ cụ thể (SQL, EF Core LINQ...). Business logic chỉ phụ thuộc
vào interface này; một class riêng ở tầng hạ tầng mới implement nó bằng công nghệ cụ thể.

**Vấn đề nó giải quyết:** Cho phép business logic được viết và test mà không cần một
database thật (dùng fake/in-memory implementation trong unit test); cho phép đổi công
nghệ lưu trữ (VD SQL Server → PostgreSQL, hoặc thêm cache) mà không phải sửa business
logic, vì business logic chưa từng biết công nghệ lưu trữ là gì.

**Trade-off / khi nào KHÔNG nên dùng:** Với ứng dụng nhỏ, CRUD đơn giản, không có kế
hoạch đổi công nghệ lưu trữ và không cần unit test tách biệt DB — thêm interface +
implementation cho mỗi entity chỉ để "gọi thẳng qua" là overhead không cần thiết (ORM như
EF Core với `DbContext`/`DbSet` bản thân nó đã là một dạng Unit of Work + Repository ở
mức generic). Repository pattern chỉ thật sự đáng giá khi có nhu cầu cụ thể về testability
hoặc tách biệt công nghệ, không phải áp dụng mặc định cho mọi dự án.

**Khái niệm dễ nhầm lẫn:** Repository khác Generic Repository (`IRepository<T>` dùng
chung cho mọi entity — dễ rò rỉ chi tiết ORM ra ngoài, hay bị phê phán là anti-pattern vì
che giấu nhu cầu thật của từng use case). Repository cũng khác Unit of Work (Unit of Work
quản lý một transaction/nhóm thay đổi được commit cùng lúc; Repository chỉ là cách truy
xuất một loại aggregate — hai khái niệm độc lập nhưng hay đi cùng nhau).

**Câu hỏi phỏng vấn thường gặp:**
1. Repository Pattern giải quyết vấn đề gì mà dùng thẳng `DbContext`/ORM không giải quyết được?
2. Generic Repository (`IRepository<T>`) có phải luôn là ý tưởng tốt không? Vì sao nhiều người coi đó là anti-pattern?
3. Repository interface có nên trả về `IQueryable<T>` không? Trade-off là gì?

**Liên hệ OrderFlow:** `ICustomerRepository`, `IOrderRepository`, `IProductRepository`,
`IPaymentRepository` — mỗi interface chỉ khai báo đúng method mà use case thực tế cần
(VD `ICustomerRepository` ban đầu chỉ có `ExistsAsync`, CreateCustomer mới thêm
`ExistsByEmailAsync`/`AddAsync` khi thật sự cần), không dùng generic repository dùng
chung cho mọi entity — pattern này đã được áp dụng xuyên suốt từ use case CreateOrder,
không phải điều mới của CreateCustomer, nhưng đây là lần đầu được ghi lại thành khái niệm
riêng trong tài liệu này.

---

## General Concept: Centralized Exception Handling / Exception-to-HTTP Status Mapping

**Định nghĩa tổng quát:** Thay vì mỗi endpoint tự viết `try/catch` để chuyển exception
thành HTTP response, hệ thống có MỘT điểm xử lý exception tập trung (middleware, filter,
hoặc exception handler toàn cục) — nơi duy nhất quyết định loại exception nào tương ứng
với status code nào, và định dạng response lỗi trông ra sao.

**Vấn đề nó giải quyết:** Tránh lặp lại logic "bắt lỗi rồi convert sang status code" ở
từng controller/action; đảm bảo mọi lỗi cùng loại luôn trả về cùng format response nhất
quán trên toàn hệ thống; tách rời "loại lỗi nghiệp vụ" (exception type) khỏi "cách nó
được biểu diễn cho client" (HTTP status + body).

**Trade-off / khi nào KHÔNG nên dùng:** Nếu các exception type không được thiết kế có
chủ đích (không phân biệt rõ lỗi validate/not-found/conflict...), việc map tập trung sẽ
kém chính xác — dễ rơi vào tình trạng "mọi lỗi đều thành 400 hoặc 500". Cách tiếp cận này
đòi hỏi kỷ luật: mỗi tầng chỉ nên ném đúng loại exception có ý nghĩa nghiệp vụ, không phải
exception chung chung (`Exception`, `InvalidOperationException`...).

**Khái niệm dễ nhầm lẫn:** Khác với validation ở tầng input/DTO (chặn request sai cấu
trúc trước khi vào business logic) — cơ chế này xử lý lỗi *sau khi* business logic đã
chạy và ném exception ra. Cũng khác với Result/Either pattern (trả lỗi như một giá trị
thay vì exception) — đây là lựa chọn kiến trúc khác, đánh đổi giữa "exception rõ ràng,
dễ đọc" và "không dùng exception cho control flow, performance tốt hơn khi lỗi xảy ra
thường xuyên".

**Câu hỏi phỏng vấn thường gặp:**
1. Vì sao nên xử lý exception tập trung một chỗ thay vì try/catch ở từng controller?
2. Nếu 2 loại exception khác nhau vô tình map ra cùng 1 status code, hệ quả gì với client API?
3. So sánh cách tiếp cận "throw exception + handler tập trung" với "trả Result/Either object" — trade-off là gì?

**Liên hệ OrderFlow:** `GlobalExceptionHandler` (implement `IExceptionHandler` của
ASP.NET Core) map `ValidationException`/`DomainException` → 400, `NotFoundException` →
404, `InvalidOrderStateException`/`InsufficientStockException` → 409, còn lại → 500 kèm
`ProblemDetails`. Cơ chế này có từ CreateOrder; CreateCustomer chỉ tái sử dụng, không tạo
case mới — nhưng chính vì `ValidationException` (Application) và `DomainException`
(Domain) đều map cùng 400, sự khác biệt giữa 2 exception này chỉ có ý nghĩa nội bộ
(layer nào phát hiện lỗi), không ảnh hưởng gì tới client.

---

## General Concept: Static Factory Method vs Public Constructor

**Định nghĩa tổng quát:** Thay vì cho phép tạo object qua constructor `public` (ai cũng
gọi `new T(...)` được, kể cả khi tham số vô nghĩa), object chỉ được tạo qua một static
method (thường tên `Create`) — constructor thật bị giấu (`private`). Method này có toàn
quyền validate tham số đầu vào trước khi object thành hình, và có thể trả về tên method
có ý nghĩa nghiệp vụ hơn `new` (VD `Order.Create(...)` rõ ràng hơn `new Order(...)`).

**Vấn đề nó giải quyết:** Đảm bảo **không thể nào** tồn tại một object ở trạng thái
không hợp lệ trong hệ thống — vì con đường duy nhất để tạo object đã bắt buộc đi qua
validate. Với constructor `public` thường, invariant chỉ được đảm bảo nếu *mọi* nơi gọi
constructor đều nhớ tự validate trước — một giả định dễ vỡ khi codebase lớn dần.

**Trade-off / khi nào KHÔNG nên dùng:** Với object đơn giản, không mang invariant nào
đáng kể (DTO thuần dữ liệu, record chỉ để truyền tin giữa layer), việc ép dùng static
factory là thừa phức tạp — constructor/record positional parameter là đủ. Factory method
chỉ đáng giá khi object có invariant thật sự cần bảo vệ.

**Khái niệm dễ nhầm lẫn:** Khác với Factory Pattern/Abstract Factory (GoF) — đó là pattern
tạo ra object thuộc nhiều loại con khác nhau tùy điều kiện runtime (polymorphism khi tạo
object); static factory method ở đây đơn giản hơn nhiều, chỉ nhằm mục đích validate +
đặt tên có ý nghĩa cho việc tạo MỘT loại object cụ thể. Cũng khác Builder Pattern (dùng
khi object có nhiều tham số optional, cần build từng bước).

**Câu hỏi phỏng vấn thường gặp:**
1. Static factory method giải quyết vấn đề gì mà constructor public không giải quyết được?
2. Nếu entity có private setter + static factory, EF Core làm sao để materialize object đó từ database (không có public constructor nhận đủ tham số)?

**Liên hệ OrderFlow:** `Customer.Create(name, email, phone)` với constructor `private
Customer() { }` (constructor rỗng này thật ra dành riêng cho EF Core materialize object
khi đọc từ DB, không dùng để tạo object mới trong business logic) — cùng pattern với
`Product.Create`, `Order.Create`, `OrderItem.Create` đã dùng từ trước; CreateCustomer là
lần đầu người viết tự áp dụng pattern này từ con số 0 thay vì sửa trên entity có sẵn.

---

## General Concept: Check-Then-Act Race Condition (TOCTOU) & Database Constraint làm tuyến phòng thủ cuối

**Định nghĩa tổng quát:** "Check-then-act" là khi code kiểm tra một điều kiện (VD: "giá
trị X đã tồn tại chưa?"), rồi dựa trên kết quả đó thực hiện một hành động (VD: "nếu chưa,
thêm mới"). Giữa bước *check* và bước *act* luôn có một khoảng hở thời gian — nếu có một
luồng/request khác cũng thực hiện đúng trình tự đó gần như đồng thời, cả hai có thể cùng
"thấy" điều kiện chưa tồn tại và cùng hành động, dẫn đến vi phạm điều mà lẽ ra phải được
đảm bảo duy nhất (VD: 2 bản ghi trùng nhau lọt qua dù logic đã "kiểm tra trước"). Đây là
một dạng cụ thể của TOCTOU (Time-Of-Check to Time-Of-Use) race condition.

**Vấn đề nó giải quyết (của giải pháp, không phải của bản thân race condition):** Không
có cách nào ở tầng application logic tự nó loại bỏ hoàn toàn race condition này nếu chỉ
dựa vào "check rồi act" — giải pháp thực sự phải đến từ một cơ chế đảm bảo tính duy nhất
ở tầng thấp hơn, nơi có khả năng nguyên tử hóa (atomic) việc kiểm tra + ghi, ví dụ: unique
constraint/index ở database (database tự đảm bảo tính duy nhất bất kể có bao nhiêu request
tới cùng lúc), hoặc một cơ chế khóa (lock) rõ ràng.

**Trade-off / khi nào KHÔNG nên dùng:** Bỏ qua check ở application layer (chỉ dựa vào DB
constraint) làm giảm trải nghiệm người dùng — lỗi chỉ lộ ra như một database exception khó
đọc thay vì một thông báo nghiệp vụ rõ ràng ngay từ đầu. Ngược lại, chỉ check ở
application mà không có constraint ở DB thì không an toàn thật sự dưới tải đồng thời. Cách
tiếp cận đúng thường là **cả hai**: check ở application để trả lỗi thân thiện cho phần lớn
trường hợp (không đồng thời), CỘNG với constraint ở DB làm tuyến phòng thủ cuối cho phần
hiếm khi đụng độ thật — và code phải xử lý được exception từ tuyến phòng thủ đó, không để
nó rơi tự do thành lỗi 500 không rõ nghĩa.

**Khái niệm dễ nhầm lẫn:** Khác với Optimistic Concurrency Control (dùng version/RowVersion
để phát hiện xung đột khi *update* một bản ghi đã tồn tại — đây là race condition khi
*tạo mới*, không phải khi cập nhật). Cũng khác Pessimistic Locking (chủ động khóa trước khi
đọc, chặn luồng khác truy cập — cách tiếp cận hoàn toàn khác, đánh đổi throughput lấy an
toàn tuyệt đối tại thời điểm đọc).

**Câu hỏi phỏng vấn thường gặp:**
1. "Tôi đã check email chưa tồn tại trước khi insert rồi, sao vẫn có thể bị trùng?" — giải thích tại sao.
2. Unique constraint ở database giải quyết được race condition gì mà application-level check không giải quyết được, và ngược lại có gì application check làm được mà DB constraint không làm được (VD thông báo lỗi thân thiện)?
3. So sánh cơ chế phòng chống race condition này với Optimistic Concurrency (RowVersion) — chúng bảo vệ hai tình huống khác nhau thế nào?

**Liên hệ OrderFlow:** `CreateCustomerCommandHandler` gọi `ExistsByEmailAsync` rồi mới gọi
`AddAsync` — có khoảng hở giữa 2 bước. `customers.email` có unique index ở DB
(`CustomerConfiguration`) làm tuyến phòng thủ cuối, nhưng hiện tại `GlobalExceptionHandler`
**chưa có case nào bắt lỗi unique-constraint violation từ DB** — đây là gap đã được phát
hiện khi review CreateCustomer, chưa được vá, cần nhớ lại khi có dịp.
