SELECT table_catalog [database], table_schema [schema], table_name [name], table_type [type]
FROM INFORMATION_SCHEMA.TABLES
GO

CREATE TABLE HttpLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Timestamp DATETIME2,
    IpAddress NVARCHAR(45),
    Method NVARCHAR(10),
    Url NVARCHAR(2048),
    StatusCode INT,
    BytesSent BIGINT,
    Referer NVARCHAR(2048),
    UserAgent NVARCHAR(500),
    ProcessedDate DATETIME2 DEFAULT GETUTCDATE()
);

-- テーブル作成確認
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HttpLogs';

-- カラム定義確認
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'HttpLogs'
ORDER BY ORDINAL_POSITION;


