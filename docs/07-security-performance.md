# OrderFlow — Bảo mật và hiệu năng

## Phần 1. Bảo mật (OF-07)

Mỗi mục ghi quyết định và test chứng minh. Các test nằm trong `tests/OrderFlow.IntegrationTests/Security/`.

### Mật khẩu: hash chứ không encrypt

Mã hoá (encrypt) là hai chiều: có khoá thì giải mã được. Nếu lộ khoá thì lộ toàn bộ mật khẩu. Hash là một chiều: hệ thống không cần biết mật khẩu gốc, chỉ cần hash mật khẩu người dùng gửi lên rồi so sánh.

OrderFlow dùng PBKDF2-SHA256 với 600.000 vòng và salt riêng cho mỗi mật khẩu. Hash chậm có chủ đích, để việc dò thử mật khẩu tốn kém. Salt làm cho hai người dùng cùng mật khẩu có hash khác nhau.

### 401 khác 403 thế nào

401 nghĩa là hệ thống không biết bạn là ai: thiếu token, token hết hạn, sai chữ ký, token bị sửa. 403 nghĩa là hệ thống biết bạn là ai, nhưng bạn không có quyền làm việc này, ví dụ Warehouse gọi endpoint của Sales.

Test: `ApiSecurityTests` có `NoToken_Returns401`, `ExpiredToken_Returns401`, `TokenSignedWithAnotherKey_Returns401`, `UnsignedAlgNoneToken_Returns401`, `TamperedPayload_Returns401`, `WrongRole_Returns403`.

### Vì sao trả 404 cho đơn của người khác

Nếu trả 403, người gọi biết được đơn đó có tồn tại, và có thể dò lần lượt các id để biết hệ thống có bao nhiêu đơn. Trả 404 làm cho đơn của người khác trông giống hệt đơn không tồn tại.

Test: `DataAccessSecurityTests.Customer_GetsOwnOrder_But404ForSomeoneElses`.

### Rate limit theo IP và khoá theo account

Hai lớp này chặn hai kiểu tấn công khác nhau.

Rate limit theo IP (fixed window, 10 request/phút cho `/api/auth/*`) chặn một máy thử rất nhiều tài khoản. Nhưng nó không chặn được nhiều máy cùng dò một tài khoản.

Khoá theo account chặn đúng trường hợp đó: sai 5 lần trong 15 phút thì email đó bị khoá 15 phút, API trả 429 kèm `Retry-After`. Trong lúc khoá, mật khẩu đúng cũng bị từ chối.

Các quyết định:
- Lưu trong bảng `login_throttles` thay vì bộ nhớ, để khoá vẫn còn sau khi restart, và đúng khi chạy nhiều instance.
- Tính theo email chứ không theo user. Email không tồn tại cũng bị đếm và bị khoá y hệt, nên không dùng lockout để dò được email nào đã đăng ký.
- Kiểm tra khoá trước khi chạy PBKDF2, để request của kẻ tấn công không tốn CPU.
- Tăng bộ đếm bằng một câu `MERGE ... WITH (HOLDLOCK)`. Đọc rồi mới ghi thì các request đồng thời sẽ làm mất lần đếm.

Đánh đổi đã chấp nhận:
- Kẻ tấn công có thể cố tình khoá tài khoản của người khác trong 15 phút. Vì khoá chỉ là tạm thời, đây là từ chối dịch vụ có giới hạn, đổi lại việc dò mật khẩu gần như không thể.
- Mỗi email lạ tạo một dòng trong bảng, và chưa có việc dọn dẹp định kỳ. Rate limit theo IP giới hạn tốc độ tăng của bảng này.
- Rate limit dùng `RemoteIpAddress`. Khi đứng sau reverse proxy thì phải cấu hình forwarded headers, nếu không mọi người dùng sẽ chung một IP.

Test: `AccountLockoutTests` có 6 test, gồm cả 20 lần sai đồng thời: số lần đếm trong database đúng bằng số response 401.

### SQL injection

Giá trị người dùng không bao giờ được ghép vào câu SQL. EF Core gửi chúng dưới dạng tham số (`@p0`), nên SQL Server luôn coi chúng là dữ liệu, không bao giờ là lệnh. `EF.Functions.Like(p.Name, $"%{search}%")` trong `ProductRepository` tuy có chuỗi nội suy, nhưng đây là biểu thức LINQ được dịch thành tham số, không phải chuỗi SQL.

Các câu SQL thô trong dự án cũng dùng tham số:
- `FromSqlInterpolated` và `ExecuteSqlInterpolatedAsync` đổi mỗi `{giá trị}` thành một tham số.
- `GetByIdsWithUpdateLockAsync` dùng `FromSqlRaw`, nhưng chuỗi được ghép chỉ chứa các vị trí `{0}, {1}`, còn id truyền riêng làm tham số.

Test: `SearchWithSqlInjectionPayload_IsTreatedAsPlainText` thử `' OR 1=1 --`, `%' OR '1'='1` và `'; DROP TABLE products; --`. Cả ba đều trả 0 sản phẩm, và bảng vẫn còn nguyên.

Giới hạn đã biết: ký tự `%` và `_` trong ô tìm kiếm vẫn hoạt động như ký tự đại diện của LIKE. Đây không phải injection, chỉ là tìm kiếm rộng hơn người dùng nghĩ.

### Least privilege

Nếu một ngày kẻ tấn công chạy được SQL qua ứng dụng, thiệt hại bị giới hạn bởi quyền của tài khoản mà ứng dụng dùng. Vì vậy app không được là `db_owner`.

Script `db/scripts/create-app-user.sql` tạo login `orderflow_app` với `db_datareader` và `db_datawriter`. Mật khẩu truyền bằng biến sqlcmd, không nằm trong file. Migration chạy bằng tài khoản owner riêng.

Bằng chứng:
- Test `AppRoles_CanReadAndWriteData_ButCannotChangeTheSchema` dùng `EXECUTE AS` với user có đúng 2 role này: SELECT và UPDATE chạy được, DROP, ALTER, CREATE TABLE đều bị từ chối.
- Ngày 04/11/2026 đã chạy thật script trên LocalDB và đăng nhập bằng `orderflow_app`: SELECT chạy được. `DROP TABLE products` bị lỗi 3701, `ALTER TABLE products` bị lỗi 1088. Login tạm đã được xoá sau đó.

Chưa kiểm chứng: chạy toàn bộ API bằng tài khoản này. Vì quyền ghi đọc đã đủ cho mọi câu lệnh, rủi ro thấp, nhưng nên làm khi có Docker Compose ở OF-11.

Giới hạn đã biết: `db_datawriter` vẫn cho phép xoá hoặc sửa dữ liệu. Least privilege chỉ giới hạn việc đổi cấu trúc, không bảo vệ được dữ liệu nếu đã có SQL injection. Đó là lý do cần cả tham số hoá ở trên.

### Log không chứa dữ liệu nhạy cảm

Lỗi đã tìm thấy và đã sửa ngày 04/11/2026: `LoggingEmailSender` (email giả lập) ghi cả nội dung email ở mức Information, tức là mã OTP nằm trong log ở mọi môi trường. Ai đọc được log thì xác minh được email của người khác.

Hiện tại nội dung email chỉ được ghi khi môi trường là Development, để lập trình viên đọc được mã OTP khi chạy local.

Test: `SensitiveDataLoggingTests` thu toàn bộ log của quá trình đăng ký và đăng nhập qua API thật, rồi assert không chứa OTP, mật khẩu hay JWT. Test này đỏ trên code cũ, xanh sau khi sửa.

### Quản lý secret

JWT signing key và connection string đọc từ cấu hình, không nằm trong code. Ứng dụng dừng ngay khi khởi động nếu signing key thiếu hoặc quá ngắn (`JwtOptions.FromConfiguration`).

## Phần 2. Hiệu năng

Index và execution plan nằm ở `docs/04-database-design.md` (OF-08). Phần này nói về cách dùng EF Core (OF-09).

Số đo ngày 06/11/2026 trên `OrderFlow_Perf` (100.000 orders, LocalDB, một máy). Mỗi query chạy 8 lần, bỏ lần đầu, lấy trung vị. Logical reads là số đáng tin nhất. Thời gian chỉ xem như độ lớn.

### Tracking và AsNoTracking

Mặc định EF theo dõi (track) mọi entity nó tải về, để biết cái gì đổi khi gọi SaveChanges. Việc theo dõi tốn bộ nhớ và CPU. Các truy vấn chỉ đọc trong OrderFlow đều dùng `AsNoTracking()`. Truy vấn dùng để sửa (`GetByIdForUpdateAsync`) thì vẫn tracked.

Truy vấn có projection (`Select` ra DTO) thì không có entity nào để theo dõi, nên không cần `AsNoTracking`.

### Include và projection

`Include` tải entity kèm các entity liên quan, nghĩa là mọi cột của chúng. Projection bằng `Select(o => new OrderListItem(...))` chỉ lấy đúng các cột được chọn, và EF dịch thẳng thành SQL.

Trước ngày 06/11, danh sách order tải nguyên entity: 7 cột, gồm `row_version`, `created_at`, `updated_at`. Sau đó handler mới chọn 4 trường. Bây giờ SQL chỉ lấy 4 cột. Câu SQL EF sinh ra đã được kiểm tra trong log: `SELECT [o].[id], [o].[customer_id], [o].[status], [o].[total_amount] FROM [orders] ...`.

Đo trên trang 2.500: cả hai cách đều 526 logical reads. Thời gian 10 ms so với 12 ms, khác biệt nằm trong độ dao động. Với bảng orders này, projection không giảm việc đọc của database, vì vẫn đọc cùng các trang của clustered index. Lợi ích là ít dữ liệu truyền qua mạng hơn, ít object phải tạo hơn, và không lộ cột nội bộ. Lợi ích sẽ rõ hơn với bảng có cột lớn, hoặc khi có index phủ đủ các cột được chọn.

### N+1

N+1 là khi tải một danh sách N dòng bằng 1 câu, rồi với mỗi dòng lại chạy thêm 1 câu để lấy dữ liệu liên quan. Tổng cộng là N + 1 câu, nên chi phí tăng theo kích thước trang. Nguyên nhân thường gặp là lazy loading, hoặc gọi repository bên trong vòng lặp.

Cách phát hiện trong OrderFlow: `SqlCommandCounter`, một interceptor của EF, đếm số câu SQL của mỗi request. Test `QueryCountTests` gọi mỗi endpoint danh sách với pageSize 10 và 100, rồi so sánh số câu. N+1 sẽ lộ ra ngay vì số câu tăng theo pageSize.

Kết quả rà soát ngày 06/11: không có N+1. Lazy loading không bật, không handler nào gọi database trong vòng lặp. `GET /api/orders`, `/api/products`, `/api/customers`, `/api/payments` đều đúng 2 câu (COUNT và một trang) với mọi pageSize. Test giữ nguyên trạng thái này, để nếu sau này ai thêm N+1 thì test sẽ đỏ.

Chỗ thừa thật đã sửa: `GET /api/customers/{id}/order-stats` chạy 4 câu. Một câu kiểm tra customer tồn tại, rồi COUNT, SUM, MAX chạy riêng trên cùng tập order. Đã gộp COUNT, SUM, MAX thành một câu tổng hợp, nên giờ còn 2 câu. Test đỏ trên code cũ (4 câu), xanh sau khi sửa (2 câu).

### IQueryable và IEnumerable

`IQueryable` là truy vấn chưa chạy. Mỗi `Where`, `Select`, `Skip` được nối vào và EF dịch tất cả thành một câu SQL. `IEnumerable` là dữ liệu đã nằm trong bộ nhớ, mọi thao tác sau đó chạy bằng C#.

Nếu gọi `ToList()` quá sớm rồi mới `Where`, database sẽ trả về cả bảng, rồi mới lọc trong bộ nhớ. `OrderQueries` giữ mọi thứ ở dạng `IQueryable` cho tới `ToListAsync` cuối cùng.

### Offset và keyset

Offset (`OFFSET n ROWS FETCH NEXT 20`) phải đọc và bỏ qua n dòng trước trang cần lấy, nên trang càng sâu càng đắt. Còn phải chạy thêm COUNT để tính tổng số trang. Keyset (`WHERE id > @lastSeen ORDER BY id`, client gửi lại id cuối của trang trước) seek thẳng tới vị trí cần, chi phí không đổi theo độ sâu.

Đo thật, pageSize 20, cùng 4 cột:
- Trang 1: offset 3 reads, keyset 3 reads.
- Trang 2.500: offset 526 reads, khoảng 10 ms. Keyset 3 reads, khoảng 4 ms.
- Trang 4.999 (trang cuối): offset 1.046 reads, khoảng 22 ms. Keyset 3 reads, khoảng 2 ms.
- Câu COUNT mà offset chạy kèm mỗi trang: 349 reads, khoảng 10 ms.
- Lọc PendingPayment, trang cuối: offset 1.046 reads, khoảng 17 ms. Keyset 8 reads, khoảng 2 ms.

Vì vậy trang cuối của offset tốn khoảng 1.395 reads mỗi request (trang cộng COUNT), còn keyset tốn 3.

Đánh đổi:
- Keyset không nhảy thẳng được tới trang 2.500, chỉ đi tới trang kế tiếp. Nó hợp với cuộn vô hạn hoặc nút "xem thêm".
- Keyset không có tổng số trang.
- Phải sắp xếp theo một khoá duy nhất và ổn định. Ở đây là `id`. Nếu muốn sắp theo ngày thì cursor phải gồm cả (ngày, id).

Vì thế OrderFlow giữ cả hai. `GET /api/orders` (offset) dành cho bảng quản trị cần số trang. `GET /api/orders/cursor` (keyset) dành cho danh sách dài, không cần tổng.

## Phần 3. Quan sát hệ thống (OF-10)

### Request đi qua ASP.NET Core thế nào

Request đi qua lần lượt các middleware theo đúng thứ tự trong `Program.cs`, tới controller, rồi response đi ngược lại qua chính các middleware đó. Thứ tự của OrderFlow và lý do:

1. `CorrelationIdMiddleware`: đứng đầu, để mọi dòng log sau nó, kể cả log lỗi, đều có correlation id.
2. `UseExceptionHandler`: bắt mọi exception ở các bước sau, đổi thành ProblemDetails.
3. `UseStatusCodePages`: biến các 401, 403, 404 trống thành ProblemDetails.
4. `UseHttpsRedirection`, CORS, rate limiting: chặn sớm các request không hợp lệ hoặc quá nhiều, trước khi tốn công xác thực.
5. `UseAuthentication`, rồi `UseAuthorization`: phải xác định "bạn là ai" trước khi hỏi "bạn được làm gì".
6. `MapControllers`.

Đặt sai thứ tự sẽ gây lỗi khó thấy. Ví dụ đặt correlation id sau exception handler thì log của request lỗi sẽ mất id. Đặt authorization trước authentication thì mọi request đều bị coi là chưa đăng nhập.

### Correlation ID

Mỗi request có một id. Nếu client gửi header `X-Correlation-Id` hợp lệ thì dùng lại, để theo được một yêu cầu qua nhiều service. Nếu không có, hoặc giá trị không hợp lệ, thì sinh GUID mới. Hợp lệ nghĩa là dài 1 đến 64 ký tự, chỉ gồm chữ, số, `-`, `_`, `.`, `:`. Không tin dữ liệu client vì giá trị này được ghi vào log: một giá trị có xuống dòng có thể giả mạo dòng log khác (log injection).

Id được:
- Gán vào `HttpContext.TraceIdentifier`.
- Trả về trong header của mọi response, kể cả response lỗi.
- Đưa vào trường `correlationId` của mọi ProblemDetails.
- Đặt làm log scope cho cả request.

Khi người dùng báo lỗi, chỉ cần họ gửi `correlationId` trong response lỗi là lọc ra được toàn bộ log của đúng request đó.

ASP.NET Core cũng có sẵn `TraceId` theo chuẩn W3C (Activity), và nó vẫn xuất hiện trong log. OrderFlow dùng thêm header riêng vì nó đơn giản và dễ đọc cho người dùng.

### Structured logging

Log console là JSON, mỗi dòng một object, có scope (`appsettings.json`: `FormatterName: json`, `IncludeScopes: true`). Các giá trị trong message template (`{OrderId}`) trở thành trường riêng, nên lọc được theo `OrderId` hay `CorrelationId` mà không phải dò chuỗi.

Sự kiện nghiệp vụ nằm ở `src/OrderFlow.Application/Logging/BusinessEvents.cs`, viết bằng `[LoggerMessage]` (source generator, không tốn chi phí cấp phát khi log level đang tắt):
- 1001 OrderCreated
- 1002 PaymentRecorded
- 1003 OrderPaid
- 1004 OrderCancelled
- 1005 ReconciliationApplied
- 1006 ConcurrencyConflict (Warning)

Hai quy tắc:
- Chỉ log sau khi transaction đã commit. Log không được kể một việc đã bị rollback.
- Chỉ log id, số tiền, trạng thái. Không log tên, email, mật khẩu, OTP hay token.

Mức log:
- Information cho sự kiện nghiệp vụ.
- Warning cho lỗi 4xx đã xử lý.
- Error, kèm stack trace, cho 500.

Hai category được chỉnh:
- `ExceptionHandlerMiddleware` của framework đặt `None`. Nó tự ghi Error "An unhandled exception has occurred" cho mọi exception, kể cả lỗi 409 đã xử lý, nên gây báo động giả. `GlobalExceptionHandler` đã tự ghi đúng mức.
- Câu SQL của EF chỉ log ở môi trường Development.

### Log mẫu thật

Chạy API ở môi trường Production trên `OrderFlow_Perf` ngày 07/11/2026, rồi lần lượt tạo order, thanh toán, gửi lại cùng mã giao dịch với số tiền khác, và huỷ một order đã trả tiền. Lọc theo correlation id, mỗi request chỉ còn đúng các dòng của nó (dạng rút gọn: id, EventId, mức log, category, message):

- sample-create, 1001, Information, CreateOrderCommandHandler: "Order 100002 created for customer 2: 1 line(s), total 24.00"
- sample-pay, 1002, Information, ProcessPaymentCommandHandler: "Payment 102123 of 24.0 recorded for order 100002 via SimPay with status Succeeded"
- sample-pay, 1003, Information, ProcessPaymentCommandHandler: "Order 100002 fully paid"
- sample-bad-payment, 0, Warning, GlobalExceptionHandler: "ConflictException while processing POST /api/orders/100002/payments: Provider transaction 'LOG-SAMPLE-100002' was already recorded with different details."
- sample-cancel-paid, 0, Warning, GlobalExceptionHandler: "InvalidOrderStateException while processing POST /api/orders/100002/cancel: Order cannot be cancelled from status 'Paid'."

Một dòng JSON đầy đủ của sự kiện PaymentRecorded, đã bỏ bớt các scope khác:

```json
{
  "Timestamp": "2026-11-07T03:11:54.720Z",
  "EventId": 1002,
  "LogLevel": "Information",
  "Category": "OrderFlow.Application.Features.Payments.ProcessPayment.ProcessPaymentCommandHandler",
  "Message": "Payment 102123 of 24.0 recorded for order 100002 via SimPay with status Succeeded",
  "State": {
    "PaymentAttemptId": 102123,
    "OrderId": 100002,
    "Amount": 24.0,
    "Provider": "SimPay",
    "PaymentStatus": "Succeeded"
  },
  "Scopes": [
    { "Message": "CorrelationId:sample-pay", "CorrelationId": "sample-pay" }
  ]
}
```

Trong lần chạy này, log không chứa mật khẩu hay JWT. Mình đã kiểm tra bằng cách tìm chính hai chuỗi đó trong file log.

### Lifetime của DI

- Singleton: một instance cho cả ứng dụng. Ví dụ `JwtTokenGenerator`, `Pbkdf2PasswordHasher`, `TimeProvider`. Phải an toàn khi nhiều luồng dùng cùng lúc.
- Scoped: một instance cho mỗi request. Ví dụ `OrderFlowDbContext` và mọi repository. Nhờ vậy các repository trong cùng request dùng chung một DbContext và cùng một transaction (ADR-006).
- Transient: mỗi lần xin là một instance mới.

Inject Scoped vào Singleton là lỗi captive dependency: singleton giữ mãi một DbContext của request đầu tiên, dùng chung cho mọi request sau, ở nhiều luồng cùng lúc. Trong Development, ASP.NET Core bật `ValidateScopes` nên phát hiện lỗi này ngay khi resolve. Ở Production thì không, và lỗi chỉ lộ ra khi chạy thật. Reconcile tạo scope riêng cho từng giao dịch bằng `IServiceScopeFactory`, chính là cách đúng khi cần dịch vụ Scoped ngoài phạm vi một request.

### CancellationToken và command timeout

`CancellationToken` từ ASP.NET Core được truyền từ controller xuống tới mọi câu EF. Khi client ngắt kết nối, câu SQL đang chạy được huỷ, không tốn tài nguyên cho một response không ai nhận.

Command timeout mặc định 30 giây, đổi được qua `Database:CommandTimeoutSeconds`. Một câu bị treo (do khoá hay mạng) sẽ lỗi thay vì giữ connection và request mãi.

### Validation cho mọi command

Cả 23 command handler đều được đăng ký bằng `AddValidatedCommandHandler`, và controller chỉ nhận `ICommandHandler<,>`, nên mọi command đều đi qua `ValidationCommandHandlerDecorator`. Test `PipelineCoverageTests` đỏ nếu thêm command mà quên đăng ký, hoặc controller nhận class handler cụ thể. Mình đã thử bỏ đăng ký `RestockProductCommandHandler` để kiểm tra, và test báo đúng tên handler đó.

Hiện mới có validator cho 4 command auth. Các command khác vẫn tự validate trong handler. Pipeline đã sẵn sàng, nên khi chuyển quy tắc sang validator riêng chỉ cần thêm class validator, không phải sửa đăng ký hay controller.

