-- Least-privilege SQL login for the OrderFlow API (OF-07).
--
-- The API only needs to read and write rows, so it gets db_datareader + db_datawriter and is
-- NOT db_owner: if an attacker ever got SQL through the app, they could not DROP or ALTER
-- tables. Migrations run separately, under an owner account.
--
-- Run it as an administrator. The password is a sqlcmd variable so it is never stored in this file:
--   sqlcmd -S <server> -i db/scripts/create-app-user.sql -v DatabaseName="OrderFlow" AppPassword="<strong password>"
--
-- Safe to run again: existing login and user are kept, and the password is updated.

:setvar LoginName "orderflow_app"
:on error exit

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(LoginName)')
    CREATE LOGIN [$(LoginName)] WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN [$(LoginName)] WITH PASSWORD = N'$(AppPassword)';
GO

USE [$(DatabaseName)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(LoginName)')
    CREATE USER [$(LoginName)] FOR LOGIN [$(LoginName)];
GO

ALTER ROLE db_datareader ADD MEMBER [$(LoginName)];
ALTER ROLE db_datawriter ADD MEMBER [$(LoginName)];
GO
