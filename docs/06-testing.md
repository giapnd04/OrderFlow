# OrderFlow — Testing Baseline & Gap Register

- Requirement: OF-01 Baseline & gap register
- Ngày đo: 27/10/2026
- Commit đo: `4ba9479` (trước khi xoá test scaffolding rỗng)
- Môi trường: .NET SDK 9.0.304, Windows 11

---

## 1. Baseline

Lệnh chạy ở thư mục gốc repo:

```bash
dotnet test OrderFlow.sln
```

Kết quả trước khi dọn: 311 test, tất cả pass, không có test nào fail hoặc skip.

- OrderFlow.UnitTests: 256 pass.
- OrderFlow.IntegrationTests: 55 pass.

Kết quả sau khi dọn: 309 test, tất cả pass.

- OrderFlow.UnitTests: 255 pass.
- OrderFlow.IntegrationTests: 54 pass.

Mỗi project giảm đúng một test vì đã bỏ `UnitTest1.Test1`. Đây là test template rỗng do `dotnet new xunit` sinh ra: nó luôn pass nhưng không kiểm tra gì. Vì vậy 309 là số test thật của project.

---

## 2. Test pyramid hiện tại

Unit test nằm trong `tests/OrderFlow.UnitTests` và không chạm database.

- `Application/`: 37 file, mỗi file test handler của một use case.
- `Domain/`: 2 file.
- `Infrastructure/`: 1 file.

Integration test chỉ có thư mục `tests/OrderFlow.IntegrationTests/Security`, gồm `ApiSecurityTests` và `EndpointAuthorizationCoverageTests`. Các test này dựng API thật trong bộ nhớ bằng `WebApplicationFactory<Program>`, nhưng `ApiFactory` cố tình dùng một connection string không kết nối được (`Server=127.0.0.1,1;Database=Unreachable`). Nghĩa là integration test hiện tại cũng không chạm SQL Server.

E2E test: chưa có.

Hệ quả là test integration mới chỉ kiểm tra HTTP pipeline: authentication, authorization, rate limit và ProblemDetails, ở những request bị chặn trước khi tới repository. Chưa có test nào kiểm chứng transaction, CHECK constraint, unique index hay rowversion trên SQL Server thật.

---

## 3. Inventory

### 3.1 Endpoint

Có 35 action trong 6 controller. Nguồn: `src/OrderFlow.API/Controllers/*.cs`. Mỗi dòng dưới đây ghi method, route và policy bắt buộc.

Auth (`/api/auth`): cả 3 endpoint đều AllowAnonymous và dùng rate limit `Auth`.

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/confirm-email`

Customers (`/api/customers`)

- `POST /api/customers`: Sales
- `GET /api/customers`: Staff
- `GET /api/customers/{id}`: Staff
- `PUT /api/customers/{id}`: Sales
- `GET /api/customers/{id}/order-stats`: Staff
- `PUT /api/customers/{id}/email`: Sales
- `DELETE /api/customers/{id}`: AdminOnly
- `POST /api/customers/{id}/deactivate`: Sales
- `POST /api/customers/{id}/reactivate`: Sales

Orders (`/api/orders`)

- `POST /api/orders`: CustomerOrSales
- `GET /api/orders`: Authenticated
- `GET /api/orders/{id}`: Authenticated, kèm kiểm tra ownership
- `POST /api/orders/{id}/cancel`: CustomerOrSales
- `POST /api/orders/{id}/confirm`: Sales
- `POST /api/orders/{id}/ship`: Warehouse
- `POST /api/orders/{id}/deliver`: Warehouse
- `GET /api/orders/summary`: Staff
- `GET /api/orders/{id}/payments`: Staff
- `GET /api/orders/{id}/payments/summary`: Staff
- `POST /api/orders/{id}/payments`: Sales

Payments (`/api/payments`)

- `GET /api/payments`: Sales
- `GET /api/payments/{id}`: Sales

Products (`/api/products`)

- `POST /api/products`: Sales
- `GET /api/products`: Authenticated
- `GET /api/products/{id}`: Authenticated
- `PUT /api/products/{id}`: Sales
- `POST /api/products/{id}/discontinue`: Sales
- `POST /api/products/{id}/reactivate`: Sales
- `GET /api/products/low-stock`: Staff
- `POST /api/products/{id}/restock`: Warehouse
- `POST /api/products/{id}/reserve`: Warehouse

Users (`/api/users`)

- `POST /api/users/staff`: AdminOnly

### 3.2 Migration

Có 7 migration trong `src/OrderFlow.Infrastructure/Persistence/Migrations/`, theo thứ tự thời gian:

1. InitialCreate (09/09/2026): tạo schema v1 gồm các bảng customers, products, orders, order_items và payment_attempts, cùng các CHECK constraint, unique index và rowversion.
2. UpdateOrderStatusLifecycle (13/09/2026): thay CHECK `ck_orders_status` theo vòng đời trạng thái mới.
3. AddProductReservedQuantity (13/09/2026): thêm cột `products.reserved_quantity` và hai CHECK: `reserved_quantity >= 0` và `reserved_quantity <= stock_quantity`.
4. AllowShippedOrderStatus (18/09/2026): mở rộng `ck_orders_status` để cho phép trạng thái Shipped.
5. AllowDeliveredOrderStatus (19/09/2026): mở rộng `ck_orders_status` để cho phép trạng thái Delivered.
6. AddCustomerStatus (23/09/2026): thêm cột `customers.status` và CHECK `ck_customers_status`.
7. AddAuthentication (03/10/2026): tạo bảng `users` (có FK tới customers) và bảng `email_verification_otps`, kèm unique index trên email.

---

## 4. Gap register

Mỗi gap ghi vấn đề, bằng chứng trong code, requirement xử lý (OF-xx), mức ưu tiên và trạng thái. Trạng thái Open nghĩa là chưa xử lý. Closed nghĩa là đã xử lý trong commit baseline này. Mức ưu tiên lấy theo file requirement.

### G-01. ProcessPayment commit hai lần, không có transaction

- OF-03 · P0 · Closed (29/10: bọc trong ITransactionRunner, xem mục 7 và ADR-006)
- Vấn đề: handler ghi payment và cập nhật order bằng hai lần commit riêng, không có transaction bao ngoài. Nếu lần commit thứ hai lỗi, payment `Succeeded` đã được lưu nhưng order vẫn ở `PendingPayment`, và reservation chưa được fulfill.
- Bằng chứng:
  - `ProcessPaymentCommandHandler.cs:66` gọi `_payments.AddAsync`, hàm này gọi `SaveChangesAsync` ngay tại `PaymentRepository.cs:22`.
  - Sau đó `ProcessPaymentCommandHandler.cs:98` gọi `_orders.UpdateAsync` và commit lần thứ hai.
  - `ITransactionRunner` đã có sẵn nhưng mới chỉ Register và ConfirmEmailOtp dùng.

### G-02. Chống payment trùng mới làm được một phần

- OF-05 · P0 · Open
- Vấn đề:
  - `ProviderTransactionId` cho phép NULL.
  - Unique index có filter `IS NOT NULL`, nên payment không có mã giao dịch thì không bị chặn trùng.
  - Hai request đồng thời có thể cùng ghi `Succeeded`.
- Bằng chứng: `PaymentAttemptConfiguration.cs:53-55` định nghĩa index có filter. `ProcessPaymentRequest.cs:8` khai báo trường này là `string?`.

### G-03. Không có integration test chạm SQL Server thật

- OF-02 · P0 · In progress (28/10: đã có `SqlServerFixture` và 3 test CHECK constraint cho products, xem mục 6)
- Vấn đề: chưa có fixture database, cũng chưa có thư mục test cho Orders và Products.
- Bằng chứng: `tests/OrderFlow.IntegrationTests/Security/ApiFactory.cs` dùng connection string không kết nối được. Thư mục integration chỉ có `Security/`.

### G-04. Optimistic concurrency chưa được kiểm chứng

- OF-04 · P0 · Closed (30/10: chứng minh không oversell, mục 8. 31/10: đo 4 phương án, viết ADR-007, mục 9; chuyển CreateOrder sang UPDLOCK, mục 10)
- Vấn đề: rowversion và việc map lỗi sang 409 mới chỉ có trong code. Chưa có test chạy đồng thời trên SQL Server thật, nên chưa chứng minh được hệ thống không oversell.
- Bằng chứng: `ProductConfiguration.cs:47` và `OrderConfiguration.cs:33` khai báo `IsRowVersion`. `GlobalExceptionHandler.cs:100` map `DbUpdateConcurrencyException` sang 409.

### G-05. CreateOrder / CancelOrder chưa có test rollback

- OF-02, OF-04 · P0 · Open
- Vấn đề: hai use case này nguyên tử nhờ chỉ gọi `SaveChanges` một lần (implicit transaction). Tuy vậy chưa có test rollback thật để chứng minh điều đó.
- Bằng chứng: handler của CreateOrder và CancelOrder. Muốn viết test cho gap này thì cần SQL fixture của G-03 trước.

### G-06. Rate limit chỉ theo IP

- OF-07 · P0 · Open
- Vấn đề: rate limit chỉ phân vùng theo IP: global 300 request/phút, auth 10 request/phút. Chưa có throttle theo account, nên nhiều IP có thể cùng dò một tài khoản.
- Bằng chứng: `RateLimitingExtensions.cs:24-25` đặt giới hạn, `RateLimitingExtensions.cs:71` phân vùng theo `RemoteIpAddress`.

### G-07. Validation pipeline không thống nhất

- OF-10 · P0 · Open
- Vấn đề: decorator validation mới chỉ đăng ký cho 4 command auth/user. Các command còn lại tự validate trong handler.
- Bằng chứng: `DependencyInjection.cs:53-56`. Ví dụ `ProcessPaymentCommandHandler.cs` tự ném `ValidationException`.

### G-08. Logging thưa, không có correlation ID

- OF-10 · P0 · Open
- Vấn đề: `ILogger` chỉ xuất hiện ở 3 file, nên không truy vết được một request qua các bước nghiệp vụ.
- Bằng chứng: `GlobalExceptionHandler.cs`, `BootstrapAdminExtensions.cs`, `LoggingEmailSender.cs`.

### G-09. Chưa có Docker và CI

- OF-11 · P0 · Open
- Vấn đề: chưa có Dockerfile hay docker-compose, cũng không có thư mục `.github/`.
- Bằng chứng: thư mục gốc repo.

### G-10. Các file tài liệu 04–08 đang rỗng

- OF-12 · P0 · Open
- Vấn đề: docs/04–08 đều là file 0 byte. Riêng file 06 đã được lấp bằng chính tài liệu này.
- Bằng chứng: `docs/04-database-design.md`, `docs/05-api-design.md`, `docs/07-security-performance.md`, `docs/08-deployment.md`.

### G-11. Reservation không hết hạn

- OF-13 · P1 · Open
- Vấn đề: đơn chưa thanh toán giữ hàng cho tới khi bị huỷ.
- Bằng chứng: `README.md:130`.

### G-12. Test scaffolding rỗng làm sai số test

- OF-01 · P0 · Closed (đã xoá)
- Vấn đề: có 2 test luôn pass mà không kiểm tra gì, cùng 1 class rỗng.
- Bằng chứng: `tests/OrderFlow.IntegrationTests/UnitTest1.cs`, `tests/OrderFlow.IntegrationTests/TestBase.cs`, `tests/OrderFlow.UnitTests/UnitTest1.cs`.

### G-13. File .http đã cũ và đặt sai chỗ

- OF-12 · P2 · Open
- Vấn đề: file `.http` dùng để thử API thủ công đã cũ, và có một bản bị đặt nhầm trong project Application.
- Bằng chứng: `src/OrderFlow.API/OrderFlow.API.http`, `src/OrderFlow.Application/OrderFlow.API.http`.

---

## 5. Thứ tự xử lý đề xuất

Làm G-03 (hạ tầng test với SQL Server thật) trước tiên, vì G-01, G-02, G-04 và G-05 đều cần nó để có bằng chứng. Sau đó làm G-01, rồi G-02, rồi G-04, theo lịch tuần 1.

---

## 6. Integration test với SQL Server thật (28/10/2026)

Từ ngày 2, test có thể chạy trên một SQL Server thật, đã áp đủ 7 migration của production. Nhờ vậy test kiểm được những thứ mà fake repository hay EF InMemory không kiểm được: CHECK constraint, unique index, foreign key, rowversion và transaction.

### Cách hoạt động

Fixture nằm ở `tests/OrderFlow.IntegrationTests/Fixtures/SqlServerFixture.cs` và chọn database theo thứ tự sau:

1. Nếu có biến môi trường `ORDERFLOW_TEST_CONNECTION`, fixture dùng chuỗi kết nối đó. Đây là cách chạy trên máy chưa có Docker, ví dụ dùng LocalDB.
2. Nếu không có biến đó, fixture dùng Testcontainers để chạy một container `mcr.microsoft.com/mssql/server:2022-latest`. Cách này dùng khi đã cài Docker và khi chạy CI.

Sau khi kết nối, fixture gọi `Database.MigrateAsync()` để dựng schema bằng chính các migration thật, không dùng `EnsureCreated`.

Dữ liệu được reset trước mỗi test bằng Respawn. Respawn xoá mọi dòng theo đúng thứ tự foreign key, nhưng giữ nguyên schema và bảng `__EFMigrationsHistory`.

Vì Respawn xoá sạch dữ liệu, fixture từ chối chạy nếu tên database trong `ORDERFLOW_TEST_CONNECTION` không chứa chữ "Test". Như vậy không bao giờ xoá nhầm database dev `OrderFlow`.

Mọi class test cần database đều gắn `[Collection("SqlServer")]`. Các class này dùng chung một fixture và chạy tuần tự, nên không giẫm lên dữ liệu của nhau.

### Cách chạy trên máy không có Docker (LocalDB)

PowerShell:

```powershell
$env:ORDERFLOW_TEST_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Database=OrderFlow_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test OrderFlow.sln
```

Database `OrderFlow_IntegrationTests` sẽ được tự tạo ở lần chạy đầu tiên. Nếu không đặt biến này và cũng không có Docker, 3 test database sẽ fail với thông báo hướng dẫn hai cách trên. Các test khác vẫn chạy bình thường.

### Test đã có

File `tests/OrderFlow.IntegrationTests/Products/ProductCheckConstraintTests.cs` có 3 test. Các test này insert bằng SQL thô, vì `Product.Create` và `Product.Reserve` đã chặn các giá trị sai ở tầng domain. Mục đích là chứng minh database là lớp phòng thủ cuối cùng, kể cả khi code ứng dụng bị bỏ qua.

- Insert product hợp lệ thì thành công. Đây là test đối chứng, để chắc rằng hai test sau fail vì constraint chứ không phải vì câu SQL sai.
- Insert product có `stock_quantity = -1` thì SQL Server từ chối: lỗi 547, constraint `ck_products_stock_quantity`.
- Insert product có `reserved_quantity` (6) lớn hơn `stock_quantity` (5) thì SQL Server từ chối: lỗi 547, constraint `ck_products_reserved_not_exceed_stock`.

### Kết quả đo 28/10/2026

Chạy với LocalDB, kết quả là 312 test, tất cả pass:

- Unit: 255 pass.
- Integration: 57 pass, gồm 54 test security cũ và 3 test mới.

Chạy lại lần thứ hai vẫn xanh. Điều đó cho thấy Respawn reset đúng dữ liệu và migration không bị áp lại.

### Việc còn lại của G-03 / OF-02

- Ghi ADR-009 về lựa chọn Testcontainers và LocalDB.
- Viết test unique SKU trùng thì API trả 409.
- Ghi chú ít nhất 2 lỗi mà fake repository không phát hiện được.
- Chạy được trong CI. Phần này phụ thuộc OF-11.

---

## 7. Transaction và fault injection (29/10/2026)

Mục này đóng gap G-01. Cách làm: cố tình làm một lần ghi bị lỗi rồi xem dữ liệu còn lại trong database.

### Công cụ

File `tests/OrderFlow.IntegrationTests/Fixtures/FaultInjection.cs` có hai interceptor của EF Core:

- `FailOnSaveChangesInterceptor(n)` làm lần gọi SaveChanges thứ n bị lỗi, trước khi gửi SQL. Nó mô phỏng việc lần ghi thứ hai của một use case bị hỏng giữa chừng.
- `FailOnCommandInterceptor("...")` làm lỗi lệnh SQL đầu tiên có chứa đoạn text cho trước. Các lệnh trước đó trong cùng lần SaveChanges đã chạy rồi, nên đây là lỗi xảy ra giữa một lần lưu.

`SqlServerFixture.CreateServiceProvider` dựng DI bằng đúng `AddApplication()` và `AddInfrastructure()` của production. Vì vậy handler trong test là handler thật, dùng repository thật và `ITransactionRunner` thật. Hàm này cũng ghi log SQL và log transaction ra output của test.

### Test và kết quả

ProcessPaymentAtomicityTests có 2 test.

- Happy path: thanh toán đủ tiền thì order thành Paid, stock giảm 2, reserved về 0.
- Lỗi ở lần lưu thứ 2: không được còn payment nào. Order vẫn PendingPayment, stock vẫn 10, reserved vẫn 2.

Trước khi sửa, test lỗi chạy đỏ, với message: "Expected the payment to roll back with the failed order update, but found 1 payment row(s) while the order is still PendingPayment." Đây chính là bằng chứng của G-01 trên SQL Server thật.

Sau khi bọc handler trong `ITransactionRunner`, test chuyển sang xanh. Log SQL của test lỗi cho thấy thứ tự: Began transaction, INSERT INTO payment_attempts, rồi Disposing transaction mà không có Committed. Transaction bị huỷ mà không commit thì SQL Server rollback. Ở happy path, log có thêm UPDATE orders, UPDATE products và Committed transaction.

CreateOrderRollbackTests có 1 test. Test làm lỗi lệnh INSERT INTO order_items. Lúc đó INSERT INTO orders và UPDATE products (giữ chỗ) đã chạy xong. Kết quả: database không còn order, không còn order item, reserved vẫn 0. Test này xanh ngay mà không cần sửa code. Nó chứng minh implicit transaction của một lần SaveChanges là đủ khi use case chỉ lưu một lần.

Kết luận, đây cũng là câu trả lời cho câu phỏng vấn "implicit transaction đủ khi nào": một lần SaveChanges thì đủ; từ hai lần trở lên thì phải có transaction bao ngoài. Chi tiết xem ADR-006.

### Số test đo 29/10/2026

Chạy với LocalDB, kết quả là 315 test, tất cả pass. Chạy lại lần hai vẫn xanh.

- Unit: 255 pass. 9 test cũ của ProcessPayment vẫn xanh, sau khi truyền thêm FakeTransactionRunner.
- Integration: 60 pass, gồm 57 test cũ và 3 test mới.

---

## 8. Concurrency: nhiều người cùng mua món cuối cùng (30/10/2026)

Mục này là phần 1 của G-04 / OF-04. Câu hỏi cần trả lời: khi nhiều request CreateOrder cùng lúc tranh nhau một product chỉ còn 1 cái, hệ thống có bán quá số hàng không?

### Cách test

File `tests/OrderFlow.IntegrationTests/Orders/CreateOrderConcurrencyTests.cs`.

- Seed 1 customer và 1 product có `stock_quantity = 1`.
- Dựng API thật bằng `SqlApiFactory`, nối vào SQL Server của test. Mỗi request HTTP có DI scope riêng, nên có DbContext riêng, giống như các request thật từ nhiều người dùng.
- Bắn N lệnh `POST /api/orders` cùng lúc, với N = 10 và N = 50, mỗi lệnh mua 1 cái. Mọi request cùng chờ một "cổng xuất phát" (TaskCompletionSource) rồi mới được thả ra một lượt, để chúng thật sự chồng lên nhau trong database.
- Kiểm tra:
  - Đúng 1 response 201, N − 1 response 409, không có response nào khác (ví dụ 500 do deadlock).
  - Database có đúng 1 order, `reserved_quantity = 1` và `reserved_quantity <= stock_quantity`.

Rate limit global 300 request/phút theo IP được nâng lên trong factory này. Mọi request của test đều đến từ cùng một "IP", nên nếu không nâng thì rate limit sẽ làm sai kết quả.

### Kết quả đo thật: chạy lặp 20 lần

Cả 20 lần đều pass. Không lần nào bán quá, không lần nào có response 500.

Với N = 10, cả 20 lần đều cho cùng kết quả: 1 request thành công, 9 request nhận 409 "Concurrent modification", 0 request nhận 409 "Insufficient stock".

Với N = 50, cả 20 lần đều có 1 request thành công. 49 request còn lại chia thành hai loại 409:
- "Concurrent modification": ít nhất 10, nhiều nhất 19, trung bình 13,5.
- "Insufficient stock": phần còn lại, từ 30 đến 39.

Số "Concurrent modification" ở 20 lần chạy N = 50 lần lượt là: 13, 14, 16, 12, 13, 13, 19, 10, 13, 16, 13, 11, 13, 14, 12, 14, 13, 12, 13, 16.

### Vì sao có hai loại 409

Mỗi request CreateOrder đọc product, gọi `Reserve(1)` trong bộ nhớ, rồi SaveChanges. Lệnh SaveChanges sinh ra câu `UPDATE products ... WHERE id = @id AND row_version = @old`.

"Concurrent modification" xảy ra khi request đọc product trước lúc người thắng commit. Lúc đó request vẫn thấy còn 1 cái, nên `Reserve` thành công trong bộ nhớ. Nhưng khi UPDATE, `row_version` đã đổi, câu lệnh cập nhật 0 dòng, và EF ném `DbUpdateConcurrencyException`. Rowversion là thứ chặn được oversell ở đây.

"Insufficient stock" xảy ra khi request đọc product sau lúc người thắng commit. Lúc đó request thấy `reserved_quantity = 1`, nên `Product.Reserve` từ chối ngay trong domain mà không cần ghi gì.

Với N = 10, cả 10 request đều kịp đọc trước khi có ai commit, nên toàn bộ 9 lỗi đều do rowversion chặn. Với N = 50, các request đến muộn đọc phải dữ liệu đã commit, nên phần lớn bị domain chặn.

Cả hai trường hợp đều trả 409 và không ghi sai dữ liệu. Điều này cho thấy có hai lớp bảo vệ: kiểm tra trong domain cho dữ liệu đã thấy, và rowversion cho cuộc đua giữa lúc đọc và lúc ghi. Nếu chỉ có lớp domain thì 9 request của N = 10 đều đã đặt được hàng, tức là bán quá.

### Số test đo 30/10/2026

Chạy với LocalDB, kết quả là 317 test, tất cả pass.

- Unit: 255.
- Integration: 62, gồm 60 test cũ và 2 case mới (N = 10 và N = 50).

### Việc còn lại của G-04 (Ngày 5)

- So sánh rowversion với hai cách khác: conditional UPDATE (`UPDATE ... SET reserved = reserved + 1 WHERE stock - reserved >= 1`) và khoá bi quan `UPDLOCK`.
- Quyết định có tự retry khi gặp "Concurrent modification" hay không. Hiện tại người dùng nhận 409 dù lúc đó có thể vẫn còn hàng (trường hợp tồn kho lớn hơn 1).

---

## 9. So sánh các cách chống oversell (31/10/2026)

Mục này là phần 2 của G-04 / OF-04. Mục 8 đã chứng minh rowversion không bán quá. Mục này trả lời câu hỏi tiếp theo: các cách khác thì sao, và rowversion có từ chối nhầm người mua trong lúc vẫn còn hàng không?

### Cách thử

Bốn cách làm cùng một bước "giữ chỗ 1 sản phẩm và tạo order" được cài trong project test (`tests/OrderFlow.IntegrationTests/Orders/ReservationStrategies.cs`). Code production không đổi.

1. RowVersion: giống production hiện tại. Đọc product, gọi `Product.Reserve`, lưu, để rowversion chặn lần ghi dựa trên dữ liệu cũ.
2. RowVersionRetry3: giống RowVersion, nhưng khi thua cuộc đua rowversion thì đọc lại và thử thêm, tối đa 3 lần. Lỗi hết hàng thật thì không thử lại.
3. ConditionalUpdate: một câu `UPDATE products SET reserved_quantity = reserved_quantity + 1 WHERE id = @id AND stock_quantity - reserved_quantity >= 1`. Cập nhật 0 dòng nghĩa là hết hàng.
4. UpdLock: trong transaction, đọc product bằng `WITH (UPDLOCK, ROWLOCK)` rồi mới gọi `Product.Reserve` và lưu. Người khác phải chờ khoá.

File `ReservationStrategyComparisonTests.cs` cho cả 4 cách chạy qua 3 kịch bản, tổng cộng 12 case:
- Tồn kho 1, N = 10.
- Tồn kho 1, N = 50.
- Tồn kho 10, N = 50.

Mỗi case chạy một vòng làm nóng trước, sau đó mới chạy vòng đo thời gian. Assert chung cho mọi cách: không bán quá tồn kho, số order bằng số giữ chỗ thành công, và `reserved_quantity <= stock_quantity`. Riêng ConditionalUpdate và UpdLock còn phải bán đúng min(tồn kho, N).

### Kết quả đo thật: chạy lặp 20 lần, cả 20 lần pass

Thời gian ghi dưới đây là trung vị của 20 lần, tính cho cả N request chạy xong.

Tồn kho 1, N = 10: cả 4 cách đều bán đúng 1 cái ở mọi lần chạy.
- RowVersion: 4 ms.
- RowVersionRetry3: 6 ms.
- ConditionalUpdate: 4,5 ms.
- UpdLock: 6 ms.

Tồn kho 1, N = 50: cả 4 cách đều bán đúng 1 cái ở mọi lần chạy.
- RowVersion: 22 ms.
- RowVersionRetry3: 33,5 ms.
- ConditionalUpdate: 26 ms.
- UpdLock: 21 ms.

Tồn kho 10, N = 50: các cách khác nhau rõ rệt.
- RowVersion chỉ bán được 1 đến 3 cái, trung bình 1,4, mất 24 ms. Khoảng 48 người nhận 409 dù kho vẫn còn 8 đến 9 cái.
- RowVersionRetry3 bán được 7 đến 9 cái, trung bình 8,0, và chưa lần nào bán đủ 10. Mất 125 ms, chậm gấp 4 đến 5 lần các cách khác.
- ConditionalUpdate bán đúng 10 ở cả 20 lần, mất 29 ms.
- UpdLock bán đúng 10 ở cả 20 lần, mất 31 ms.

Không cách nào bán quá. Không lần nào có deadlock hay timeout.

Độ dài đoạn code giữ chỗ trong bài thử:
- RowVersion: 11 dòng.
- RowVersionRetry3: 20 dòng.
- ConditionalUpdate: 24 dòng. Quy tắc tồn kho nằm trong câu SQL, không đi qua `Product.Reserve`.
- UpdLock: 17 dòng. Vẫn dùng `Product.Reserve`.

### Rút ra

Rowversion là optimistic concurrency. Nó chỉ tốt khi xung đột hiếm. Khi 50 người tranh nhau 10 món, gần như ai cũng đọc cùng một phiên bản của dòng product. Một người ghi thắng, những người còn lại đều thất bại, dù kho vẫn còn hàng. Đây là xung đột giả.

Retry chỉ chữa triệu chứng. Mỗi vòng retry lại là một cuộc đua giống hệt, nên 3 lần retry vẫn để sót trung bình 2 món, và thời gian tăng gấp 4 đến 5 lần.

ConditionalUpdate và UpdLock bỏ hẳn cuộc đua: người mua xếp hàng chờ khoá của dòng product, rồi quyết định dựa trên số liệu mới nhất. Vì vậy số giữ chỗ thành công luôn đúng bằng min(tồn kho, N), và thời gian chỉ chênh vài ms so với RowVersion.

Quyết định được ghi trong ADR-007:
- Giữ rowversion làm lưới an toàn cho mọi lệnh ghi khác.
- Không thêm retry.
- Đề xuất (Proposed, chưa làm) chuyển bước giữ chỗ của CreateOrder sang UPDLOCK trong ITransactionRunner, khoá các product theo thứ tự id tăng dần để tránh deadlock.

### Số test đo 31/10/2026

Chạy với LocalDB, kết quả là 329 test, tất cả pass.

- Unit: 255.
- Integration: 74, gồm 62 test cũ và 12 case so sánh mới.

---

## 10. CreateOrder dùng UPDLOCK (31/10/2026)

Mục này thực hiện quyết định của ADR-007 và đóng G-04.

### Thay đổi

- `IProductRepository` có thêm `GetByIdsWithUpdateLockAsync`. `ProductRepository` chạy câu `SELECT * FROM products WITH (UPDLOCK, ROWLOCK) WHERE id IN (...)`, mỗi id là một tham số SQL, id được loại trùng và sắp xếp tăng dần.
- `CreateOrderCommandHandler` nhận thêm `ITransactionRunner`. Phần validate input vẫn ở ngoài. Từ bước kiểm tra customer, đọc product có khoá, kiểm tra product, `Product.Reserve`, tới lúc tạo và lưu order đều chạy trong một transaction.
- Unit test: 11 fake của `IProductRepository` được thêm method mới. Fake của CreateOrder trả dữ liệu giống `GetByIdsAsync`, và `HandlerFor` truyền thêm `FakeTransactionRunner`. 255 unit test vẫn xanh.

Vì sao phải sắp xếp id tăng dần: nếu hai order cùng mua sản phẩm A và B nhưng một bên khoá A trước, bên kia khoá B trước, thì mỗi bên giữ một dòng và chờ dòng còn lại. Đó là deadlock. Khoá theo cùng một thứ tự thì không thể xảy ra vòng chờ như vậy.

Vì sao không viết `FromSql(...).Where(...)`: EF sẽ bọc câu SQL thành subquery. Khi đó câu bên trong có hint UPDLOCK nhưng không có điều kiện id, nên SQL Server sẽ khoá mọi dòng nó quét qua chứ không chỉ các product cần mua.

### Bằng chứng

Log SQL của `CreateOrderRollbackTests` cho thấy thứ tự: Began transaction, SELECT ... WITH (UPDLOCK, ROWLOCK), INSERT INTO orders, UPDATE products, rồi lỗi được cố tình gây ra và Disposing transaction mà không commit. Database không còn gì, test vẫn xanh.

`CreateOrderConcurrencyTests` chạy qua HTTP thật, giờ có 3 case. Mình chạy lặp 20 lần, cả 20 lần pass, và kết quả lần nào cũng giống hệt nhau:
- Tồn kho 1, N = 10: 1 order thành công, 9 lỗi 409 "Insufficient stock", 0 lỗi "Concurrent modification".
- Tồn kho 1, N = 50: 1 thành công, 49 lỗi "Insufficient stock", 0 lỗi "Concurrent modification".
- Tồn kho 10, N = 50: 10 thành công, 40 lỗi "Insufficient stock", 0 lỗi "Concurrent modification".

So với trước khi đổi (mục 8 và mục 9):
- Không còn lỗi "Concurrent modification" ở luồng này, vì người mua xếp hàng chờ khoá thay vì đua nhau ghi. Test assert con số này luôn bằng 0.
- Với tồn kho 10, hệ thống bán đủ 10 thay vì trung bình 1,4. Người mua chỉ bị từ chối khi kho thật sự hết hàng.

Về thời gian: mỗi case HTTP mất khoảng 0,1 đến 0,4 giây, nhưng con số đó gồm cả thời gian dựng API test, nên không dùng để so sánh. Số so sánh công bằng giữa các phương án là bài đo ở mục 9: UpdLock có trung vị 31 ms với tồn kho 10 và N = 50.

### Số test đo 31/10/2026

Chạy với LocalDB, kết quả là 330 test, tất cả pass.

- Unit: 255.
- Integration: 75, gồm 74 test cũ và 1 case HTTP mới (tồn kho 10, N = 50).
