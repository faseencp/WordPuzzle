-- WordPuzzle database schema.
-- Run once via sqlcmd/SSMS against SQL Server Express. No SQL Agent, no EF migrations.
--
-- Example:
--   sqlcmd -S localhost\SQLEXPRESS -Q "CREATE DATABASE [WordPuzzle]"
--   sqlcmd -S localhost\SQLEXPRESS -d WordPuzzle -i schema.sql

IF DB_ID('WordPuzzle') IS NULL
BEGIN
    RAISERROR('Run this script against the WordPuzzle database (create it first: CREATE DATABASE [WordPuzzle]).', 16, 1);
    RETURN;
END

ALTER DATABASE [WordPuzzle] SET RECOVERY SIMPLE;
GO

IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.ParticipantCodes', 'U') IS NOT NULL DROP TABLE dbo.ParticipantCodes;
IF OBJECT_ID('dbo.Rounds', 'U') IS NOT NULL DROP TABLE dbo.Rounds;
IF OBJECT_ID('dbo.KeyValueStore', 'U') IS NOT NULL DROP TABLE dbo.KeyValueStore;
IF OBJECT_ID('dbo.EarthMapResults', 'U') IS NOT NULL DROP TABLE dbo.EarthMapResults;
IF OBJECT_ID('dbo.EarthMapCodes', 'U') IS NOT NULL DROP TABLE dbo.EarthMapCodes;
IF OBJECT_ID('dbo.EarthMapBatches', 'U') IS NOT NULL DROP TABLE dbo.EarthMapBatches;
GO

CREATE TABLE dbo.Rounds (
    Seed             nvarchar(64)   NOT NULL PRIMARY KEY,
    Tier             nvarchar(20)   NOT NULL,
    Category         nvarchar(20)   NOT NULL,
    GridSize         int            NOT NULL,
    WordsCsv         nvarchar(1000) NOT NULL,
    ParticipantCount int            NOT NULL,
    Status           tinyint        NOT NULL DEFAULT 0, -- 0=Created, 1=Started, 2=Closed
    CreatedAtUtc     datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
    StartedAtUtc     datetime2      NULL
);
GO

CREATE TABLE dbo.ParticipantCodes (
    Id           int identity(1,1) NOT NULL PRIMARY KEY,
    Seed         nvarchar(64)  NOT NULL REFERENCES dbo.Rounds(Seed),
    Code         nvarchar(20)  NOT NULL,
    IsClaimed    bit           NOT NULL DEFAULT 0,
    ClaimedUnit  nvarchar(200) NULL,
    ClaimedName  nvarchar(200) NULL,
    ClaimedAtUtc datetime2     NULL,
    CONSTRAINT UQ_ParticipantCodes_Seed_Code UNIQUE (Seed, Code)
);
GO

CREATE TABLE dbo.Results (
    Id                 int identity(1,1) NOT NULL PRIMARY KEY,
    ParticipantCodeId  int       NOT NULL UNIQUE REFERENCES dbo.ParticipantCodes(Id),
    Seed               nvarchar(64) NOT NULL REFERENCES dbo.Rounds(Seed),
    WordsFound         int       NOT NULL,
    TotalWords         int       NOT NULL,
    TimeSeconds         int       NOT NULL,
    SubmittedAtUtc     datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE INDEX IX_Results_Seed ON dbo.Results(Seed);
GO

-- Generic key/value store backing the Earth Map Challenge's admin-managed
-- rounds, leaderboard, and settings (it originally used a fictional
-- window.storage API with the same get(key)/set(key, value) shape, so the
-- frontend only needed its storage functions swapped to call this instead).
CREATE TABLE dbo.KeyValueStore (
    [Key]        nvarchar(100)  NOT NULL PRIMARY KEY,
    Value        nvarchar(MAX)  NOT NULL,
    UpdatedAtUtc datetime2      NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- Earth Map Challenge participant codes + live leaderboard. Mirrors the word
-- search Rounds/ParticipantCodes/Results shape, but this file has no
-- per-instance "seed" (everyone always plays at the same URL, pulling a
-- random subset of the admin-managed question pool) -- BatchKey plays that
-- role instead: each "Generate Codes" click starts a new batch, and the
-- current batch is just whichever one was created most recently.
CREATE TABLE dbo.EarthMapBatches (
    BatchKey         nvarchar(64)  NOT NULL PRIMARY KEY,
    ParticipantCount int           NOT NULL,
    Category         nvarchar(20)  NOT NULL DEFAULT 'secondary', -- 'junior' or 'secondary', same as word search
    CreatedAtUtc     datetime2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.EarthMapCodes (
    Id           int identity(1,1) NOT NULL PRIMARY KEY,
    BatchKey     nvarchar(64)  NOT NULL REFERENCES dbo.EarthMapBatches(BatchKey),
    Code         nvarchar(20)  NOT NULL,
    IsClaimed    bit           NOT NULL DEFAULT 0,
    ClaimedAtUtc datetime2     NULL,
    CONSTRAINT UQ_EarthMapCodes_BatchKey_Code UNIQUE (BatchKey, Code)
);
GO

CREATE TABLE dbo.EarthMapResults (
    Id             int identity(1,1) NOT NULL PRIMARY KEY,
    EarthMapCodeId int       NOT NULL UNIQUE REFERENCES dbo.EarthMapCodes(Id),
    BatchKey       nvarchar(64) NOT NULL REFERENCES dbo.EarthMapBatches(BatchKey),
    Score          int       NOT NULL,
    LocationCount  int       NOT NULL,
    SubmittedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE INDEX IX_EarthMapResults_BatchKey ON dbo.EarthMapResults(BatchKey);
GO
