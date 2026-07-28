-- Incremental migration for an ALREADY-DEPLOYED WordPuzzle database.
-- Adds the KeyValueStore table only -- does not touch Rounds/ParticipantCodes/
-- Results, unlike re-running the full schema.sql (which DROPs and recreates
-- those tables and would wipe any real word search competition data).
--
-- Run:
--   sqlcmd -S localhost\SQLEXPRESS -U <admin> -d WordPuzzle -i 001_add_keyvaluestore.sql

IF OBJECT_ID('dbo.KeyValueStore', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.KeyValueStore (
        [Key]        nvarchar(100)  NOT NULL PRIMARY KEY,
        Value        nvarchar(MAX)  NOT NULL,
        UpdatedAtUtc datetime2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT 'Created dbo.KeyValueStore.';
END
ELSE
BEGIN
    PRINT 'dbo.KeyValueStore already exists -- nothing to do.';
END
GO
