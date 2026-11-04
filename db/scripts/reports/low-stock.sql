-- SQL form of GET /api/products/low-stock. stock_quantity - reserved_quantity is an expression,
-- so it cannot seek an index; on a small catalog a scan is the right plan.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @threshold int = 10;
DECLARE @limit int = 50;

SELECT TOP (@limit)
       id AS ProductId,
       sku AS Sku,
       name AS Name,
       stock_quantity - reserved_quantity AS AvailableQuantity
FROM products
WHERE status = N'Active'
  AND stock_quantity - reserved_quantity <= @threshold
ORDER BY stock_quantity - reserved_quantity, id;
