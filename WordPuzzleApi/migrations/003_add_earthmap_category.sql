-- Incremental migration for an ALREADY-DEPLOYED WordPuzzle database.
-- Adds the Category column to EarthMapBatches only.
--
-- Run:
--   sqlcmd -S localhost\SQLEXPRESS -U <admin> -d WordPuzzle -i 003_add_earthmap_category.sql

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.EarthMapBatches') AND name = 'Category'
)
BEGIN
    ALTER TABLE dbo.EarthMapBatches
    ADD Category nvarchar(20) NOT NULL DEFAULT 'secondary';
    PRINT 'Added Category column to dbo.EarthMapBatches.';
END
ELSE
BEGIN
    PRINT 'Category column already exists on dbo.EarthMapBatches -- skipped.';
END
GO
