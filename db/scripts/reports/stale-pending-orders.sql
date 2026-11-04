-- Orders still waiting for payment after a cutoff, oldest first. The API computes the cutoff
-- from "now minus X hours"; a fixed value here keeps measurements repeatable.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @cutoff datetime2 = '2026-10-01';
DECLARE @limit int = 100;

SELECT TOP (@limit)
       id AS OrderId,
       customer_id AS CustomerId,
       total_amount AS TotalAmount,
       created_at AS CreatedAt
FROM orders
WHERE status = N'PendingPayment'
  AND created_at < @cutoff
ORDER BY created_at, id;
