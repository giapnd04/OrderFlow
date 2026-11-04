-- Fixed performance dataset for OF-08: 1,000 customers, 1,000 products, 100,000 orders,
-- 300,000 order items and their payment attempts.
--
-- Every value is computed from a row number (no RAND, no GETDATE), so each run produces the
-- same data and measurements stay comparable. Run it on a dedicated, empty, migrated database:
--
--   dotnet ef database update --project src/OrderFlow.Infrastructure --connection "Server=(localdb)\MSSQLLocalDB;Database=OrderFlow_Perf;Trusted_Connection=True;TrustServerCertificate=True"
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d OrderFlow_Perf -I -b -i db/scripts/seed-100k.sql
--
-- Distribution: about 5% PendingPayment, 3% Cancelled, the rest paid in some stage, spread
-- evenly from 2025-01-01 to the end of October 2026. 10% of orders also have a Failed attempt.

:on error exit
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_NAME() NOT LIKE N'%Perf%'
    THROW 50001, 'seed-100k.sql only runs on a database whose name contains "Perf".', 1;

IF EXISTS (SELECT 1 FROM orders)
    THROW 50002, 'orders is not empty. Seed an empty database.', 1;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

WITH d AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n))
SELECT d1.n + d2.n * 10 + d3.n * 100 + d4.n * 1000 + d5.n * 10000 + 1 AS n
INTO #numbers
FROM d d1 CROSS JOIN d d2 CROSS JOIN d d3 CROSS JOIN d d4 CROSS JOIN d d5;

CREATE UNIQUE CLUSTERED INDEX ix_numbers ON #numbers (n);

SET IDENTITY_INSERT customers ON;
INSERT INTO customers (id, name, email, status, created_at, updated_at)
SELECT n,
       CONCAT(N'Customer ', FORMAT(n, 'D4')),
       CONCAT(N'customer', n, N'@seed.example.com'),
       N'Active', '2025-01-01', '2025-01-01'
FROM #numbers WHERE n <= 1000;
SET IDENTITY_INSERT customers OFF;

SET IDENTITY_INSERT products ON;
INSERT INTO products (id, sku, name, price, stock_quantity, reserved_quantity, status, created_at, updated_at)
SELECT n,
       CONCAT(N'SKU-', FORMAT(n, 'D5')),
       CONCAT(N'Product ', FORMAT(n, 'D4')),
       10 + n % 90,
       n * 37 % 500 + 20,
       n % 20,
       CASE WHEN n % 50 = 0 THEN N'Discontinued' ELSE N'Active' END,
       '2025-01-01', '2025-01-01'
FROM #numbers WHERE n <= 1000;
SET IDENTITY_INSERT products OFF;

SET IDENTITY_INSERT orders ON;
INSERT INTO orders (id, customer_id, total_amount, status, created_at, updated_at)
SELECT n,
       CAST(CAST(n AS bigint) * n % 1000 AS int) + 1,
       0,
       CASE WHEN n % 20 = 0 THEN N'PendingPayment'
            WHEN n % 33 = 1 THEN N'Cancelled'
            WHEN n % 4 = 0 THEN N'Paid'
            WHEN n % 4 = 1 THEN N'Confirmed'
            WHEN n % 4 = 2 THEN N'Shipped'
            ELSE N'Delivered' END,
       DATEADD(SECOND, n * 577, CAST('2025-01-01' AS datetime2)),
       DATEADD(SECOND, n * 577, CAST('2025-01-01' AS datetime2))
FROM #numbers;
SET IDENTITY_INSERT orders OFF;

INSERT INTO order_items (order_id, product_id, quantity, unit_price)
SELECT x.n, x.product_id, 1 + (x.n + k.k) % 3, 10 + x.product_id % 90
FROM (VALUES (1), (2), (3)) k(k)
CROSS APPLY (SELECT n, (n * 31 + k.k * 97) % 1000 + 1 AS product_id FROM #numbers) x;

UPDATE o
SET total_amount = t.total
FROM orders o
JOIN (SELECT order_id, SUM(subtotal) AS total FROM order_items GROUP BY order_id) t ON t.order_id = o.id;

INSERT INTO payment_attempts (order_id, amount, status, provider, provider_transaction_id, created_at, updated_at)
SELECT o.id, o.total_amount, N'Failed', N'SimPay', NULL, DATEADD(MINUTE, 1, o.created_at), DATEADD(MINUTE, 1, o.created_at)
FROM orders o
WHERE o.id % 10 = 3;

INSERT INTO payment_attempts (order_id, amount, status, provider, provider_transaction_id, created_at, updated_at)
SELECT o.id, o.total_amount, N'Succeeded', N'SimPay', CONCAT(N'SEED-', o.id), DATEADD(MINUTE, 5, o.created_at), DATEADD(MINUTE, 5, o.created_at)
FROM orders o
WHERE o.status NOT IN (N'PendingPayment', N'Cancelled');

DROP TABLE #numbers;
GO
