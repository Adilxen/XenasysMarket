SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Year int = YEAR(GETDATE());
DECLARE @SeqKey nvarchar(64) = N'ACC_NO_' + CAST(@Year AS nvarchar(4));
IF NOT EXISTS (SELECT 1 FROM PlatformSequences WHERE SequenceKey=@SeqKey)
  INSERT INTO PlatformSequences(SequenceKey, LastValue) VALUES (@SeqKey, 0);

DECLARE @Companies TABLE (rn int IDENTITY(1,1), CompanyId int);
INSERT INTO @Companies(CompanyId)
SELECT t.Id FROM Tenants t
WHERE t.IsActive=1 AND t.Id IN (1,3,4,5,6)
  AND EXISTS (SELECT 1 FROM Categories c WHERE c.CompanyId=t.Id AND c.IsActive=1)
  AND EXISTS (SELECT 1 FROM Brands b WHERE b.CompanyId=t.Id AND b.IsActive=1)
  AND EXISTS (SELECT 1 FROM ControlAccounts ca WHERE ca.CompanyId=t.Id AND ca.ControlKind=N'INVENTORY' AND ca.IsActive=1);

DECLARE @CompanyCount int = (SELECT COUNT(*) FROM @Companies);
DECLARE @PerCompany int = 200;
DECLARE @Need int = @CompanyCount * @PerCompany;

UPDATE PlatformSequences SET LastValue = LastValue + @Need WHERE SequenceKey=@SeqKey;
DECLARE @AccEnd bigint = (SELECT LastValue FROM PlatformSequences WHERE SequenceKey=@SeqKey);
DECLARE @AccCursor bigint = @AccEnd - @Need + 1;

DECLARE @cid int;
DECLARE ccur CURSOR LOCAL FAST_FORWARD FOR SELECT CompanyId FROM @Companies ORDER BY CompanyId;
OPEN ccur;
FETCH NEXT FROM ccur INTO @cid;
WHILE @@FETCH_STATUS = 0
BEGIN
  IF NOT EXISTS (SELECT 1 FROM ProductCodeSequences WHERE CompanyId=@cid)
    INSERT INTO ProductCodeSequences(CompanyId, LastValue) VALUES (@cid, 0);

  DECLARE @tc char(6) = (SELECT TenantCode FROM Tenants WHERE Id=@cid);
  DECLARE @cat int = (SELECT TOP 1 Id FROM Categories WHERE CompanyId=@cid AND IsActive=1 ORDER BY Id);
  DECLARE @sub int = (SELECT TOP 1 s.Id FROM SubCategories s INNER JOIN Categories c ON c.Id=s.CategoryId WHERE c.CompanyId=@cid AND s.IsActive=1 ORDER BY s.Id);
  DECLARE @br int = (SELECT TOP 1 Id FROM Brands WHERE CompanyId=@cid AND IsActive=1 ORDER BY Id);
  DECLARE @uid int = (SELECT TOP 1 Id FROM Units WHERE IsActive=1 ORDER BY Id);
  DECLARE @uc nvarchar(20) = (SELECT TOP 1 Code FROM Units WHERE IsActive=1 ORDER BY Id);
  DECLARE @inv int = (SELECT TOP 1 Id FROM ControlAccounts WHERE CompanyId=@cid AND ControlKind=N'INVENTORY' AND IsActive=1);

  UPDATE ProductCodeSequences SET LastValue = LastValue + @PerCompany WHERE CompanyId=@cid;
  DECLARE @pEnd bigint = (SELECT LastValue FROM ProductCodeSequences WHERE CompanyId=@cid);
  DECLARE @pStart bigint = @pEnd - @PerCompany + 1;
  DECLARE @i bigint = @pStart;

  WHILE @i <= @pEnd
  BEGIN
    DECLARE @sku char(16) = UPPER(@tc) + '01' + RIGHT('00000000' + CAST(@i AS varchar(8)), 8);
    IF NOT EXISTS (SELECT 1 FROM Products WHERE CompanyId=@cid AND ProductCode=@sku)
    BEGIN
      DECLARE @acc nvarchar(16) = N'ACC-' + CAST(@Year AS nvarchar(4)) + N'-' + RIGHT('00000' + CAST(@AccCursor AS varchar(5)), 5);
      DECLARE @acode nvarchar(32) = RTRIM(@tc) + N'-P' + RIGHT('00000' + CAST(@i AS varchar(5)), 5);
      DECLARE @pname nvarchar(150) = N'Load Product ' + CAST(@i AS nvarchar(20)) + N' C' + CAST(@cid AS nvarchar(10));
      DECLARE @title nvarchar(150) = @pname;

      INSERT INTO SubsidiaryAccounts(CompanyId, ControlAccountId, SubsidiaryAccountCode, SubsidiaryAccountTitle, AccountNo, ProductCode, IsActive, IsSystemGenerated, SourceEntityType, InsertDate, InsertUserId)
      VALUES (@cid, @inv, @acode, @title, @acc, @sku, 1, 1, N'Product', SYSUTCDATETIME(), 1);
      DECLARE @sid int = SCOPE_IDENTITY();

      INSERT INTO Products(CompanyId, CategoryId, SubCategoryId, BrandId, UnitId, ProductName, Packaging, UnitOfMeasurement, ProductCode, MinimumStockLevel, InvoicePrice, RetailPrice, ImagePath, IsActive, SubsidiaryAccountId, InsertDate, InsertUserId)
      VALUES (@cid, @cat, @sub, @br, @uid, @pname, N'Pack', @uc, @sku, 10, 50.00, 75.00, N'/', 1, @sid, SYSUTCDATETIME(), 1);
      DECLARE @pid int = SCOPE_IDENTITY();
      UPDATE SubsidiaryAccounts SET SourceEntityId=@pid WHERE Id=@sid;
    END
    SET @AccCursor = @AccCursor + 1;
    SET @i = @i + 1;
  END

  FETCH NEXT FROM ccur INTO @cid;
END
CLOSE ccur; DEALLOCATE ccur;

SELECT COUNT(*) AS ProductTotal FROM Products;
SELECT CompanyId, COUNT(*) AS Cnt FROM Products GROUP BY CompanyId ORDER BY CompanyId;
