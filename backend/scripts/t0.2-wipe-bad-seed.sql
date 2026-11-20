/*
  T0.2 — Wipe flawed seed rows before re-seed.
  Run against: XenasysMarketplace on localhost\SQLEXPRESS

  Order respects FKs. Does NOT drop schema — only baseline seed tables.
*/

USE [XenasysMarketplace];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- Clear memberships first (FK to users/tenants/roles)
DELETE FROM dbo.user_tenant_memberships;
DELETE FROM dbo.users;
DELETE FROM dbo.roles;

-- COA automation nodes (subsidiaries may reference controls — clear if empty env)
IF OBJECT_ID(N'dbo.subsidiary_accounts', N'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.subsidiary_accounts;
END

DELETE FROM dbo.control_accounts;
DELETE FROM dbo.master_accounts;

-- Master demo tenant (products/vendors should be empty in Phase 0)
IF OBJECT_ID(N'dbo.products', N'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.products;
END

DELETE FROM dbo.tenants WHERE [Code] = N'MASTER';

COMMIT TRAN;
GO

PRINT N'T0.2 wipe complete. Restart API (or run database update + start) to re-seed.';
GO
