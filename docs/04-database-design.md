# OrderFlow — Thiết kế database và tối ưu truy vấn

Tài liệu này ghi index, lý do và số đo thật (OF-08). Toàn bộ số đo chạy ngày 05/11/2026 trên LocalDB, trên một máy. Chúng dùng để so sánh trước và sau, không phải hiệu năng production.

## 1. Bảng chính

- customers, products: dữ liệu gốc.
- orders, order_items: đơn hàng và các dòng hàng. `subtotal` là cột tính sẵn (persisted).
- payment_attempts: mỗi lần thanh toán. Chỉ `Succeeded` mới tính là tiền đã thu.
- users, email_verification_otps, login_throttles: xác thực.
- sim.provider_transactions: sổ giao dịch giả lập của nhà cung cấp, dùng cho đối soát.

Ràng buộc dữ liệu nằm ở database: CHECK (số lượng, trạng thái, `reserved_quantity <= stock_quantity`), unique index (email, sku, mã giao dịch), và khoá ngoại.

## 2. Khái niệm index

Clustered index là chính bảng, sắp xếp theo khoá. Mỗi bảng có một cái, ở đây là khoá chính `id`.

Non-clustered index là cấu trúc riêng, chứa cột khoá và con trỏ về dòng. Nếu query cần cột không có trong index, SQL Server phải quay lại bảng lấy thêm, đó là Key Lookup.

Composite index có nhiều cột, và thứ tự cột quan trọng. Index `(a, b)` giống danh bạ xếp theo họ rồi tên: tìm được theo `a`, theo `a` và `b`, nhưng không seek được nếu chỉ có `b`. Thường đặt cột lọc bằng `=` trước, cột lọc theo khoảng sau.

Covering index chứa đủ mọi cột query cần, nên không cần Key Lookup. `INCLUDE` thêm cột vào tầng lá mà không làm khoá to ra.

Filtered index chỉ chứa các dòng thoả điều kiện `WHERE`, nên nhỏ và rẻ khi ghi. Query phải nêu đúng điều kiện đó bằng literal. Nếu trạng thái truyền bằng tham số, SQL Server không dùng được filtered index, vì lúc biên dịch chưa biết giá trị. Vì thế `ReportRepository` viết thẳng `status = N'Succeeded'`.

Index Seek đi thẳng tới vị trí cần tìm. Index Scan đọc toàn bộ index. Scan không phải lúc nào cũng xấu: với bảng nhỏ hoặc khi cần gần hết dữ liệu, scan là đúng.

Sargable nghĩa là điều kiện dùng được index để seek: cột phải đứng nguyên, không bị bọc trong hàm hay phép tính.

Quy trình tối ưu query chậm: đo → xem plan → sửa query hoặc thêm index → đo lại. Không thêm index theo cảm tính.

## 3. Dataset đo

`db/scripts/seed-100k.sql` tạo database đo `OrderFlow_Perf`: 1.000 customers, 1.000 products, 100.000 orders, 300.000 order_items, 102.121 payment_attempts (92.121 Succeeded, 10.000 Failed). Có 5.000 đơn PendingPayment. Dữ liệu sinh bằng công thức nên chạy lại ra đúng như cũ. Script từ chối chạy trên database không có chữ "Perf" trong tên.

Cách đo: `SET STATISTICS IO` cho logical reads, `SET STATISTICS TIME` cho thời gian. Mỗi query chạy 6 lần, bỏ lần đầu, lấy trung vị của 5 lần sau. Logical reads ổn định và là số nên tin nhất. Thời gian dao động giữa các lần đo, nên chỉ xem như độ lớn.

## 4. Kết quả từng báo cáo

Query nằm ở `db/scripts/reports/`. Plan đầy đủ xem bằng cách mở file trong SSMS, bật Include Actual Execution Plan (Ctrl+M) rồi chạy. Tài liệu này ghi text plan từ `SET SHOWPLAN_TEXT`.

### Đơn PendingPayment quá hạn

Index: `ix_orders_pending_created_at`, khoá `created_at`, INCLUDE `customer_id, total_amount`, filter `status = N'PendingPayment'`.

Trước: 1.046 logical reads, khoảng 17 ms. Plan: Clustered Index Scan toàn bảng, rồi Sort, rồi Top.
Sau: 2 logical reads, dưới 1 ms. Plan: Index Seek rồi Top, không còn Sort.

Lý do: chỉ 5% đơn đang chờ thanh toán, nên index chỉ có 22 trang so với 1.046 trang của bảng. Khoá `created_at` đã sắp xếp sẵn, nên query dừng ngay khi đủ 100 dòng. Actual rows của seek là 100 vì Top dừng sớm, trong khi có tất cả 4.776 dòng khớp điều kiện.

### Doanh thu theo tháng

Index: `ix_payment_attempts_succeeded_created_at`, khoá `created_at`, INCLUDE `amount`, filter `status = N'Succeeded'`.

Trước: 1.408 logical reads, khoảng 38 ms. Plan: Clustered Index Scan.
Sau: 143 logical reads, khoảng 28 ms. Plan: Index Seek.

Reads giảm gần 10 lần, nhưng thời gian giảm ít. Dữ liệu đã nằm sẵn trong RAM nên đọc trang rất rẻ, và phần lớn thời gian nằm ở việc gom nhóm 41.773 dòng (Hash Match Aggregate). Khi dữ liệu không còn trong cache, giảm reads sẽ có tác động lớn hơn. Lần đo này không kiểm tra trường hợp đó.

Estimated và actual rows: seek này ước tính 16.780 dòng nhưng thực tế là 41.773 dòng. Script dùng biến cục bộ (`DECLARE @from`), nên lúc biên dịch SQL Server chưa biết giá trị và phải đoán theo tỉ lệ cố định. Lệch nhiều như vậy có thể làm optimizer chọn sai plan. Ở đây plan vẫn đúng.

### Top khách theo tiền đã trả

Index: `ix_payment_attempts_succeeded_order_id`, khoá `order_id`, INCLUDE `amount`, filter `status = N'Succeeded'`.

Trước: 1.776 logical reads (payment 1.408, orders 349, customers 19), khoảng 119 ms. Plan: 2 lần scan, Hash Join.
Sau: 1.329 logical reads (orders 1.046, payment 264, customers 19), khoảng 51 ms ở lần đo cuối, 84 ms ở lần đo trước. Plan: Merge Join, vì cả hai bên đã sắp theo order id.

Đây là index có lợi ít nhất cho báo cáo. Report này duyệt hết tiền đã thu, nên không thể seek. Reads của orders còn tăng từ 349 lên 1.046, vì Merge Join đọc bảng thay vì index hẹp `ix_orders_customer_id`. Tổng reads vẫn giảm, và bỏ được Hash Join tốn CPU.

Lý do chính để giữ index này là luồng thanh toán: `GetSucceededAmountAsync` chạy trong mỗi lần ProcessPayment, khi đang giữ khoá order. Câu tính tổng cho một order giảm từ 5 reads (Index Seek cộng Key Lookup) xuống 2 reads (Index Seek, không Key Lookup).

### Sản phẩm sắp hết

Không thêm index. 19 logical reads, dưới 1 ms, Clustered Index Scan.

Lý do: bảng chỉ có 1.000 dòng, và `stock_quantity - reserved_quantity` là biểu thức nên không seek được. Muốn seek thì phải thêm cột tính sẵn và index, tức là tăng chi phí cho mọi lần giữ chỗ hàng, để tiết kiệm một việc vốn đã rất rẻ. Nếu catalog lớn lên nhiều thì xem lại.

## 5. Chứng minh sargability

Hai query cho cùng kết quả, chạy trên cùng index `ix_payment_attempts_succeeded_created_at`:
- `YEAR(created_at) = 2026`: 311 reads, khoảng 22 ms, Index Scan.
- `created_at >= '2026-01-01' AND created_at < '2027-01-01'`: 143 reads, khoảng 9 ms, Index Seek.

Bọc cột trong hàm `YEAR()` thì SQL Server phải tính hàm cho từng dòng, nên phải đọc hết index.

### Vì sao LIKE '%x%' không seek được

Index sắp xếp theo thứ tự từ đầu chuỗi, giống từ điển. Tìm từ bắt đầu bằng "SKU-0012" thì mở thẳng tới đúng chỗ. Tìm từ có chứa "123" ở bất kỳ đâu thì không biết bắt đầu từ đâu, phải đọc tất cả.

Đo trên `ix_products_sku`:
- `sku LIKE '%123%'`: Index Scan, 8 reads.
- `sku LIKE 'SKU-0012%'`: Index Seek, 2 reads.

Bảng products nhỏ nên chênh lệch chỉ vài trang, nhưng khác biệt ở plan là rõ. Ô tìm kiếm `GET /api/products?search=` dùng `LIKE '%...%'`, nên trên bảng lớn sẽ là scan. Khi đó cần full-text search.

## 6. Cái giá của index

Mỗi index tốn dung lượng và làm chậm mọi lần ghi vào cột liên quan. Lần đo này chưa đo tác động khi ghi. Kích thước trên dataset đo:
- `ix_orders_pending_created_at`: 22 trang, 5.000 dòng.
- `ix_payment_attempts_succeeded_created_at`: 311 trang, 92.121 dòng.
- `ix_payment_attempts_succeeded_order_id`: 264 trang, 92.121 dòng.

Một điều đã gặp khi làm: khi thêm index có filter trên `order_id`, EF tự xoá index khoá ngoại `ix_payment_attempts_order_id`, vì coi index mới đã thay thế nó. Index có filter không chứa dòng Failed, nên không dùng được cho "mọi payment của một order". Migration tự sinh phải được đọc lại trước khi áp. Giải pháp là khai báo rõ index thường trong `PaymentAttemptConfiguration`.

## 7. Cách chạy lại

```bash
dotnet ef database update --project src/OrderFlow.Infrastructure --connection "Server=(localdb)\MSSQLLocalDB;Database=OrderFlow_Perf;Trusted_Connection=True;TrustServerCertificate=True"
```

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -d OrderFlow_Perf -I -b -i db/scripts/seed-100k.sql
```

Muốn đo lại trạng thái "trước index": chạy `dotnet ef database update AddLoginThrottles` với cùng `--connection`, rồi `update` lại sau khi đo.
