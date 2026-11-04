-- Customers ranked by money paid. RANK gives equal amounts the same rank and skips the next
-- number, so the result can hold more than @top rows when customers tie.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @top int = 10;

WITH paid AS (
    SELECT o.customer_id, SUM(p.amount) AS paid_amount, COUNT(*) AS payment_count
    FROM payment_attempts p
    JOIN orders o ON o.id = p.order_id
    WHERE p.status = N'Succeeded'
    GROUP BY o.customer_id
),
ranked AS (
    SELECT customer_id, paid_amount, payment_count,
           RANK() OVER (ORDER BY paid_amount DESC) AS customer_rank
    FROM paid
)
SELECT r.customer_rank AS [Rank],
       r.customer_id AS CustomerId,
       c.name AS CustomerName,
       r.paid_amount AS PaidAmount,
       r.payment_count AS PaymentCount
FROM ranked r
JOIN customers c ON c.id = r.customer_id
WHERE r.customer_rank <= @top
ORDER BY r.customer_rank, r.customer_id;
