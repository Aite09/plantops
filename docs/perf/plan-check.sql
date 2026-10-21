-- Query-plan check for the work-order list and the dashboard (README, "Performance").
-- Run it ONLY against a throwaway database created for it (PlantOpsPerf): it inserts 100,000 synthetic work orders.
-- It prints each query's plan (STATISTICS PROFILE) and its reads (STATISTICS IO). Look for "Index Seek" on
-- IX_WorkOrders_Status_PlantUnitId versus "Clustered Index Scan" on PK_WorkOrders.
SET NOCOUNT ON;
GO

-- 97% Closed, like a plant with a few years of history; spread over the 10 seeded pieces of equipment.
WITH n AS (
    SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b),
eq AS (
    SELECT Id, PlantUnitId, ROW_NUMBER() OVER (ORDER BY Tag) - 1 AS k FROM dbo.Equipment),
rows AS (
    SELECT n.i, eq.Id AS EquipmentId, eq.PlantUnitId,
           CASE WHEN n.i % 100 < 97 THEN N'Closed'
                WHEN n.i % 100 = 97 THEN N'Open'
                WHEN n.i % 100 = 98 THEN N'Approved'
                WHEN n.i % 200 = 99 THEN N'Assigned'
                ELSE N'InProgress' END AS Status
    FROM n JOIN eq ON eq.k = n.i % 10)
INSERT INTO dbo.WorkOrders (Id, Title, Description, Priority, Status, PlantUnitId, EquipmentId, ReportedByUserId,
                            AssignedToUserId, CreatedAtUtc)
SELECT NEWID(), CONCAT(N'Synthetic fault ', i), REPLICATE(N'Description text. ', 25), (i % 4) + 1, Status,
       PlantUnitId, EquipmentId, NEWID(),
       CASE WHEN Status IN (N'Assigned', N'InProgress') THEN NEWID() END,
       DATEADD(MINUTE, -15 * i, SYSDATETIMEOFFSET())
FROM rows;
UPDATE STATISTICS dbo.WorkOrders;
GO

SET STATISTICS IO ON;
SET STATISTICS PROFILE ON;
GO

-- 1. Work-order list, default view: the count, then page 1 (GET /api/work-orders?activeOnly=true).
SELECT COUNT(*) FROM dbo.WorkOrders AS w WHERE w.Status <> N'Closed';
EXEC sp_executesql N'SELECT w.Id, w.Title, w.Priority, w.Status, p.Code, e.Tag, w.AssignedToUserId, w.CreatedAtUtc
FROM dbo.WorkOrders AS w
JOIN dbo.PlantUnits AS p ON w.PlantUnitId = p.Id
JOIN dbo.Equipment AS e ON w.EquipmentId = e.Id
WHERE w.Status <> N''Closed''
ORDER BY w.Priority DESC, w.CreatedAtUtc, w.Id
OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY', N'@p int, @p1 int', @p = 0, @p1 = 20;

-- 2. Work-order list filtered by status (?status=Open).
EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.WorkOrders AS w WHERE w.Status = @status',
    N'@status nvarchar(20)', @status = N'Open';

-- 3. Dashboard: needs attention, its count and then the top 25 (the API sends the cutoff as a parameter).
DECLARE @cutoff datetimeoffset = DATEADD(DAY, -7, SYSDATETIMEOFFSET());
EXEC sp_executesql N'SELECT COUNT(*)
FROM dbo.WorkOrders AS w
WHERE w.Status <> N''Closed''
  AND ((w.Priority >= 3 AND w.Status IN (N''Open'', N''Approved'')) OR w.CreatedAtUtc < @cutoff)', N'@cutoff datetimeoffset', @cutoff = @cutoff;
EXEC sp_executesql N'SELECT TOP(@p) w.Id, w.Title, w.Priority, w.Status, p.Code, e.Tag, w.AssignedToUserId, w.CreatedAtUtc
FROM dbo.WorkOrders AS w
JOIN dbo.PlantUnits AS p ON w.PlantUnitId = p.Id
JOIN dbo.Equipment AS e ON w.EquipmentId = e.Id
WHERE w.Status <> N''Closed''
  AND ((w.Priority >= 3 AND w.Status IN (N''Open'', N''Approved'')) OR w.CreatedAtUtc < @cutoff)
ORDER BY w.Priority DESC, w.CreatedAtUtc, w.Id', N'@p int, @cutoff datetimeoffset', @p = 25, @cutoff = @cutoff;

-- 4. Dashboard: open work per unit and status.
SELECT p.Id, p.Code, p.Name,
    (SELECT COUNT(*) FROM dbo.WorkOrders AS w WHERE w.Status = N'Open' AND w.PlantUnitId = p.Id),
    (SELECT COUNT(*) FROM dbo.WorkOrders AS w WHERE w.Status = N'Approved' AND w.PlantUnitId = p.Id),
    (SELECT COUNT(*) FROM dbo.WorkOrders AS w WHERE w.Status = N'Assigned' AND w.PlantUnitId = p.Id),
    (SELECT COUNT(*) FROM dbo.WorkOrders AS w WHERE w.Status = N'InProgress' AND w.PlantUnitId = p.Id)
FROM dbo.PlantUnits AS p
ORDER BY p.Code;

-- 5. Dashboard: work held per technician.
SELECT w.AssignedToUserId,
       COUNT(CASE WHEN w.Status = N'Assigned' THEN 1 END),
       COUNT(CASE WHEN w.Status = N'InProgress' THEN 1 END)
FROM dbo.WorkOrders AS w
WHERE w.Status IN (N'Assigned', N'InProgress') AND w.AssignedToUserId IS NOT NULL
GROUP BY w.AssignedToUserId;
GO

SET STATISTICS PROFILE OFF;
SET STATISTICS IO OFF;
GO
