-- Verify the contract tables against the live database.
--
-- READ-ONLY. Every statement here is SELECT, PRINT, DECLARE or SET. The workflow
-- greps this file for anything else and refuses to run if it finds it, so this
-- cannot quietly become a script that changes something.
--
-- It prints "RESULT: ALL CHECKS PASSED" only when every check succeeds; the
-- workflow greps for exactly that line, so a partial pass fails the run.
--
-- If the tables are genuinely absent, the row-count queries near the end will not
-- compile and sqlcmd reports "Invalid object name 'dbo.Contracts'". That is a
-- clear enough answer to the question this script exists to ask, and the run
-- fails either way because the RESULT line is never printed.

SET NOCOUNT ON;

DECLARE @problems int = 0;
DECLARE @msg nvarchar(400);

PRINT '=== Tables ===';

IF OBJECT_ID('dbo.Contracts', 'U') IS NULL
BEGIN
    PRINT 'FAIL: table Contracts does not exist. The start-up DDL did not run.';
    SET @problems = @problems + 1;
END
ELSE PRINT 'ok   Contracts exists';

IF OBJECT_ID('dbo.ContractEvents', 'U') IS NULL
BEGIN
    PRINT 'FAIL: table ContractEvents does not exist.';
    SET @problems = @problems + 1;
END
ELSE PRINT 'ok   ContractEvents exists';

PRINT '';
PRINT '=== Columns the application reads and writes ===';

-- Every column the entity maps. A missing one means the DDL and the model have
-- drifted, and the failure would surface as a runtime error on first use.
--
-- A table constructor rather than a table variable: populating one of those needs
-- a write statement, and the workflow's read-only guard rightly refuses to
-- distinguish a write to a table variable from any other write.
WITH expected(TableName, ColumnName) AS (
    SELECT TableName, ColumnName FROM (VALUES
        ('Contracts','Id'), ('Contracts','OrganizationId'), ('Contracts','LeadId'),
        ('Contracts','DealId'), ('Contracts','Status'), ('Contracts','Title'),
        ('Contracts','Body'), ('Contracts','BodyHashAtSend'),
        ('Contracts','CounterpartyName'), ('Contracts','CounterpartyEmail'),
        ('Contracts','CreatedByUserId'), ('Contracts','CreatedAtUtc'),
        ('Contracts','UpdatedAtUtc'), ('Contracts','SentAtUtc'), ('Contracts','SentByUserId'),
        ('Contracts','SigningTokenHash'), ('Contracts','SigningTokenExpiresAtUtc'),
        ('Contracts','FirstViewedAtUtc'), ('Contracts','ClientSignatureName'),
        ('Contracts','ClientSignedAtUtc'), ('Contracts','ClientSignatureIp'),
        ('Contracts','ClientSignatureUserAgent'), ('Contracts','CounterSignatureName'),
        ('Contracts','CounterSignedAtUtc'), ('Contracts','CounterSignedByUserId'),
        ('Contracts','CounterSignatureIp'), ('Contracts','ClosedReason'),
        ('Contracts','ExecutedCopySentAtUtc'),
        ('ContractEvents','Id'), ('ContractEvents','ContractId'), ('ContractEvents','Type'),
        ('ContractEvents','Detail'), ('ContractEvents','ActorUserId'),
        ('ContractEvents','ActorLabel'), ('ContractEvents','Ip'),
        ('ContractEvents','UserAgent'), ('ContractEvents','AtUtc')
    ) AS v(TableName, ColumnName)
)
SELECT e.TableName, e.ColumnName AS MissingColumn
FROM expected e
WHERE NOT EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = e.TableName AND c.name = e.ColumnName
);

-- Rows returned by the statement above, so the count needs no second query.
DECLARE @missing int = @@ROWCOUNT;

IF @missing > 0
BEGIN
    SET @msg = 'FAIL: ' + CAST(@missing AS varchar(10)) + ' expected column(s) missing (listed above).';
    PRINT @msg;
    SET @problems = @problems + 1;
END
ELSE PRINT 'ok   all 37 expected columns present';

PRINT '';
PRINT '=== The signing token index ===';

-- The public signing path looks contracts up by this column alone. Without the
-- index every signing request is a table scan; without uniqueness two live
-- contracts could share a link.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    JOIN sys.tables t ON t.object_id = i.object_id
    WHERE t.name = 'Contracts' AND c.name = 'SigningTokenHash')
BEGIN
    PRINT 'FAIL: no index on Contracts.SigningTokenHash.';
    SET @problems = @problems + 1;
END
ELSE
BEGIN
    SELECT i.name AS IndexName, i.is_unique AS IsUnique, i.has_filter AS IsFiltered,
           i.filter_definition AS FilterDefinition
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    JOIN sys.tables t ON t.object_id = i.object_id
    WHERE t.name = 'Contracts' AND c.name = 'SigningTokenHash';
    PRINT 'ok   index on SigningTokenHash present (properties above)';
END

PRINT '';
PRINT '=== The tables are queryable ===';

-- Proves the application can actually read them, not merely that they exist.
-- Counts only; no contract content is ever printed by this script.
DECLARE @contracts int = (SELECT COUNT(*) FROM dbo.Contracts);
DECLARE @events int = (SELECT COUNT(*) FROM dbo.ContractEvents);
SET @msg = 'ok   Contracts rows: ' + CAST(@contracts AS varchar(20))
         + ', ContractEvents rows: ' + CAST(@events AS varchar(20));
PRINT @msg;

PRINT '';
PRINT '=== Status values in use ===';
-- Any value here outside the known set would mean the app and the database
-- disagree about the state machine's vocabulary.
SELECT Status, COUNT(*) AS [Rows] FROM dbo.Contracts GROUP BY Status ORDER BY Status;

PRINT '';
IF @problems = 0
    PRINT 'RESULT: ALL CHECKS PASSED';
ELSE
BEGIN
    SET @msg = 'RESULT: ' + CAST(@problems AS varchar(10)) + ' CHECK(S) FAILED';
    PRINT @msg;
END
