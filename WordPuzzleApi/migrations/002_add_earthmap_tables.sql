-- Incremental migration for an ALREADY-DEPLOYED WordPuzzle database.
-- Adds the Earth Map Challenge batch/code/result tables only -- does not
-- touch Rounds/ParticipantCodes/Results/KeyValueStore or their data.
--
-- Run:
--   sqlcmd -S localhost\SQLEXPRESS -U <admin> -d WordPuzzle -i 002_add_earthmap_tables.sql

IF OBJECT_ID('dbo.EarthMapBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EarthMapBatches (
        BatchKey         nvarchar(64)  NOT NULL PRIMARY KEY,
        ParticipantCount int           NOT NULL,
        CreatedAtUtc     datetime2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT 'Created dbo.EarthMapBatches.';
END
ELSE PRINT 'dbo.EarthMapBatches already exists -- skipped.';
GO

IF OBJECT_ID('dbo.EarthMapCodes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EarthMapCodes (
        Id           int identity(1,1) NOT NULL PRIMARY KEY,
        BatchKey     nvarchar(64)  NOT NULL REFERENCES dbo.EarthMapBatches(BatchKey),
        Code         nvarchar(20)  NOT NULL,
        IsClaimed    bit           NOT NULL DEFAULT 0,
        ClaimedAtUtc datetime2     NULL,
        CONSTRAINT UQ_EarthMapCodes_BatchKey_Code UNIQUE (BatchKey, Code)
    );
    PRINT 'Created dbo.EarthMapCodes.';
END
ELSE PRINT 'dbo.EarthMapCodes already exists -- skipped.';
GO

IF OBJECT_ID('dbo.EarthMapResults', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EarthMapResults (
        Id             int identity(1,1) NOT NULL PRIMARY KEY,
        EarthMapCodeId int       NOT NULL UNIQUE REFERENCES dbo.EarthMapCodes(Id),
        BatchKey       nvarchar(64) NOT NULL REFERENCES dbo.EarthMapBatches(BatchKey),
        Score          int       NOT NULL,
        LocationCount  int       NOT NULL,
        SubmittedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_EarthMapResults_BatchKey ON dbo.EarthMapResults(BatchKey);
    PRINT 'Created dbo.EarthMapResults.';
END
ELSE PRINT 'dbo.EarthMapResults already exists -- skipped.';
GO
