-- TheCallAttendance database schema.
-- Run once via sqlcmd/SSMS against SQL Server Express. No SQL Agent, no EF migrations.
--
-- Example:
--   sqlcmd -S localhost\SQLEXPRESS -Q "CREATE DATABASE [TheCallAttendance]"
--   sqlcmd -S localhost\SQLEXPRESS -d TheCallAttendance -i schema.sql

IF DB_ID('TheCallAttendance') IS NULL
BEGIN
    RAISERROR('Run this script against the TheCallAttendance database (create it first: CREATE DATABASE [TheCallAttendance]).', 16, 1);
    RETURN;
END

ALTER DATABASE [TheCallAttendance] SET RECOVERY SIMPLE;
GO

IF OBJECT_ID('dbo.AttendanceEntries', 'U') IS NOT NULL DROP TABLE dbo.AttendanceEntries;
GO

-- One table for both "confirmation" (pre-event RSVP) and "attendance"
-- (during-event check-in), distinguished by Kind. The two are deliberately
-- NOT linked to each other -- independent tallies, no personal code
-- matching one submission to the other.
CREATE TABLE dbo.AttendanceEntries (
    Id             int identity(1,1) NOT NULL PRIMARY KEY,
    Kind           nvarchar(20)  NOT NULL,  -- 'confirmation' or 'attendance'
    Name           nvarchar(200) NOT NULL,
    Country        nvarchar(50)  NOT NULL,
    DeviceId       nvarchar(64)  NOT NULL,  -- random ID the client generates once, kept in localStorage
    SubmittedAtUtc datetime2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_AttendanceEntries_Kind_DeviceId UNIQUE (Kind, DeviceId)
);
GO

CREATE INDEX IX_AttendanceEntries_Kind_Country ON dbo.AttendanceEntries(Kind, Country);
GO
