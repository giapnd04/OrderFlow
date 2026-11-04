-- Money actually collected per month. Only Succeeded attempts count.
-- A range on created_at stays sargable; YEAR(created_at) = 2026 in the WHERE would not.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @from datetime2 = '2026-01-01';
DECLARE @to datetime2 = '2027-01-01';

SELECT YEAR(created_at) AS [Year],
       MONTH(created_at) AS [Month],
       COUNT(*) AS PaymentCount,
       SUM(amount) AS Revenue
FROM payment_attempts
WHERE status = N'Succeeded'
  AND created_at >= @from
  AND created_at < @to
GROUP BY YEAR(created_at), MONTH(created_at)
ORDER BY [Year], [Month];
