-- One-time database setup, run by the SQL server's Entra admin (the owner) with sqlcmd: see infra/README.md step 8.
-- Creates contained users for the two managed identities from their CLIENT IDs. For service principals and managed
-- identities Azure SQL expects the client ID, not the object ID, and it does not check it: a wrong ID gives a user
-- that silently never matches. SID + TYPE = E means the server needs no Microsoft Graph permissions.
-- Safe to run again.
:on error exit
SET NOCOUNT ON;

-- Identity's tables live in the "auth" schema. Created here, owned by dbo, so the deploy identity never creates or
-- owns a schema (the migration's EnsureSchema then does nothing).
IF SCHEMA_ID(N'auth') IS NULL
    EXEC (N'CREATE SCHEMA [auth] AUTHORIZATION [dbo];');

DECLARE @sid NVARCHAR(MAX);

-- The API at run time: read and write rows, nothing else. No DDL, so a compromised app can't change the schema.
IF DATABASE_PRINCIPAL_ID(N'$(AppIdentityName)') IS NULL
BEGIN
    SET @sid = CONVERT(VARCHAR(MAX), CONVERT(VARBINARY(16), CONVERT(UNIQUEIDENTIFIER, N'$(AppClientId)')), 1);
    EXEC (N'CREATE USER [$(AppIdentityName)] WITH SID = ' + @sid + N', TYPE = E;');
END;
ALTER ROLE db_datareader ADD MEMBER [$(AppIdentityName)];
ALTER ROLE db_datawriter ADD MEMBER [$(AppIdentityName)];

-- The GitHub deploy job: applies migrations (DDL) and seeds the demo data (rows).
IF DATABASE_PRINCIPAL_ID(N'$(DeployIdentityName)') IS NULL
BEGIN
    SET @sid = CONVERT(VARCHAR(MAX), CONVERT(VARBINARY(16), CONVERT(UNIQUEIDENTIFIER, N'$(DeployClientId)')), 1);
    EXEC (N'CREATE USER [$(DeployIdentityName)] WITH SID = ' + @sid + N', TYPE = E;');
END;
ALTER ROLE db_ddladmin ADD MEMBER [$(DeployIdentityName)];
ALTER ROLE db_datareader ADD MEMBER [$(DeployIdentityName)];
ALTER ROLE db_datawriter ADD MEMBER [$(DeployIdentityName)];

-- Result: each user, its roles, and its SID read back as a GUID. That GUID must equal the client ID output by Bicep.
SELECT p.name AS [user], r.name AS [role], CONVERT(UNIQUEIDENTIFIER, CONVERT(VARBINARY(16), p.sid)) AS sid_as_client_id
FROM sys.database_role_members AS m
JOIN sys.database_principals AS p ON p.principal_id = m.member_principal_id
JOIN sys.database_principals AS r ON r.principal_id = m.role_principal_id
WHERE p.name IN (N'$(AppIdentityName)', N'$(DeployIdentityName)')
ORDER BY p.name, r.name;
