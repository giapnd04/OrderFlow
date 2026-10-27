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

- OF-03 · P0 · Open
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

- OF-02 · P0 · Open
- Vấn đề: chưa có fixture database, cũng chưa có thư mục test cho Orders và Products.
- Bằng chứng: `tests/OrderFlow.IntegrationTests/Security/ApiFactory.cs` dùng connection string không kết nối được. Thư mục integration chỉ có `Security/`.

### G-04. Optimistic concurrency chưa được kiểm chứng

- OF-04 · P0 · Open
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
