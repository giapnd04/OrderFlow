# INC-001: Khách đã bị trừ tiền nhưng đơn vẫn PendingPayment

Đây là bài diễn tập trên dữ liệu giả lập, không phải sự cố xảy ra trên hệ thống thật. OrderFlow là project cá nhân, chưa có khách hàng thật và chưa nối với cổng thanh toán thật. Mọi trạng thái lỗi được tái hiện bằng test trong `tests/OrderFlow.IntegrationTests/Payments/PaymentReconciliationTests.cs`. Vì vậy tài liệu này không có số liệu về số khách hay số tiền bị ảnh hưởng.

Trạng thái: đã có công cụ xử lý và đã kiểm chứng bằng test (03/11/2026).

## Triệu chứng

Khách báo đã bị trừ tiền, nhưng đơn vẫn hiển thị chưa thanh toán. Hệ thống không báo lỗi gì ở thời điểm đó.

## 1. Xác định phạm vi

Câu hỏi đầu tiên là: có bao nhiêu đơn đang ở tình trạng này? Câu SQL dưới đây so sánh số tiền nhà cung cấp đã thu với số tiền OrderFlow đã ghi, cho các đơn còn PendingPayment. Câu này chỉ đọc dữ liệu, nên chạy được ngay mà không ảnh hưởng gì.

```sql
SELECT o.id AS order_id,
       o.status,
       o.total_amount,
       COALESCE(SUM(t.amount), 0) AS provider_collected,
       (SELECT COALESCE(SUM(p.amount), 0)
        FROM payment_attempts p
        WHERE p.order_id = o.id AND p.status = N'Succeeded') AS recorded
FROM orders o
JOIN sim.provider_transactions t ON t.order_id = o.id
WHERE o.status = N'PendingPayment'
GROUP BY o.id, o.status, o.total_amount;
```

Đơn bị ảnh hưởng có `provider_collected` lớn hơn hoặc bằng `total_amount` mà trạng thái vẫn PendingPayment.

Có hai dạng:
- `recorded` nhỏ hơn số đã thu: OrderFlow chưa từng biết về khoản tiền này, ví dụ callback bị mất.
- `recorded` đã đủ: tiền đã ghi nhưng order chưa được cập nhật. Đây là dấu vết của lỗi G-01.

Trong bảng `sim.provider_transactions`, nguồn sự thật về việc tiền đã thu hay chưa là nhà cung cấp, không phải database của OrderFlow.

## 2. Ảnh hưởng

- Khách đã mất tiền nhưng hàng không được xử lý: order không qua được bước xác nhận và giao hàng.
- Hàng vẫn đang giữ chỗ (`reserved_quantity`), nên không bị bán cho người khác, nhưng tồn kho cũng chưa được trừ.
- Khách có thể thanh toán lại, và bị trừ tiền lần hai.

## 3. Bảo vệ khách hàng trước, sửa code sau

Trước khi tìm nguyên nhân:
- Không huỷ các đơn này, và không để ai huỷ tay. Huỷ đơn sẽ trả hàng về kho trong khi khách đã trả tiền.
- Không yêu cầu khách thanh toán lại. Trả lời khách rằng tiền đã được ghi nhận và đơn đang được xử lý.
- Không sửa dữ liệu bằng tay trực tiếp trên database. Sửa tay sẽ bỏ qua các quy tắc về tồn kho và số tiền.

## 4. Nguyên nhân gốc

Hai nguyên nhân dẫn tới cùng một triệu chứng:

Lỗi G-01, đã sửa ngày 29/10/2026. `ProcessPaymentCommandHandler` ghi payment và cập nhật order bằng hai lần commit riêng. Nếu lần thứ hai lỗi, payment đã lưu nhưng order vẫn PendingPayment. Test fault injection đã tái hiện được trên SQL Server thật, và lỗi này đã được sửa bằng transaction (ADR-006). Tuy nhiên, sửa code không chữa được dữ liệu đã sai từ trước.

Callback bị mất. Nhà cung cấp đã thu tiền nhưng callback không tới được OrderFlow, do mạng hoặc do API đang ngừng. Không có code nào của OrderFlow sai trong trường hợp này. Vì thế luôn cần một cơ chế đối soát, chứ không thể chỉ dựa vào callback.

## 5. Xử lý

Endpoint chỉ dành cho Administrator: `POST /api/admin/reconciliation/payments`.

Với mỗi giao dịch nhà cung cấp đã thu, nó gọi lại đúng `ProcessPaymentCommandHandler`. Không có đường ghi tiền thứ hai, nên mọi quy tắc đều được áp dụng: khoá order, replay, chặn ghi vượt, transaction.
- Chưa có payment: ghi payment. Nếu đủ tiền thì chuyển order sang Paid và trừ tồn kho.
- Đã có payment nhưng order kẹt ở PendingPayment: hoàn tất order, không thêm dòng payment.
- Đã khớp: không làm gì.

Các trường hợp sau không tự sửa, mà đưa vào danh sách `needsReview` để người xem xét:
- Order đã bị huỷ mà nhà cung cấp đã thu tiền: khách cần được hoàn tiền. Hệ thống chưa có luồng hoàn tiền.
- Số tiền của nhà cung cấp khác với payment đã ghi.
- Ghi vào sẽ vượt tổng tiền của order.
- Giao dịch trỏ tới order không tồn tại.

Một giao dịch lỗi không làm dừng cả lô. Mỗi giao dịch chạy trong DbContext riêng, nên lỗi của giao dịch này không ảnh hưởng giao dịch sau.

## 6. Kiểm chứng

`PaymentReconciliationTests` có 7 test trên SQL Server, qua API thật. Chạy lặp 20 lần, cả 20 lần pass.

- Nhà cung cấp đã thu, chưa có payment: sau reconcile có 1 payment, order Paid, tồn kho giảm đúng. Chạy lần 2: báo cáo 0 payment mới, 0 order hoàn tất, dữ liệu giữ nguyên.
- Trạng thái của lỗi G-01: order chuyển sang Paid và vẫn chỉ có 1 dòng payment.
- Order đã huỷ, cùng một giao dịch lành: giao dịch lỗi được báo cáo và không đổi dữ liệu, giao dịch lành vẫn được xử lý.
- Số tiền lệch và order không tồn tại: được báo cáo, không đổi dữ liệu.
- Token Sales nhận 403, không có token nhận 401.

Sau khi chạy reconcile, chạy lại câu SQL ở mục 1: các đơn đã sửa không còn xuất hiện. Các đơn còn lại phải có mặt trong `needsReview`.

## 7. Phòng ngừa

Đã làm:
- ProcessPayment chạy trong một transaction (ADR-006).
- Replay và khoá order khi thanh toán (ADR-008). Callback gửi lại cũng tự hoàn tất được order bị kẹt.
- CHECK constraint bắt buộc có mã giao dịch khi Succeeded.

Chưa làm:
- Chạy reconcile tự động theo lịch. Hiện tại phải có người gọi endpoint.
- Đối soát chiều ngược lại: OrderFlow có payment Succeeded mà nhà cung cấp không có giao dịch. Endpoint hiện tại không phát hiện trường hợp này.
- Cảnh báo tự động khi có đơn bị lệch. Log có cấu trúc thuộc OF-10.
- Luồng hoàn tiền cho order đã huỷ. Thuộc OF-17.
- Ghi audit log cho mỗi lần reconcile. Hiện tại kết quả chỉ nằm trong response.
